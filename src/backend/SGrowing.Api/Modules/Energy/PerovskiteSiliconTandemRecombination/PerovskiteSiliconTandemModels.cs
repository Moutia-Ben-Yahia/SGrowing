namespace SGrowing.Api.Modules.Energy.PerovskiteSiliconTandemRecombination;

public sealed record PerovskiteSiliconTandemTelemetry
{
    public required string TandemCellId { get; init; }
    public string ArchitectureType { get; init; } = "Monolithic-2T"; // Monolithic-2T or 4-Terminal
    public double TopPerovskiteCurrentDensityMaPerCm2 { get; init; } = 19.8; // J_sc,top (mA/cm^2)
    public double BottomSiliconCurrentDensityMaPerCm2 { get; init; } = 20.2; // J_sc,bot (mA/cm^2)
    public double TopPerovskiteOpenCircuitVoltageVolts { get; init; } = 1.22; // V_oc,top (V)
    public double BottomSiliconOpenCircuitVoltageVolts { get; init; } = 0.72; // V_oc,bot (V)
    public double TunnelJunctionContactResistivityMOhmCm2 { get; init; } = 12.0; // rho_c (mOhm*cm^2, target < 20 mOhm*cm^2)
    public double FillFactorPercent { get; init; } = 81.5; // FF (%)
    public double BulkPerovskiteSrhRecombinationLossMv { get; init; } = 25.0; // Delta V_SRH (mV)
    public double HalidePhaseSegregationDefectDensityCm3 { get; init; } = 3.5e15; // N_trap (cm^-3)
}

public sealed record PerovskiteSiliconTandemAnalysisResult
{
    public required string TandemCellId { get; init; }
    public required double SeriesMatchedCurrentDensityMaPerCm2 { get; init; }
    public required double CurrentMismatchRatioPercent { get; init; }
    public required double TunnelJunctionOhmicVoltageDropMv { get; init; }
    public required double TandemOpenCircuitVoltageVolts { get; init; }
    public required double PowerConversionEfficiencyPercent { get; init; }
    public required bool IsCurrentMatchingOptimal { get; init; }
    public required bool IsTunnelJunctionOhmicLossLow { get; init; }
    public required bool IsTandemEfficiencyRecordGrade { get; init; }
    public required string TandemOperationRegime { get; init; }
    public required string TandemOptimizationRecommendation { get; init; }
}
