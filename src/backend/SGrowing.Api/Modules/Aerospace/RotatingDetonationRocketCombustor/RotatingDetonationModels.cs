namespace SGrowing.Api.Modules.Aerospace.RotatingDetonationRocketCombustor;

public sealed record RotatingDetonationTelemetry(
    string EngineId,
    double CombustorOuterDiameterMillimeters, // D_o: e.g. 150.0 mm
    double AnnularGapWidthMillimeters, // w_gap: e.g. 8.0 mm
    double ChamberTotalPressureBar, // P_0: e.g. 45.0 bar
    double DetonationWaveVelocityMetersPerSecond, // v_det: e.g. 2450.0 m/s
    double TheoreticalCjVelocityMetersPerSecond, // v_CJ: e.g. 2550.0 m/s
    int ObservedWaveCount, // N_wave: e.g. 2
    double CounterRotatingWaveIntensityRatio, // I_CR / I_primary: e.g. 0.05
    double TotalPropellantMassFlowRateKgPerSec // m_dot: e.g. 12.5 kg/s
);

public sealed record RotatingDetonationAnalysisResult(
    string EngineId,
    double DetonationWaveFrequencyKilohertz,
    double CjVelocityDeficitPercent,
    double EstimatedPressureGainRatio,
    double SpecificImpulseEstimatedSeconds,
    string DetonationRegime,
    string CombustorControlGuidance,
    bool IsPressureGainCombustionStable
);

public interface IRotatingDetonationRocketCombustorEngine
{
    RotatingDetonationAnalysisResult AnalyzeDetonationCombustor(RotatingDetonationTelemetry telemetry);
}
