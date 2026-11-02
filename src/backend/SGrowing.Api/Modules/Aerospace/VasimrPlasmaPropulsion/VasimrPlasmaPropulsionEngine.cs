namespace SGrowing.Api.Modules.Aerospace.VasimrPlasmaPropulsion;

public class VasimrPlasmaPropulsionEngine
{
    private const double StandardGravityG0 = 9.80665; // m/s^2
    private const double ThrusterEfficiencyEta = 0.65; // Helicon + ICRH coupling efficiency

    public VasimrPlasmaAnalysisResult AnalyzePropulsion(VasimrPlasmaTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.SatelliteId))
        {
            throw new ArgumentException("Satellite ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.OrbitalVelocityMPerSec <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Orbital velocity must be positive.");
        }

        if (telemetry.CurrentIspSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Specific impulse must be positive.");
        }

        if (telemetry.AvailableRfPowerWatts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "RF power must be positive.");
        }

        // Aerodynamic Drag Force: F_drag = 0.5 * rho * v^2 * Cd * A [N]
        double dynamicPressure = 0.5 * telemetry.AtmosphericDensityKgPerM3 * Math.Pow(telemetry.OrbitalVelocityMPerSec, 2);
        double dragForceNewtons = dynamicPressure * telemetry.DragCoefficient * telemetry.SatelliteRamAreaM2;

        // VASIMR Thrust Generated: F_thrust = (2 * eta * P_RF) / (g_0 * Isp) [N]
        double thrustNewtons = (2.0 * ThrusterEfficiencyEta * telemetry.AvailableRfPowerWatts) / (StandardGravityG0 * telemetry.CurrentIspSeconds);

        // Net Thrust Margin: Delta_F = F_thrust - F_drag
        double netMarginNewtons = thrustNewtons - dragForceNewtons;
        bool isAltitudeLossPrevented = netMarginNewtons > 0.0;

        // Propellant mass flow rate: m_dot = F / (g_0 * Isp) [kg/s -> mg/s]
        double mDotMilligramsPerSec = (thrustNewtons / (StandardGravityG0 * telemetry.CurrentIspSeconds)) * 1e6;

        string mode;
        string advisory;

        if (dragForceNewtons >= 2.0 || dragForceNewtons >= (thrustNewtons * 0.75))
        {
            mode = "MAX_THRUST_MAGNETIC_STORM_OVERRIDE";
            advisory = $"SEVERE DRAG SURGE: Thermospheric drag ({dragForceNewtons:F2} N) threatening orbital decay at {telemetry.OrbitalAltitudeKm:F0} km. Commanding VASIMR ICRH detuning to high-density mode (Isp ~4000s) to maximize raw thrust and prevent re-entry.";
        }
        else if (dragForceNewtons >= 0.40)
        {
            mode = "BALANCED_DRAG_MAKEUP";
            advisory = $"ACTIVE DRAG MAKEUP: Drag force {dragForceNewtons:F2} N counteracted by {thrustNewtons:F2} N VASIMR thrust (Net margin +{netMarginNewtons:F2} N). Balanced Isp mode (~8000s) conserving propellant.";
        }
        else
        {
            mode = "HIGH_ISP_ORBITAL_MAINTENANCE";
            advisory = $"NOMINAL VLEO CRUISE: Atmospheric drag minimal ({dragForceNewtons:F3} N). Operating at peak efficiency Isp={telemetry.CurrentIspSeconds:F0}s (consumption: {mDotMilligramsPerSec:F1} mg/s).";
        }

        return new VasimrPlasmaAnalysisResult(
            telemetry.SatelliteId,
            Math.Round(dragForceNewtons, 3),
            Math.Round(thrustNewtons, 3),
            Math.Round(netMarginNewtons, 3),
            isAltitudeLossPrevented,
            mode,
            Math.Round(mDotMilligramsPerSec, 2),
            advisory);
    }
}
