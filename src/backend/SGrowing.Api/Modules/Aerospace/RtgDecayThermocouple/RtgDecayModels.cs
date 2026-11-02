namespace SGrowing.Api.Modules.Aerospace.RtgDecayThermocouple;

public record RtgHardwareTelemetry(
    string RtgId,
    double InitialThermalPowerWatts = 4400.0, // e.g., GPHS-RTG with 18 fuel modules
    double InitialElectricalPowerWatts = 295.0,
    double YearsSinceFuelEncapsulation = 12.5,
    string ThermocoupleMaterial = "SiGe", // Silicon-Germanium or Skutterudite
    double CurrentBusVoltageVolts = 30.0,
    double HotJunctionTemperatureCelsius = 1000.0,
    double ColdJunctionTemperatureCelsius = 290.0,
    double SpacecraftTotalLoadDemandWatts = 190.0
);

public record MissionPowerLimits(
    double MinAllowableBusPowerWatts = 180.0, // Minimum bus power before safe mode
    double CriticalLoadSheddingThresholdWatts = 205.0
);

public record RtgEvaluationRequest(
    RtgHardwareTelemetry Telemetry,
    MissionPowerLimits Limits
);

public record RtgDecayAssessment(
    string RtgId,
    double CurrentThermalPowerWatts,
    double CurrentAvailableElectricalPowerWatts,
    double ThermocoupleConversionEfficiencyPercent,
    double ProjectedPowerWattsInTenYears,
    bool LoadSheddingMandatoryDetected,
    string PowerMarginStatus,
    string PowerManagementDirective
);
