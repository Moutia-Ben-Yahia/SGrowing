using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.CRM;

public record ForecastPeriodSummaryDto(
    Guid PeriodId,
    string PeriodName,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    decimal TargetQuotaAmount,
    decimal ClosedWonRevenue,
    decimal WeightedForecastRevenue,
    decimal UnweightedPipelineRevenue,
    double QuotaAttainmentPercentage,
    int TotalDealsCount,
    List<StageForecastBreakdownDto> Stages
);

public record StageForecastBreakdownDto(
    string StageName,
    int DealsCount,
    decimal TotalValue,
    double ProbabilityWeightPercent,
    decimal WeightedValue
);

public interface ISalesForecastingService
{
    Task<SalesForecastPeriod> CreateForecastPeriodAsync(Guid tenantId, string name, DateTime startDateUtc, DateTime endDateUtc, decimal targetQuota);
    Task<ForecastPeriodSummaryDto> GetForecastSummaryAsync(Guid tenantId, Guid periodId);
    Task<List<SalesForecastPeriod>> GetForecastPeriodsAsync(Guid tenantId);
    Task<SalesCommissionPlan> CreateCommissionPlanAsync(Guid tenantId, string planName, decimal baseRate = 10.0m, decimal threshold = 100.0m, decimal acceleratedRate = 15.0m);
    Task<SalesCommissionStatement> CalculateRepCommissionsAsync(Guid tenantId, Guid periodId, Guid planId, Guid repUserId, string repName);
    Task<SalesCommissionStatement> ApproveCommissionStatementAsync(Guid tenantId, Guid statementId, string notes = "Approved by Sales Leadership");
}

public class SalesForecastingService : ISalesForecastingService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public SalesForecastingService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<SalesForecastPeriod> CreateForecastPeriodAsync(Guid tenantId, string name, DateTime startDateUtc, DateTime endDateUtc, decimal targetQuota)
    {
        var period = SalesForecastPeriod.Create(tenantId, name, startDateUtc, endDateUtc, targetQuota);
        _db.SalesForecastPeriods.Add(period);
        await _db.SaveChangesAsync();
        return period;
    }

    public async Task<List<SalesForecastPeriod>> GetForecastPeriodsAsync(Guid tenantId)
    {
        return await _db.SalesForecastPeriods
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.StartDateUtc)
            .ToListAsync();
    }

    public async Task<ForecastPeriodSummaryDto> GetForecastSummaryAsync(Guid tenantId, Guid periodId)
    {
        var period = await _db.SalesForecastPeriods.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == periodId);
        if (period == null)
            throw new KeyNotFoundException($"Forecast period {periodId} not found.");

        var deals = await _db.Deals
            .Where(d => d.TenantId == tenantId &&
                        d.ExpectedCloseDate.HasValue &&
                        d.ExpectedCloseDate.Value >= period.StartDateUtc &&
                        d.ExpectedCloseDate.Value <= period.EndDateUtc)
            .ToListAsync();

        var stageGroups = deals.GroupBy(d => d.Stage).ToList();
        var stageMetrics = new List<StageForecastBreakdownDto>();

        decimal closedWon = 0m;
        decimal weightedTotal = 0m;
        decimal unweightedPipeline = 0m;

        foreach (var group in stageGroups)
        {
            var stage = group.Key;
            var stageDeals = group.ToList();
            var totalStageVal = stageDeals.Sum(d => d.Amount);

            double weight = stage switch
            {
                DealStage.Won => 1.0,
                DealStage.Proposal => 0.6,
                DealStage.Qualified => 0.3,
                DealStage.Prospecting => 0.1,
                _ => 0.0
            };

            decimal weightedVal = Math.Round(totalStageVal * (decimal)weight, 2);

            if (stage == DealStage.Won)
            {
                closedWon += totalStageVal;
            }
            else if (stage != DealStage.Lost)
            {
                unweightedPipeline += totalStageVal;
            }

            weightedTotal += weightedVal;

            stageMetrics.Add(new StageForecastBreakdownDto(
                StageName: stage.ToString(),
                DealsCount: stageDeals.Count,
                TotalValue: totalStageVal,
                ProbabilityWeightPercent: weight * 100.0,
                WeightedValue: weightedVal
            ));
        }

        period.ClosedWonRevenue = closedWon;
        period.WeightedForecastRevenue = weightedTotal;
        period.UnweightedPipelineRevenue = unweightedPipeline;
        await _db.SaveChangesAsync();

        double attainment = period.TargetQuotaAmount > 0
            ? (double)(closedWon / period.TargetQuotaAmount) * 100.0
            : 0.0;

        return new ForecastPeriodSummaryDto(
            PeriodId: period.Id,
            PeriodName: period.Name,
            StartDateUtc: period.StartDateUtc,
            EndDateUtc: period.EndDateUtc,
            TargetQuotaAmount: period.TargetQuotaAmount,
            ClosedWonRevenue: closedWon,
            WeightedForecastRevenue: weightedTotal,
            UnweightedPipelineRevenue: unweightedPipeline,
            QuotaAttainmentPercentage: Math.Round(attainment, 1),
            TotalDealsCount: deals.Count,
            Stages: stageMetrics
        );
    }

    public async Task<SalesCommissionPlan> CreateCommissionPlanAsync(Guid tenantId, string planName, decimal baseRate = 10.0m, decimal threshold = 100.0m, decimal acceleratedRate = 15.0m)
    {
        var plan = SalesCommissionPlan.Create(tenantId, planName, baseRate, threshold, acceleratedRate);
        _db.SalesCommissionPlans.Add(plan);
        await _db.SaveChangesAsync();
        return plan;
    }

    public async Task<SalesCommissionStatement> CalculateRepCommissionsAsync(Guid tenantId, Guid periodId, Guid planId, Guid repUserId, string repName)
    {
        var period = await _db.SalesForecastPeriods.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == periodId);
        if (period == null) throw new KeyNotFoundException($"Forecast period {periodId} not found.");

        var plan = await _db.SalesCommissionPlans.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == planId);
        if (plan == null) throw new KeyNotFoundException($"Commission plan {planId} not found.");

        // Find deals won by rep in period
        var wonDeals = await _db.Deals
            .Where(d => d.TenantId == tenantId &&
                        d.OwnerId == repUserId &&
                        d.Stage == DealStage.Won &&
                        d.ExpectedCloseDate.HasValue &&
                        d.ExpectedCloseDate.Value >= period.StartDateUtc &&
                        d.ExpectedCloseDate.Value <= period.EndDateUtc)
            .ToListAsync();

        decimal totalWon = wonDeals.Sum(d => d.Amount);
        decimal commission;

        // Quota attainment threshold check: if rep exceeded quota, accelerate
        if (period.TargetQuotaAmount > 0 && totalWon > period.TargetQuotaAmount)
        {
            decimal basePortion = period.TargetQuotaAmount * (plan.BaseRatePercent / 100.0m);
            decimal surplus = totalWon - period.TargetQuotaAmount;
            decimal acceleratedPortion = surplus * (plan.AcceleratedRatePercent / 100.0m);
            commission = Math.Round(basePortion + acceleratedPortion, 2);
        }
        else
        {
            commission = Math.Round(totalWon * (plan.BaseRatePercent / 100.0m), 2);
        }

        var statement = SalesCommissionStatement.Create(
            tenantId,
            periodId,
            repUserId,
            repName,
            wonDeals.Count,
            totalWon,
            commission
        );

        _db.SalesCommissionStatements.Add(statement);
        await _db.SaveChangesAsync();
        return statement;
    }

    public async Task<SalesCommissionStatement> ApproveCommissionStatementAsync(Guid tenantId, Guid statementId, string notes = "Approved by Sales Leadership")
    {
        var statement = await _db.SalesCommissionStatements.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == statementId);
        if (statement == null)
            throw new KeyNotFoundException($"Commission statement {statementId} not found.");

        statement.Status = CommissionStatementStatus.Approved;
        statement.ApprovedAtUtc = DateTime.UtcNow;
        statement.Notes = notes;
        await _db.SaveChangesAsync();

        await _outbox.EnqueueAsync(tenantId, "Sales.CommissionApproved", new
        {
            StatementId = statement.Id,
            statement.SalesRepUserId,
            statement.SalesRepName,
            statement.CalculatedCommission,
            statement.ApprovedAtUtc
        });

        return statement;
    }
}
