namespace SGrowing.Api.Modules.Aerospace.SolarSailRadiationPressure;

public sealed record SolarSailTelemetry(
    string SpacecraftId,
    double HeliocentricDistanceAstronomicalUnits, // r in AU (1 AU = Earth orbit)
    double TotalSailAreaSquareMeters, // e.g. 1200 m^2
    double TotalSpacecraftMassKg, // e.g. 35 kg
    double SailSpecularReflectivityCoefficient, // eta in [0.80, 0.99]
    double SailConePitchAngleDegrees, // alpha in [0, 90 deg]
    double MembraneFilmThicknessMicrometers, // e.g. 2.5 um Kapton/CP1
    double SailMembraneTemperatureKelvin // e.g. 350 K
);

public sealed record SolarSailAnalysisResult(
    string SpacecraftId,
    double SolarRadiationPressureMicroPascals,
    double GeneratedPhotonThrustForceMilliNewtons,
    double CharacteristicAccelerationMmPerSec2,
    double SolarLightnessFactorBeta,
    string SailNavigationRegime,
    string AttitudeControlGuidance,
    bool IsTrajectoryNominal
);

public interface ISolarSailRadiationPressureEngine
{
    SolarSailAnalysisResult AnalyzeSolarSail(SolarSailTelemetry telemetry);
}
