using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Support.CapacityPlanning;

public interface ICapacityPlanningEngine
{
    CapacityPlan GetPlan(string planId);
    CapacityPlan RebalanceCapacity(RebalanceCapacityRequest request);
    CapacityPlan SimulateSurge(SimulateSurgeRequest request);
}

public class CapacityPlanningEngine : ICapacityPlanningEngine
{
    private readonly ConcurrentDictionary<string, CapacityPlan> _plans = new(StringComparer.OrdinalIgnoreCase);

    public CapacityPlanningEngine()
    {
        // Seed standard 24h capacity plan
        var intervals = new List<HourlyDemandForecast>();
        for (int h = 0; h < 24; h++)
        {
            // Business hours peak (hours 9-18)
            bool isPeak = h >= 9 && h <= 18;
            int volume = isPeak ? 180 + (h * 5) : 40 + (h * 2);
            int required = isPeak ? (int)Math.Ceiling(volume / 15.0) : (int)Math.Ceiling(volume / 20.0);
            int scheduled = isPeak ? (h == 14 ? required - 3 : required) : required; // Deficit at hour 14
            bool deficit = scheduled < required;
            double sla = deficit ? 82.5 : 98.2;

            intervals.Add(new HourlyDemandForecast(
                HourOfDay: h,
                ExpectedIncomingVolume: volume,
                RequiredStaffHeadcount: required,
                ScheduledStaffHeadcount: scheduled,
                PredictedSlaAttainmentPct: sla,
                HasStaffingDeficit: deficit
            ));
        }

        var plan = new CapacityPlan(
            PlanId: "cap_plan_global_support",
            QueueName: "Global Omnichannel Tier-1 Support",
            TotalDailyVolume: intervals.Sum(i => i.ExpectedIncomingVolume),
            AiDeflectionRatePct: 45.0,
            HourlyIntervals: intervals,
            TotalDeficitHours: intervals.Count(i => i.HasStaffingDeficit),
            LastOptimizedAtUtc: DateTime.UtcNow.AddHours(-1)
        );

        _plans[plan.PlanId] = plan;
    }

    public CapacityPlan GetPlan(string planId)
    {
        if (_plans.TryGetValue(planId, out var plan))
        {
            return plan;
        }
        return _plans.Values.First();
    }

    public CapacityPlan RebalanceCapacity(RebalanceCapacityRequest request)
    {
        var plan = GetPlan(request.PlanId);

        var updatedIntervals = plan.HourlyIntervals.Select(i =>
        {
            if (i.HasStaffingDeficit)
            {
                int added = Math.Max(1, request.AdditionalAgentsAllocated);
                int newScheduled = i.ScheduledStaffHeadcount + added;
                bool stillDeficit = newScheduled < i.RequiredStaffHeadcount;
                return i with
                {
                    ScheduledStaffHeadcount = newScheduled,
                    HasStaffingDeficit = stillDeficit,
                    PredictedSlaAttainmentPct = stillDeficit ? 89.0 : 99.1
                };
            }
            return i;
        }).ToList();

        var rebalanced = plan with
        {
            HourlyIntervals = updatedIntervals,
            TotalDeficitHours = updatedIntervals.Count(i => i.HasStaffingDeficit),
            LastOptimizedAtUtc = DateTime.UtcNow
        };

        _plans[plan.PlanId] = rebalanced;
        return rebalanced;
    }

    public CapacityPlan SimulateSurge(SimulateSurgeRequest request)
    {
        var plan = GetPlan(request.PlanId);
        double multiplier = 1.0 + (request.SurgePercentage / 100.0);

        var updatedIntervals = plan.HourlyIntervals.Select(i =>
        {
            int surgedVolume = (int)Math.Round(i.ExpectedIncomingVolume * multiplier);
            int newRequired = (int)Math.Ceiling(surgedVolume / 14.0);
            bool deficit = i.ScheduledStaffHeadcount < newRequired;
            double sla = deficit ? Math.Max(65.0, 95.0 - ((newRequired - i.ScheduledStaffHeadcount) * 8.0)) : 98.0;

            return i with
            {
                ExpectedIncomingVolume = surgedVolume,
                RequiredStaffHeadcount = newRequired,
                HasStaffingDeficit = deficit,
                PredictedSlaAttainmentPct = Math.Round(sla, 1)
            };
        }).ToList();

        var surged = plan with
        {
            TotalDailyVolume = updatedIntervals.Sum(i => i.ExpectedIncomingVolume),
            HourlyIntervals = updatedIntervals,
            TotalDeficitHours = updatedIntervals.Count(i => i.HasStaffingDeficit),
            LastOptimizedAtUtc = DateTime.UtcNow
        };

        _plans[plan.PlanId] = surged;
        return surged;
    }
}
