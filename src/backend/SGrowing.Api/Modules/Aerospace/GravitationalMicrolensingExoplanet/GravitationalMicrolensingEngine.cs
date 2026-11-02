namespace SGrowing.Api.Modules.Aerospace.GravitationalMicrolensingExoplanet;

public class GravitationalMicrolensingEngine
{
    public MicrolensingAnalysisResult AnalyzeEvent(MicrolensingEventTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.ImpactParameterU0 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Impact parameter u0 must be positive.");
        }

        if (telemetry.EinsteinCrossingTimeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Einstein crossing time must be positive.");
        }

        // Paczynski single-lens baseline calculation
        double tau = (telemetry.CurrentTimeDays - telemetry.TimeOfClosestApproachDays) / telemetry.EinsteinCrossingTimeDays;
        double u = Math.Sqrt(Math.Pow(telemetry.ImpactParameterU0, 2) + Math.Pow(tau, 2));
        
        // Handle numerical stability for u -> 0
        u = Math.Max(u, 1e-6);
        double paczynskiA0 = (Math.Pow(u, 2) + 2.0) / (u * Math.Sqrt(Math.Pow(u, 2) + 4.0));

        // Anomaly residual fraction
        double anomalyResidualFraction = (telemetry.ObservedMagnification - paczynskiA0) / paczynskiA0;

        // Caustic topology classification based on projected separation s
        string causticTopology;
        if (telemetry.ProjectedSeparationS >= 0.9 && telemetry.ProjectedSeparationS <= 1.1)
        {
            causticTopology = "Resonant (Central 6-Cusp Hyper-Caustic)";
        }
        else if (telemetry.ProjectedSeparationS < 0.9)
        {
            causticTopology = "Close Binary (Central Diamond + Triangular Planetary Caustics)";
        }
        else
        {
            causticTopology = "Wide Binary (Central Diamond + Dual Planetary Fold Caustics)";
        }

        // Caustic crossing detection threshold
        bool isCausticCrossing = Math.Abs(anomalyResidualFraction) >= 0.035 || Math.Abs(telemetry.ObservedMagnification - paczynskiA0) >= 0.5;

        // Crossing duration in minutes: Delta t = 2 * rho * tE / sin(phi) [approx phi = 45 deg]
        double sinPhi = Math.Sin(Math.PI / 4.0);
        double crossingDurationDays = (2.0 * telemetry.SourceAngularRadiusRho * telemetry.EinsteinCrossingTimeDays) / sinPhi;
        double crossingDurationMinutes = crossingDurationDays * 24.0 * 60.0;

        string alertLevel;
        string followUpCadence;
        string actionableSummary;

        if (isCausticCrossing)
        {
            if (Math.Abs(anomalyResidualFraction) >= 0.15 || telemetry.ObservedMagnification >= 50.0)
            {
                alertLevel = "CRITICAL_CAUSTIC_INGRESS";
                followUpCadence = "Ultra-Fast (30s) High-Cadence Multi-Band Photometry & 8m-Class Spectroscopy";
                actionableSummary = $"Immediate planetary caustic crossing detected with {anomalyResidualFraction:P1} deviation from Paczynski profile. Mass ratio q={telemetry.EstimatedMassRatioQ:E2}, s={telemetry.ProjectedSeparationS:F3}. Estimated caustic crossing window is {crossingDurationMinutes:F1} minutes.";
            }
            else
            {
                alertLevel = "ACTIVE_PLANETARY_PERTURBATION";
                followUpCadence = "5-Minute Targeted Robotic Telescope Network Photometry";
                actionableSummary = $"Significant planetary perturbation observed ({anomalyResidualFraction:P1} deviation). Finite-source crossing underway ({causticTopology}). Scheduled alert dispatched to global robotic telescope network.";
            }
        }
        else
        {
            alertLevel = "NOMINAL_POINT_LENS";
            followUpCadence = "Standard 1-Hour Survey Cadence";
            actionableSummary = $"Event tracks standard Paczynski curve within {anomalyResidualFraction:P2} tolerance. No caustic intersection currently observed.";
        }

        return new MicrolensingAnalysisResult(
            telemetry.EventId,
            Math.Round(paczynskiA0, 4),
            Math.Round(anomalyResidualFraction, 4),
            isCausticCrossing,
            causticTopology,
            Math.Round(crossingDurationMinutes, 2),
            alertLevel,
            followUpCadence,
            actionableSummary);
    }
}
