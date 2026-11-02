using System;

namespace SGrowing.Api.Modules.Aerospace.DualStageHallThrusterAnodeErosion;

public sealed class DualStageHallThrusterAnodeErosionEngine : IDualStageHallThrusterAnodeErosionEngine
{
    public DualStageHallAnalysisResult AnalyzeThrusterErosionAndThrust(DualStageHallTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var isXenon = string.Equals(telemetry.PropellantType, "Xenon", StringComparison.OrdinalIgnoreCase);
        // Ion mass: Xenon (131.29 amu) vs Krypton (83.80 amu)
        var ionMassKg = isXenon ? 2.18e-25 : 1.39e-25;
        const double elementaryCharge = 1.60217663e-19;
        const double g0 = 9.80665;

        // Exhaust velocity v_e = sqrt(2 * q * V_acc / m_ion) * divergence_eff (~0.94)
        var vAcc = Math.Max(50.0, telemetry.AccelerationStageVoltageVolts);
        var exhaustVelocityMps = Math.Sqrt((2.0 * elementaryCharge * vAcc) / ionMassKg) * 0.94;

        // Specific impulse Isp = v_e / g0
        var ispSeconds = Math.Round(exhaustVelocityMps / g0, 1);

        // Thrust F = m_dot * v_e (m_dot in mg/s -> kg/s * 1e-6, thrust in mN)
        var massFlowKgPerSec = (telemetry.AnodePropellantMassFlowMgPerSec / 1000.0) / 1000.0;
        var thrustMilliNewtons = Math.Round(massFlowKgPerSec * exhaustVelocityMps * 1000.0, 1);

        // Projected remaining lifetime based on 25,000 um maximum permissible channel recession
        const double maxChannelRecessionMicrons = 25000.0;
        var erosionRate = Math.Max(0.05, telemetry.ChannelWallErosionRateMicronsPerHour);
        var remainingLifetimeHours = Math.Round(maxChannelRecessionMicrons / erosionRate, 1);

        string regime;
        string command;
        bool isOperational;

        if (telemetry.GimbalVectorMisalignmentDegrees > 2.2)
        {
            regime = "GIMBAL_VECTOR_DRIFT_EXCEEDED";
            command = "Thrust vector misaligned with spacecraft center of mass (>2.2 deg). Step gimbal stepper actuator to trim moment arm.";
            isOperational = false;
        }
        else if (telemetry.ChannelWallErosionRateMicronsPerHour > 3.2 || telemetry.MagneticShieldingCuspDisplacementMillimeters > 2.0)
        {
            regime = "ACCELERATION_CHANNEL_EROSION_HAZARD";
            command = "Magnetic shielding cusp topology displaced from channel lip. Increase trim coil current to restore zero-field tangential line.";
            isOperational = false;
        }
        else if (!telemetry.ActiveCathodeNeutralizerRedundantReady)
        {
            regime = "DOUBLE_CHARGED_ION_ANOMALY";
            command = "Redundant cathode neutralizer offline. Spacecraft chassis charging hazard; ignite backup BaO hollow cathode.";
            isOperational = false;
        }
        else
        {
            regime = "MAGNETIC_SHIELDING_NOMINAL_PROPULSION";
            command = "Magnetic shielding boundary intact. Dual-stage Hall thruster operating with high Isp (>3000s) and minimal channel sputtering.";
            isOperational = true;
        }

        return new DualStageHallAnalysisResult(
            ThrusterId: telemetry.ThrusterId,
            TotalThrustMillinewtons: thrustMilliNewtons,
            SpecificImpulseSeconds: ispSeconds,
            ProjectedThrusterRemainingLifetimeHours: remainingLifetimeHours,
            ThrusterHealthRegime: regime,
            MagneticTrimCommand: command,
            IsThrusterOperational: isOperational
        );
    }
}
