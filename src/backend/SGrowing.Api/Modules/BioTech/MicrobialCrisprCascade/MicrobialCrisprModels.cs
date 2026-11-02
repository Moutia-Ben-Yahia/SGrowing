namespace SGrowing.Api.Modules.BioTech.MicrobialCrisprCascade;

public record BioreactorState(
    string BioreactorId,
    string HostOrganism = "Escherichia_coli_BL21",
    double OpticalDensity600 = 12.5,
    double DissolvedOxygenPercent = 38.0,
    double GlucoseConcentrationGramsPerLiter = 4.2,
    double AcetateConcentrationGramsPerLiter = 0.8,
    double TargetMetaboliteGramsPerLiter = 6.4,
    int ActiveSgRnaGuideCount = 4
);

public record MetabolicSwitchThresholds(
    double CriticalOd600Trigger = 8.0,
    double MaxAllowableAcetateGramsPerLiter = 2.0,
    double MinimumDissolvedOxygenPercent = 20.0
);

public record MicrobialCascadeEvaluationRequest(
    BioreactorState State,
    MetabolicSwitchThresholds Thresholds
);

public record MetabolicCascadeAssessment(
    string BioreactorId,
    bool Dcas9CascadeTriggered,
    double CarbonFluxRedirectionPercent,
    double PredictedFinalYieldGramsPerLiter,
    double MetabolicBurdenIndex,
    string FermentationPhase,
    string RecommendedCascadeDirective
);
