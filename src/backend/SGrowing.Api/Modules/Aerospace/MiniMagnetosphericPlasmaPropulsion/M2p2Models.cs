namespace SGrowing.Api.Modules.Aerospace.MiniMagnetosphericPlasmaPropulsion;

public sealed record M2p2Telemetry(
    string SpacecraftId,
    double SolarWindSpeedKmPerSec,
    double SolarWindProtonDensityPerCubicCm,
    double HeliconPlasmaRfPowerKilowatts,
    double HeliconPlasmaFeedRateMilligramsPerSec,
    double SuperconductingDipoleFieldTesla,
    double SuperconductingCoilTemperatureKelvin,
    double MagnetosphereRadiusKilometers,
    double HeliocentricDistanceAstronomicalUnits
);

public sealed record M2p2AnalysisResult(
    string SpacecraftId,
    double SolarWindDynamicPressureNanoPascals,
    double NetThrustNewtons,
    double RadiationDoseAttenuationPercent,
    string PropulsionAndShieldingRegime,
    string MagnetosphereControlAction,
    bool IsSystemOperational
);

public interface IMiniMagnetosphericPlasmaPropulsionEngine
{
    M2p2AnalysisResult AnalyzePlasmaMagnetosphere(M2p2Telemetry telemetry);
}
