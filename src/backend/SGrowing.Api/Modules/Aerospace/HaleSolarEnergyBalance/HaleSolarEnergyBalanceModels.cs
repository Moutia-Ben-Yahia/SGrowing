namespace SGrowing.Api.Modules.Aerospace.HaleSolarEnergyBalance;

public sealed record HaleSolarTelemetry(
    string AircraftTailNumber,
    double FlightAltitudeKilometers,
    double SolarZenithAngleDegrees,
    double UpperWingPvAreaMetersSquared,
    double SolarArrayPowerGeneratedKilowatts,
    double PropulsionAndAvionicsPowerDemandKilowatts,
    double BatteryPackStateOfChargePercent,
    double BatteryCapacityKilowattHours,
    double NightDurationHours,
    bool GravitationalGlidePotentialBufferingActive
);

public sealed record HaleSolarAnalysisResult(
    string AircraftTailNumber,
    double NetPowerBalanceKilowatts,
    double ProjectedMidnightMinimumSocPercent,
    double GravitationalPotentialEnergyStoredKWh,
    string DiurnalEnergyRegime,
    string FlightProfileEnergyCommand,
    bool IsPerpetualFlightSustained
);

public interface IHaleSolarEnergyBalanceEngine
{
    HaleSolarAnalysisResult AnalyzeDiurnalEnergyBalance(HaleSolarTelemetry telemetry);
}
