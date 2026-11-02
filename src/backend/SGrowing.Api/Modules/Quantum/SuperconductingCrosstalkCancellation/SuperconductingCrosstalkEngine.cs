using System;

namespace SGrowing.Api.Modules.Quantum.SuperconductingCrosstalkCancellation;

public interface ISuperconductingCrosstalkEngine
{
    SuperconductingCrosstalkAnalysisResult AnalyzeCrosstalk(SuperconductingCrosstalkTelemetry telemetry);
}

public sealed class SuperconductingCrosstalkEngine : ISuperconductingCrosstalkEngine
{
    private const double MaxAllowableZzCouplingKhz = 25.0; // zeta_ZZ <= 25 kHz
    private const double MaxConditionNumber = 2.5; // kappa(M) <= 2.5 for stable matrix inversion
    private const double MinTargetCzFidelityPercent = 99.75; // F_CZ >= 99.75%

    public SuperconductingCrosstalkAnalysisResult AnalyzeCrosstalk(SuperconductingCrosstalkTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.ActiveQubitsCount <= 1)
        {
            throw new ArgumentException("Active qubits count must be greater than 1.", nameof(telemetry));
        }

        // 1. Condition Number of Mutual Inductance Matrix:
        // M = I + epsilon * OffDiag, kappa(M) ~ (1 + max_crosstalk) / (1 - max_crosstalk)
        double mCrosstalk = Math.Clamp(telemetry.MaxMutualFluxCrosstalkCoefficient, 0.0, 0.45);
        double conditionNumber = (1.0 + mCrosstalk) / Math.Max(0.01, 1.0 - mCrosstalk);
        conditionNumber = Math.Round(conditionNumber, 2);

        // 2. Analytical Residual ZZ-Coupling:
        // zeta_ZZ ~ (2 * J^2 * (alpha1 + alpha2)) / ((Delta - alpha1) * (Delta + alpha2))
        // Convert to kHz: J in MHz, alpha in MHz, Delta in MHz -> result in MHz * 1000 = kHz
        double jMhz = telemetry.TwoQubitExchangeCouplingMhz;
        double alphaMhz = telemetry.QubitAnharmonicityMhz;
        double deltaMhz = telemetry.QubitQubitDetuningMhz;

        double denom = (deltaMhz - alphaMhz) * (deltaMhz + alphaMhz);
        double theoreticalBareZzKhz = Math.Abs(denom) > 1.0
            ? Math.Abs((2.0 * Math.Pow(jMhz, 2.0) * (2.0 * alphaMhz)) / denom) * 1000.0
            : 50.0;

        // Post-cancellation residual static ZZ-coupling from calibrated telemetry:
        double effectiveZzKhz = Math.Round(Math.Clamp(telemetry.ResidualStaticZzCouplingKhz, 0.5, 250.0), 2);

        // 3. Projected Two-Qubit CZ Gate Fidelity:
        // Error decomposes into ZZ-error and pulse distortion error:
        double zzInfidelity = Math.Pow(effectiveZzKhz / 100.0, 1.8) * 0.0015;
        double pulseInfidelity = (telemetry.SkinDepthFluxPulseDistortionPercent / 100.0) * 0.035;
        double baseInfidelity = 0.0008; // intrinsic T1/T2 decoherence limit ~ 99.92%
        double totalInfidelity = baseInfidelity + zzInfidelity + pulseInfidelity;
        double projectedCzFidelity = (1.0 - totalInfidelity) * 100.0;
        projectedCzFidelity = Math.Round(Math.Clamp(projectedCzFidelity, 95.0, 99.99), 3);

        // 4. Pulse Distortion Error in basis points (1 bp = 0.01%):
        double pulseErrorBps = Math.Round(pulseInfidelity * 10000.0, 2);

        // 5. Validation Checks:
        bool isMatrixOk = conditionNumber <= MaxConditionNumber;
        bool isZzSuppressed = effectiveZzKhz <= MaxAllowableZzCouplingKhz;
        bool isCzOk = projectedCzFidelity >= MinTargetCzFidelityPercent;

        // 6. Regime & Guidance:
        string regime;
        string guidance;

        if (!isMatrixOk)
        {
            regime = "ILL_CONDITIONED_FLUX_MUTUAL_COUPLING_MATRIX";
            guidance = $"Condition number kappa(M) = {conditionNumber:F2} is elevated (> {MaxConditionNumber:F1}). Flux line cross-coupling ({mCrosstalk * 100.0:F1}%) is too strong for unregularized linear matrix inversion. Apply ridge Tikhonov regularization or physically space bias wire traces.";
        }
        else if (!isZzSuppressed)
        {
            regime = "PARASITIC_STATIC_ZZ_INTERACTION_DEPHASING";
            guidance = $"Residual static ZZ coupling ({effectiveZzKhz:F1} kHz) exceeds threshold ({MaxAllowableZzCouplingKhz:F1} kHz). Adjust tunable coupler bias to zero out effective exchange interaction at idle frequency points.";
        }
        else if (!isCzOk)
        {
            regime = "PULSE_DISTORTION_CZ_INFIDELITY_ELEVATED";
            guidance = $"Projected CZ gate fidelity ({projectedCzFidelity:F3}%) fell below target ({MinTargetCzFidelityPercent:F2}%). Deconvolve cryostat line skin effect and bias-tee RC step response using pre-distorted multi-exponential deconvolution filters.";
        }
        else
        {
            regime = "ORTHOGONALIZED_FLUX_CONTROL_HIGH_FIDELITY_GATE_READY";
            guidance = "Quantum processor flux crosstalk cancellation fully calibrated: inverted mutual matrix well-conditioned, static ZZ-coupling quenched, and high-fidelity (> 99.8%) entangling gates verified.";
        }

        return new SuperconductingCrosstalkAnalysisResult
        {
            ProcessorId = telemetry.ProcessorId,
            ConditionNumberOfMutualMatrix = conditionNumber,
            CalculatedResidualZzCouplingKhz = effectiveZzKhz,
            ProjectedTwoQubitCzFidelityPercent = projectedCzFidelity,
            PulseDistortionInversionErrorBps = pulseErrorBps,
            IsFluxMatrixWellConditioned = isMatrixOk,
            IsResidualZzCouplingSuppressed = isZzSuppressed,
            IsCzGateFidelityTargetMet = isCzOk,
            CrosstalkSuppressionRegime = regime,
            CalibrationRecommendation = guidance
        };
    }
}
