namespace SGrowing.Api.Modules.Healthcare.MriMagnetQuenchArbiter;

public record MriMagnetState(
    string ScannerId,
    double MagneticFieldTesla = 3.0,
    double OperatingCurrentAmperes = 800.0,
    double CoilResistiveVoltageMilliVolts = 0.5,
    double LiquidHeliumBoilOffRateLitersPerMin = 1.2,
    double CryostatVacuumPressureMbar = 1e-6,
    double ColdHeadTemperatureKelvin = 4.2
);

public record QuenchProtectionLimits(
    double MaxAllowableResistiveVoltageMilliVolts = 50.0,
    double MaxAllowableBoilOffRateLitersPerMin = 12.0,
    double MaxAllowableVacuumPressureMbar = 1e-4,
    double MaxAllowableHotSpotTempKelvin = 250.0,
    double DumpResistorOhms = 0.5
);

public record MriMagnetQuenchRequest(
    MriMagnetState State,
    QuenchProtectionLimits Limits
);

public record MriMagnetQuenchDecision(
    string ScannerId,
    bool QuenchPrecursorDetected,
    bool EmergencyDumpTriggered,
    double EstimatedHotSpotTemperatureKelvin,
    double StoredMagneticEnergyMegaJoules,
    double EnergyExtractionRateMegaWatts,
    string SafetyInterlockStatus,
    string RecommendedAction
);
