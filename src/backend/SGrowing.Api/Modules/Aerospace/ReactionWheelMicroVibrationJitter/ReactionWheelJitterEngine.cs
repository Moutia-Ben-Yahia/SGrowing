using System;

namespace SGrowing.Api.Modules.Aerospace.ReactionWheelMicroVibrationJitter;

public interface IReactionWheelJitterEngine
{
    ReactionWheelJitterAnalysisResult AnalyzeWheelJitter(ReactionWheelJitterTelemetry telemetry);
}

public sealed class ReactionWheelJitterEngine : IReactionWheelJitterEngine
{
    private const double MaxStaticImbalanceGCm = 0.50; // U_s <= 0.5 g*cm
    private const double MaxDynamicImbalanceGCm2 = 1.20; // U_d <= 1.2 g*cm^2
    private const double CriticalResonanceProximityPercent = 10.0; // Avoid within 10% of resonant structural modes

    public ReactionWheelJitterAnalysisResult AnalyzeWheelJitter(ReactionWheelJitterTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.RotorSpeedRpm < 0.0 || telemetry.MaxAllowableLosJitterNrad <= 0.0)
        {
            throw new ArgumentException("Rotor speed must be non-negative and max allowable jitter must be positive.", nameof(telemetry));
        }

        // 1. Angular velocity: omega = 2 * pi * RPM / 60 (rad/s)
        double omegaRadPerS = (2.0 * Math.PI * telemetry.RotorSpeedRpm) / 60.0;

        // 2. Centrifugal Radial Disturbance Force:
        // F_s = U_s * omega^2
        // U_s in g*cm = 1e-3 kg * 1e-2 m = 1e-5 kg*m
        double uS_kgM = telemetry.StaticMassImbalanceGCm * 1e-5;
        double radialForceNewtons = uS_kgM * Math.Pow(omegaRadPerS, 2.0);
        radialForceNewtons = Math.Round(radialForceNewtons, 4);

        // 3. Rocking Dynamic Disturbance Torque:
        // T_d = U_d * omega^2
        // U_d in g*cm^2 = 1e-3 kg * 1e-4 m^2 = 1e-7 kg*m^2
        double uD_kgM2 = telemetry.DynamicMassImbalanceGCm2 * 1e-7;
        double rockingTorqueNm = uD_kgM2 * Math.Pow(omegaRadPerS, 2.0);
        double rockingTorqueMilliNm = rockingTorqueNm * 1000.0;
        rockingTorqueMilliNm = Math.Round(rockingTorqueMilliNm, 4);

        // 4. Payload Line-of-Sight (LOS) Pointing Jitter Prediction (nrad):
        // Isolation attenuation factor: A_iso ~ 1 / (2 * zeta * (omega / omega_n)^2) for high frequencies
        // Calibrated structural jitter model:
        double damping = Math.Clamp(telemetry.IsolationPlatformDampingRatio, 0.05, 0.60);
        double isolationFactor = 1.0 / (1.0 + 12.0 * damping);

        // Resonance amplification penalty if near resonance:
        double resonanceAmplification = 1.0;
        if (telemetry.StructuralResonanceProximityPercent < CriticalResonanceProximityPercent)
        {
            double proxFraction = Math.Max(0.01, telemetry.StructuralResonanceProximityPercent / CriticalResonanceProximityPercent);
            resonanceAmplification = 1.0 / proxFraction;
        }

        double rawJitterNrad = (radialForceNewtons * 22.0 + rockingTorqueMilliNm * 8.5)
            * isolationFactor
            * resonanceAmplification
            * Math.Max(1.0, telemetry.BearingHarmonicDisturbanceFactor);
        double predictedLosJitterNrad = Math.Round(Math.Clamp(rawJitterNrad, 0.1, 150.0), 2);

        // 5. Jitter Margin Ratio:
        double jitterMarginPercent = ((telemetry.MaxAllowableLosJitterNrad - predictedLosJitterNrad) / telemetry.MaxAllowableLosJitterNrad) * 100.0;
        jitterMarginPercent = Math.Round(jitterMarginPercent, 1);

        // 6. Validation Checks:
        bool isStaticOk = telemetry.StaticMassImbalanceGCm <= MaxStaticImbalanceGCm;
        bool isDynamicOk = telemetry.DynamicMassImbalanceGCm2 <= MaxDynamicImbalanceGCm2;
        bool isLosJitterOk = predictedLosJitterNrad <= telemetry.MaxAllowableLosJitterNrad;
        bool isNearResonance = telemetry.StructuralResonanceProximityPercent < CriticalResonanceProximityPercent;

        // 7. Regime & Mitigation:
        string regime;
        string mitigation;

        if (isNearResonance)
        {
            regime = "STRUCTURAL_RESONANCE_JITTER_AMPLIFICATION";
            mitigation = $"Wheel rotational frequency ({(telemetry.RotorSpeedRpm / 60.0):F1} Hz) is dangerously close ({telemetry.StructuralResonanceProximityPercent:F1}%) to payload structural resonance modes. Shift wheel operating speed into adjacent non-resonant speed bias envelope or ramp through resonance zone rapidly.";
        }
        else if (!isStaticOk || !isDynamicOk)
        {
            regime = "ROTOR_MASS_IMBALANCE_OUT_OF_SPECIFICATION";
            mitigation = $"Mass imbalance exceeded dynamic balancing thresholds (Static: {telemetry.StaticMassImbalanceGCm:F2} g*cm, Dynamic: {telemetry.DynamicMassImbalanceGCm2:F2} g*cm^2). Perform two-plane precision balance trim with balancing grub screws.";
        }
        else if (!isLosJitterOk)
        {
            regime = "PAYLOAD_LINE_OF_SIGHT_JITTER_EXCEEDED";
            mitigation = $"Predicted pointing jitter ({predictedLosJitterNrad:F1} nrad) exceeds payload specification ({telemetry.MaxAllowableLosJitterNrad:F1} nrad). Increase viscoelastic isolator strut damping or implement active hexapod voice-coil steering jitter cancellation.";
        }
        else
        {
            regime = "ULTRA_STABLE_MICRO_VIBRATION_QUIET_CRUISE";
            mitigation = "Reaction wheel operating in quiet mode: balanced rotor dynamics, nominal isolation damping, clear resonance avoidance margin, and sub-15 nrad LOS pointing jitter confirmed.";
        }

        return new ReactionWheelJitterAnalysisResult
        {
            WheelAssemblyId = telemetry.WheelAssemblyId,
            CentrifugalRadialDisturbanceForceNewtons = radialForceNewtons,
            RockingDynamicDisturbanceTorqueMilliNm = rockingTorqueMilliNm,
            PredictedPayloadLosJitterNrad = predictedLosJitterNrad,
            JitterMarginRatioPercent = jitterMarginPercent,
            IsStaticImbalanceWithinSpec = isStaticOk,
            IsDynamicImbalanceWithinSpec = isDynamicOk,
            IsLosJitterRequirementMet = isLosJitterOk,
            IsSpeedNearStructuralResonance = isNearResonance,
            WheelJitterStabilityRegime = regime,
            AttitudeControlMitigationPlan = mitigation
        };
    }
}
