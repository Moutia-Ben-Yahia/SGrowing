namespace SGrowing.Api.Modules.Energy.UhvCoronaIonization;

public record UhvConductorBundle(
    string TransmissionLineId = "UHV-DEFAULT-1100KV",
    double LineVoltageKv = 1100.0,
    int SubconductorsPerBundle = 8,
    double SubconductorRadiusCm = 1.5,
    double BundleSpacingCm = 45.0,
    double LineLengthKm = 650.0
);

public record AtmosphericWeatherCondition(
    double AmbientTempCelsius = 15.0,
    double BarometricPressureTorr = 760.0,
    double RelativeHumidityPercent = 55.0,
    double PrecipitationRateMmPerHour = 0.0
);

public record CoronaEvaluationRequest(
    UhvConductorBundle Geometry,
    AtmosphericWeatherCondition Weather
);

public record GridCoronaLossProfile(
    string TransmissionLineId,
    double RelativeAirDensityDelta,
    double CriticalDisruptiveGradientKvPerCm,
    double OperatingGradientKvPerCm,
    double CoronaPowerLossKwPerKm,
    double TotalTransmissionLossMw,
    double AudibleNoiseDba,
    string WeatherRegime, // "FAIR_WEATHER", "HUMID_FOG", "HEAVY_PRECIPITATION"
    string GridAdvisory
);
