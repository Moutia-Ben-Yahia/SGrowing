namespace SGrowing.Api.Modules.BioTech.CarTCellExhaustionKinetics;

public record CarTCellPhenotypeTelemetry(
    string BatchId,
    string TargetAntigen = "CD19",
    double Pd1PositiveFraction = 0.22,
    double Lag3PositiveFraction = 0.18,
    double Tim3PositiveFraction = 0.12,
    double StemCellMemoryFraction = 0.35, // T_SCM (CD45RA+ CD62L+ CD27+)
    double DaysPostInfusion = 14.0,
    double Il2SecretionPicogramsPerMl = 450.0,
    double ChronicAntigenLoadIndex = 0.40
);

public record ExhaustionThresholdLimits(
    double MaxAllowableTerminalExhaustionIndex = 0.55,
    double MinAllowableStemCellMemoryFraction = 0.15,
    double TargetPersistenceHalfLifeDays = 21.0
);

public record CarTExhaustionEvaluationRequest(
    CarTCellPhenotypeTelemetry Telemetry,
    ExhaustionThresholdLimits Limits
);

public record CarTExhaustionAssessment(
    string BatchId,
    double ExhaustionHazardIndex,
    double EstimatedPersistenceHalfLifeDays,
    bool TerminalExhaustionRiskDetected,
    string ImmunologicalPhenotypeState,
    string RecommendedCytokineInterventionDirective
);
