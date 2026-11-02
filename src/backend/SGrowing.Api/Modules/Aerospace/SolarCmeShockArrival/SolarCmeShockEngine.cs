namespace SGrowing.Api.Modules.Aerospace.SolarCmeShockArrival;

public interface ISolarCmeShockEngine
{
    SolarCmeShockEvaluation AnalyzeShock(SolarCmeShockTelemetry telemetry);
}

public class SolarCmeShockEngine : ISolarCmeShockEngine
{
    public SolarCmeShockEvaluation AnalyzeShock(SolarCmeShockTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // 1 AU in km = 149,597,870.7 km
        double distanceKm = telemetry.ShockTransitDistanceAu * 149597870.7;

        // Drag-based model: v(r) = w + (v0 - w) * exp(-gamma * r)
        double v0 = telemetry.EuvDopplerLiftOffVelocityKmPerS;
        double w = telemetry.BackgroundSolarWindSpeedKmPerS;
        double gamma = Math.Max(1e-9, telemetry.AerodynamicDragDecelerationCoeff);

        // Arrival speed at target distance
        double vArrival = w + (v0 - w) * Math.Exp(-gamma * distanceKm);
        vArrival = Math.Clamp(vArrival, w * 0.8, v0);

        // Approximate average velocity for transit time: v_avg ~ 0.5 * (v0 + vArrival)
        double vAvg = 0.5 * (v0 + vArrival);
        double transitTimeHours = distanceKm / (vAvg * 3600.0);

        // Uncertainty window based on transit time (+/- 6% of total duration)
        double uncertaintyHours = Math.Clamp(transitTimeHours * 0.065, 1.2, 5.0);

        bool isGeoeffective = telemetry.CoronalMassEjectionHalfWidthDegrees >= 40.0;
        bool isSevereSuperstorm = telemetry.PredictedGeomagneticDstIndexNanoTesla <= -150.0;
        bool isProtonHazard = telemetry.SolarEnergeticProtonFluxPfu >= 100.0;
        bool isFastShock = v0 >= 1400.0 || transitTimeHours <= 24.0;

        if (isSevereSuperstorm)
        {
            regimes.Add("SEVERE_GEOMAGNETIC_SUPERSTORM_RISK");
            mitigations.Add("Alert power transmission operators to decouple long-line transformers and switch geostationary communication satellites to safe-hold attitude.");
        }

        if (isFastShock)
        {
            regimes.Add("HIGH_VELOCITY_FAST_SHOCK_COMPRESSION");
            mitigations.Add("Issue immediate high-priority space weather alert for magnetopause subsolar compression below geosynchronous orbit (r_mp < 6.6 Re).");
        }

        if (isProtonHazard)
        {
            regimes.Add("SOLAR_ENERGETIC_PARTICLE_RADIATION_SURGE");
            mitigations.Add("Order commercial polar flight diversions to equatorial corridors and instruct ISS astronauts to retreat into radiation-shielded modules.");
        }

        bool isPreparednessReady = isGeoeffective && !isSevereSuperstorm && !isProtonHazard && uncertaintyHours <= 4.5;

        return new SolarCmeShockEvaluation
        {
            EventIdentifier = telemetry.EventIdentifier,
            CalculatedTransitTimeHours = Math.Round(transitTimeHours, 1),
            ArrivalSpeedAtTargetKmPerS = Math.Round(vArrival, 1),
            ShockArrivalUncertaintyWindowHours = Math.Round(uncertaintyHours, 2),
            IsEarthDirectedGeoeffective = isGeoeffective,
            IsSevereSuperstormPredicted = isSevereSuperstorm,
            IsProtonRadiationHazardCritical = isProtonHazard,
            IsDefenseGridPreparednessReady = isPreparednessReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
