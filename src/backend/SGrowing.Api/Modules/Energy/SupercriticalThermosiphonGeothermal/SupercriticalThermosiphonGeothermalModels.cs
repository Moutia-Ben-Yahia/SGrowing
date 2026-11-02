namespace SGrowing.Api.Modules.Energy.SupercriticalThermosiphonGeothermal;

public sealed record SupercriticalThermosiphonTelemetry(
    string WellboreId,
    double WellboreDepthMeters,
    double BottomHoleTemperatureCelsius,
    double SurfaceInjectionTemperatureCelsius,
    double WorkingFluidMassFlowRateKgPerSec,
    double ThermosiphonBuoyancyHeadBar,
    double WellboreFrictionLossBar,
    double ThermalPowerExtractedMegawatts,
    bool SurfaceTurbineThrottleValveActive
);

public sealed record SupercriticalThermosiphonAnalysisResult(
    string WellboreId,
    double NetBuoyancyDrivePressureBar,
    double ThermodynamicExergyEfficiencyPercent,
    double ParasiticPumpingPowerSavedMegawatts,
    string ThermosiphonRegime,
    string WellheadControlCommand,
    bool IsThermosiphonCirculationStable
);

public interface ISupercriticalThermosiphonGeothermalEngine
{
    SupercriticalThermosiphonAnalysisResult AnalyzeThermosiphonState(SupercriticalThermosiphonTelemetry telemetry);
}
