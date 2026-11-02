namespace SGrowing.Api.Modules.Quantum.SuperconductingQubitGateArbiter;

public interface ISuperconductingQubitGateArbiterEngine
{
    QuantumGateArbiterResult SynthesizeGate(GateSynthesisRequest request);
}

public class SuperconductingQubitGateArbiterEngine : ISuperconductingQubitGateArbiterEngine
{
    public QuantumGateArbiterResult SynthesizeGate(GateSynthesisRequest request)
    {
        var q = request.TargetQubit;
        var couplings = request.Couplings ?? [];

        double durationNs = Math.Clamp(request.GateDurationNs, 10.0, 60.0);
        double sigmaNs = durationNs / 4.0;

        // Peak microwave drive amplitude in mV based on gate angle
        double angleTarget = request.GateType switch
        {
            "X_HALF_PI" => Math.PI / 2.0,
            "HADAMARD" => Math.PI / 2.0,
            _ => Math.PI // X_PI, Y_PI
        };

        // Calibrated amplitude: 180 mV for 24ns pi pulse
        double peakAmplitudeMv = (angleTarget / Math.PI) * (180.0 * (24.0 / durationNs));

        // DRAG beta coefficient: beta ~ -1 / (2 * alpha * sigma)
        // alpha in GHz (-0.31 GHz for -310 MHz), sigma in ns
        double alphaGhz = q.AnharmonicityMhz / 1000.0;
        double dragBeta = -1.0 / (2.0 * alphaGhz * sigmaNs);
        dragBeta = Math.Clamp(dragBeta, -2.5, 2.5);

        // Leakage suppression in dB with DRAG correction (~35 dB vs 12 dB without DRAG)
        double leakageSuppressionDb = 36.5 - (Math.Abs(dragBeta - 1.0) * 4.0);

        // Incoherent decoherence infidelity: epsilon_incoh ~ duration / (3 * T1) + duration / (3 * T2)
        double t1Ns = q.T1RelaxationMicroseconds * 1000.0;
        double t2Ns = q.T2DephasingMicroseconds * 1000.0;
        double decoherenceInfidelity = (durationNs / (3.0 * t1Ns)) + (durationNs / (3.0 * t2Ns));

        // Residual ZZ phase error: accumulative conditional phase over gate duration
        double totalZzKhz = couplings.Where(c => c.QubitA == q.QubitId || c.QubitB == q.QubitId).Sum(c => c.StaticZzCouplingKhz);
        double zzPhaseErrorRad = 2.0 * Math.PI * (totalZzKhz * 1e3) * (durationNs * 1e-9);
        double zzInfidelity = 0.5 * zzPhaseErrorRad * zzPhaseErrorRad;

        double totalInfidelity = decoherenceInfidelity + zzInfidelity + 0.0002; // control jitter floor
        double gateFidelity = Math.Clamp((1.0 - totalInfidelity) * 100.0, 90.0, 99.99);

        bool approved = gateFidelity >= 99.70 && Math.Abs(zzPhaseErrorRad) < 0.02;

        string action;
        if (approved)
        {
            action = $"CALIBRATION_CERTIFIED: Gate fidelity {gateFidelity:F3}% meets quantum fault-tolerance threshold (>= 99.7%). DRAG parameter beta={dragBeta:F2} locked.";
        }
        else if (Math.Abs(zzPhaseErrorRad) >= 0.02)
        {
            action = $"ZZ_CROSSTALK_WARNING: Parasitic ZZ phase error ({zzPhaseErrorRad:F4} rad) degrades gate fidelity to {gateFidelity:F3}%. Insert dynamical decoupling echo or detune spectator qubit.";
        }
        else
        {
            action = $"COHERENCE_LIMITED: T1/T2 decoherence limits fidelity to {gateFidelity:F3}%. Consider shortening gate duration to reduce thermal relaxation.";
        }

        var pulse = new SynthesizedPulseWaveform(
            PeakAmplitudeMv: Math.Round(peakAmplitudeMv, 2),
            GaussianSigmaNs: Math.Round(sigmaNs, 2),
            DragCoefficientBeta: Math.Round(dragBeta, 3),
            LeakageSuppressionDb: Math.Round(leakageSuppressionDb, 1)
        );

        return new QuantumGateArbiterResult(
            QubitId: q.QubitId,
            GateType: request.GateType,
            Pulse: pulse,
            PredictedGateFidelityPercent: Math.Round(gateFidelity, 3),
            ResidualZzPhaseErrorRadians: Math.Round(zzPhaseErrorRad, 5),
            CalibrationApproved: approved,
            CalibrationAction: action
        );
    }
}
