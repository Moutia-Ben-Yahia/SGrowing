using System;

namespace SGrowing.Api.Modules.Aerospace.HaleSolarEnergyBalance;

public sealed class HaleSolarEnergyBalanceEngine : IHaleSolarEnergyBalanceEngine
{
    public HaleSolarAnalysisResult AnalyzeDiurnalEnergyBalance(HaleSolarTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var netPowerKw = Math.Round(telemetry.SolarArrayPowerGeneratedKilowatts - telemetry.PropulsionAndAvionicsPowerDemandKilowatts, 2);

        // Gravitational potential energy stored in stratosphere climb (m = 450 kg)
        const double aircraftMassKg = 450.0;
        const double baselineAltitudeKm = 18.0;
        var deltaHeightMeters = Math.Max(0.0, (telemetry.FlightAltitudeKilometers - baselineAltitudeKm) * 1000.0);
        var potentialEnergyJoules = aircraftMassKg * 9.80665 * deltaHeightMeters;
        var potentialEnergyKwh = Math.Round(potentialEnergyJoules / 3_600_000.0, 3);

        // Nighttime energy demand: gliding potential buffering reduces propulsion draw by 25%
        var nightPowerMultiplier = telemetry.GravitationalGlidePotentialBufferingActive ? 0.75 : 1.0;
        var nightEnergyDemandKwh = telemetry.PropulsionAndAvionicsPowerDemandKilowatts * nightPowerMultiplier * Math.Max(1.0, telemetry.NightDurationHours);

        var batteryCapacity = Math.Max(1.0, telemetry.BatteryCapacityKilowattHours);
        var currentBatteryEnergyKwh = batteryCapacity * (Math.Clamp(telemetry.BatteryPackStateOfChargePercent, 0.0, 100.0) / 100.0);
        var projectedDawnEnergyKwh = currentBatteryEnergyKwh - nightEnergyDemandKwh;
        var projectedMinSocPercent = Math.Round(Math.Clamp((projectedDawnEnergyKwh / batteryCapacity) * 100.0, 0.0, 100.0), 1);

        string regime;
        string command;
        bool isPerpetual;

        if (telemetry.PropulsionAndAvionicsPowerDemandKilowatts > 12.0)
        {
            regime = "STRATOSPHERIC_JETSTREAM_DRAG_CRITICAL";
            command = "Stratospheric shear winds exceeding penetration envelope. Descend to low-shear inversion altitude band.";
            isPerpetual = false;
        }
        else if (projectedMinSocPercent < 18.0)
        {
            regime = "NIGHT_BATTERY_EXHAUSTION_WARNING";
            command = "Projected dawn battery SOC below 18% reserve limit. Throttle non-critical avionics payload and trim speed to minimum drag.";
            isPerpetual = false;
        }
        else if (netPowerKw > 6.0 && telemetry.BatteryPackStateOfChargePercent > 80.0)
        {
            regime = "SOLAR_PEAK_POTENTIAL_CLIMB_CHARGING";
            command = "Battery pack near full capacity. Execute smooth zoom climb to 21.5 km to store excess solar power as gravitational potential.";
            isPerpetual = true;
        }
        else
        {
            regime = "DIURNAL_PERPETUAL_STATION_KEEPING";
            command = "Diurnal power closure verified. Aircraft maintains persistent pseudo-satellite geostationary orbit.";
            isPerpetual = true;
        }

        return new HaleSolarAnalysisResult(
            AircraftTailNumber: telemetry.AircraftTailNumber,
            NetPowerBalanceKilowatts: netPowerKw,
            ProjectedMidnightMinimumSocPercent: projectedMinSocPercent,
            GravitationalPotentialEnergyStoredKWh: potentialEnergyKwh,
            DiurnalEnergyRegime: regime,
            FlightProfileEnergyCommand: command,
            IsPerpetualFlightSustained: isPerpetual
        );
    }
}
