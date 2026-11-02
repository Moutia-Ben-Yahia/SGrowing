namespace SGrowing.Api.Modules.Manufacturing.MbeRheedGrowthKinetics;

public sealed record MbeGrowthTelemetry(
    string ChamberId,
    double GrowthTemperatureCelsius,
    double GroupVFluxTorr,
    double GroupIIIFluxTorr,
    double RheedOscillationPeriodSeconds,
    double RheedSpecularIntensityNormalized,
    double SurfaceAdatomDiffusionLengthNanometers,
    bool SubstrateAzimuthRotatorActive
);

public sealed record MbeGrowthAnalysisResult(
    string ChamberId,
    double FluxRatioVtoIII,
    double MonolayerGrowthRateNanometersPerSecond,
    double InterfacialRoughnessEstimateAngstroms,
    string EpitaxialGrowthMode,
    string EffusionCellShutterCommand,
    bool IsCrystalStructureUniform
);

public interface IMbeRheedGrowthKineticsEngine
{
    MbeGrowthAnalysisResult AnalyzeEpitaxialGrowth(MbeGrowthTelemetry telemetry);
}
