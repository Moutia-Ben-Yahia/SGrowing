using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Finance;

#region DTOs & Domain Models

public record DayClearanceShare(
    int DayOffset,
    double Probability,
    decimal ExpectedAmount
);

public record CourierFloatBreakdownDto(
    string CourierName,
    int InTransitParcelCount,
    decimal GrossCashValue,
    decimal EstimatedFees,
    decimal NetReceivableCash,
    double AverageTurnaroundDays,
    string RemittanceFrequency,
    string ClearanceSchedulePattern,
    List<DayClearanceShare> ClearanceDistribution
);

public record ReceivableAgingItemDto(
    string InvoiceId,
    string CustomerName,
    decimal Amount,
    DateTime IssueDate,
    DateTime DueDate,
    int DaysPastDue,
    string AgingBucket, // "Current", "1-30 Days", "31-60 Days", "60+ Days"
    decimal CollectionProbability,
    decimal ExpectedRecoveryAmount
);

public record PayableScheduleItemDto(
    string PurchaseOrderId,
    string VendorName,
    decimal Amount,
    DateTime IssueDate,
    DateTime DueDate,
    string Terms, // "Net 15", "Net 30", "Net 60"
    string Status
);

public record AgingBucketSummaryDto(
    string BucketName,
    decimal Amount,
    int Count,
    decimal CollectionProbability,
    decimal ExpectedRecoverableAmount
);

public record ReceivablesPayablesAgingDto(
    decimal TotalReceivables,
    decimal TotalPayables,
    decimal NetWorkingCapitalTradePosition,
    List<AgingBucketSummaryDto> ReceivablesBuckets,
    List<AgingBucketSummaryDto> PayablesBuckets,
    List<ReceivableAgingItemDto> DetailedReceivables,
    List<PayableScheduleItemDto> DetailedPayables
);

public record DailyCashflowPointDto(
    int DayIndex,
    DateTime Date,
    decimal OpeningBalance,
    decimal InflowDirectSales,
    decimal InflowCourierRemittance,
    decimal InflowReceivables,
    decimal InflowOther,
    decimal TotalInflow,
    decimal OutflowPayables,
    decimal OutflowPayroll,
    decimal OutflowRentLease,
    decimal OutflowSaasOverhead,
    decimal OutflowOther,
    decimal TotalOutflow,
    decimal NetDailyFlow,
    decimal ClosingBalance,
    decimal SafetyThreshold,
    bool IsDeficit,
    decimal DeficitGap
);

public record WorkingCapitalMetrics(
    decimal DSO,
    decimal DPO,
    decimal DIO,
    decimal CashConversionCycle,
    decimal TrappedLiquidityRatio,
    decimal WorkingCapitalFloat
);

public record CashflowSummaryDto(
    decimal CurrentLiquidReserve,
    decimal TotalInTransitCourierFloat,
    decimal TotalAccountsReceivable,
    decimal TotalAccountsPayable,
    decimal TrappedLiquidityRatio,
    decimal DaysSalesOutstanding,
    decimal DaysPayableOutstanding,
    decimal DaysInventoryOutstanding,
    decimal CashConversionCycle,
    decimal SafetyThreshold,
    int ProjectionHorizonDays,
    int DeficitDaysCount,
    decimal LowestProjectedBalance,
    DateTime LowestBalanceDate,
    DateTime? FirstDeficitDate,
    string DeficitSeverity, // "Healthy", "Warning", "Critical"
    decimal PeakCashBalance,
    DateTime PeakBalanceDate,
    decimal AverageDailyBurnRate,
    decimal AverageDailyInflow,
    List<CourierFloatBreakdownDto> TopCouriers
);

public record FactoringSimulationRequestDto(
    Guid TenantId,
    decimal AdvanceAmount,
    decimal DiscountFeePercentage = 2.5m,
    string SourceAssetType = "CourierFloat", // "CourierFloat", "Receivables", "Blended"
    bool ApplyOffsetToProjection = true
);

public record TrajectoryPointComparisonDto(
    int DayIndex,
    DateTime Date,
    decimal BaselineCash,
    decimal FactoredCash,
    decimal SafetyThreshold,
    bool BaselineIsDeficit,
    bool FactoredIsDeficit
);

public record FactoringSimulationResultDto(
    decimal RequestedAdvance,
    decimal MaxFactorableAmount,
    decimal DiscountFeePercentage,
    decimal FactoringFeeAmount,
    decimal NetCashInflowImmediate,
    decimal BaselineLowestBalance,
    decimal FactoredLowestBalance,
    decimal NetLiquidityImprovement,
    int BaselineDeficitDays,
    int FactoredDeficitDays,
    int DeficitDaysEliminated,
    DateTime? BaselineFirstDeficitDate,
    DateTime? FactoredFirstDeficitDate,
    List<TrajectoryPointComparisonDto> TrajectoryComparison
);

public record CashflowAdjustmentDto(
    Guid Id,
    Guid TenantId,
    DateTime DateUtc,
    decimal Amount,
    string Category,
    string Description,
    DateTime CreatedAtUtc
);

public record RecordAdjustmentRequestDto(
    Guid TenantId,
    DateTime DateUtc,
    decimal Amount,
    string Category,
    string Description
);

#endregion

#region Service Contract

public interface ICashflowForecastingService
{
    Task<CashflowSummaryDto> GetSummaryAsync(Guid tenantId, decimal? safetyThreshold = null, CancellationToken ct = default);
    Task<List<DailyCashflowPointDto>> GenerateProjectionAsync(Guid tenantId, int horizonDays = 90, decimal? safetyThreshold = null, CancellationToken ct = default);
    Task<List<CourierFloatBreakdownDto>> GetCourierFloatBreakdownAsync(Guid tenantId, CancellationToken ct = default);
    Task<ReceivablesPayablesAgingDto> GetReceivablesPayablesAgingAsync(Guid tenantId, CancellationToken ct = default);
    Task<FactoringSimulationResultDto> SimulateFactoringAdvanceAsync(FactoringSimulationRequestDto request, CancellationToken ct = default);
    Task<CashflowAdjustmentDto> RecordAdjustmentAsync(RecordAdjustmentRequestDto request, CancellationToken ct = default);
    Task<List<CashflowAdjustmentDto>> GetAdjustmentsAsync(Guid tenantId, CancellationToken ct = default);
    WorkingCapitalMetrics CalculateWorkingCapitalMetrics(
        decimal receivables,
        decimal payables,
        decimal inventory,
        decimal creditSales,
        decimal cogs,
        decimal courierFloat,
        decimal overdueReceivables,
        decimal cashReserve);
    List<DayClearanceShare> CalculateWeibullClearanceSchedule(decimal netAmount, double meanDays, int maxDays = 14, double shape = 2.0);
}

#endregion

#region Implementation

public class CashflowForecastingService : ICashflowForecastingService
{
    private readonly SGrowingDbContext? _db;
    private static readonly ConcurrentDictionary<Guid, List<CashflowAdjustmentDto>> _adjustmentsStore = new();

    public const decimal DefaultSafetyThreshold = 15000.0m;
    public const decimal DefaultStartingReserve = 42500.0m;

    public CashflowForecastingService(SGrowingDbContext? db = null)
    {
        _db = db;
    }

    public async Task<CashflowSummaryDto> GetSummaryAsync(Guid tenantId, decimal? safetyThreshold = null, CancellationToken ct = default)
    {
        var threshold = safetyThreshold ?? DefaultSafetyThreshold;
        var projection = await GenerateProjectionAsync(tenantId, 90, threshold, ct);
        var couriers = await GetCourierFloatBreakdownAsync(tenantId, ct);
        var aging = await GetReceivablesPayablesAgingAsync(tenantId, ct);

        decimal currentReserve = DefaultStartingReserve;
        decimal totalCourierFloat = couriers.Sum(c => c.NetReceivableCash);
        decimal totalReceivables = aging.TotalReceivables;
        decimal totalPayables = aging.TotalPayables;

        decimal overdueReceivables = aging.DetailedReceivables
            .Where(r => r.AgingBucket is "31-60 Days" or "60+ Days")
            .Sum(r => r.Amount);

        // Benchmark quarterly sales & COGS
        decimal creditSales90d = 214000.0m;
        decimal cogs90d = 132000.0m;
        decimal averageInventory = 65000.0m;

        var metrics = CalculateWorkingCapitalMetrics(
            totalReceivables,
            totalPayables,
            averageInventory,
            creditSales90d,
            cogs90d,
            totalCourierFloat,
            overdueReceivables,
            currentReserve);

        var deficitDays = projection.Where(p => p.IsDeficit).ToList();
        var lowestPoint = projection.OrderBy(p => p.ClosingBalance).FirstOrDefault() ?? projection.First();
        var peakPoint = projection.OrderByDescending(p => p.ClosingBalance).FirstOrDefault() ?? projection.First();

        var firstDeficit = deficitDays.FirstOrDefault()?.Date;
        string severity = "Healthy";
        if (lowestPoint.ClosingBalance < 0)
        {
            severity = "Critical";
        }
        else if (deficitDays.Any())
        {
            severity = "Warning";
        }

        decimal avgDailyInflow = projection.Average(p => p.TotalInflow);
        decimal avgDailyBurn = projection.Average(p => p.TotalOutflow);

        return new CashflowSummaryDto(
            CurrentLiquidReserve: currentReserve,
            TotalInTransitCourierFloat: totalCourierFloat,
            TotalAccountsReceivable: totalReceivables,
            TotalAccountsPayable: totalPayables,
            TrappedLiquidityRatio: metrics.TrappedLiquidityRatio,
            DaysSalesOutstanding: metrics.DSO,
            DaysPayableOutstanding: metrics.DPO,
            DaysInventoryOutstanding: metrics.DIO,
            CashConversionCycle: metrics.CashConversionCycle,
            SafetyThreshold: threshold,
            ProjectionHorizonDays: 90,
            DeficitDaysCount: deficitDays.Count,
            LowestProjectedBalance: lowestPoint.ClosingBalance,
            LowestBalanceDate: lowestPoint.Date,
            FirstDeficitDate: firstDeficit,
            DeficitSeverity: severity,
            PeakCashBalance: peakPoint.ClosingBalance,
            PeakBalanceDate: peakPoint.Date,
            AverageDailyBurnRate: Math.Round(avgDailyBurn, 2),
            AverageDailyInflow: Math.Round(avgDailyInflow, 2),
            TopCouriers: couriers
        );
    }

    public async Task<List<DailyCashflowPointDto>> GenerateProjectionAsync(
        Guid tenantId,
        int horizonDays = 90,
        decimal? safetyThreshold = null,
        CancellationToken ct = default)
    {
        var threshold = safetyThreshold ?? DefaultSafetyThreshold;
        var couriers = await GetCourierFloatBreakdownAsync(tenantId, ct);
        var aging = await GetReceivablesPayablesAgingAsync(tenantId, ct);
        var adjustments = await GetAdjustmentsAsync(tenantId, ct);

        // Map initial in-transit courier float into upcoming clearance calendar
        var courierDailyClearance = new Dictionary<int, decimal>();
        foreach (var c in couriers)
        {
            foreach (var share in c.ClearanceDistribution)
            {
                int day = share.DayOffset;
                if (!courierDailyClearance.ContainsKey(day))
                    courierDailyClearance[day] = 0m;
                courierDailyClearance[day] += share.ExpectedAmount;
            }
        }

        // Map receivables collections to due dates (overdue collections are recovered over days 15-45)
        var receivablesDailyInflow = new Dictionary<int, decimal>();
        var baseDate = DateTime.UtcNow.Date;
        foreach (var rec in aging.DetailedReceivables)
        {
            int dueOffset;
            if (rec.DaysPastDue > 0)
            {
                // Overdue invoices require collection follow-up, clearing around days 15-40
                dueOffset = Math.Min(horizonDays, 14 + (rec.DaysPastDue / 3));
            }
            else
            {
                dueOffset = Math.Max(1, (int)(rec.DueDate.Date - baseDate).TotalDays);
            }

            if (dueOffset <= horizonDays)
            {
                if (!receivablesDailyInflow.ContainsKey(dueOffset))
                    receivablesDailyInflow[dueOffset] = 0m;
                receivablesDailyInflow[dueOffset] += rec.ExpectedRecoveryAmount;
            }
        }

        // Map payables to due dates
        var payablesDailyOutflow = new Dictionary<int, decimal>();
        foreach (var pay in aging.DetailedPayables)
        {
            int dueOffset = Math.Max(1, (int)(pay.DueDate.Date - baseDate).TotalDays);
            if (dueOffset <= horizonDays)
            {
                if (!payablesDailyOutflow.ContainsKey(dueOffset))
                    payablesDailyOutflow[dueOffset] = 0m;
                payablesDailyOutflow[dueOffset] += pay.Amount;
            }
        }

        // Rolling buffer for new COD sales generated on day t that clear on day t + delay
        var rollingCodPipeline = new Dictionary<int, decimal>();

        // Build continuous simulation
        var points = new List<DailyCashflowPointDto>();
        decimal runningBalance = DefaultStartingReserve;

        for (int day = 1; day <= horizonDays; day++)
        {
            var currentDate = baseDate.AddDays(day);
            decimal opening = runningBalance;

            // 1. Daily E-Commerce GMV: 35% Prepaid Credit Card (instant), 65% COD (in-transit float)
            bool isWeekend = currentDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            decimal baseGmv = isWeekend ? 3850.0m : 2750.0m;
            decimal cyclicVariance = (decimal)(Math.Sin(day * 0.4) * 220.0);
            decimal totalDailyGmv = Math.Round(baseGmv + cyclicVariance, 2);

            decimal instantPrepaidInflow = Math.Round(totalDailyGmv * 0.35m, 2);
            decimal newCodInTransit = Math.Round(totalDailyGmv * 0.65m * 0.93m, 2); // 93% net after parcel fees

            // Queue the new COD float into future clearance days (mean delay 5 days)
            int expectedClearDay = day + 5;
            if (expectedClearDay <= horizonDays + 14)
            {
                if (!rollingCodPipeline.ContainsKey(expectedClearDay))
                    rollingCodPipeline[expectedClearDay] = 0m;
                rollingCodPipeline[expectedClearDay] += newCodInTransit;
            }

            // 2. Courier COD Remittance Inflow (Initial unremitted float + cleared rolling float)
            decimal courierRemittance = 0m;
            if (courierDailyClearance.TryGetValue(day, out var initialClearance))
            {
                courierRemittance += initialClearance;
            }
            if (rollingCodPipeline.TryGetValue(day, out var clearedRollingCod))
            {
                courierRemittance += clearedRollingCod;
            }

            // 3. Receivables Inflow
            decimal receivablesInflow = receivablesDailyInflow.TryGetValue(day, out var recAmt) ? recAmt : 0m;

            // 4. Manual Inflows / Outflows
            var dayAdjustments = adjustments.Where(a => a.DateUtc.Date == currentDate.Date).ToList();
            decimal adjInflows = dayAdjustments.Where(a => a.Amount > 0).Sum(a => a.Amount);
            decimal adjOutflows = dayAdjustments.Where(a => a.Amount < 0).Sum(a => Math.Abs(a.Amount));

            decimal totalInflow = instantPrepaidInflow + courierRemittance + receivablesInflow + adjInflows;

            // 5. Vendor Payables Outflow
            decimal payablesOutflow = payablesDailyOutflow.TryGetValue(day, out var payAmt) ? payAmt : 0m;

            // 6. Operating Outflows: Ad Spend (Meta/TikTok), Payroll, Rent Lease, SaaS, Utilities
            decimal adSpend = Math.Round(totalDailyGmv * 0.32m, 2); // 32% advertising acquisition cost
            decimal payroll = 0m;
            decimal rentLease = 0m;
            decimal saasOverhead = 0m;
            decimal otherOutflow = adSpend + adjOutflows;

            // Monthly Payroll on 25th of month (or day 25, 55, 85)
            if (currentDate.Day == 25 || day is 25 or 55 or 85)
            {
                payroll = 16500.0m;
            }

            // Warehouse Rent Lease on 5th of month
            if (currentDate.Day == 5 || day is 5 or 35 or 65)
            {
                rentLease = 4800.0m;
            }

            // SaaS & Cloud Infrastructure on 1st of month
            if (currentDate.Day == 1 || day is 1 or 31 or 61)
            {
                saasOverhead = 2400.0m;
            }

            // Utilities & Compliance on 10th of month
            if (currentDate.Day == 10 || day is 10 or 40 or 70)
            {
                otherOutflow += 1200.0m;
            }

            // Modest daily packaging/fulfillment consumables
            otherOutflow += 220.0m;

            decimal totalOutflow = payablesOutflow + payroll + rentLease + saasOverhead + otherOutflow;
            decimal netFlow = totalInflow - totalOutflow;
            decimal closing = opening + netFlow;
            runningBalance = closing;

            bool isDeficit = closing < threshold;
            decimal deficitGap = isDeficit ? Math.Max(0m, threshold - closing) : 0m;

            points.Add(new DailyCashflowPointDto(
                DayIndex: day,
                Date: currentDate,
                OpeningBalance: opening,
                InflowDirectSales: instantPrepaidInflow,
                InflowCourierRemittance: courierRemittance,
                InflowReceivables: receivablesInflow,
                InflowOther: adjInflows,
                TotalInflow: totalInflow,
                OutflowPayables: payablesOutflow,
                OutflowPayroll: payroll,
                OutflowRentLease: rentLease,
                OutflowSaasOverhead: saasOverhead,
                OutflowOther: otherOutflow,
                TotalOutflow: totalOutflow,
                NetDailyFlow: netFlow,
                ClosingBalance: closing,
                SafetyThreshold: threshold,
                IsDeficit: isDeficit,
                DeficitGap: deficitGap
            ));
        }

        return points;
    }

    public async Task<List<CourierFloatBreakdownDto>> GetCourierFloatBreakdownAsync(Guid tenantId, CancellationToken ct = default)
    {
        // If DB has real data for tenant, aggregate it
        if (_db != null)
        {
            var dbOrders = await _db.CodOrders
                .Where(o => o.TenantId == tenantId && (o.Status == CodOrderStatus.Dispatched || o.Status == CodOrderStatus.Delivered))
                .ToListAsync(ct);

            var schedules = await _db.CarrierRemittanceScheduleConfigs
                .Where(s => s.TenantId == tenantId && s.IsActive)
                .ToListAsync(ct);

            if (dbOrders.Count > 0 && schedules.Count > 0)
            {
                var list = new List<CourierFloatBreakdownDto>();
                int perCourierCount = Math.Max(1, dbOrders.Count / schedules.Count);
                decimal perCourierGross = Math.Round(dbOrders.Sum(o => o.TotalAmount) / schedules.Count, 2);

                foreach (var s in schedules)
                {
                    decimal fees = perCourierCount * s.ContractFeePerParcel;
                    decimal net = Math.Max(0m, perCourierGross - fees);
                    double turnaround = s.AverageLagDays > 0 ? (double)s.AverageLagDays : 5.0;
                    var shares = CalculateWeibullClearanceSchedule(net, turnaround, 14);

                    list.Add(new CourierFloatBreakdownDto(
                        CourierName: s.CourierName,
                        InTransitParcelCount: perCourierCount,
                        GrossCashValue: perCourierGross,
                        EstimatedFees: fees,
                        NetReceivableCash: net,
                        AverageTurnaroundDays: turnaround,
                        RemittanceFrequency: s.Frequency.ToString(),
                        ClearanceSchedulePattern: $"Weibull (Mean {turnaround:F1}d)",
                        ClearanceDistribution: shares
                    ));
                }
                return list;
            }
        }

        // Realistic High-Fidelity Multi-Courier Ledger Seed
        var carriers = new List<(string Name, int Parcels, decimal Gross, decimal FeePerParcel, double Turnaround, string Frequency)>
        {
            ("Aramex Express", 164, 15420.0m, 8.5m, 5.0, "Weekly (Tuesday)"),
            ("DHL Express COD", 92, 11850.0m, 11.0m, 3.0, "Bi-Weekly (Mon/Thu)"),
            ("Yalidine Local Courier", 148, 9340.0m, 7.0m, 8.0, "Weekly (Thursday)"),
            ("FedEx Ground COD", 46, 4280.0m, 8.0m, 4.0, "Rolling 4-Day Batch")
        };

        var result = new List<CourierFloatBreakdownDto>();
        foreach (var c in carriers)
        {
            decimal feeTotal = c.Parcels * c.FeePerParcel;
            decimal net = Math.Max(0m, c.Gross - feeTotal);
            var shares = CalculateWeibullClearanceSchedule(net, c.Turnaround, 14, 2.0);

            result.Add(new CourierFloatBreakdownDto(
                CourierName: c.Name,
                InTransitParcelCount: c.Parcels,
                GrossCashValue: c.Gross,
                EstimatedFees: feeTotal,
                NetReceivableCash: net,
                AverageTurnaroundDays: c.Turnaround,
                RemittanceFrequency: c.Frequency,
                ClearanceSchedulePattern: $"Weibull Delay Curve (Mean {c.Turnaround:F1}d)",
                ClearanceDistribution: shares
            ));
        }

        return result;
    }

    public async Task<ReceivablesPayablesAgingDto> GetReceivablesPayablesAgingAsync(Guid tenantId, CancellationToken ct = default)
    {
        var baseDate = DateTime.UtcNow.Date;

        // B2B Wholesale Invoices
        var receivables = new List<ReceivableAgingItemDto>
        {
            new("INV-2026-001", "Carrefour Maghreb Retail", 18500.0m, baseDate.AddDays(-12), baseDate.AddDays(18), 0, "Current", 0.95m, 18500.0m * 0.95m),
            new("INV-2026-002", "Monoprix Distribution Hub", 14200.0m, baseDate.AddDays(-22), baseDate.AddDays(8), 0, "Current", 0.95m, 14200.0m * 0.95m),
            new("INV-2026-003", "Atlas Superstores SARL", 9800.0m, baseDate.AddDays(-38), baseDate.AddDays(-8), 8, "1-30 Days", 0.85m, 9800.0m * 0.85m),
            new("INV-2026-004", "BioVibe Organic Boutiques", 5600.0m, baseDate.AddDays(-44), baseDate.AddDays(-14), 14, "1-30 Days", 0.85m, 5600.0m * 0.85m),
            new("INV-2026-005", "MedPharm Wholesale Group", 7300.0m, baseDate.AddDays(-75), baseDate.AddDays(-45), 45, "31-60 Days", 0.60m, 7300.0m * 0.60m),
            new("INV-2026-006", "Sahara Duty Free Outlets", 4500.0m, baseDate.AddDays(-82), baseDate.AddDays(-52), 52, "31-60 Days", 0.60m, 4500.0m * 0.60m),
            new("INV-2026-007", "Apex Hypermarket Chain", 3000.0m, baseDate.AddDays(-110), baseDate.AddDays(-80), 80, "60+ Days", 0.30m, 3000.0m * 0.30m)
        };

        // Vendor Purchase Orders (Accounts Payable)
        var payables = new List<PayableScheduleItemDto>
        {
            new("PO-2026-881", "Alpha Eco-Packaging Ltd", 8400.0m, baseDate.AddDays(-16), baseDate.AddDays(14), "Net 30", "Scheduled"),
            new("PO-2026-894", "BioBotanical Raw Ingredients Corp", 19500.0m, baseDate.AddDays(-32), baseDate.AddDays(28), "Net 60", "Scheduled"),
            new("PO-2026-905", "Nordic Glass Container Works", 12200.0m, baseDate.AddDays(-15), baseDate.AddDays(45), "Net 60", "Scheduled"),
            new("PO-2026-920", "Global Freight & Forwarding Int.", 6800.0m, baseDate.AddDays(-5), baseDate.AddDays(25), "Net 30", "Scheduled"),
            new("PO-2026-935", "OmniCarton Automated Fulfillment", 9100.0m, baseDate.AddDays(-2), baseDate.AddDays(58), "Net 60", "PendingApproval")
        };

        // Summarize AR Buckets
        var arBuckets = new List<AgingBucketSummaryDto>
        {
            new("Current (0-30d)", receivables.Where(r => r.AgingBucket == "Current").Sum(r => r.Amount), receivables.Count(r => r.AgingBucket == "Current"), 0.95m, receivables.Where(r => r.AgingBucket == "Current").Sum(r => r.ExpectedRecoveryAmount)),
            new("1-30 Days Past Due", receivables.Where(r => r.AgingBucket == "1-30 Days").Sum(r => r.Amount), receivables.Count(r => r.AgingBucket == "1-30 Days"), 0.85m, receivables.Where(r => r.AgingBucket == "1-30 Days").Sum(r => r.ExpectedRecoveryAmount)),
            new("31-60 Days Past Due", receivables.Where(r => r.AgingBucket == "31-60 Days").Sum(r => r.Amount), receivables.Count(r => r.AgingBucket == "31-60 Days"), 0.60m, receivables.Where(r => r.AgingBucket == "31-60 Days").Sum(r => r.ExpectedRecoveryAmount)),
            new("60+ Days Past Due", receivables.Where(r => r.AgingBucket == "60+ Days").Sum(r => r.Amount), receivables.Count(r => r.AgingBucket == "60+ Days"), 0.30m, receivables.Where(r => r.AgingBucket == "60+ Days").Sum(r => r.ExpectedRecoveryAmount))
        };

        // Summarize AP Buckets
        var apBuckets = new List<AgingBucketSummaryDto>
        {
            new("Due Within 15 Days", payables.Where(p => (p.DueDate - baseDate).TotalDays <= 15).Sum(p => p.Amount), payables.Count(p => (p.DueDate - baseDate).TotalDays <= 15), 1.0m, payables.Where(p => (p.DueDate - baseDate).TotalDays <= 15).Sum(p => p.Amount)),
            new("Net 30 (16-30 Days)", payables.Where(p => (p.DueDate - baseDate).TotalDays > 15 && (p.DueDate - baseDate).TotalDays <= 30).Sum(p => p.Amount), payables.Count(p => (p.DueDate - baseDate).TotalDays > 15 && (p.DueDate - baseDate).TotalDays <= 30), 1.0m, payables.Where(p => (p.DueDate - baseDate).TotalDays > 15 && (p.DueDate - baseDate).TotalDays <= 30).Sum(p => p.Amount)),
            new("Net 60 (31-60 Days)", payables.Where(p => (p.DueDate - baseDate).TotalDays > 30 && (p.DueDate - baseDate).TotalDays <= 60).Sum(p => p.Amount), payables.Count(p => (p.DueDate - baseDate).TotalDays > 30 && (p.DueDate - baseDate).TotalDays <= 60), 1.0m, payables.Where(p => (p.DueDate - baseDate).TotalDays > 30 && (p.DueDate - baseDate).TotalDays <= 60).Sum(p => p.Amount)),
            new("60+ Days Out", payables.Where(p => (p.DueDate - baseDate).TotalDays > 60).Sum(p => p.Amount), payables.Count(p => (p.DueDate - baseDate).TotalDays > 60), 1.0m, payables.Where(p => (p.DueDate - baseDate).TotalDays > 60).Sum(p => p.Amount))
        };

        decimal totalAr = receivables.Sum(r => r.Amount);
        decimal totalAp = payables.Sum(p => p.Amount);
        decimal netTrade = totalAr - totalAp;

        return new ReceivablesPayablesAgingDto(
            TotalReceivables: totalAr,
            TotalPayables: totalAp,
            NetWorkingCapitalTradePosition: netTrade,
            ReceivablesBuckets: arBuckets,
            PayablesBuckets: apBuckets,
            DetailedReceivables: receivables,
            DetailedPayables: payables
        );
    }

    public async Task<FactoringSimulationResultDto> SimulateFactoringAdvanceAsync(
        FactoringSimulationRequestDto request,
        CancellationToken ct = default)
    {
        var baseline = await GenerateProjectionAsync(request.TenantId, 90, DefaultSafetyThreshold, ct);
        var couriers = await GetCourierFloatBreakdownAsync(request.TenantId, ct);
        var aging = await GetReceivablesPayablesAgingAsync(request.TenantId, ct);

        decimal courierFloatAvailable = couriers.Sum(c => c.NetReceivableCash);
        decimal currentArAvailable = aging.DetailedReceivables.Where(r => r.AgingBucket == "Current").Sum(r => r.Amount);

        decimal maxFactorable = request.SourceAssetType switch
        {
            "CourierFloat" => courierFloatAvailable,
            "Receivables" => currentArAvailable,
            _ => courierFloatAvailable + currentArAvailable
        };

        decimal approvedAdvance = Math.Min(Math.Max(0m, request.AdvanceAmount), maxFactorable);
        decimal feeRate = request.DiscountFeePercentage > 0 ? request.DiscountFeePercentage : 2.5m;
        decimal feeAmount = Math.Round(approvedAdvance * (feeRate / 100.0m), 2);
        decimal netImmediateCash = approvedAdvance - feeAmount;

        // Simulate new trajectory with instant liquidity injection and offset
        var comparison = new List<TrajectoryPointComparisonDto>();
        decimal runningOffset = approvedAdvance;
        decimal runningFactoredCash = DefaultStartingReserve;

        for (int i = 0; i < baseline.Count; i++)
        {
            var basePoint = baseline[i];
            decimal factoredInflow = basePoint.TotalInflow;

            if (basePoint.DayIndex == 1)
            {
                // Immediate capital injection
                factoredInflow += netImmediateCash;
            }

            // Offset against scheduled float clearance to prevent double counting
            if (request.ApplyOffsetToProjection && runningOffset > 0 && basePoint.InflowCourierRemittance > 0)
            {
                decimal deduction = Math.Min(runningOffset, basePoint.InflowCourierRemittance);
                factoredInflow -= deduction;
                runningOffset -= deduction;
            }

            decimal factoredNet = factoredInflow - basePoint.TotalOutflow;
            decimal factoredClosing = (i == 0 ? DefaultStartingReserve : runningFactoredCash) + factoredNet;
            runningFactoredCash = factoredClosing;

            bool baselineDeficit = basePoint.IsDeficit;
            bool factoredDeficit = factoredClosing < DefaultSafetyThreshold;

            comparison.Add(new TrajectoryPointComparisonDto(
                DayIndex: basePoint.DayIndex,
                Date: basePoint.Date,
                BaselineCash: basePoint.ClosingBalance,
                FactoredCash: factoredClosing,
                SafetyThreshold: DefaultSafetyThreshold,
                BaselineIsDeficit: baselineDeficit,
                FactoredIsDeficit: factoredDeficit
            ));
        }

        decimal baseLowest = baseline.Min(p => p.ClosingBalance);
        decimal factoredLowest = comparison.Min(p => p.FactoredCash);
        int baseDeficitDays = baseline.Count(p => p.IsDeficit);
        int factoredDeficitDays = comparison.Count(p => p.FactoredIsDeficit);
        int eliminatedDays = Math.Max(0, baseDeficitDays - factoredDeficitDays);

        var baseFirstDeficit = baseline.FirstOrDefault(p => p.IsDeficit)?.Date;
        var factoredFirstDeficit = comparison.FirstOrDefault(p => p.FactoredIsDeficit)?.Date;

        return new FactoringSimulationResultDto(
            RequestedAdvance: request.AdvanceAmount,
            MaxFactorableAmount: maxFactorable,
            DiscountFeePercentage: feeRate,
            FactoringFeeAmount: feeAmount,
            NetCashInflowImmediate: netImmediateCash,
            BaselineLowestBalance: baseLowest,
            FactoredLowestBalance: factoredLowest,
            NetLiquidityImprovement: factoredLowest - baseLowest,
            BaselineDeficitDays: baseDeficitDays,
            FactoredDeficitDays: factoredDeficitDays,
            DeficitDaysEliminated: eliminatedDays,
            BaselineFirstDeficitDate: baseFirstDeficit,
            FactoredFirstDeficitDate: factoredFirstDeficit,
            TrajectoryComparison: comparison
        );
    }

    public Task<CashflowAdjustmentDto> RecordAdjustmentAsync(RecordAdjustmentRequestDto request, CancellationToken ct = default)
    {
        var adj = new CashflowAdjustmentDto(
            Id: Guid.NewGuid(),
            TenantId: request.TenantId,
            DateUtc: request.DateUtc,
            Amount: request.Amount,
            Category: string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category,
            Description: string.IsNullOrWhiteSpace(request.Description) ? "Manual Adjustment" : request.Description,
            CreatedAtUtc: DateTime.UtcNow
        );

        _adjustmentsStore.AddOrUpdate(
            request.TenantId,
            new List<CashflowAdjustmentDto> { adj },
            (_, list) => { lock (list) { list.Add(adj); } return list; });

        return Task.FromResult(adj);
    }

    public Task<List<CashflowAdjustmentDto>> GetAdjustmentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_adjustmentsStore.TryGetValue(tenantId, out var list))
        {
            lock (list)
            {
                return Task.FromResult(list.OrderBy(a => a.DateUtc).ToList());
            }
        }
        return Task.FromResult(new List<CashflowAdjustmentDto>());
    }

    public WorkingCapitalMetrics CalculateWorkingCapitalMetrics(
        decimal receivables,
        decimal payables,
        decimal inventory,
        decimal creditSales,
        decimal cogs,
        decimal courierFloat,
        decimal overdueReceivables,
        decimal cashReserve)
    {
        const decimal periodDays = 90.0m;

        decimal dso = creditSales > 0 ? Math.Round((receivables / creditSales) * periodDays, 1) : 0m;
        decimal dpo = cogs > 0 ? Math.Round((payables / cogs) * periodDays, 1) : 0m;
        decimal dio = cogs > 0 ? Math.Round((inventory / cogs) * periodDays, 1) : 0m;
        decimal ccc = Math.Round(dio + dso - dpo, 1);

        decimal totalOperatingLiquidity = Math.Max(1.0m, cashReserve + courierFloat + receivables);
        decimal trappedLiquidity = courierFloat + overdueReceivables;
        decimal trappedRatio = Math.Round((trappedLiquidity / totalOperatingLiquidity) * 100.0m, 1);

        return new WorkingCapitalMetrics(dso, dpo, dio, ccc, trappedRatio, courierFloat);
    }

    public List<DayClearanceShare> CalculateWeibullClearanceSchedule(
        decimal netAmount,
        double meanDays,
        int maxDays = 14,
        double shape = 2.0)
    {
        if (netAmount <= 0)
        {
            return Enumerable.Range(1, maxDays)
                .Select(d => new DayClearanceShare(d, 0.0, 0m))
                .ToList();
        }

        // Weibull scale parameter λ calculated from mean: Mean = λ * Γ(1 + 1/shape)
        double gamma = shape switch
        {
            2.0 => 0.88622692545,
            1.5 => 0.90274529295,
            2.5 => 0.88726381750,
            3.0 => 0.89297951157,
            _ => 0.88622692545
        };

        double lambda = Math.Max(0.5, meanDays / gamma);
        var rawProbs = new double[maxDays];
        double prevCdf = 0.0;

        for (int day = 1; day <= maxDays; day++)
        {
            double t = day;
            double cdf = 1.0 - Math.Exp(-Math.Pow(t / lambda, shape));
            double prob = Math.Max(0.0, cdf - prevCdf);
            rawProbs[day - 1] = prob;
            prevCdf = cdf;
        }

        double totalProb = rawProbs.Sum();
        if (totalProb <= 0.0) totalProb = 1.0;

        var result = new List<DayClearanceShare>();
        decimal distributed = 0m;

        for (int i = 0; i < maxDays; i++)
        {
            double normalized = rawProbs[i] / totalProb;
            decimal dayAmt = i == maxDays - 1
                ? netAmount - distributed
                : Math.Round(netAmount * (decimal)normalized, 2);

            distributed += dayAmt;
            result.Add(new DayClearanceShare(i + 1, Math.Round(normalized, 4), dayAmt));
        }

        return result;
    }
}

#endregion
