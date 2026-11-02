namespace SGrowing.Api.Modules.Energy.DmfcRutheniumPoisoning;

public record DmfcCellTelemetry(
    string StackId,
    double MethanolFeedConcentrationMolar = 1.0,
    double OperatingTemperatureCelsius = 60.0,
    double AnodePotentialVoltsVsRhe = 0.45,
    double CarbonMonoxideCoverageFraction = 0.42, // theta_CO
    double MethanolCrossoverCurrentDensityMilliAmpsPerCm2 = 65.0,
    double CellOperatingVoltageVolts = 0.48,
    double CurrentDensityMilliAmpsPerCm2 = 120.0
);

public record DmfcProtectionThresholds(
    double MaxAllowableCoCoverageFraction = 0.65,
    double MaxAllowableCrossoverCurrentMilliAmpsPerCm2 = 110.0,
    double AnodeElectrochemicalStrippingPulsePotentialVolts = 0.85
);

public record DmfcEvaluationRequest(
    DmfcCellTelemetry Telemetry,
    DmfcProtectionThresholds Thresholds
);

public record DmfcPoisoningMitigationPlan(
    string StackId,
    double StackElectricalPowerDensityMilliWattsPerCm2,
    double FuelUtilizationEfficiencyPercent,
    bool AnodeCoPoisoningCriticalDetected,
    bool ExcessiveMethanolCrossoverDetected,
    string CatalyticRegime,
    string RecommendedElectrochemicalDirective
);
