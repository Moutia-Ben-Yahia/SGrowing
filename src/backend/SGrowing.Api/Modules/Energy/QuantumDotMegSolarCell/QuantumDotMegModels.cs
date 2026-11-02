namespace SGrowing.Api.Modules.Energy.QuantumDotMegSolarCell;

public record QuantumDotParameters(
    string CellId,
    string CoreMaterial = "PbSe", // Lead selenide
    string ShellMaterial = "CdSe",
    double QuantumDotDiameterNanometers = 4.2,
    double BandgapEnergyElectronVolts = 0.95,
    double IncidentSolarIrradianceWattsPerM2 = 1000.0,
    double IncidentPhotonEnergyElectronVolts = 2.85, // 3x bandgap threshold for MEG
    double CarrierCollectionEfficiency = 0.88,
    double AugerRecombinationRatePicoseconds = 45.0
);

public record MegOperationalThresholds(
    double MegEnergyThresholdMultiplier = 2.5, // E_photon >= 2.5 * E_g
    double MinAllowableQuantumYield = 1.0,
    double TargetPowerConversionEfficiencyPercent = 35.0
);

public record QuantumDotMegEvaluationRequest(
    QuantumDotParameters Parameters,
    MegOperationalThresholds Thresholds
);

public record QuantumDotMegAssessment(
    string CellId,
    bool MegThresholdExceeded,
    double CarrierMultiplicationQuantumYield,
    double PowerConversionEfficiencyPercent,
    double AugerRecombinationPenaltyPercent,
    string SolarCellEfficiencyRegime,
    string OptimizationDirective
);
