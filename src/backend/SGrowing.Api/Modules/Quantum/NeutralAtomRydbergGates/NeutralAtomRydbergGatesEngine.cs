using System;

namespace SGrowing.Api.Modules.Quantum.NeutralAtomRydbergGates;

public sealed class NeutralAtomRydbergGatesEngine : INeutralAtomRydbergGatesEngine
{
    public NeutralAtomRydbergAnalysisResult AnalyzeRydbergGate(NeutralAtomRydbergTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var rabiGhz = Math.Max(0.0001, telemetry.LaserRabiFrequencyMegahertz * 1e-3);
        var c6 = Math.Max(1.0, telemetry.VanDerWaalsC6CoefficientGhzMicrometer6);
        var spacing = Math.Max(0.5, telemetry.InteratomicSpacingMicrometers);

        // Rydberg Blockade Radius: R_b = (C6 / hbar*Omega)^(1/6)
        var blockadeRadiusUm = Math.Pow(c6 / rabiGhz, 1.0 / 6.0);

        // Interaction energy V(R) = C6 / R^6 in GHz
        var interactionEnergyGhz = c6 / Math.Pow(spacing, 6.0);
        var blockadeRatio = interactionEnergyGhz / rabiGhz;

        // Gate duration tau_gate ~ 2*pi / Omega (in microseconds)
        var gateDurationUs = (2.0 * Math.PI) / Math.Max(0.1, telemetry.LaserRabiFrequencyMegahertz);
        var lifetimeUs = Math.Max(1.0, telemetry.SpontaneousEmissionLifetimeMicroseconds);

        // Spontaneous emission infidelity: eps_se = gateDuration / lifetime
        var spontaneousEmissionInfidelity = gateDurationUs / lifetimeUs;
        var spontaneousEmissionBps = spontaneousEmissionInfidelity * 10_000.0;

        // Jitter dephasing contribution
        var jitterNm = Math.Max(0.0, telemetry.OpticalTweezerPositionJitterNanometers);
        var jitterInfidelity = Math.Pow(jitterNm / 500.0, 2.0) * 0.002;

        // Blockade leakage infidelity if spacing approaches or exceeds blockade radius
        var blockadeLeakageInfidelity = spacing >= blockadeRadiusUm 
            ? 0.05 
            : 0.001 * Math.Pow(spacing / blockadeRadiusUm, 6.0);

        var totalInfidelity = spontaneousEmissionInfidelity + jitterInfidelity + blockadeLeakageInfidelity;
        var gateFidelityPercent = Math.Clamp((1.0 - totalInfidelity) * 100.0, 50.0, 99.99);

        string regime;
        string guidance;
        bool isHighFidelity;

        if (spacing >= blockadeRadiusUm || blockadeRatio < 5.0)
        {
            regime = "RYDBERG_BLOCKADE_BREAKDOWN_CROSS_TALK";
            guidance = "Interatomic spacing exceeds blockade radius or interaction energy ratio < 5. Double excitation cross-talk present. Reduce tweezer pitch or increase laser power.";
            isHighFidelity = false;
        }
        else if (jitterNm > 120.0)
        {
            regime = "TWEEZER_POSITION_JITTER_DEPHASING_WARNING";
            guidance = "Optical tweezer position jitter exceeds 120 nm causing motional Doppler dephasing. Engage active piezo-mirror acoustic stabilization.";
            isHighFidelity = false;
        }
        else if (gateFidelityPercent < 99.0)
        {
            regime = "SUB_OPTIMAL_GATE_FIDELITY_REGIME";
            guidance = "Two-qubit entangling gate fidelity below 99.0% threshold. Optimize adiabatic rapid passage laser pulse shaping.";
            isHighFidelity = false;
        }
        else
        {
            regime = "OPTIMAL_RYDBERG_BLOCKADE_ENTANGLEMENT_REGIME";
            guidance = "Strong Rydberg blockade established with robust interaction energy. High-fidelity entangling CZ/CNOT gate operation confirmed.";
            isHighFidelity = true;
        }

        return new NeutralAtomRydbergAnalysisResult(
            RegisterId: telemetry.RegisterId,
            RydbergBlockadeRadiusMicrometers: blockadeRadiusUm,
            CalculatedTwoQubitGateFidelityPercent: gateFidelityPercent,
            SpontaneousEmissionInfidelityBasisPoints: spontaneousEmissionBps,
            BlockadeInteractionEnergyGigaHertz: interactionEnergyGhz,
            RydbergGateRegime: regime,
            ControlGuidance: guidance,
            IsGateFidelityHigh: isHighFidelity
        );
    }
}
