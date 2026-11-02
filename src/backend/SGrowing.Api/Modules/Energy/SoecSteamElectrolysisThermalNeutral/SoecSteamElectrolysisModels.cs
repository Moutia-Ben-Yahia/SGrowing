namespace SGrowing.Api.Modules.Energy.SoecSteamElectrolysisThermalNeutral;

public record SoecSteamElectrolysisTelemetry
{
    public required string ElectrolyzerStackUid { get; init; }
    public double OperatingTemperatureCelsius { get; init; } = 800.0; // 700 - 850 C
    public double OperatingCellVoltageVolts { get; init; } = 1.285; // Target around V_tn ~ 1.28 - 1.30 V
    public double ThermalNeutralVoltageVolts { get; init; } = 1.290; // V_tn
    public double ReversibleNernstPotentialVolts { get; init; } = 1.025;
    public double CurrentDensityAmperesPerCm2 { get; init; } = 1.15; // Target >= 1.0 A/cm2
    public double SteamUtilizationPercent { get; init; } = 74.5; // Target <= 85.0%
    public double AreaSpecificResistanceOhmCm2 { get; init; } = 0.22; // Target <= 0.28
    public double OxygenPartialPressureAtElectrodeBar { get; init; } = 45.0; // Target <= 120 bar
    public double FaradayEfficiencyPercent { get; init; } = 99.1; // Target >= 98.5%
}

public record SoecSteamElectrolysisEvaluation
{
    public required string ElectrolyzerStackUid { get; init; }
    public double ThermalNeutralVoltageDeviationVolts { get; init; }
    public double HydrogenProductionRateNm3PerHourPerM2 { get; init; }
    public bool IsOperatingInThermalNeutralBand { get; init; }
    public bool IsSteamStarvationAverted { get; init; }
    public bool IsElectrodeDelaminationAverted { get; init; }
    public bool IsSoecElectrolyzerOperationCertified { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
