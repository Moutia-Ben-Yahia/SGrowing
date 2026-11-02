using System;

namespace SGrowing.Api.Modules.Quantum.TopologicalMajoranaBraiding;

public sealed class TopologicalMajoranaBraidingEngine : ITopologicalMajoranaBraidingEngine
{
    private const double HbarMicroEvNs = 0.6582119; // hbar in ueV * ns
    private const double BoltzmannConstMicroEvPerMilliKelvin = 0.0861733; // k_B in ueV / mK

    public TopologicalMajoranaAnalysisResult AnalyzeMajoranaBraiding(TopologicalMajoranaTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var bTesla = telemetry.AppliedZeemanMagneticFieldTesla;
        var bcTesla = telemetry.CriticalTopologicalFieldThresholdTesla;
        var gapUev = Math.Max(1.0, telemetry.TopologicalSuperconductingGapMicroElectronVolts);

        var lengthNm = Math.Max(100.0, telemetry.NanowireLengthMicrometers * 1000.0);
        var xiNm = Math.Max(10.0, telemetry.MajoranaLocalizationLengthNanometers);
        var ratio = lengthNm / xiNm;

        // Majorana wavefunction overlap energy splitting: deltaE = Delta_top * exp(-L / xi)
        var deltaEMicroEv = bTesla > bcTesla 
            ? gapUev * Math.Exp(-Math.Min(40.0, ratio))
            : gapUev; // In trivial phase, gap is conventional, no zero-energy mode

        // Adiabatic braiding time constraint
        var tBraidNs = Math.Max(0.1, telemetry.BraidingOperationDurationNanoseconds);
        var minAdiabaticTimeNs = HbarMicroEvNs / gapUev;
        var adiabaticRatio = tBraidNs / Math.Max(0.001, minAdiabaticTimeNs);

        // Non-adiabatic Landau-Zener transition leakage
        var lzInfidelity = Math.Exp(-Math.Min(30.0, (2.0 * Math.PI * gapUev * tBraidNs) / HbarMicroEvNs));

        // Quasiparticle poisoning infidelity: eps_qp = T_braid / tau_qp
        var tauQpNs = Math.Max(1.0, telemetry.QuasiparticlePoisoningTimeMicroseconds * 1000.0);
        var qpInfidelity = tBraidNs / tauQpNs;

        // Thermal thermalization ratio
        var kBT = telemetry.CryogenicBaseTemperatureMilliKelvin * BoltzmannConstMicroEvPerMilliKelvin;
        var thermalInfidelity = Math.Exp(-Math.Min(30.0, gapUev / Math.Max(0.01, kBT)));

        // Overlap parity dephasing contribution
        var overlapInfidelity = deltaEMicroEv / gapUev;

        var totalInfidelity = lzInfidelity + qpInfidelity + thermalInfidelity + overlapInfidelity;
        var braidingFidelityPercent = Math.Clamp((1.0 - totalInfidelity) * 100.0, 10.0, 99.999);

        // Effective parity protection lifetime (dominated by QP poisoning and overlap)
        var parityLifetimeUs = telemetry.QuasiparticlePoisoningTimeMicroseconds / (1.0 + (deltaEMicroEv * 10.0));

        string regime;
        string guidance;
        bool isFaultTolerant;

        if (bTesla <= bcTesla)
        {
            regime = "TRIVIAL_SUPERCONDUCTING_PHASE_NO_MZMS";
            guidance = "Applied Zeeman field below critical threshold (B <= B_c). System resides in trivial s-wave superconducting phase without zero-energy Majorana modes. Increase magnetic field.";
            isFaultTolerant = false;
        }
        else if (ratio < 5.0)
        {
            regime = "MAJORANA_OVERLAP_HYBRIDIZATION_COLLAPSE";
            guidance = "Nanowire length L is too short relative to localization length xi (L/xi < 5). End MZMs hybridize with non-zero energy splitting deltaE, destroying topological degeneracy. Use longer nanowires.";
            isFaultTolerant = false;
        }
        else if (telemetry.CryogenicBaseTemperatureMilliKelvin > 150.0)
        {
            regime = "THERMAL_QUASIPARTICLE_POISONING_HAZARD";
            guidance = "Cryogenic base temperature exceeds 150 mK. Thermal quasiparticle poisoning destroys topological fermion parity protection. Chill dilution refrigerator stage.";
            isFaultTolerant = false;
        }
        else
        {
            regime = "TOPOLOGICALLY_PROTECTED_NON_ABELIAN_BRAIDING";
            guidance = "Nanowire in topological superconducting phase with isolated non-Abelian MZMs. Quantum braiding operation topologically protected against local decoherence.";
            isFaultTolerant = true;
        }

        return new TopologicalMajoranaAnalysisResult(
            QubitJunctionId: telemetry.QubitJunctionId,
            MajoranaWavefunctionOverlapSplittingMicroEv: deltaEMicroEv,
            AdiabaticBraidingFidelityPercent: braidingFidelityPercent,
            ParityProtectionLifetimeMicroseconds: parityLifetimeUs,
            TopologicalQuantumRegime: regime,
            BraidingControlGuidance: guidance,
            IsBraidingOperationFaultTolerant: isFaultTolerant
        );
    }
}
