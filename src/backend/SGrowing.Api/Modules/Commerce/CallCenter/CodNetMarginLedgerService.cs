using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.CallCenter;

public class CodNetMarginLedgerService : ICodNetMarginLedgerService
{
    private readonly SGrowingDbContext _db;

    public CodNetMarginLedgerService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CodNetMarginLedgerEntry> CalculateAndRecordMarginAsync(
        Guid tenantId,
        Guid orderId,
        string orderNumber,
        decimal grossRevenue,
        decimal cogs,
        decimal packagingCost,
        decimal adSpendCost,
        decimal courierShippingFee,
        decimal returnRatePercent,
        decimal returnPenaltyFee)
    {
        var entry = CodNetMarginLedgerEntry.Calculate(
            tenantId,
            orderId,
            orderNumber,
            grossRevenue,
            cogs,
            packagingCost,
            adSpendCost,
            courierShippingFee,
            returnRatePercent,
            returnPenaltyFee);

        // Replace previous evaluation if existing
        var existing = await _db.CodNetMarginLedgerEntries
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.OrderId == orderId);

        if (existing != null)
        {
            _db.CodNetMarginLedgerEntries.Remove(existing);
        }

        _db.CodNetMarginLedgerEntries.Add(entry);
        await _db.SaveChangesAsync();

        return entry;
    }

    public async Task<CodNetMarginLedgerEntry?> GetOrderMarginAsync(Guid tenantId, Guid orderId)
    {
        return await _db.CodNetMarginLedgerEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.OrderId == orderId);
    }

    public async Task<CodMarginSummaryDto> GetTenantMarginSummaryAsync(Guid tenantId, DateTime? fromUtc = null)
    {
        var query = _db.CodNetMarginLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (fromUtc.HasValue)
        {
            query = query.Where(e => e.CalculatedAtUtc >= fromUtc.Value);
        }

        var entries = await query.ToListAsync();
        if (entries.Count == 0)
        {
            return new CodMarginSummaryDto(tenantId, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        }

        var totalRev = entries.Sum(e => e.GrossRevenue);
        var totalCogs = entries.Sum(e => e.Cogs);
        var totalPkg = entries.Sum(e => e.PackagingCost);
        var totalAds = entries.Sum(e => e.AdSpendCost);
        var totalCourier = entries.Sum(e => e.CourierShippingFee);
        var totalRisk = entries.Sum(e => e.ReturnRiskCost);
        var totalProfit = entries.Sum(e => e.NetProfit);
        var marginPct = totalRev > 0 ? Math.Round((totalProfit / totalRev) * 100m, 2) : 0m;
        var aov = Math.Round(totalRev / entries.Count, 2);

        return new CodMarginSummaryDto(
            tenantId,
            entries.Count,
            totalRev,
            totalCogs,
            totalPkg,
            totalAds,
            totalCourier,
            totalRisk,
            totalProfit,
            marginPct,
            aov);
    }
}
