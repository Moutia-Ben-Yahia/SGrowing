namespace SGrowing.Api.Modules.Energy.SolidOxideFuelCellTriplePhaseBoundary;

public record SolidOxideFuelCellTelemetry
{
    public required string StackId { get; init; }
    public double OperatingTemperatureCelsius { get; init; } = 750.0; // 650 - 850 C
    public double CurrentDensityAmpsPerCm2 { get; init; } = 0.65; // ~0.5 - 1.2 A/cm2
    public double SteamToCarbonRatio { get; init; } = 2.4; // Target >= 2.0 to avoid coking
    public double TriplePhaseBoundaryLengthMicronsPerMicron2 { get; init; } = 6.8; // Target >= 4.5
    public double AreaSpecificResistanceOhmCm2 { get; init; } = 0.22; // Target <= 0.30 Ohm.cm2
    public double AnodeInletMethaneFractionPercent { get; init; } = 25.0; // Reformed syngas
    public double FuelUtilizationPercent { get; init; } = 72.0; // 65 - 80%
    public double AnodeOxygenPartialPressureAtm { get; init; } = 1.0e-17; // Should be < 1.0e-14 to prevent Ni -> NiO oxidation
    public double InternalReformingTemperatureGradientCelsius { get; init; } = 18.0; // <= 30 C
}

public record SolidOxideFuelCellEvaluation
{
    public required string StackId { get; init; }
    public double CalculatedNernstVoltageVolts { get; init; }
    public double CalculatedOperatingCellVoltageVolts { get; init; }
    public double StackPowerDensityWattsPerCm2 { get; init; }
    public double CokingRiskIndex { get; init; }
    public bool IsSteamToCarbonRatioSafe { get; init; }
    public bool IsTriplePhaseBoundaryIntact { get; init; }
    public bool IsNickelAnodeProtectedFromOxidation { get; init; }
    public bool IsStackHighEfficiencyOperating { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
