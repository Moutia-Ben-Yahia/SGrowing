using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

#region DTOs & Contracts

public record CarrierRoutingScoreDto(
    Guid CourierProviderId,
    string CourierName,
    string ApiCode,
    string DestinationGovernorate,
    string CountryCode,
    decimal HistoricalDeliveryRatePercent,
    decimal BaseDeliveryFee,
    decimal ReturnFeePenalty,
    decimal AverageTransitDays,
    decimal CodAmount,
    decimal ExpectedNetValue,
    string FormulaExpression,
    string RecommendationReason);

public record GovernorateCarrierMatrixDto(
    string Governorate,
    string CountryCode,
    List<CarrierRoutingScoreDto> RankedCarriers);

public record CarrierAutoRouteAssignmentResult(
    Guid OrderId,
    string OrderNumber,
    string Governorate,
    decimal CodAmount,
    Guid SelectedCourierId,
    string SelectedCourierName,
    decimal AssignedShippingFee,
    decimal ExpectedNetValue,
    string RecommendationReason,
    List<CarrierRoutingScoreDto> AlternativesRanked);

public record ManifestParcelItemDto(
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string PhoneNumber,
    string Destination,
    string TrackingNumber,
    string AwbBarcode,
    decimal CodAmount,
    decimal ExpectedFreightCost);

public record ShippingManifestBatchResult(
    Guid ManifestId,
    string ManifestNumber,
    Guid CourierProviderId,
    string CourierName,
    int TotalParcels,
    decimal TotalDeclaredCodAmount,
    decimal ExpectedFreightCost,
    string Status,
    DateTime CreatedAtUtc,
    string BarcodeString,
    List<ManifestParcelItemDto> Parcels);

public record ShippingManifestSummaryDto(
    Guid Id,
    string ManifestNumber,
    Guid CourierProviderId,
    string CourierName,
    int OrderCount,
    decimal TotalCashAmount,
    decimal ExpectedFreightCost,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? HandedOverAtUtc);

public record ReportedParcelRowDto(
    string TrackingNumber,
    string ReportedStatus, // "Delivered", "Returned", "Customer Refused", etc.
    decimal CashCollected,
    decimal CourierFee,
    string? CourierNotes = null);

public record DisputeTicketDto(
    string TicketNumber,
    string TrackingNumber,
    Guid? OrderId,
    string DiscrepancyType,
    decimal DisputedAmount,
    string DisputeReason,
    DateTime CreatedAtUtc);

public record RemittanceAuditReportDto(
    Guid StatementId,
    string StatementReference,
    Guid CourierProviderId,
    string CourierName,
    int TotalParcelsProcessed,
    decimal TotalCollectedCash,
    decimal ApprovedDeliveryFees,
    decimal ReturnPenalties,
    decimal DiscrepancyHoldback,
    decimal NetCashSettlement,
    int ClearedCount,
    int DiscrepancyCount,
    List<RemittanceItemMatchDetailDto> MatchDetails,
    List<DisputeTicketDto> DisputeTickets,
    DateTime ReconciledAtUtc);

public record RemittanceItemMatchDetailDto(
    Guid MatchId,
    string TrackingNumber,
    Guid? OrderId,
    string? OrderNumber,
    string? CustomerName,
    string ReportedStatus,
    decimal ReportedCashCollected,
    decimal SystemExpectedAmount,
    decimal CashVariance,
    decimal CourierFeeDeducted,
    string DiscrepancyType,
    string? DisputeTicketNumber,
    decimal HoldbackAmount,
    string? ResolutionNotes,
    bool IsResolved);

public record DiscrepancyResolutionRequest(
    string ReasonCode, // e.g. "Courier clerical error", "Parcel lost", "Merchant approved partial payment", "Phantom return confirmed"
    string Notes,
    string ResolvedBy);

public record RemittanceKpisDto(
    decimal TotalCodInTransit,
    decimal TotalCashCollectedByCouriers,
    decimal ClearedMerchantSettlement,
    int UnresolvedDiscrepanciesCount,
    decimal UnresolvedDiscrepanciesValue);

#endregion

public interface ICodCarrierAutoDispatchService
{
    // 1. Regional Courier Auto-Routing Matrix
    Task<List<GovernorateCarrierMatrixDto>> GetCarrierRoutingMatrixAsync(
        Guid tenantId,
        string? countryCode = null,
        string? governorate = null);

    Task<List<CarrierRoutingScoreDto>> EvaluateCarrierRankingAsync(
        Guid tenantId,
        string governorate,
        decimal codAmount,
        string countryCode = "TN",
        decimal packageWeightKg = 1.0m);

    Task<CarrierAutoRouteAssignmentResult> AutoRouteAndAssignOrderAsync(
        Guid tenantId,
        Guid orderId,
        decimal? packageWeightKg = null);

    // 2. Shipping Manifest Generation (MNF-YYYYMMDD-XXXXX)
    Task<ShippingManifestBatchResult> GenerateManifestBatchAsync(
        Guid tenantId,
        Guid courierProviderId,
        List<Guid> orderIds,
        string? createdBy = null);

    Task<List<ShippingManifestSummaryDto>> ListManifestsAsync(
        Guid tenantId,
        string? status = null);

    Task<ShippingManifestBatchResult> GetManifestByIdAsync(
        Guid tenantId,
        Guid manifestId);

    // 3. Cash Remittance Statement CSV Reconciler
    Task<RemittanceAuditReportDto> ReconcileRemittanceStatementAsync(
        Guid tenantId,
        Guid courierProviderId,
        string statementReference,
        string? csvContent,
        List<ReportedParcelRowDto>? parsedRows = null,
        string reconciledBy = "Auditor");

    Task<RemittanceItemMatchDetailDto> ResolveDiscrepancyAsync(
        Guid tenantId,
        Guid matchId,
        DiscrepancyResolutionRequest request);

    // 4. Financial Reconciliation Cockpit KPIs
    Task<RemittanceKpisDto> GetReconciliationKpisAsync(Guid tenantId);
}

public class CodCarrierAutoDispatchService : ICodCarrierAutoDispatchService
{
    private readonly SGrowingDbContext _db;

    public static readonly string[] TunisianGovernorates = new[]
    {
        "Tunis", "Ariana", "Ben Arous", "Manouba", "Nabeul", "Zaghouan",
        "Bizerte", "Beja", "Jendouba", "Kef", "Siliana", "Sousse",
        "Monastir", "Mahdia", "Sfax", "Kairouan", "Kasserine", "Sidi Bouzid",
        "Gabes", "Medenine", "Tataouine", "Gafsa", "Tozeur", "Kebili"
    };

    public static readonly string[] EgyptianGovernorates = new[]
    {
        "Cairo", "Giza", "Alexandria", "Qalyubia", "Sharqia", "Dakahlia",
        "Gharbia", "Port Said", "Suez", "Asyut", "Sohag", "Aswan"
    };

    public CodCarrierAutoDispatchService(SGrowingDbContext db)
    {
        _db = db;
    }

    #region 1. Regional Courier Auto-Routing Matrix

    public async Task<List<GovernorateCarrierMatrixDto>> GetCarrierRoutingMatrixAsync(
        Guid tenantId,
        string? countryCode = null,
        string? governorate = null)
    {
        var targetCountry = string.IsNullOrWhiteSpace(countryCode) ? "TN" : countryCode.Trim().ToUpperInvariant();
        var governoratesToEvaluate = targetCountry == "EG"
            ? EgyptianGovernorates
            : TunisianGovernorates;

        if (!string.IsNullOrWhiteSpace(governorate))
        {
            var match = governoratesToEvaluate.FirstOrDefault(g => g.Equals(governorate.Trim(), StringComparison.OrdinalIgnoreCase));
            governoratesToEvaluate = match != null ? new[] { match } : new[] { governorate.Trim() };
        }

        const decimal benchmarkCodAmount = 85.0m; // Standard sample COD amount for matrix evaluation
        var matrix = new List<GovernorateCarrierMatrixDto>();

        foreach (var gov in governoratesToEvaluate)
        {
            var ranked = await EvaluateCarrierRankingAsync(tenantId, gov, benchmarkCodAmount, targetCountry);
            matrix.Add(new GovernorateCarrierMatrixDto(gov, targetCountry, ranked));
        }

        return matrix;
    }

    public async Task<List<CarrierRoutingScoreDto>> EvaluateCarrierRankingAsync(
        Guid tenantId,
        string governorate,
        decimal codAmount,
        string countryCode = "TN",
        decimal packageWeightKg = 1.0m)
    {
        var cleanGov = governorate?.Trim() ?? "Tunis";
        var cleanCountry = string.IsNullOrWhiteSpace(countryCode) ? "TN" : countryCode.Trim().ToUpperInvariant();

        // Ensure active rate cards & couriers exist for tenant
        var couriers = await _db.CourierProviders
            .Where(c => c.TenantId == tenantId && c.IsActive && c.SupportedCountries.Contains(cleanCountry))
            .ToListAsync();

        var rateCards = await _db.CarrierRateCards
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .ToListAsync();

        var metrics = await _db.CarrierPerformanceMetrics
            .Where(m => m.TenantId == tenantId && m.GovernorateOrWilaya.ToLower() == cleanGov.ToLower())
            .ToListAsync();

        // If no providers or rate cards are seeded in DB for this tenant, fallback to standard regional baseline
        var availableCarriers = BuildCarrierBaseline(cleanCountry, couriers, rateCards, metrics, cleanGov);

        var scores = new List<CarrierRoutingScoreDto>();

        foreach (var carrier in availableCarriers)
        {
            // Expected Net Value Scoring Formula:
            // E[Value(c, g)] = P_delivered(c, g) * CodAmount - Fee_delivery(c) - (1 - P_delivered(c, g)) * Fee_return(c)
            var pDelivered = carrier.DeliverySuccessRate;
            var feeDelivery = carrier.BaseDeliveryFee;
            var feeReturn = carrier.ReturnFeePenalty;

            var grossDelivered = pDelivered * codAmount;
            var returnPenaltyCost = (1m - pDelivered) * feeReturn;
            var expectedNetValue = grossDelivered - feeDelivery - returnPenaltyCost;

            var formulaExpr = $"({pDelivered:P1} * {codAmount:F2}) - {feeDelivery:F2} - ({(1m - pDelivered):P1} * {feeReturn:F2}) = {expectedNetValue:F2}";
            var reason = $"{carrier.CourierName} in {cleanGov}: {pDelivered * 100m:F1}% success rate, {feeDelivery:F2} {carrier.Currency} base fee, return risk -{returnPenaltyCost:F2} {carrier.Currency}.";

            scores.Add(new CarrierRoutingScoreDto(
                CourierProviderId: carrier.CourierProviderId,
                CourierName: carrier.CourierName,
                ApiCode: carrier.ApiCode,
                DestinationGovernorate: cleanGov,
                CountryCode: cleanCountry,
                HistoricalDeliveryRatePercent: Math.Round(pDelivered * 100m, 1),
                BaseDeliveryFee: feeDelivery,
                ReturnFeePenalty: feeReturn,
                AverageTransitDays: carrier.AverageTransitDays,
                CodAmount: codAmount,
                ExpectedNetValue: Math.Round(expectedNetValue, 2),
                FormulaExpression: formulaExpr,
                RecommendationReason: reason));
        }

        // Rank by highest expected net value
        return scores.OrderByDescending(s => s.ExpectedNetValue).ToList();
    }

    public async Task<CarrierAutoRouteAssignmentResult> AutoRouteAndAssignOrderAsync(
        Guid tenantId,
        Guid orderId,
        decimal? packageWeightKg = null)
    {
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId)
            ?? throw new InvalidOperationException($"Order with ID {orderId} not found for tenant {tenantId}.");

        var weight = packageWeightKg ?? 1.0m;
        var ranked = await EvaluateCarrierRankingAsync(tenantId, order.GovernorateOrWilaya, order.TotalAmount, order.CountryCode, weight);

        if (!ranked.Any())
        {
            throw new InvalidOperationException($"No eligible carrier found for destination '{order.GovernorateOrWilaya}'.");
        }

        var optimal = ranked.First();

        order.ShippingFee = optimal.BaseDeliveryFee;
        var assignmentNote = $"[Auto-Dispatched: {optimal.CourierName} ({optimal.ApiCode}) | E[Net]={optimal.ExpectedNetValue:F2} | Rate={optimal.HistoricalDeliveryRatePercent:F1}% | {DateTime.UtcNow:yyyy-MM-dd HH:mm}]";
        order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? assignmentNote : $"{order.Notes} {assignmentNote}";

        await _db.SaveChangesAsync();

        return new CarrierAutoRouteAssignmentResult(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            Governorate: order.GovernorateOrWilaya,
            CodAmount: order.TotalAmount,
            SelectedCourierId: optimal.CourierProviderId,
            SelectedCourierName: optimal.CourierName,
            AssignedShippingFee: optimal.BaseDeliveryFee,
            ExpectedNetValue: optimal.ExpectedNetValue,
            RecommendationReason: optimal.RecommendationReason,
            AlternativesRanked: ranked);
    }

    #endregion

    #region 2. Shipping Manifest Generation (MNF-YYYYMMDD-XXXXX)

    public async Task<ShippingManifestBatchResult> GenerateManifestBatchAsync(
        Guid tenantId,
        Guid courierProviderId,
        List<Guid> orderIds,
        string? createdBy = null)
    {
        if (orderIds == null || !orderIds.Any())
        {
            throw new ArgumentException("At least one order ID must be provided to generate a shipping manifest.", nameof(orderIds));
        }

        var courier = await _db.CourierProviders.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == courierProviderId);
        var courierName = courier?.Name ?? "Regional Express Courier";
        var courierCode = courier?.ApiCode ?? "EXP";

        var orders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId && orderIds.Contains(o.Id))
            .ToListAsync();

        if (orders.Count != orderIds.Count)
        {
            var missingIds = orderIds.Except(orders.Select(o => o.Id)).ToList();
            throw new InvalidOperationException($"Orders not found in tenant system: {string.Join(", ", missingIds)}");
        }

        // Generate manifest unique sequence: MNF-YYYYMMDD-XXXXX
        var todayStr = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var randomSuffix = GenerateRandomAlphaNumeric(5);
        var manifestNumber = $"MNF-{todayStr}-{randomSuffix}";

        // Fetch or resolve carrier rate card for fee calculations
        var rateCard = await _db.CarrierRateCards
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.CourierProviderId == courierProviderId && r.IsActive);
        var defaultBaseFee = rateCard?.BaseFee ?? 7.000m;

        var parcelItems = new List<ManifestParcelItemDto>();
        decimal totalDeclaredCod = 0m;
        decimal totalFreightCost = 0m;
        var now = DateTime.UtcNow;

        for (int i = 0; i < orders.Count; i++)
        {
            var order = orders[i];
            var seq = (i + 1).ToString("D4", CultureInfo.InvariantCulture);
            var trackingNumber = $"AWB-{courierCode}-{todayStr}-{seq}";
            var barcode = $"*{trackingNumber}*";
            var freightFee = order.ShippingFee > 0 ? order.ShippingFee : defaultBaseFee;

            totalDeclaredCod += order.TotalAmount;
            totalFreightCost += freightFee;

            // Lock order: Transition to Dispatched
            order.Status = CodOrderStatus.Dispatched;
            order.Notes = (order.Notes ?? "") + $" [Manifest: {manifestNumber} | Tracking: {trackingNumber}]";

            // Find or create ShipmentRecord
            var existingShipment = await _db.ShipmentRecords
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.OrderId == order.Id);

            if (existingShipment == null)
            {
                var shipment = new ShipmentRecord
                {
                    TenantId = tenantId,
                    OrderId = order.Id,
                    CourierProviderId = courierProviderId,
                    CourierName = courierName,
                    TrackingNumber = trackingNumber,
                    AwbBarcode = barcode,
                    CashToCollect = order.TotalAmount,
                    CashRemitted = 0m,
                    Status = CourierShipmentStatus.Created,
                    DispatchedAtUtc = now
                };
                _db.ShipmentRecords.Add(shipment);
            }
            else
            {
                existingShipment.CourierProviderId = courierProviderId;
                existingShipment.CourierName = courierName;
                existingShipment.TrackingNumber = trackingNumber;
                existingShipment.AwbBarcode = barcode;
                existingShipment.Status = CourierShipmentStatus.Created;
                existingShipment.DispatchedAtUtc = now;
            }

            parcelItems.Add(new ManifestParcelItemDto(
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                CustomerName: order.CustomerName,
                PhoneNumber: order.PhoneNumber,
                Destination: $"{order.GovernorateOrWilaya}, {order.DelegationOrCommune} ({order.ShippingAddress})",
                TrackingNumber: trackingNumber,
                AwbBarcode: barcode,
                CodAmount: order.TotalAmount,
                ExpectedFreightCost: freightFee));
        }

        var manifest = new ShippingManifest
        {
            TenantId = tenantId,
            CourierProviderId = courierProviderId,
            CourierName = courierName,
            ManifestNumber = manifestNumber,
            OrderCount = orders.Count,
            TotalCashAmount = totalDeclaredCod,
            ExpectedFreightCost = totalFreightCost,
            Status = "PendingPickup",
            CreatedAtUtc = now,
            OrderIdsJson = JsonSerializer.Serialize(orders.Select(o => o.Id).ToList())
        };

        _db.ShippingManifests.Add(manifest);
        await _db.SaveChangesAsync();

        return new ShippingManifestBatchResult(
            ManifestId: manifest.Id,
            ManifestNumber: manifestNumber,
            CourierProviderId: courierProviderId,
            CourierName: courierName,
            TotalParcels: orders.Count,
            TotalDeclaredCodAmount: totalDeclaredCod,
            ExpectedFreightCost: totalFreightCost,
            Status: "PendingPickup",
            CreatedAtUtc: now,
            BarcodeString: $"*{manifestNumber}*",
            Parcels: parcelItems);
    }

    public async Task<List<ShippingManifestSummaryDto>> ListManifestsAsync(Guid tenantId, string? status = null)
    {
        var query = _db.ShippingManifests.Where(m => m.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var cleanStatus = status.Trim();
            query = query.Where(m => m.Status == cleanStatus);
        }

        var manifests = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync();

        return manifests.Select(m => new ShippingManifestSummaryDto(
            Id: m.Id,
            ManifestNumber: m.ManifestNumber,
            CourierProviderId: m.CourierProviderId,
            CourierName: m.CourierName,
            OrderCount: m.OrderCount,
            TotalCashAmount: m.TotalCashAmount,
            ExpectedFreightCost: m.ExpectedFreightCost,
            Status: m.Status,
            CreatedAtUtc: m.CreatedAtUtc,
            HandedOverAtUtc: m.HandedOverAtUtc)).ToList();
    }

    public async Task<ShippingManifestBatchResult> GetManifestByIdAsync(Guid tenantId, Guid manifestId)
    {
        var manifest = await _db.ShippingManifests
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == manifestId)
            ?? throw new InvalidOperationException($"Shipping manifest {manifestId} not found.");

        List<Guid> orderIds = new();
        try
        {
            orderIds = JsonSerializer.Deserialize<List<Guid>>(manifest.OrderIdsJson) ?? new();
        }
        catch
        {
            // fallback empty
        }

        var orders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId && orderIds.Contains(o.Id))
            .ToListAsync();

        var shipments = await _db.ShipmentRecords
            .Where(s => s.TenantId == tenantId && orderIds.Contains(s.OrderId))
            .ToListAsync();

        var parcels = new List<ManifestParcelItemDto>();
        foreach (var order in orders)
        {
            var shipment = shipments.FirstOrDefault(s => s.OrderId == order.Id);
            var tracking = shipment?.TrackingNumber ?? $"AWB-PENDING-{order.OrderNumber}";
            var barcode = shipment?.AwbBarcode ?? $"*{tracking}*";

            parcels.Add(new ManifestParcelItemDto(
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                CustomerName: order.CustomerName,
                PhoneNumber: order.PhoneNumber,
                Destination: $"{order.GovernorateOrWilaya}, {order.DelegationOrCommune} ({order.ShippingAddress})",
                TrackingNumber: tracking,
                AwbBarcode: barcode,
                CodAmount: order.TotalAmount,
                ExpectedFreightCost: order.ShippingFee));
        }

        return new ShippingManifestBatchResult(
            ManifestId: manifest.Id,
            ManifestNumber: manifest.ManifestNumber,
            CourierProviderId: manifest.CourierProviderId,
            CourierName: manifest.CourierName,
            TotalParcels: manifest.OrderCount,
            TotalDeclaredCodAmount: manifest.TotalCashAmount,
            ExpectedFreightCost: manifest.ExpectedFreightCost,
            Status: manifest.Status,
            CreatedAtUtc: manifest.CreatedAtUtc,
            BarcodeString: $"*{manifest.ManifestNumber}*",
            Parcels: parcels);
    }

    #endregion

    #region 3. Cash Remittance Statement CSV Reconciler

    public async Task<RemittanceAuditReportDto> ReconcileRemittanceStatementAsync(
        Guid tenantId,
        Guid courierProviderId,
        string statementReference,
        string? csvContent,
        List<ReportedParcelRowDto>? parsedRows = null,
        string reconciledBy = "Auditor")
    {
        var courier = await _db.CourierProviders.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == courierProviderId);
        var courierName = courier?.Name ?? "Courier Partner";

        var rows = parsedRows ?? new List<ReportedParcelRowDto>();
        if (!string.IsNullOrWhiteSpace(csvContent))
        {
            var parsedFromCsv = ParseRemittanceCsv(csvContent);
            rows.AddRange(parsedFromCsv);
        }

        if (!rows.Any())
        {
            throw new ArgumentException("No valid remittance rows were provided or parsed from the statement.", nameof(rows));
        }

        var rateCard = await _db.CarrierRateCards
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.CourierProviderId == courierProviderId && r.IsActive);
        var agreedDeliveryFee = rateCard?.BaseFee ?? 7.000m;
        var agreedReturnPenalty = 3.500m;

        var statement = new CourierRemittanceStatement
        {
            TenantId = tenantId,
            CourierProviderId = courierProviderId,
            CourierName = courierName,
            StatementReference = statementReference.Trim(),
            StatementDateUtc = DateTime.UtcNow,
            TotalParcelsReported = rows.Count,
            TotalCashCollected = rows.Sum(r => r.CashCollected),
            TotalCourierFeesDeducted = rows.Sum(r => r.CourierFee),
            NetRemittedAmount = rows.Sum(r => r.CashCollected - r.CourierFee),
            Status = RemittanceStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            ReconciledAtUtc = DateTime.UtcNow,
            ReconciledBy = reconciledBy
        };

        _db.CourierRemittanceStatements.Add(statement);
        await _db.SaveChangesAsync();

        var matchDetails = new List<RemittanceItemMatchDetailDto>();
        var disputeTickets = new List<DisputeTicketDto>();

        decimal totalCollectedCash = 0m;
        decimal approvedDeliveryFees = 0m;
        decimal returnPenalties = 0m;
        decimal discrepancyHoldback = 0m;

        int clearedCount = 0;
        int discrepancyCount = 0;

        foreach (var row in rows)
        {
            var cleanTracking = row.TrackingNumber?.Trim() ?? string.Empty;
            var cleanStatus = row.ReportedStatus?.Trim() ?? "Delivered";

            var shipment = await _db.ShipmentRecords
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.TrackingNumber == cleanTracking);

            CodOrder? order = null;
            List<CallOutcomeRecord> callLogs = new();

            if (shipment != null)
            {
                order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == shipment.OrderId);
                if (order != null)
                {
                    callLogs = await _db.CallOutcomeRecords
                        .Where(c => c.TenantId == tenantId && c.OrderId == order.Id)
                        .ToListAsync();
                }
            }

            decimal expectedAmount = shipment?.CashToCollect ?? 0m;
            decimal variance = row.CashCollected - expectedAmount;
            totalCollectedCash += row.CashCollected;

            var match = new RemittanceItemMatch
            {
                TenantId = tenantId,
                StatementId = statement.Id,
                TrackingNumber = cleanTracking,
                ReportedStatus = cleanStatus,
                ReportedCashCollected = row.CashCollected,
                SystemExpectedAmount = expectedAmount,
                DifferenceAmount = variance,
                CourierFeeDeducted = row.CourierFee,
                OrderId = shipment?.OrderId,
                IsResolved = false
            };

            // Discrepancy Classification Engine
            if (shipment == null)
            {
                // 1. Unaccounted Parcel
                match.DiscrepancyType = RemittanceDiscrepancyType.UnaccountedParcel;
                match.HoldbackAmount = row.CashCollected; // Hold entire cash pending verification
                match.DisputeTicketNumber = $"DISP-UNACC-{cleanTracking}";
                match.ResolutionNotes = "Parcel tracking number not found in SGrowing dispatch manifests.";
                discrepancyCount++;
                discrepancyHoldback += match.HoldbackAmount;

                disputeTickets.Add(new DisputeTicketDto(
                    match.DisputeTicketNumber,
                    cleanTracking,
                    null,
                    "UnaccountedParcel",
                    match.HoldbackAmount,
                    "Tracking number not found in SGrowing outbound shipment records.",
                    DateTime.UtcNow));
            }
            else if (IsReturnedOrRefusedStatus(cleanStatus))
            {
                // Inspect call history for Phantom Return detection:
                // If courier claims "Customer Refused", but call logs show order was verified/confirmed and customer never refused,
                // or if call logs prove customer was never contacted by courier.
                bool isPhantomReturn = DetectPhantomReturn(order, callLogs, row.CourierNotes);

                if (isPhantomReturn)
                {
                    match.DiscrepancyType = RemittanceDiscrepancyType.PhantomReturn;
                    // Courier falsely marked refusal; hold back the parcel's expected COD value + return fee penalty
                    match.HoldbackAmount = expectedAmount > 0 ? expectedAmount : agreedReturnPenalty;
                    match.DisputeTicketNumber = $"DISP-PHANTOM-{cleanTracking}";
                    match.ResolutionNotes = "Phantom Return Detected: Merchant confirmed with buyer; buyer was never contacted by courier before refusal status was set.";
                    discrepancyCount++;
                    discrepancyHoldback += match.HoldbackAmount;

                    disputeTickets.Add(new DisputeTicketDto(
                        match.DisputeTicketNumber,
                        cleanTracking,
                        order?.Id,
                        "PhantomReturnDiscrepancy",
                        match.HoldbackAmount,
                        "Courier marked parcel 'Customer Refused' / 'Returned', but call audit proves customer verified intent and courier never contacted recipient.",
                        DateTime.UtcNow));
                }
                else
                {
                    // Legitimate Return
                    match.DiscrepancyType = RemittanceDiscrepancyType.ExactMatch;
                    match.IsResolved = true;
                    match.ResolutionNotes = "Legitimate customer return verified.";
                    clearedCount++;
                    returnPenalties += agreedReturnPenalty;

                    shipment.Status = CourierShipmentStatus.ReturnedToSender;
                    if (order != null) order.Status = CodOrderStatus.Returned;
                }
            }
            else if (cleanStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
            {
                if (variance == 0m)
                {
                    // Fully Cleared Match
                    match.DiscrepancyType = RemittanceDiscrepancyType.ExactMatch;
                    match.IsResolved = true;
                    match.ResolutionNotes = "Full cleared remittance. COD collected matches expected amount.";
                    clearedCount++;
                    approvedDeliveryFees += row.CourierFee > 0 ? row.CourierFee : agreedDeliveryFee;

                    shipment.Status = CourierShipmentStatus.Delivered;
                    shipment.CashRemitted = row.CashCollected;
                    shipment.DeliveredAtUtc = DateTime.UtcNow;
                    if (order != null) order.Status = CodOrderStatus.Delivered;
                }
                else
                {
                    // Over / Under-collection Discrepancy
                    match.DiscrepancyType = variance < 0m
                        ? RemittanceDiscrepancyType.ShortPayment
                        : RemittanceDiscrepancyType.Overpayment;

                    match.HoldbackAmount = Math.Abs(variance);
                    match.DisputeTicketNumber = $"DISP-VAR-{cleanTracking}";
                    match.ResolutionNotes = variance < 0m
                        ? $"Short-collection variance: Courier remitted {row.CashCollected:F2} instead of {expectedAmount:F2} (Diff: -{Math.Abs(variance):F2})."
                        : $"Over-collection variance: Courier remitted {row.CashCollected:F2} instead of {expectedAmount:F2} (Diff: +{variance:F2}).";

                    discrepancyCount++;
                    discrepancyHoldback += match.HoldbackAmount;

                    disputeTickets.Add(new DisputeTicketDto(
                        match.DisputeTicketNumber,
                        cleanTracking,
                        order?.Id,
                        "OverUnderCollectionDiscrepancy",
                        match.HoldbackAmount,
                        match.ResolutionNotes,
                        DateTime.UtcNow));

                    shipment.Status = CourierShipmentStatus.Delivered;
                    shipment.CashRemitted = row.CashCollected;
                }
            }

            _db.RemittanceItemMatches.Add(match);

            matchDetails.Add(new RemittanceItemMatchDetailDto(
                MatchId: match.Id,
                TrackingNumber: cleanTracking,
                OrderId: shipment?.OrderId,
                OrderNumber: order?.OrderNumber,
                CustomerName: order?.CustomerName,
                ReportedStatus: cleanStatus,
                ReportedCashCollected: row.CashCollected,
                SystemExpectedAmount: expectedAmount,
                CashVariance: variance,
                CourierFeeDeducted: row.CourierFee,
                DiscrepancyType: match.DiscrepancyType.ToString(),
                DisputeTicketNumber: match.DisputeTicketNumber,
                HoldbackAmount: match.HoldbackAmount,
                ResolutionNotes: match.ResolutionNotes,
                IsResolved: match.IsResolved));
        }

        // Net Cash Settlement Formula:
        // Net Cash Settlement = Total Collected Cash - Approved Delivery Fees - Return Penalties - Discrepancy Holdback
        decimal netCashSettlement = totalCollectedCash - approvedDeliveryFees - returnPenalties - discrepancyHoldback;

        statement.Status = discrepancyCount > 0 ? RemittanceStatus.DiscrepancyFlagged : RemittanceStatus.Reconciled;
        statement.NetRemittedAmount = netCashSettlement;
        await _db.SaveChangesAsync();

        return new RemittanceAuditReportDto(
            StatementId: statement.Id,
            StatementReference: statement.StatementReference,
            CourierProviderId: courierProviderId,
            CourierName: courierName,
            TotalParcelsProcessed: rows.Count,
            TotalCollectedCash: Math.Round(totalCollectedCash, 2),
            ApprovedDeliveryFees: Math.Round(approvedDeliveryFees, 2),
            ReturnPenalties: Math.Round(returnPenalties, 2),
            DiscrepancyHoldback: Math.Round(discrepancyHoldback, 2),
            NetCashSettlement: Math.Round(netCashSettlement, 2),
            ClearedCount: clearedCount,
            DiscrepancyCount: discrepancyCount,
            MatchDetails: matchDetails,
            DisputeTickets: disputeTickets,
            ReconciledAtUtc: DateTime.UtcNow);
    }

    public async Task<RemittanceItemMatchDetailDto> ResolveDiscrepancyAsync(
        Guid tenantId,
        Guid matchId,
        DiscrepancyResolutionRequest request)
    {
        var match = await _db.RemittanceItemMatches
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matchId)
            ?? throw new InvalidOperationException($"Remittance match record {matchId} not found.");

        match.IsResolved = true;
        var auditEntry = $"[Resolved by {request.ResolvedBy} on {DateTime.UtcNow:yyyy-MM-dd HH:mm} | Reason: {request.ReasonCode} | Notes: {request.Notes}]";
        match.ResolutionNotes = string.IsNullOrWhiteSpace(match.ResolutionNotes)
            ? auditEntry
            : $"{match.ResolutionNotes} | {auditEntry}";

        // Release holdback upon resolution
        match.HoldbackAmount = 0m;

        await _db.SaveChangesAsync();

        var order = match.OrderId.HasValue
            ? await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == match.OrderId.Value)
            : null;

        return new RemittanceItemMatchDetailDto(
            MatchId: match.Id,
            TrackingNumber: match.TrackingNumber,
            OrderId: match.OrderId,
            OrderNumber: order?.OrderNumber,
            CustomerName: order?.CustomerName,
            ReportedStatus: match.ReportedStatus,
            ReportedCashCollected: match.ReportedCashCollected,
            SystemExpectedAmount: match.SystemExpectedAmount,
            CashVariance: match.DifferenceAmount,
            CourierFeeDeducted: match.CourierFeeDeducted,
            DiscrepancyType: match.DiscrepancyType.ToString(),
            DisputeTicketNumber: match.DisputeTicketNumber,
            HoldbackAmount: match.HoldbackAmount,
            ResolutionNotes: match.ResolutionNotes,
            IsResolved: match.IsResolved);
    }

    #endregion

    #region 4. Financial Reconciliation Cockpit KPIs

    public async Task<RemittanceKpisDto> GetReconciliationKpisAsync(Guid tenantId)
    {
        // 1. Total COD in Transit: Shipments created/in transit where cash has not been remitted
        var inTransitShipments = await _db.ShipmentRecords
            .Where(s => s.TenantId == tenantId && (s.Status == CourierShipmentStatus.Created || s.Status == CourierShipmentStatus.InTransit || s.Status == CourierShipmentStatus.PickedUp || s.Status == CourierShipmentStatus.OutForDelivery))
            .SumAsync(s => s.CashToCollect);

        // 2. Total Cash Collected by Couriers (Delivered but pending final cleared settlement)
        var unremittedDelivered = await _db.ShipmentRecords
            .Where(s => s.TenantId == tenantId && s.Status == CourierShipmentStatus.Delivered && s.CashRemitted > 0)
            .SumAsync(s => s.CashRemitted);

        // 3. Cleared Merchant Settlement ready for payout
        var settledStatements = await _db.CourierRemittanceStatements
            .Where(s => s.TenantId == tenantId && s.Status == RemittanceStatus.Reconciled)
            .SumAsync(s => s.NetRemittedAmount);

        // 4. Unresolved discrepancies count & value
        var unresolvedMatches = await _db.RemittanceItemMatches
            .Where(m => m.TenantId == tenantId && !m.IsResolved)
            .ToListAsync();

        int unresolvedCount = unresolvedMatches.Count;
        decimal unresolvedValue = unresolvedMatches.Sum(m => m.HoldbackAmount > 0 ? m.HoldbackAmount : Math.Abs(m.DifferenceAmount));

        return new RemittanceKpisDto(
            TotalCodInTransit: Math.Round(inTransitShipments, 2),
            TotalCashCollectedByCouriers: Math.Round(unremittedDelivered, 2),
            ClearedMerchantSettlement: Math.Round(settledStatements, 2),
            UnresolvedDiscrepanciesCount: unresolvedCount,
            UnresolvedDiscrepanciesValue: Math.Round(unresolvedValue, 2));
    }

    #endregion

    #region Helpers & Parser

    public static List<ReportedParcelRowDto> ParseRemittanceCsv(string csvContent)
    {
        var rows = new List<ReportedParcelRowDto>();
        if (string.IsNullOrWhiteSpace(csvContent)) return rows;

        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1) return rows;

        // Auto-detect delimiter: comma or semicolon or tab
        var headerLine = lines[0];
        char delimiter = ',';
        if (headerLine.Contains(';') && headerLine.Count(c => c == ';') >= headerLine.Count(c => c == ','))
            delimiter = ';';
        else if (headerLine.Contains('\t'))
            delimiter = '\t';

        var headers = headerLine.Split(delimiter).Select(h => h.Trim().ToLowerInvariant().Replace("\"", "")).ToArray();

        int trackingIdx = Array.FindIndex(headers, h => h.Contains("tracking") || h.Contains("awb") || h.Contains("code") || h.Contains("colis"));
        int statusIdx = Array.FindIndex(headers, h => h.Contains("status") || h.Contains("statut") || h.Contains("etat"));
        int amountIdx = Array.FindIndex(headers, h => h.Contains("amount") || h.Contains("cash") || h.Contains("montant") || h.Contains("collected") || h.Contains("total"));
        int feeIdx = Array.FindIndex(headers, h => h.Contains("fee") || h.Contains("frais") || h.Contains("cost") || h.Contains("tarif"));
        int notesIdx = Array.FindIndex(headers, h => h.Contains("note") || h.Contains("reason") || h.Contains("motif"));

        // Fallback indexes if headers match standard 4 columns
        if (trackingIdx < 0) trackingIdx = 0;
        if (statusIdx < 0) statusIdx = 1;
        if (amountIdx < 0) amountIdx = 2;
        if (feeIdx < 0 && headers.Length > 3) feeIdx = 3;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(delimiter).Select(p => p.Trim().Replace("\"", "")).ToArray();
            if (parts.Length <= trackingIdx) continue;

            var tracking = parts[trackingIdx];
            var status = (statusIdx >= 0 && parts.Length > statusIdx) ? parts[statusIdx] : "Delivered";

            decimal cash = 0m;
            if (amountIdx >= 0 && parts.Length > amountIdx)
            {
                var amtStr = parts[amountIdx].Replace("TND", "").Replace("DT", "").Replace("EGP", "").Trim();
                decimal.TryParse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture, out cash);
                if (cash == 0m && decimal.TryParse(amtStr, NumberStyles.Any, new CultureInfo("fr-FR"), out var frCash))
                {
                    cash = frCash;
                }
            }

            decimal fee = 0m;
            if (feeIdx >= 0 && parts.Length > feeIdx)
            {
                var feeStr = parts[feeIdx].Replace("TND", "").Replace("DT", "").Replace("EGP", "").Trim();
                decimal.TryParse(feeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out fee);
                if (fee == 0m && decimal.TryParse(feeStr, NumberStyles.Any, new CultureInfo("fr-FR"), out var frFee))
                {
                    fee = frFee;
                }
            }

            string? notes = (notesIdx >= 0 && parts.Length > notesIdx) ? parts[notesIdx] : null;

            rows.Add(new ReportedParcelRowDto(tracking, status, cash, fee, notes));
        }

        return rows;
    }

    private static bool IsReturnedOrRefusedStatus(string status)
    {
        var lower = status.ToLowerInvariant();
        return lower.Contains("return") || lower.Contains("retour") ||
               lower.Contains("refus") || lower.Contains("annul") ||
               lower.Contains("failed") || lower.Contains("injoignable") ||
               lower.Contains("absent") || lower.Contains("non réclamé");
    }

    private static bool DetectPhantomReturn(CodOrder? order, List<CallOutcomeRecord> callLogs, string? courierNotes)
    {
        if (order == null) return false;

        // Check if customer explicitly cancelled or requested callback with refusal
        bool customerRefusedInCallLogs = callLogs.Any(c => c.Outcome == CallOutcome.CancelledByCustomer);

        // Check if merchant successfully confirmed the order
        bool merchantConfirmedOrder = order.Status == CodOrderStatus.Confirmed ||
                                       order.ConfirmedAtUtc != null ||
                                       callLogs.Any(c => c.Outcome == CallOutcome.Confirmed);

        // Phantom Return Criteria:
        // 1. Merchant call center vetted & confirmed the order with customer.
        // 2. Call logs contain ZERO customer refusal outcomes.
        // 3. Courier marked as customer refused / uncontacted or fake reason without actual customer refusal.
        if (merchantConfirmedOrder && !customerRefusedInCallLogs)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(courierNotes) &&
            (courierNotes.Contains("never contacted", StringComparison.OrdinalIgnoreCase) ||
             courierNotes.Contains("customer denies refusal", StringComparison.OrdinalIgnoreCase) ||
             courierNotes.Contains("phantom", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string GenerateRandomAlphaNumeric(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var result = new StringBuilder(length);
        foreach (var b in bytes)
        {
            result.Append(chars[b % chars.Length]);
        }
        return result.ToString();
    }

    private record CarrierBaselineSpec(
        Guid CourierProviderId,
        string CourierName,
        string ApiCode,
        string Currency,
        decimal DeliverySuccessRate,
        decimal BaseDeliveryFee,
        decimal ReturnFeePenalty,
        decimal AverageTransitDays);

    private static List<CarrierBaselineSpec> BuildCarrierBaseline(
        string countryCode,
        List<CourierProvider> dbCouriers,
        List<CarrierRateCard> dbRateCards,
        List<CarrierPerformanceMetric> dbMetrics,
        string governorate)
    {
        var list = new List<CarrierBaselineSpec>();

        if (countryCode == "EG")
        {
            // Egyptian couriers (Bosta, Mylerz)
            AddCarrierSpec(list, dbCouriers, dbRateCards, dbMetrics, "Bosta Egypt", "BST", "EGP", 0.91m, 45.0m, 20.0m, 1.2m, governorate);
            AddCarrierSpec(list, dbCouriers, dbRateCards, dbMetrics, "Mylerz Delivery", "MYL", "EGP", 0.88m, 48.0m, 22.0m, 1.5m, governorate);
            AddCarrierSpec(list, dbCouriers, dbRateCards, dbMetrics, "Aramex Egypt", "ARX-EG", "EGP", 0.86m, 52.0m, 25.0m, 1.8m, governorate);
        }
        else
        {
            // Tunisian 15+ Couriers (Aramex, First Delivery, Livri, Express Courrier)
            // Grand Tunis vs South vs Sahel specializations from Mallatech / TikTak PRO architecture
            bool isGrandTunis = governorate.Equals("Tunis", StringComparison.OrdinalIgnoreCase) ||
                                governorate.Equals("Ariana", StringComparison.OrdinalIgnoreCase) ||
                                governorate.Equals("Ben Arous", StringComparison.OrdinalIgnoreCase) ||
                                governorate.Equals("Manouba", StringComparison.OrdinalIgnoreCase);

            bool isSouth = governorate.Equals("Tataouine", StringComparison.OrdinalIgnoreCase) ||
                           governorate.Equals("Medenine", StringComparison.OrdinalIgnoreCase) ||
                           governorate.Equals("Kebili", StringComparison.OrdinalIgnoreCase) ||
                           governorate.Equals("Tozeur", StringComparison.OrdinalIgnoreCase) ||
                           governorate.Equals("Gafsa", StringComparison.OrdinalIgnoreCase);

            bool isSahelOrSfax = governorate.Equals("Sousse", StringComparison.OrdinalIgnoreCase) ||
                                 governorate.Equals("Monastir", StringComparison.OrdinalIgnoreCase) ||
                                 governorate.Equals("Mahdia", StringComparison.OrdinalIgnoreCase) ||
                                 governorate.Equals("Sfax", StringComparison.OrdinalIgnoreCase);

            // 1. Aramex Tunisia (Excels in South & national reach)
            decimal arxRate = isSouth ? 0.92m : 0.87m;
            decimal arxFee = isSouth ? 8.5m : 8.0m;
            AddCarrierSpec(list, dbCouriers, dbRateCards, dbMetrics, "Aramex Tunisia", "ARX", "TND", arxRate, arxFee, 4.0m, 1.8m, governorate);

            // 2. First Delivery (Dominates Grand Tunis with lowest fee)
            decimal fstRate = isGrandTunis ? 0.94m : 0.83m;
            decimal fstFee = isGrandTunis ? 6.5m : 7.5m;
            AddCarrierSpec(list, dbCouriers, dbRateCards, dbMetrics, "First Delivery", "FST", "TND", fstRate, fstFee, 3.0m, 1.2m, governorate);

            // 3. Livri Express (Highest performer in Sahel & Sfax)
            decimal lvrRate = isSahelOrSfax ? 0.93m : 0.86m;
            decimal lvrFee = 7.0m;
            AddCarrierSpec(list, dbCouriers, dbRateCards, dbMetrics, "Livri Express", "LVR", "TND", lvrRate, lvrFee, 3.5m, 1.4m, governorate);

            // 4. Express Courrier (Solid North & Coastal)
            decimal expRate = 0.88m;
            decimal expFee = 7.5m;
            AddCarrierSpec(list, dbCouriers, dbRateCards, dbMetrics, "Express Courrier", "EXP", "TND", expRate, expFee, 3.5m, 1.5m, governorate);
        }

        return list;
    }

    private static void AddCarrierSpec(
        List<CarrierBaselineSpec> list,
        List<CourierProvider> dbCouriers,
        List<CarrierRateCard> dbRateCards,
        List<CarrierPerformanceMetric> dbMetrics,
        string defaultName,
        string apiCode,
        string currency,
        decimal defaultRate,
        decimal defaultFee,
        decimal defaultReturnFee,
        decimal defaultTransitDays,
        string governorate)
    {
        var existingCourier = dbCouriers.FirstOrDefault(c => c.ApiCode.Equals(apiCode, StringComparison.OrdinalIgnoreCase) || c.Name.Contains(defaultName, StringComparison.OrdinalIgnoreCase));
        var courierId = existingCourier?.Id ?? Guid.NewGuid();
        var courierName = existingCourier?.Name ?? defaultName;

        var existingCard = dbRateCards.FirstOrDefault(r => r.CourierProviderId == courierId);
        var baseFee = existingCard?.BaseFee ?? defaultFee;

        var metric = dbMetrics.FirstOrDefault(m => m.CourierProviderId == courierId && m.GovernorateOrWilaya.Equals(governorate, StringComparison.OrdinalIgnoreCase));
        var rate = metric != null ? metric.DeliveryRatePercentage / 100m : defaultRate;
        var returnFee = metric?.ReturnFee ?? defaultReturnFee;
        var transitDays = metric?.AverageTransitDays ?? defaultTransitDays;

        list.Add(new CarrierBaselineSpec(
            courierId,
            courierName,
            apiCode,
            currency,
            rate,
            baseFee,
            returnFee,
            transitDays));
    }

    #endregion
}
