using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface ICourierRemittanceClearingService
{
    Task<RemittanceReconciliationResult> ReconcileStatementAsync(
        Guid tenantId,
        Guid courierProviderId,
        string statementReference,
        List<ReportedRemittanceItemDto> items,
        string reconciledBy = "FinanceManager");

    Task<RemittanceItemMatch> ResolveDiscrepancyAsync(Guid tenantId, Guid matchId, string resolutionNotes);
    Task<MerchantNetProfitabilitySummary> CalculateNetProfitabilityAsync(Guid tenantId, DateTime startDateUtc, DateTime endDateUtc, decimal estimatedCogsPercent = 35.0m);
}

public record ReportedRemittanceItemDto(string TrackingNumber, string ReportedStatus, decimal CashCollected, decimal CourierFee);
public record RemittanceReconciliationResult(CourierRemittanceStatement Statement, List<RemittanceItemMatch> Matches, int DiscrepancyCount, decimal NetDiscrepancyAmount);

public class CourierRemittanceClearingService : ICourierRemittanceClearingService
{
    private readonly SGrowingDbContext _dbContext;

    public CourierRemittanceClearingService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RemittanceReconciliationResult> ReconcileStatementAsync(
        Guid tenantId,
        Guid courierProviderId,
        string statementReference,
        List<ReportedRemittanceItemDto> items,
        string reconciledBy = "FinanceManager")
    {
        var courier = await _dbContext.CourierProviders
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == courierProviderId)
            ?? throw new InvalidOperationException($"Courier provider {courierProviderId} not found.");

        var statement = new CourierRemittanceStatement
        {
            TenantId = tenantId,
            CourierProviderId = courierProviderId,
            CourierName = courier.Name,
            StatementReference = statementReference.Trim(),
            StatementDateUtc = DateTime.UtcNow,
            TotalParcelsReported = items.Count,
            TotalCashCollected = items.Sum(i => i.CashCollected),
            TotalCourierFeesDeducted = items.Sum(i => i.CourierFee),
            NetRemittedAmount = items.Sum(i => i.CashCollected - i.CourierFee),
            Status = RemittanceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            ReconciledAtUtc = DateTime.UtcNow,
            ReconciledBy = reconciledBy
        };
        _dbContext.CourierRemittanceStatements.Add(statement);
        await _dbContext.SaveChangesAsync();

        var matches = new List<RemittanceItemMatch>();
        int discrepancyCount = 0;
        decimal netDiscrepancy = 0;

        foreach (var item in items)
        {
            var shipment = await _dbContext.ShipmentRecords
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.TrackingNumber == item.TrackingNumber);

            var match = new RemittanceItemMatch
            {
                TenantId = tenantId,
                StatementId = statement.Id,
                TrackingNumber = item.TrackingNumber,
                ReportedStatus = item.ReportedStatus,
                ReportedCashCollected = item.CashCollected,
                CourierFeeDeducted = item.CourierFee,
                OrderId = shipment?.OrderId,
                SystemExpectedAmount = shipment?.CashToCollect ?? 0,
                DifferenceAmount = item.CashCollected - (shipment?.CashToCollect ?? 0),
                IsResolved = false
            };

            if (shipment == null)
            {
                match.DiscrepancyType = RemittanceDiscrepancyType.UnaccountedParcel;
                match.ResolutionNotes = "Tracking number not found in SGrowing dispatched shipments.";
                discrepancyCount++;
                netDiscrepancy += item.CashCollected;
            }
            else if (item.ReportedStatus == "Delivered")
            {
                if (match.DifferenceAmount == 0)
                {
                    match.DiscrepancyType = RemittanceDiscrepancyType.ExactMatch;
                    match.IsResolved = true;
                }
                else if (match.DifferenceAmount < 0)
                {
                    match.DiscrepancyType = RemittanceDiscrepancyType.ShortPayment;
                    match.ResolutionNotes = $"Short-payment of {Math.Abs(match.DifferenceAmount):N2} detected.";
                    discrepancyCount++;
                    netDiscrepancy += match.DifferenceAmount;
                }
                else
                {
                    match.DiscrepancyType = RemittanceDiscrepancyType.Overpayment;
                    match.ResolutionNotes = $"Overpayment of {match.DifferenceAmount:N2} detected.";
                    discrepancyCount++;
                    netDiscrepancy += match.DifferenceAmount;
                }

                // Update shipment remitted cash
                shipment.CashRemitted = item.CashCollected;
                shipment.Status = CourierShipmentStatus.Delivered;
            }
            else if (item.ReportedStatus == "Returned")
            {
                match.DiscrepancyType = item.CashCollected > 0 ? RemittanceDiscrepancyType.FeeDispute : RemittanceDiscrepancyType.ExactMatch;
                match.IsResolved = item.CashCollected == 0;
                if (shipment != null)
                {
                    shipment.Status = CourierShipmentStatus.ReturnedToSender;
                }
            }

            matches.Add(match);
            _dbContext.RemittanceItemMatches.Add(match);
        }

        statement.Status = discrepancyCount > 0 ? RemittanceStatus.DiscrepancyFlagged : RemittanceStatus.Reconciled;
        await _dbContext.SaveChangesAsync();

        return new RemittanceReconciliationResult(statement, matches, discrepancyCount, netDiscrepancy);
    }

    public async Task<RemittanceItemMatch> ResolveDiscrepancyAsync(Guid tenantId, Guid matchId, string resolutionNotes)
    {
        var match = await _dbContext.RemittanceItemMatches
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matchId)
            ?? throw new InvalidOperationException($"Match {matchId} not found.");

        match.IsResolved = true;
        match.ResolutionNotes = (match.ResolutionNotes ?? "") + $" | Resolved: {resolutionNotes}";

        await _dbContext.SaveChangesAsync();
        return match;
    }

    public async Task<MerchantNetProfitabilitySummary> CalculateNetProfitabilityAsync(Guid tenantId, DateTime startDateUtc, DateTime endDateUtc, decimal estimatedCogsPercent = 35.0m)
    {
        var statements = await _dbContext.CourierRemittanceStatements
            .Where(s => s.TenantId == tenantId && s.StatementDateUtc >= startDateUtc && s.StatementDateUtc <= endDateUtc)
            .ToListAsync();

        decimal grossCashCollected = statements.Sum(s => s.TotalCashCollected);
        decimal courierFees = statements.Sum(s => s.TotalCourierFeesDeducted);

        // Fetch ad spend from attribution touchpoints
        var adSpend = await _dbContext.AttributionTouchpoints
            .Where(t => t.TenantId == tenantId && t.OccurredAtUtc >= startDateUtc && t.OccurredAtUtc <= endDateUtc)
            .SumAsync(t => t.Cost);

        decimal cogs = grossCashCollected * (estimatedCogsPercent / 100m);

        return new MerchantNetProfitabilitySummary
        {
            TenantId = tenantId,
            PeriodStartUtc = startDateUtc,
            PeriodEndUtc = endDateUtc,
            GrossRevenueCollected = grossCashCollected,
            TotalCogs = cogs,
            TotalAdSpend = adSpend,
            TotalCourierFees = courierFees
        };
    }
}
