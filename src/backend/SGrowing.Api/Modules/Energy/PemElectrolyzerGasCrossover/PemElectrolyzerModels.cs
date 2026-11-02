namespace SGrowing.Api.Modules.Energy.PemElectrolyzerGasCrossover;

public record PemElectrolyzerTelemetry
{
    public required string StackUnitId { get; init; }
    public double OperatingCurrentDensityAmpsPerCm2 { get; init; } = 2.2; // 1.5 - 3.0 A/cm2
    public double OperatingTemperatureCelsius { get; init; } = 78.0; // ~60 - 85 C
    public double OperatingCathodeHydrogenPressureBar { get; init; } = 30.0; // High pressure H2 (20 - 35 bar)
    public double OperatingAnodeOxygenPressureBar { get; init; } = 2.0;
    public double MembraneThicknessMicrons { get; init; } = 125.0; // Nafion 115/212 (50 - 175 um)
    public double HydrogenInOxygenAnodeFractionPercent { get; init; } = 0.85; // Target <= 1.8%, safe limit 2.0%
    public double TitaniumBipolarPlateContactResistanceMilliOhmCm2 { get; init; } = 14.5; // <= 25 mOhm.cm2
    public double CellVoltageDegradationMicrovoltsPerHour { get; init; } = 4.2; // <= 15 uV/hr
    public double WaterFeedConductivityMicroSiemensPerCm { get; init; } = 0.08; // High purity DI water < 0.1 uS/cm
}

public record PemElectrolyzerEvaluation
{
    public required string StackUnitId { get; init; }
    public double CalculatedOperatingCellVoltageVolts { get; init; }
    public double StackHighHeatingValueEfficiencyPercent { get; init; }
    public double HydrogenCrossoverExplosionSafetyMargin { get; init; }
    public bool IsGasCrossoverSafe { get; init; }
    public bool IsBipolarPlateContactOptimal { get; init; }
    public bool IsMembranePinholeAbsent { get; init; }
    public bool IsHighCurrentElectrolysisReady { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
