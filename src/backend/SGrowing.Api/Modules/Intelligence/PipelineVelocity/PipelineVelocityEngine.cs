using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Intelligence.PipelineVelocity;

public interface IPipelineVelocityEngine
{
    PipelineVelocityOverview GetOverview();
    AccelerationResult AccelerateStage(AccelerateStageRequest request);
}

public class PipelineVelocityEngine : IPipelineVelocityEngine
{
    private readonly ConcurrentDictionary<string, StageVelocityMetric> _stages = new(StringComparer.OrdinalIgnoreCase);

    public PipelineVelocityEngine()
    {
        // Seed pipeline stages
        var s1 = new StageVelocityMetric("stg_discovery", "Qualification & Discovery", 45, 12.0, 10.0, 18.5, false);
        var s2 = new StageVelocityMetric("stg_demo", "Technical Deep Dive & Demo", 32, 16.5, 12.0, 22.0, true);
        var s3 = new StageVelocityMetric("stg_security", "InfoSec & Compliance Review", 24, 28.0, 14.0, 12.0, true);
        var s4 = new StageVelocityMetric("stg_procurement", "Procurement & Legal Redlines", 18, 24.5, 15.0, 8.0, true);
        var s5 = new StageVelocityMetric("stg_closing", "Executive Sign-off & Closing", 14, 7.5, 7.0, 4.0, false);

        _stages[s1.StageId] = s1;
        _stages[s2.StageId] = s2;
        _stages[s3.StageId] = s3;
        _stages[s4.StageId] = s4;
        _stages[s5.StageId] = s5;
    }

    public PipelineVelocityOverview GetOverview()
    {
        var stagesList = _stages.Values.OrderBy(s => s.StageId).ToList();
        var totalDeals = stagesList.Sum(s => s.DealsCount);
        const decimal totalPipelineValue = 3850000m;
        const double overallWinRate = 28.5; // 28.5%
        var avgCycleDays = stagesList.Sum(s => s.AverageDwellDays);

        // Daily Velocity = (Deals * WinRate% * ACV) / Days
        var averageAcv = totalPipelineValue / Math.Max(1, totalDeals);
        var estimatedWonArr = (decimal)(totalDeals * (overallWinRate / 100.0)) * averageAcv;
        var dailyVelocity = avgCycleDays > 0 ? estimatedWonArr / (decimal)avgCycleDays : 0;

        return new PipelineVelocityOverview(
            TotalActiveDeals: totalDeals,
            TotalPipelineValue: totalPipelineValue,
            OverallWinRatePercentage: overallWinRate,
            AverageSalesCycleDays: Math.Round(avgCycleDays, 1),
            DailyVelocityRate: Math.Round(dailyVelocity, 2),
            Stages: stagesList
        );
    }

    public AccelerationResult AccelerateStage(AccelerateStageRequest request)
    {
        if (!_stages.TryGetValue(request.StageId, out var existing))
        {
            throw new KeyNotFoundException($"Stage {request.StageId} not found.");
        }

        var reductionDays = Math.Max(2.0, (existing.AverageDwellDays - existing.BenchmarkDays) * 0.5);
        var updatedDays = Math.Max(existing.BenchmarkDays, existing.AverageDwellDays - reductionDays);
        var isStillBottleneck = updatedDays > existing.BenchmarkDays * 1.2;

        var updated = existing with
        {
            AverageDwellDays = Math.Round(updatedDays, 1),
            IsBottleneck = isStillBottleneck
        };

        _stages[request.StageId] = updated;

        var projectedLift = (decimal)(reductionDays * 1250.0);

        return new AccelerationResult(
            request.StageId,
            updated.AverageDwellDays,
            projectedLift,
            $"Executed playbook '{request.PlaybookAction}'. Stage cycle compressed by {reductionDays:F1} days."
        );
    }
}
