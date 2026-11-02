namespace SGrowing.Api.Modules.Energy.SolidStateBatteryDendriteSuppression;

public sealed record SolidStateBatteryDendriteTelemetry
{
    public required string CellPackId { get; init; }
    public string SolidElectrolyteType { get; init; } = "LLZO"; // e.g. LLZO Garnet or LPS Sulfide
    public double AppliedCurrentDensityMaPerCm2 { get; init; } = 1.8; // j_applied (mA/cm^2)
    public double UniaxialStackPressureMpa { get; init; } = 3.2; // P_stack (MPa, target 1.5 - 5.0 MPa)
    public double SolidElectrolyteShearModulusGpa { get; init; } = 60.0; // G_SE (GPa, LLZO ~ 60 GPa, LPS ~ 18 GPa)
    public double ElectrolyteFractureToughnessMpaM12 { get; init; } = 1.25; // K_Ic (MPa*m^0.5)
    public double GrainBoundaryFlawRadiusMicrons { get; init; } = 0.85; // flaw size a (microns)
    public double CellTemperatureCelsius { get; init; } = 30.0; // deg C
    public double BaselineCriticalCurrentDensityMaPerCm2 { get; init; } = 2.4; // j_CCD0 at 1 MPa
}

public sealed record SolidStateBatteryDendriteAnalysisResult
{
    public required string CellPackId { get; init; }
    public required double EffectiveCriticalCurrentDensityMaPerCm2 { get; init; }
    public required double CurrentDensitySafetyRatio { get; init; }
    public required double CrackTipStressIntensityFactorMpaM12 { get; init; }
    public required double StackPressureToYieldRatio { get; init; }
    public required bool IsStackPressureWithinOptimalEnvelope { get; init; }
    public required bool IsDendritePropagationSuppressed { get; init; }
    public required bool IsFastChargeSafe { get; init; }
    public required string BatteryIntegrityRegime { get; init; }
    public required string PressureManagementProtocol { get; init; }
}
