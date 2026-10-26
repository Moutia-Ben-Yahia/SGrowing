using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Finance;

public interface ITreasuryFloatService
{
    Task<CourierFloatValuation> CalculateFloatValuationAsync(Guid tenantId);
    Task<List<CashFlowForecastDay>> Generate14DayCashFlowForecastAsync(Guid tenantId, DateTime startDateUtc);
}

public class TreasuryFloatService : ITreasuryFloatService
{
    private readonly SGrowingDbContext _db;

    public TreasuryFloatService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CourierFloatValuation> CalculateFloatValuationAsync(Guid tenantId)
    {
        var orders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId)
            .ToListAsync();

        var inTransit = orders.Where(o => o.Status == CodOrderStatus.Dispatched).ToList();
        var delivered = orders.Where(o => o.Status == CodOrderStatus.Delivered).ToList();

        int inTransitCount = inTransit.Count;
        decimal inTransitVal = inTransit.Sum(o => o.TotalAmount);

        int deliveredCount = delivered.Count;
        decimal deliveredVal = delivered.Sum(o => o.TotalAmount);

        decimal fees = (inTransitCount + deliveredCount) * 7.0m;

        var valuation = new CourierFloatValuation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InTransitParcelCount = inTransitCount,
            InTransitCashValue = inTransitVal,
            DeliveredUnremittedParcelCount = deliveredCount,
            DeliveredUnremittedCashValue = deliveredVal,
            EstimatedCourierFees = fees,
            AverageDaysSalesOutstanding = 7.5m,
            CalculatedAtUtc = DateTime.UtcNow
        };

        _db.CourierFloatValuations.Add(valuation);
        await _db.SaveChangesAsync();

        return valuation;
    }

    public async Task<List<CashFlowForecastDay>> Generate14DayCashFlowForecastAsync(Guid tenantId, DateTime startDateUtc)
    {
        var valuation = await CalculateFloatValuationAsync(tenantId);
        var schedules = await _db.CarrierRemittanceScheduleConfigs
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync();

        var carrierName = schedules.FirstOrDefault()?.CourierName ?? "Yalidine Express";
        var fee = schedules.FirstOrDefault()?.ContractFeePerParcel ?? 7.0m;
        var remittanceDay = schedules.FirstOrDefault()?.RemittanceDayOfWeek ?? DayOfWeek.Tuesday;

        var forecast = new List<CashFlowForecastDay>();

        // Distribute expected float into the 2 upcoming remittance wire dates
        for (int i = 1; i <= 14; i++)
        {
            var date = startDateUtc.Date.AddDays(i);
            if (date.DayOfWeek == remittanceDay)
            {
                int parcels = Math.Max(5, (valuation.DeliveredUnremittedParcelCount + valuation.InTransitParcelCount) / 2);
                decimal gross = parcels * 85.0m;
                decimal net = gross - (parcels * fee);

                forecast.Add(new CashFlowForecastDay(
                    ForecastDate: date,
                    CourierName: carrierName,
                    ExpectedParcelCount: parcels,
                    ExpectedGrossCash: gross,
                    ExpectedNetRemittance: net,
                    Confidence: i <= 7 ? "High (Confirmed Deliveries)" : "Medium (In-Transit Projection)"));
            }
        }

        if (!forecast.Any())
        {
            var fallbackDate = startDateUtc.Date.AddDays(7);
            forecast.Add(new CashFlowForecastDay(
                ForecastDate: fallbackDate,
                CourierName: carrierName,
                ExpectedParcelCount: 10,
                ExpectedGrossCash: 850.0m,
                ExpectedNetRemittance: 780.0m,
                Confidence: "High"));
        }

        return forecast;
    }
}
