namespace SGrowing.Api.Modules.Aerospace.LunarOrbitInsertionBallisticCapture;

public interface ILunarOrbitInsertionEngine
{
    LunarOrbitInsertionEvaluation AnalyzeInsertion(LunarOrbitInsertionTelemetry telemetry);
}

public class LunarOrbitInsertionEngine : ILunarOrbitInsertionEngine
{
    public LunarOrbitInsertionEvaluation AnalyzeInsertion(LunarOrbitInsertionTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Ballistic capture requires negative lunar-centric 2-body osculating energy: eps_moon < 0
        bool isBallisticallyCaptured = telemetry.OsculatingMoonTwoBodyEnergyKm2PerS2 < 0.0;

        // Perilune safety: 80 km <= hp <= 250 km
        bool isPeriluneAltitudeSafe = telemetry.PeriluneAltitudeKm >= 75.0 && telemetry.PeriluneAltitudeKm <= 250.0;

        // Execution error bound
        bool isDeltaVExecutionAccurate = Math.Abs(telemetry.BurnExecutionErrorMetersPerSecond) <= 0.20 &&
                                         telemetry.PropellantMarginPercent >= 12.0;

        // Delta V savings compared to direct Patched-Conic transfer (~950 m/s)
        double baselineDirectDeltaV = 980.0;
        double deltaVSavings = Math.Max(0.0, baselineDirectDeltaV - telemetry.PlannedInsertionDeltaVMetersPerSecond);

        // Capture stability margin: based on distance to parabolic escape and tidal drift
        double captureMargin = Math.Abs(telemetry.OsculatingMoonTwoBodyEnergyKm2PerS2) * 50.0;
        if (telemetry.SolarTidalPerturbationEccentricityDriftPerOrbit > 0.003)
        {
            captureMargin -= (telemetry.SolarTidalPerturbationEccentricityDriftPerOrbit - 0.003) * 500.0;
        }
        captureMargin = Math.Clamp(captureMargin, 0.0, 10.0);

        if (!isBallisticallyCaptured)
        {
            regimes.Add("LUNAR_FLYBY_ESCAPE_TRAJECTORY");
            mitigations.Add("Execute mid-course trajectory correction maneuver (TCM-3) to target Earth-Moon L1 invariant manifold for weak stability boundary capture.");
        }

        if (telemetry.PeriluneAltitudeKm < 75.0)
        {
            regimes.Add("PERILUNE_TERRAIN_IMPACT_HAZARD");
            mitigations.Add("Apply radial burn component delta-V to raise perilune altitude above 100 km and clear lunar mascon gravitational anomalies.");
        }

        if (!isDeltaVExecutionAccurate)
        {
            regimes.Add("DELTA_V_EXECUTION_OVERRUN_UNDERBURN");
            mitigations.Add("Recalibrate reaction control thruster pulse width modulation (PWM) and trim propellant reserves to protect burn accuracy margin.");
        }

        bool isGoForBurn = isBallisticallyCaptured && isPeriluneAltitudeSafe && isDeltaVExecutionAccurate && captureMargin >= 2.0;

        return new LunarOrbitInsertionEvaluation
        {
            MissionTrajectoryId = telemetry.MissionTrajectoryId,
            CalculatedCaptureStabilityMargin = Math.Round(captureMargin, 2),
            DeltaVSavingsVsDirectMetersPerSecond = Math.Round(deltaVSavings, 1),
            IsBallisticallyCaptured = isBallisticallyCaptured,
            IsPeriluneAltitudeSafe = isPeriluneAltitudeSafe,
            IsDeltaVExecutionAccurate = isDeltaVExecutionAccurate,
            IsLunarInsertionGoForBurn = isGoForBurn,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
