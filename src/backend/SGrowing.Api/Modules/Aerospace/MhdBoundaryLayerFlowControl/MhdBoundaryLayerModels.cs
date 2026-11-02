namespace SGrowing.Api.Modules.Aerospace.MhdBoundaryLayerFlowControl;

public sealed record MhdBoundaryLayerTelemetry(
    string HypersonicVehicleId,
    double FreeStreamMachNumber, // e.g. Mach 6.0 - 12.0
    double FlightAltitudeKilometers, // e.g. 28 - 45 km
    double AppliedMagneticFieldTesla, // B: e.g. 1.0 - 3.5 T
    double BoundaryLayerPlasmaElectricalConductivitySiemensPerM, // sigma: e.g. 20 - 150 S/m
    double FlowInteractionLengthMeters, // L: e.g. 2.0 m
    double UpstreamDynamicPressureKiloPascals, // q: e.g. 45 kPa
    double MhdGeneratorLoadFactorK, // K = E / (u*B) in [0, 1] (K ~ 0.5 for max power)
    double WallSurfaceHeatFluxMegaWattsPerM2 // Heat flux: e.g. 1.5 - 4.5 MW/m^2
);

public sealed record MhdBoundaryLayerAnalysisResult(
    string HypersonicVehicleId,
    double MhdInteractionParameterStuartNumber,
    double SkinFrictionDragReductionPercent,
    double ExtractedMhdElectricPowerKiloWatts,
    double InducedLorentzDecelerationForceKiloNewtons,
    string MhdFlowRegime,
    string BoundaryLayerControlGuidance,
    bool IsMhdControlEffective
);

public interface IMhdBoundaryLayerFlowControlEngine
{
    MhdBoundaryLayerAnalysisResult AnalyzeMhdBoundaryLayer(MhdBoundaryLayerTelemetry telemetry);
}
