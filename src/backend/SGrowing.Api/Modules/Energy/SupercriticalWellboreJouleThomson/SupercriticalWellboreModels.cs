namespace SGrowing.Api.Modules.Energy.SupercriticalWellboreJouleThomson;

public record SupercriticalWellboreTelemetry(
    string WellboreId,
    double WellboreDepthMeters,
    double BottomHoleTemperatureCelsius,
    string WorkingFluid,
    double MassFlowRateKgPerSec,
    double AnnulusInletTemperatureCelsius,
    double AnnulusInletPressureMpa,
    double TubingOutletTemperatureCelsius,
    double TubingOutletPressureMpa,
    double SurfaceChokePressureDropMpa);

public record SupercriticalWellboreAnalysisResult(
    string WellboreId,
    double ThermosiphonDrivingPressureMpa,
    double FrictionalPressureLossMpa,
    bool IsSelfDrivenThermosiphonSustained,
    double JouleThomsonTemperatureShiftCelsius,
    double EstimatedThermalPowerOutputMegaWatts,
    string OperationalState,
    string RecommendedChokeActuation,
    string DiagnosticSummary);
