namespace SGrowing.Api.Modules.Aerospace.HallThrusterPoleErosion;

public interface IHallThrusterPoleErosionEngine
{
    HallThrusterErosionAssessment EvaluateThruster(HallThrusterEvaluationRequest request);
}

public class HallThrusterPoleErosionEngine : IHallThrusterPoleErosionEngine
{
    private const double StandardGravity = 9.80665; // m/s^2

    public HallThrusterErosionAssessment EvaluateThruster(HallThrusterEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var lim = request.Limits;

        // Effective exhaust velocity: v_eff ~ sqrt(2 * q * V_d / m) * correction factor
        // For Xenon propellant with 0.90 divergence/charge correction factor:
        double effectiveExhaustVelocityMPerSec = 1080.0 * Math.Sqrt(Math.Max(50.0, tel.AnodeDischargeVoltageVolts));

        // Specific impulse I_sp = v_eff / g_0 in seconds
        double specificImpulseSeconds = effectiveExhaustVelocityMPerSec / StandardGravity;

        // Thrust in mN: F = m_dot * v_eff
        double massFlowKgPerSec = (tel.PropellantMassFlowRateMgPerSec * 1e-6);
        double thrustNewtons = massFlowKgPerSec * effectiveExhaustVelocityMPerSec;
        double thrustMilliNewtons = thrustNewtons * 1000.0;

        // Anode efficiency: eta_a = F^2 / (2 * m_dot * P_anode)
        double dischargePowerWatts = Math.Max(10.0, tel.AnodeDischargeVoltageVolts * tel.AnodeDischargeCurrentAmperes);
        double anodeEfficiencyPercent = Math.Clamp(100.0 * (Math.Pow(thrustNewtons, 2.0) / (2.0 * Math.Max(1e-7, massFlowKgPerSec) * dischargePowerWatts)), 35.0, 68.0);

        // Pole piece erosion and remaining operational life:
        // Magnetic pole pieces can tolerate ~120 microns total sputtering before iron magnetic saturation failure
        double erosionRate = Math.Max(1.0, tel.InnerPolePieceErosionMicronsPer1000Hours);
        double cumulativeErosionMicrons = (erosionRate * (tel.CumulativeFiredHours / 1000.0));
        double remainingHours = Math.Max(0.0, (120.0 - cumulativeErosionMicrons) / (erosionRate / 1000.0));

        bool erosionCritical = tel.InnerPolePieceErosionMicronsPer1000Hours >= lim.MaxAllowablePoleErosionMicronsPer1000Hours;
        bool breathingAnomaly = tel.BreathingModeOscillationPeakKHz is < 15.0 or > 28.0;

        string healthState;
        string directive;

        if (erosionCritical)
        {
            healthState = "CRITICAL_POLE_SPUTTERING_HAZARD";
            directive = $"INCREASE_MAGNETIC_SHIELDING: Pole erosion rate ({tel.InnerPolePieceErosionMicronsPer1000Hours:F1} um/1kh) exceeds threshold. Increase magnetic coil current to {tel.MagneticCoilCurrentAmperes * 1.15:F1} A to establish magnetic shielding tangency lines and deflect grazing backscatter ions.";
        }
        else if (breathingAnomaly)
        {
            healthState = "BREATHING_MODE_DISCHARGE_INSTABILITY";
            directive = $"TRIM_DISCHARGE_VOLTAGE: Breathing mode frequency ({tel.BreathingModeOscillationPeakKHz:F1} kHz) deviates from nominal 20-25 kHz band. Adjust anode voltage by -15V to stabilize neutral atom replenishment cadence in the acceleration channel.";
        }
        else
        {
            healthState = "NOMINAL_PROPULSION_STEADY";
            directive = $"PROPULSION_NOMINAL: Isp ({specificImpulseSeconds:F0} s) and thrust ({thrustMilliNewtons:F1} mN) on target. Magnetic pole erosion within safe deep-space transit margins.";
        }

        return new HallThrusterErosionAssessment(
            ThrusterId: tel.ThrusterId,
            ThrustMilliNewtons: Math.Round(thrustMilliNewtons, 1),
            SpecificImpulseSeconds: Math.Round(specificImpulseSeconds, 1),
            AnodeEfficiencyPercent: Math.Round(anodeEfficiencyPercent, 1),
            EstimatedRemainingOperationalHours: Math.Round(remainingHours, 0),
            PolePieceErosionCriticalDetected: erosionCritical,
            ThrusterHealthState: healthState,
            MagneticFieldAdjustmentDirective: directive
        );
    }
}
