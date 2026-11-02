namespace SGrowing.Api.Modules.Aerospace.AerobrakingAtmosphericDrag;

public interface IAerobrakingDragEngine
{
    AerobrakingDragEvaluation AnalyzeDragPass(AerobrakingDragTelemetry telemetry);
}

public class AerobrakingDragEngine : IAerobrakingDragEngine
{
    public AerobrakingDragEvaluation AnalyzeDragPass(AerobrakingDragTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Effective atmospheric density with dust storm factor
        double effectiveDensity = telemetry.AtmosphericDensityKgPerM3 * telemetry.DustStormDensitySurgeFactor;
        double vPeriMps = telemetry.SpacecraftVelocityAtPeriapsisKmPerS * 1000.0;

        // Approximate drag pass delta-V = 0.5 * Cd * (A/m) * rho * v^2 * t_effective
        // where Cd ~ 2.2 in free-molecular flow, t_effective ~ 120s near periapsis
        double cd = 2.2;
        double tEff = 135.0;
        double ballisticFactor = (cd * telemetry.SpacecraftDragAreaM2) / Math.Max(100.0, telemetry.SpacecraftMassKg);
        double deltaV = 0.5 * ballisticFactor * effectiveDensity * vPeriMps * vPeriMps * tEff; // Scaled pass delta V

        // Peak convective heat flux ~ C_conv * sqrt(rho / R_nose) * v^3
        // For Mars/typical blunt body C_conv ~ 1.8e-8
        double noseRadiusM = 1.2;
        double qStagWPerCm2 = 1.8e-8 * Math.Sqrt(effectiveDensity / noseRadiusM) * Math.Pow(vPeriMps, 3.0) / 1e4;
        qStagWPerCm2 = Math.Clamp(qStagWPerCm2, 0.05, 50.0);

        // Approximate orbital period reduction in minutes per pass (scaled by deltaV)
        double periodDecayMin = deltaV * 3.8;

        bool isCorridorSafe = telemetry.PeriapsisAltitudeKm >= 115.0 && telemetry.PeriapsisAltitudeKm <= 140.0;
        bool isThermalSafe = telemetry.PeakSolarArrayTemperatureCelsius <= 165.0 && qStagWPerCm2 <= 20.0;
        bool isTorqueSafe = telemetry.AerodynamicBendingTorqueNewtonMeters <= 45.0;

        if (telemetry.DustStormDensitySurgeFactor > 1.80)
        {
            regimes.Add("ATMOSPHERIC_DUST_STORM_DENSITY_SURGE");
            mitigations.Add("Execute emergency periapsis raise maneuver (+5 km) to offset planetary dust-storm thermal and aerodynamic density spikes.");
        }

        if (!isTorqueSafe)
        {
            regimes.Add("SOLAR_ARRAY_AERODYNAMIC_MOMENT_EXCEEDANCE");
            mitigations.Add("Feather solar arrays to minimum-drag edge-on aerodynamic orientation during atmospheric passage.");
        }

        if (telemetry.PeriapsisAltitudeKm < 115.0)
        {
            regimes.Add("PERIAPSIS_ALTITUDE_CORRIDOR_DECAY");
            mitigations.Add("Perform apoapsis impulsive burn to restore periapsis corridor within the aerothermal safety corridor.");
        }

        bool isGoForPass = isCorridorSafe && isThermalSafe && isTorqueSafe && telemetry.DustStormDensitySurgeFactor <= 1.80;

        return new AerobrakingDragEvaluation
        {
            PassIdentifier = telemetry.PassIdentifier,
            CalculatedPassDeltaVMetersPerSecond = Math.Round(deltaV, 3),
            PeakStagnationHeatFluxWattsPerCm2 = Math.Round(qStagWPerCm2, 3),
            OrbitalPeriodDecayMinutesPerPass = Math.Round(periodDecayMin, 2),
            IsPeriapsisCorridorSafe = isCorridorSafe,
            IsThermalLoadWithinLimits = isThermalSafe,
            IsStructuralAeroTorqueSafe = isTorqueSafe,
            IsPassGoForAtmosphericEntry = isGoForPass,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
