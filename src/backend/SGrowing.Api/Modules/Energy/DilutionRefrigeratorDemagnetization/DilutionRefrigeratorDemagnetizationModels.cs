namespace SGrowing.Api.Modules.Energy.DilutionRefrigeratorDemagnetization;

public sealed record DilutionRefrigeratorTelemetry(
    string CryostatId,
    double MixingChamberTempKelvin,
    double StillTempKelvin,
    double He3CirculationFlowRateMolPerSec,
    double AdrMagneticFieldTesla,
    double AdrPillTemperatureKelvin,
    double SuperconductingQubitThermalLoadMicroWatts,
    bool ThermalSwitchEngaged
);

public sealed record DilutionRefrigeratorAnalysisResult(
    string CryostatId,
    double DilutionCoolingPowerMicroWatts,
    double CoolingMarginRatio,
    double AdrDemagnetizationEntropyTransferJoulesPerKelvin,
    double EffectiveColdFingerTempKelvin,
    string OperatingRegime,
    string RecommendedAction,
    bool IsThermalStable
);

public interface IDilutionRefrigeratorDemagnetizationEngine
{
    DilutionRefrigeratorAnalysisResult AnalyzeSubKelvinRefrigeration(DilutionRefrigeratorTelemetry telemetry);
}
