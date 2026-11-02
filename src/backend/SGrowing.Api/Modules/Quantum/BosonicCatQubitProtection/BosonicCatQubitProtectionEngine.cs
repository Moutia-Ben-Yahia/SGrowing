using System;

namespace SGrowing.Api.Modules.Quantum.BosonicCatQubitProtection;

public sealed class BosonicCatQubitProtectionEngine : IBosonicCatQubitProtectionEngine
{
    public BosonicCatQubitAnalysisResult AnalyzeCatQubitProtection(BosonicCatQubitTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.CavityQubitId))
        {
            throw new ArgumentException("Cavity qubit ID must be provided.", nameof(telemetry));
        }

        if (telemetry.CatStateMeanPhotonNumber <= 0 || telemetry.SinglePhotonLossRateKappa1Kilohertz <= 0 ||
            telemetry.TwoPhotonDissipationRateKappa2Kilohertz <= 0)
        {
            throw new ArgumentException("Photon numbers and dissipation rates must be strictly positive.", nameof(telemetry));
        }

        if (telemetry.MeasuredBitFlipLifetimeMilliseconds < 0 || telemetry.MeasuredPhaseFlipLifetimeMicroseconds < 0)
        {
            throw new ArgumentException("Lifetimes cannot be negative.", nameof(telemetry));
        }

        var tZMs = Math.Max(0.001, telemetry.MeasuredPhaseFlipLifetimeMicroseconds / 1000.0);
        var noiseBiasRatio = telemetry.MeasuredBitFlipLifetimeMilliseconds / tZMs;

        var alphaSq = telemetry.CatStateMeanPhotonNumber;
        var dissipationRatio = telemetry.SinglePhotonLossRateKappa1Kilohertz / telemetry.TwoPhotonDissipationRateKappa2Kilohertz;
        var confinementFidelity = Math.Clamp((1.0 - (0.5 * dissipationRatio / alphaSq)) * 100.0, 50.0, 99.99);

        string regime;
        string guidance;
        bool isBiasPreserved = noiseBiasRatio >= 5000.0 &&
                               telemetry.MeasuredBitFlipLifetimeMilliseconds >= 50.0 &&
                               alphaSq >= 2.0;

        if (alphaSq < 1.0)
        {
            regime = "COLLAPSED_VACUUM_LIMIT_INSUFFICIENT_CONFINEMENT";
            guidance = $"Mean photon count (|alpha|^2 = {alphaSq:F2} < 1.0) is too low to span distinct coherent state manifolds. Increase two-photon drive power to enlarge cat size.";
            isBiasPreserved = false;
        }
        else if (telemetry.SinglePhotonLossRateKappa1Kilohertz > 50.0)
        {
            regime = "HIGH_SINGLE_PHOTON_LOSS_RATE_DECOHERENCE_HAZARD";
            guidance = $"Elevated single-photon loss (kappa1 = {telemetry.SinglePhotonLossRateKappa1Kilohertz:F1} kHz) destroys phase coherence rapidly. Improve storage cavity Q-factor or reduce Purcell filter coupling.";
            isBiasPreserved = false;
        }
        else if (telemetry.TwoPhotonDissipationRateKappa2Kilohertz < 10.0)
        {
            regime = "WEAK_TWO_PHOTON_CONFINEMENT_DRIFT";
            guidance = $"Two-photon dissipation rate ({telemetry.TwoPhotonDissipationRateKappa2Kilohertz:F1} kHz) is insufficient to pin the cat manifold against thermal drift. Increase non-linear ATS/SQUID pump.";
            isBiasPreserved = false;
        }
        else if (noiseBiasRatio >= 10000.0 && telemetry.MeasuredBitFlipLifetimeMilliseconds >= 100.0)
        {
            regime = "EXPONENTIAL_BIT_FLIP_SUPPRESSED_HIGH_BIAS_CAT_REGIME";
            guidance = $"Exponential bit-flip protection achieved (T_X = {telemetry.MeasuredBitFlipLifetimeMilliseconds:F0} ms, bias = {noiseBiasRatio:E1}). Concatenate with a 1D phase-flip repetition code for fault tolerance.";
        }
        else
        {
            regime = "NOMINAL_BOSONIC_CAT_QUBIT_OPERATION_REGIME";
            guidance = $"Cat qubit operating with moderate bias ({noiseBiasRatio:F0}x). Confinement fidelity is {confinementFidelity:F2}%.";
        }

        return new BosonicCatQubitAnalysisResult(
            telemetry.CavityQubitId,
            Math.Round(noiseBiasRatio, 1),
            Math.Round(confinementFidelity, 2),
            regime,
            guidance,
            isBiasPreserved
        );
    }
}
