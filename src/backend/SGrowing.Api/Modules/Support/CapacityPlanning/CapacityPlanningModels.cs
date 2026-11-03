using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Support.CapacityPlanning;

public record HourlyDemandForecast(
    int HourOfDay,
    int ExpectedIncomingVolume,
    int RequiredStaffHeadcount,
    int ScheduledStaffHeadcount,
    double PredictedSlaAttainmentPct,
    bool HasStaffingDeficit
);

public record CapacityPlan(
    string PlanId,
    string QueueName,
    int TotalDailyVolume,
    double AiDeflectionRatePct,
    List<HourlyDemandForecast> HourlyIntervals,
    int TotalDeficitHours,
    DateTime LastOptimizedAtUtc
);

public record RebalanceCapacityRequest(
    string PlanId,
    int AdditionalAgentsAllocated
);

public record SimulateSurgeRequest(
    string PlanId,
    int SurgePercentage
);
