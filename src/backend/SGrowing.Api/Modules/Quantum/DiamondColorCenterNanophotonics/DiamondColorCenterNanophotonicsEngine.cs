using System;

namespace SGrowing.Api.Modules.Quantum.DiamondColorCenterNanophotonics;

public interface IDiamondColorCenterNanophotonicsEngine
{
    DiamondColorCenterAnalysisResult AnalyzeNanophotonicInterface(DiamondColorCenterTelemetry telemetry);
}

public sealed class DiamondColorCenterNanophotonicsEngine : IDiamondColorCenterNanophotonicsEngine
{
    private const double MinTargetPurcellFactor = 25.0; // F_P >= 25.0 for strong radiative Purcell enhancement
    private const double MinGuidedZplBranchingRatio = 0.85; // beta_ZPL >= 85% into guided nanobeam mode
    private const double MaxAllowableDephasingRateKhz = 250.0; // gamma_orb <= 250 kHz for ms coherence

    public DiamondColorCenterNanophotonicsEngine() { }

    public DiamondColorCenterAnalysisResult AnalyzeNanophotonicInterface(DiamondColorCenterTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.CavityQualityFactorQ <= 0.0 || telemetry.ModeVolumeVLambdaOverNCubed <= 0.0)
        {
            throw new ArgumentException("Cavity quality factor and mode volume must be positive.", nameof(telemetry));
        }

        // 1. Purcell Factor:
        // F_P = (3 / (4 * pi^2)) * (Q / V) * dipole_alignment * 1 / (1 + 4 * Q^2 * (Delta_omega / omega_0)^2)
        // omega_0 = 2 * pi * c / lambda
        double cMPerS = 2.99792458e8;
        double lambdaMeters = telemetry.ZplWavelengthNm * 1e-9;
        double opticalFreqHz = cMPerS / lambdaMeters;
        double detuningHz = telemetry.DetuningGhz * 1e9;

        double detuningFactor = 1.0 / (1.0 + 4.0 * Math.Pow(telemetry.CavityQualityFactorQ * (detuningHz / opticalFreqHz), 2.0));
        double alignment = Math.Clamp(telemetry.DipoleOrientationAlignmentFactor, 0.1, 1.0);

        double purcellFactor = (3.0 / (4.0 * Math.Pow(Math.PI, 2.0)))
            * (telemetry.CavityQualityFactorQ / telemetry.ModeVolumeVLambdaOverNCubed)
            * alignment
            * detuningFactor;
        purcellFactor = Math.Round(Math.Clamp(purcellFactor, 0.1, 250.0), 2);

        // 2. Guided Mode ZPL Branching Ratio (beta_ZPL):
        // beta_ZPL = (F_P * DW0) / (1 + F_P * DW0)
        double dw0 = Math.Clamp(telemetry.IntrinsicDebyeWallerFactor, 0.01, 0.95);
        double guidedBranching = (purcellFactor * dw0) / (1.0 + purcellFactor * dw0);
        guidedBranching = Math.Round(Math.Clamp(guidedBranching, 0.05, 0.999), 3);

        // 3. Spontaneous Emission Rate Enhancement:
        double rateEnhancement = Math.Round(1.0 + purcellFactor * dw0, 2);

        // 4. Phonon-Mediated Orbital Dephasing Rate (gamma_orb):
        // gamma_orb ~ A_phonon * Delta_gs^3 * 1 / (exp(h * Delta_gs / (k_B * T)) - 1)
        double hPlanck = 6.62607015e-34;
        double kB = 1.380649e-23;
        double deltaGsHz = telemetry.GroundStateSplittingGhz * 1e9;
        double tempK = Math.Max(0.01, telemetry.OperatingTemperatureKelvin);

        double exponent = (hPlanck * deltaGsHz) / (kB * tempK);
        double boseEinsteinFactor;
        if (exponent > 40.0)
        {
            boseEinsteinFactor = 0.0;
        }
        else
        {
            boseEinsteinFactor = 1.0 / (Math.Exp(exponent) - 1.0);
        }

        // Calibrated acoustic phonon rate in kHz:
        double dephasingRateKhz = 1.5e-23 * Math.Pow(deltaGsHz, 3.0) * boseEinsteinFactor * 1e-3;
        dephasingRateKhz = Math.Round(Math.Clamp(dephasingRateKhz, 0.01, 100000.0), 2);

        // 5. Spin Coherence Time Estimate (ms) ~ 1 / (2 * pi * gamma_orb):
        double coherenceTimeMs = dephasingRateKhz > 0.0
            ? 1000.0 / (2.0 * Math.PI * dephasingRateKhz + 1e-6)
            : 100.0;
        coherenceTimeMs = Math.Round(Math.Clamp(coherenceTimeMs, 0.001, 50.0), 3);

        // 6. Validation Checks
        bool isPurcellOk = purcellFactor >= MinTargetPurcellFactor;
        bool isBranchingOk = guidedBranching >= MinGuidedZplBranchingRatio;
        bool isDephasingOk = dephasingRateKhz <= MaxAllowableDephasingRateKhz;

        // 7. Regime & Recommendations
        string regime;
        string recommendation;

        if (!isPurcellOk)
        {
            regime = "SUB_PURCELL_RADIATIVE_COUPLING_DEFICIT";
            recommendation = $"Purcell factor ({purcellFactor:F1}) is below threshold ({MinTargetPurcellFactor:F1}). Fine-tune cavity gas condensation or laser trimming to minimize cavity-emitter frequency detuning ({telemetry.DetuningGhz:F2} GHz).";
        }
        else if (!isDephasingOk)
        {
            regime = "PHONON_INDUCED_ORBITAL_DEPHASING_COLLAPSE";
            recommendation = $"Orbital dephasing rate ({dephasingRateKhz:F1} kHz) exceeds tolerance ({MaxAllowableDephasingRateKhz:F1} kHz) at {telemetry.OperatingTemperatureKelvin:F1} K. Lower operating temperature or switch from SiV- (50 GHz splitting) to SnV-/PbV- (>= 850 GHz splitting).";
        }
        else if (!isBranchingOk)
        {
            regime = "LOW_ZPL_BRANCHING_PHONON_SIDEBAND_LEAK";
            recommendation = $"Guided ZPL branching ratio ({guidedBranching * 100.0:F1}%) fell short of optimal ({MinGuidedZplBranchingRatio * 100.0:F1}%). Boost cavity quality factor Q to channel more emission into the resonant Zero-Phonon Line.";
        }
        else
        {
            regime = "OPTIMAL_QUANTUM_REPEATER_SPIN_PHOTON_NODE";
            recommendation = "Diamond nanophotonic interface operating in ideal Purcell-enhanced regime: high ZPL guided branching, suppressed orbital phonon dephasing, and millisecond spin coherence ready for entanglement distribution.";
        }

        return new DiamondColorCenterAnalysisResult
        {
            CavityId = telemetry.CavityId,
            PurcellFactorFp = purcellFactor,
            GuidedModeBranchingRatioBetaZpl = guidedBranching,
            SpontaneousEmissionRateEnhancement = rateEnhancement,
            PhononOrbitalDephasingRateKhz = dephasingRateKhz,
            SpinCoherenceTimeEstimateMs = coherenceTimeMs,
            IsPurcellEnhancementSufficient = isPurcellOk,
            IsGuidedBranchingRatioOptimal = isBranchingOk,
            IsOrbitalDephasingSuppressed = isDephasingOk,
            OpticalInterfaceRegime = regime,
            NanophotonicRecommendation = recommendation
        };
    }
}
