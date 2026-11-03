using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Intelligence.PipelineVelocity;

public record StageVelocityMetric(
    string StageId,
    string StageName,
    int DealsCount,
    double AverageDwellDays,
    double BenchmarkDays,
    double StageDropoffRate,
    bool IsBottleneck
);

public record PipelineVelocityOverview(
    int TotalActiveDeals,
    decimal TotalPipelineValue,
    double OverallWinRatePercentage,
    double AverageSalesCycleDays,
    decimal DailyVelocityRate, // V = (Deals * WinRate * ACV) / Days
    List<StageVelocityMetric> Stages
);

public record AccelerateStageRequest(
    string StageId,
    string PlaybookAction // e.g. "TriggerExecutiveSponsorIntro", "FastTrackSecurityReview"
);

public record AccelerationResult(
    string StageId,
    double NewDwellDays,
    decimal ProjectedVelocityLift,
    string ActionLog
);
