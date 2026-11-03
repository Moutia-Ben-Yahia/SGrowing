using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.GeoIncrementality;

public sealed record GeoTestCellRecord(
    string CellId,
    string DmaRegionName, // Designated Market Area (e.g., Dallas-Ft Worth, Chicago, Seattle)
    string MarketRole, // TEST_TREATMENT, SYNTHETIC_CONTROL
    decimal BaselineWeeklyRevenueUsd,
    double SyntheticWeightCoefficient,
    double LiftConfidenceIntervalLowPct,
    double LiftConfidenceIntervalHighPct
);

public sealed record GeoIncrementalityExperimentRecord(
    string ExperimentId,
    string CampaignName,
    decimal TotalAdSpendUsd,
    DateTime StartDate,
    DateTime EndDate,
    double MeasuredIncrementalRevenueUsd,
    double TrueIncrementalRoasMultiplier,
    double PValueStatisticalSignificance,
    string Status // RUNNING_HOLD_OUT, COMPLETED_SIGNIFICANT, UNDERPOWERED
);

public sealed record GeoIncrementalityOverview(
    int ActiveExperimentsCount,
    int TotalDmaRegionsCalibrated,
    double MeanTrueIncrementalRoas,
    decimal TotalIncrementalRevenueCapturedUsd,
    IReadOnlyList<GeoIncrementalityExperimentRecord> Experiments,
    IReadOnlyList<GeoTestCellRecord> CalibratedCells
);

public sealed record LaunchGeoExperimentRequest(
    string CampaignName,
    decimal TotalAdSpendUsd,
    int TestDurationWeeks,
    IReadOnlyList<string> TreatmentDmaCodes
);

public sealed record EvaluateLiftRequest(
    string ExperimentId,
    decimal RealizedTreatmentRevenueUsd,
    decimal RealizedControlRevenueUsd
);
