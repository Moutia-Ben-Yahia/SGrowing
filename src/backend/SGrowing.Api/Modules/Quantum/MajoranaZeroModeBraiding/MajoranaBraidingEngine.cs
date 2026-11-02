namespace SGrowing.Api.Modules.Quantum.MajoranaZeroModeBraiding;

public interface IMajoranaBraidingEngine
{
    MajoranaBraidingEvaluation AnalyzeBraiding(MajoranaBraidingTelemetry telemetry);
}

public class MajoranaBraidingEngine : IMajoranaBraidingEngine
{
    private const double QuantizedConductanceG0 = 77.4809; // 2e^2 / h in uS

    public MajoranaBraidingEvaluation AnalyzeBraiding(MajoranaBraidingTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Conductance quantization error vs 2e^2/h
        double conductanceError = Math.Abs(telemetry.ZeroBiasConductanceMicroSiemens - QuantizedConductanceG0) / QuantizedConductanceG0 * 100.0;

        // Parity lifetime tau_parity = 1 / Gamma_qp (in microseconds)
        double gammaKHz = Math.Max(0.01, telemetry.QuasiparticlePoisoningRateKiloHertz);
        double parityLifetimeUs = 1000.0 / gammaKHz;

        bool isZbpQuantized = conductanceError <= 5.0;
        bool isGapOpen = telemetry.TopologicalSuperconductingGapMicroEv >= 32.0 && telemetry.TopologicalProtectionGapRatio >= 0.25;
        bool isParityProtected = telemetry.QuasiparticlePoisoningRateKiloHertz <= 1.5 && parityLifetimeUs >= 650.0;

        if (!isGapOpen)
        {
            regimes.Add("MAJORANA_TOPOLOGICAL_GAP_COLLAPSE");
            mitigations.Add("Tune in-plane Zeeman magnetic field and back-gate potential to enter the topological phase without inducing trivial Andreev bound states.");
        }

        if (!isParityProtected)
        {
            regimes.Add("QUASIPARTICLE_POISONING_PARITY_FLIP");
            mitigations.Add("Install cryogenic copper-powder microwave and infrared shielding to filter out stray pair-breaking thermal radiation.");
        }

        if (telemetry.MajoranaBraidingPhaseErrorDegrees > 2.2 || telemetry.BraidingOperationFidelityPercent < 98.5)
        {
            regimes.Add("NON_ABELIAN_BRAIDING_PHASE_INFIDELITY");
            mitigations.Add("Optimize adiabatic braiding gate duration in T-junction nanowire arrays to prevent non-adiabatic diabatic transitions to the continuum.");
        }

        bool isBraidingVerified = isZbpQuantized && isGapOpen && isParityProtected &&
                                  telemetry.MajoranaBraidingPhaseErrorDegrees <= 1.8 &&
                                  telemetry.BraidingOperationFidelityPercent >= 99.0;

        return new MajoranaBraidingEvaluation
        {
            NanowireDeviceUid = telemetry.NanowireDeviceUid,
            ConductanceQuantizationErrorPercent = Math.Round(conductanceError, 2),
            ParityLifetimeMicroseconds = Math.Round(parityLifetimeUs, 1),
            IsZeroBiasPeakQuantized = isZbpQuantized,
            IsTopologicalGapOpen = isGapOpen,
            IsParityLifetimeProtected = isParityProtected,
            IsNonAbelianBraidingVerified = isBraidingVerified,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
