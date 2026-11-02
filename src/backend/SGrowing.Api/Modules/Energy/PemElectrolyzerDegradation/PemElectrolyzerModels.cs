namespace SGrowing.Api.Modules.Energy.PemElectrolyzerDegradation;

public record PemElectrolyzerCellTelemetry(
    string StackId,
    int CellCount = 120,
    double CurrentDensityAmpsPerCm2 = 1.8,
    double OperatingTemperatureCelsius = 65.0,
    double OperatingPressureBar = 30.0,
    double CellVoltageVolts = 1.95,
    double HydrogenInOxygenPercent = 0.45,
    double FluorineEmissionRateMicrogramsPerCm2Hour = 0.12,
    double CumulativeOperatingHours = 2500.0,
    double AnodeIridiumLoadingMgPerCm2 = 1.5,
    double CathodePlatinumLoadingMgPerCm2 = 0.5
);

public record PemDegradationThresholds(
    double MaxAllowableHydrogenInOxygenPercent = 1.8,
    double MaxAllowableCellVoltageVolts = 2.20,
    double MaxAllowableFluorineEmissionRate = 0.80,
    double EndOfLifeVoltageIncreaseVolts = 0.25
);

public record PemElectrolyzerEvaluationRequest(
    PemElectrolyzerCellTelemetry Telemetry,
    PemDegradationThresholds Thresholds
);

public record PemDegradationAssessment(
    string StackId,
    double FaradayEfficiencyPercent,
    double HydrogenProductionRateNm3PerHour,
    double OhmicResistanceMilliOhmCm2,
    double EstimatedRemainingMembraneLifeHours,
    bool FlammabilityCrossoverRiskDetected,
    string StackDegradationStatus,
    string RecommendedMitigationDirective
);
