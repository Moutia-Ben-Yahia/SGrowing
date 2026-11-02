namespace SGrowing.Api.Modules.Energy.HvdcGridStabilizer;

public record SynchrophasorReading(
    string SubstationId,
    double BusVoltageKv,
    double FrequencyHz,
    double PhaseAngleDegrees,
    double RateOfChangeOfFrequencyHzPerSec,
    DateTimeOffset TimestampUtc);

public record HvdcConverterStation(
    string StationId,
    string Name,
    double RatedCapacityMw,
    double CurrentPowerFlowMw,
    double MaxRampRateMwPerSec,
    double InertiaConstantH);

public record GridStabilizationRequest(
    List<HvdcConverterStation> ConverterStations,
    List<SynchrophasorReading> PmuReadings,
    double NominalFrequencyHz = 50.0,
    double MaxAllowedAngleDriftDegrees = 15.0);

public record ConverterControlAction(
    string StationId,
    double ActivePowerAdjustmentMw,
    double SyntheticInertiaInjectedMw,
    string ControlMode,
    string Reason);

public record GridStabilizationResult(
    int SubstationsMonitored,
    double MeanFrequencyHz,
    double MaxPhaseAngleDeviationDegrees,
    List<ConverterControlAction> Actions,
    bool GridFrequencyStable,
    string GridResilienceSummary);
