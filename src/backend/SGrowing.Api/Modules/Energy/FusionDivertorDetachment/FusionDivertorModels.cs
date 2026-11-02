namespace SGrowing.Api.Modules.Energy.FusionDivertorDetachment;

public record DivertorTargetTelemetry(
    string DivertorId,
    double TargetHeatFluxMegaWattsPerM2 = 4.2,
    double TargetElectronTemperatureElectronVolts = 3.5, // eV (detached regime < 5 eV)
    double IonSaturationCurrentAmpsPerCm2 = 18.0,
    double UpstreamSolDensityM3 = 4.5e19,
    double CoreRadiatedPowerFraction = 0.32, // P_rad / P_tot
    string SeededImpurityGas = "Nitrogen", // N2, Ar, or Ne
    double CurrentGasPuffFlowRateBarLitersPerSec = 12.0
);

public record DivertorProtectionLimits(
    double MaxAllowableHeatFluxMegaWattsPerM2 = 10.0, // Monoblock tungsten thermal limit
    double MaxAllowableElectronTemperatureEv = 5.0, // Physical sputtering limit
    double MaxCoreRadiatedFraction = 0.45, // Core collapse limit
    double TargetDegreeOfDetachment = 4.0
);

public record DivertorDetachmentRequest(
    DivertorTargetTelemetry Telemetry,
    DivertorProtectionLimits Limits
);

public record DivertorDetachmentDecision(
    string DivertorId,
    double DegreeOfDetachment,
    bool DetachmentAchieved,
    bool CoreRadiativeCollapseRiskDetected,
    double RecommendedPuffFlowRateBarLitersPerSec,
    string PlasmaExhaustRegime,
    string ActuatorDirective
);
