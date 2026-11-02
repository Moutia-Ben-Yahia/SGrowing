namespace SGrowing.Api.Modules.Energy.MoltenSaltCspThermocline;

public record ThermoclineTankTelemetry(
    string TankId,
    double TankHeightMeters = 14.0,
    double TankDiameterMeters = 28.0,
    double HotZoneTemperatureCelsius = 565.0,
    double ColdZoneTemperatureCelsius = 290.0,
    double MeasuredThermoclineThicknessMeters = 2.4,
    double SaltMassFlowRateKgPerSec = 180.0,
    bool IsCharging = true, // true for solar charge (hot in at top), false for turbine discharge (cold in at bottom)
    double AmbientAirTemperatureCelsius = 28.0,
    double MinColdHeadSafetyLimitCelsius = 240.0 // Salt freeze limit at 220°C
);

public record ThermoclineOperationalLimits(
    double MaxAllowableThermoclineThicknessMeters = 4.5,
    double MaxSaltTemperatureCelsius = 585.0,
    double MinAllowableDischargeTemperatureCelsius = 540.0
);

public record ThermoclineEvaluationRequest(
    ThermoclineTankTelemetry Telemetry,
    ThermoclineOperationalLimits Limits
);

public record ThermoclineStratificationPlan(
    string TankId,
    double StoredThermalEnergyMegaWattHours,
    double EffectiveCapacityUtilizationPercent,
    double ThermalLossRateKiloWatts,
    bool ThermoclineDegradationDetected,
    bool FreezePrecursorRiskDetected,
    string StratificationStatus,
    string RecommendedFlowAdjustmentDirective
);
