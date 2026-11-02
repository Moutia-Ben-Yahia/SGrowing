namespace SGrowing.Api.Modules.Energy.AdiabaticCaesThermalStorage;

public sealed record AdiabaticCaesTelemetry(
    string CavernId,
    double CavernPressureBar,
    double CavernPressureLithostaticRatio,
    double MinimumPermissibleCavernPressureBar,
    double MaximumPermissibleCavernPressureBar,
    double AirMassFlowRateKgPerSec,
    double ThermalStorageTemperatureCelsius,
    double CompressorPowerInputMegawatts,
    double TurbinePowerOutputMegawatts,
    double CavernDailyAirLeakagePercent
);

public sealed record AdiabaticCaesAnalysisResult(
    string CavernId,
    double RoundTripExergyEfficiencyPercent,
    double AvailableStoredExergyMegaWattHours,
    double CavernGeomechanicalSafetyFactor,
    string CaesOperatingRegime,
    string DispatchAndSafetyAction,
    bool IsCaesOperational
);

public interface IAdiabaticCaesThermalStorageEngine
{
    AdiabaticCaesAnalysisResult AnalyzeAdiabaticCaes(AdiabaticCaesTelemetry telemetry);
}
