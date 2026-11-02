using System;

namespace SGrowing.Api.Modules.Quantum.GaussianBosonSampling;

public sealed class GaussianBosonSamplingEngine : IGaussianBosonSamplingEngine
{
    public GbsAnalysisResult AnalyzeGaussianBosonSampling(GbsTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var nPhotons = Math.Max(1, telemetry.DetectedOutputPhotonCount);
        var torontonianDim = 2 * nPhotons;

        // Classical Hafnian simulation complexity: O(n^3 * 2^(n/2)) -> log10 operations
        var logComplexity = 0.5 * nPhotons * Math.Log10(2.0) + 3.0 * Math.Log10(nPhotons);

        // Quantum supremacy margin
        var trans = Math.Clamp(telemetry.OverallOpticalTransmissionEfficiency, 0.1, 1.0);
        var vis = Math.Clamp(telemetry.PhotonIndistinguishabilityVisibility, 0.1, 1.0);
        var classicalDivergence = Math.Max(0.05, 1.0 - telemetry.ClassicalMcmcSampleSimilarityScore);
        var supremacyMargin = logComplexity * classicalDivergence * trans * Math.Pow(vis, 2);

        string regime;
        string action;
        bool isCertified;

        if (telemetry.InterferometerPhaseStabilityRmsRadians > 0.08)
        {
            regime = "PHASE_NOISE_DECOHERENCE_HAZARD";
            action = "Interferometer thermal drift and phase jitter (> 0.08 rad) degrades unitary Haar randomness. Engage active piezo-mirror phase locking.";
            isCertified = false;
        }
        else if (telemetry.ClassicalMcmcSampleSimilarityScore > 0.40)
        {
            regime = "CLASSICAL_MOCK_SIMULABLE_DISTRIBUTION";
            action = "Output photon correlation distribution spoofable by classical thermal or classical MCMC sampling. Increase squeezing parameter and mode count.";
            isCertified = false;
        }
        else if (telemetry.PhotonIndistinguishabilityVisibility < 0.96 || telemetry.OverallOpticalTransmissionEfficiency < 0.70)
        {
            regime = "OPTICAL_TRANSMISSION_LOSS_LIMITED";
            action = "Photon distinguishability or circuit transmission loss exceeds fault-tolerance threshold. Re-align Hong-Ou-Mandel interferometer couplers.";
            isCertified = false;
        }
        else if (nPhotons >= 45 && logComplexity > 14.0)
        {
            regime = "QUANTUM_COMPUTATIONAL_ADVANTAGE_VERIFIED";
            action = "Gaussian Boson Sampling verified beyond classical supercomputer simulation reach. Quantum advantage certified.";
            isCertified = true;
        }
        else
        {
            regime = "SUB_ADVANTAGE_BENCHMARKING_REGIME";
            action = "Photon detection count below computational advantage regime (< 45 photons). Increase pump power and detector array coverage.";
            isCertified = false;
        }

        return new GbsAnalysisResult(
            InterferometerId: telemetry.InterferometerId,
            TorontonianMatrixDimension: torontonianDim,
            Log10ClassicalHafnianSimulationComplexity: logComplexity,
            QuantumComputationalSupremacyMargin: supremacyMargin,
            SamplingRegime: regime,
            OpticalMeshCalibrationAction: action,
            IsQuantumAdvantageCertified: isCertified
        );
    }
}
