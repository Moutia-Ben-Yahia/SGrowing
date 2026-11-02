namespace SGrowing.Api.Modules.Aerospace.NuclearThermalPropulsionPebbleBed;

public sealed record NuclearThermalTelemetry(
    string ReactorCoreId,
    double ReactorThermalPowerMegaWatts, // e.g. 500 - 1500 MWth
    double PeakFuelPebbleCenterlineTemperatureKelvin, // T_core: e.g. 2600 - 3000 K
    double FuelPebbleMeltingLimitTemperatureKelvin, // T_limit: e.g. 3300 K (TRISO ZrC/UN)
    double HydrogenPropellantMassFlowRateKgPerSec, // m_dot: e.g. 10 - 35 kg/s
    double ReactorChamberPressureMegaPascals, // P_c: e.g. 5.0 - 12.0 MPa
    double PebbleBedPoreFractionPorosity, // eps: e.g. 0.38 - 0.42
    double HydrogenDissociationFractionAlpha, // alpha: e.g. 0.05 - 0.25 (H2 <-> 2H)
    double NozzleExpansionRatioArea // epsilon: e.g. 200:1 - 350:1
);

public sealed record NuclearThermalAnalysisResult(
    string ReactorCoreId,
    double CalculatedSpecificImpulseSeconds,
    double GeneratedThrustKiloNewtons,
    double MeanExhaustMolecularWeightGramsPerMole,
    double FuelCenterlineThermalMarginKelvin,
    double PebbleBedPressureDropMegaPascals,
    string ReactorThermalRegime,
    string PropulsionReactorGuidance,
    bool IsReactorOperatingSafe
);

public interface INuclearThermalPropulsionPebbleBedEngine
{
    NuclearThermalAnalysisResult AnalyzeReactorPropulsion(NuclearThermalTelemetry telemetry);
}
