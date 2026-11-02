using System;

namespace SGrowing.Api.Modules.Quantum.SuperconductingFluxoniumCoherence;

public sealed class SuperconductingFluxoniumCoherenceEngine : ISuperconductingFluxoniumCoherenceEngine
{
    public SuperconductingFluxoniumAnalysisResult AnalyzeFluxoniumCoherence(SuperconductingFluxoniumTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.QubitId))
        {
            throw new ArgumentException("Qubit ID must be provided.", nameof(telemetry));
        }

        if (telemetry.ChargingEnergyGigahertz <= 0 || telemetry.InductiveEnergyGigahertz <= 0 ||
            telemetry.JosephsonEnergyGigahertz <= 0 || telemetry.ReadoutResonatorFrequencyGigahertz <= 0)
        {
            throw new ArgumentException("Energies and resonator frequency must be strictly positive.", nameof(telemetry));
        }

        if (telemetry.MeasuredT1RelaxationMicroseconds < 0 || telemetry.MeasuredT2DephasingMicroseconds < 0)
        {
            throw new ArgumentException("Coherence times cannot be negative.", nameof(telemetry));
        }

        var fluxOffset = telemetry.ExternalFluxBiasPhi0 - 0.5; // Deviation from sweet spot

        // Fluxonium transition frequency omega_01 around half-flux sweet spot
        var plasmaFreq = Math.Sqrt(8.0 * telemetry.ChargingEnergyGigahertz * telemetry.InductiveEnergyGigahertz);
        var junctionModulation = (telemetry.JosephsonEnergyGigahertz / (2.0 * telemetry.InductiveEnergyGigahertz)) * Math.Cos(Math.PI * telemetry.ExternalFluxBiasPhi0);
        var omega01 = Math.Max(0.2, plasmaFreq * (1.0 - junctionModulation));

        // Sensitivity to flux noise: d(omega_01) / d(Phi) ~ junctionModulation * sin(pi * Phi)
        var fluxSensitivity = Math.Abs(Math.PI * plasmaFreq * (telemetry.JosephsonEnergyGigahertz / (2.0 * telemetry.InductiveEnergyGigahertz)) * Math.Sin(Math.PI * telemetry.ExternalFluxBiasPhi0));

        // Dispersive shift: chi = g^2 / Delta (Delta = omega_r - omega_01 in GHz, g in MHz)
        var detuningGhz = telemetry.ReadoutResonatorFrequencyGigahertz - omega01;
        var dispersiveShiftMhz = Math.Abs(detuningGhz) > 0.05
            ? Math.Pow(telemetry.ResonatorCouplingRateMegahertz, 2.0) / (detuningGhz * 1000.0)
            : 0.0;

        string regime;
        string guidance;
        bool isFaultTolerant = Math.Abs(fluxOffset) <= 0.02 &&
                               telemetry.MeasuredT1RelaxationMicroseconds >= 500.0 &&
                               telemetry.MeasuredT2DephasingMicroseconds >= 300.0;

        if (Math.Abs(fluxOffset) > 0.05)
        {
            regime = "OFF_SWEET_SPOT_FLUX_NOISE_DEPHASING_HAZARD";
            guidance = $"External flux bias ({telemetry.ExternalFluxBiasPhi0:F3} Phi0) deviates significantly from the 0.50 Phi0 sweet spot. First-order flux noise sensitivity is {fluxSensitivity:F2} GHz/Phi0; retune coil bias current.";
            isFaultTolerant = false;
        }
        else if (telemetry.MeasuredT1RelaxationMicroseconds < 100.0)
        {
            regime = "DIELECTRIC_OR_QUASIPARTICLE_LOSS_DOMINATED";
            guidance = $"Short T1 relaxation ({telemetry.MeasuredT1RelaxationMicroseconds:F1} us) indicates elevated dielectric loss in the shunt capacitor or quasiparticle tunneling across the Josephson junction.";
            isFaultTolerant = false;
        }
        else if (Math.Abs(fluxOffset) <= 0.01 && telemetry.MeasuredT1RelaxationMicroseconds >= 1000.0)
        {
            regime = "SWEET_SPOT_PROTECTED_ULTRA_LONG_COHERENCE_FLUXONIUM";
            guidance = $"Qubit biased at half-flux sweet spot with millisecond-scale T1 ({telemetry.MeasuredT1RelaxationMicroseconds / 1000.0:F2} ms). High dispersive shift ({dispersiveShiftMhz:F2} MHz) enables high-fidelity QND single-shot readout.";
        }
        else
        {
            regime = "NOMINAL_FLUXONIUM_OPERATION_REGIME";
            guidance = $"Operating near sweet spot with T1 = {telemetry.MeasuredT1RelaxationMicroseconds:F1} us and T2* = {telemetry.MeasuredT2DephasingMicroseconds:F1} us. Dispersive readout cavity detuned by {detuningGhz:F2} GHz.";
        }

        return new SuperconductingFluxoniumAnalysisResult(
            telemetry.QubitId,
            Math.Round(omega01, 3),
            Math.Round(dispersiveShiftMhz, 3),
            Math.Round(fluxSensitivity, 3),
            regime,
            guidance,
            isFaultTolerant
        );
    }
}
