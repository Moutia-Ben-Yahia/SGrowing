namespace SGrowing.Api.Modules.Energy.TokamakPlasmaMhd;

public record TokamakShotState(
    string ReactorId,
    long ShotNumber,
    double PlasmaCurrentMegaAmps,
    double ToroidalFieldTesla,
    double MajorRadiusMeters,
    double MinorRadiusMeters,
    double PlasmaElongationKappa,
    double ElectronDensity10Power19PerM3,
    double CoreElectronTemperatureKev,
    double LockedModeAmplitudeGauss,
    double RadiatedPowerFraction,
    double PoloidalBeta
);

public record TokamakDisruptionEvaluationRequest(
    TokamakShotState ShotState,
    double PredictionHorizonMs = 30.0
);

public record MhdInstabilityProfile(
    double EdgeSafetyFactorQ95,
    double GreenwaldDensityFraction,
    double TroyonNormalizedBeta,
    bool TearingModeLocked,
    bool DensityLimitExceeded,
    bool LowQBoundaryBreached
);

public record MhdDisruptionAssessment(
    string ReactorId,
    long ShotNumber,
    double DisruptionProbabilityPercent,
    bool EmergencyMitigationRequired,
    string MitigationTechnique, // "NONE", "ECCD_ISLAND_STABILIZATION", "SHATTERED_PELLET_INJECTION_QUENCH"
    string OperationalDirective,
    MhdInstabilityProfile InstabilityProfile
);
