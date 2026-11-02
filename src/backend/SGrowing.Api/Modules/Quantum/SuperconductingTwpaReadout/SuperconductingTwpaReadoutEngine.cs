using System;

namespace SGrowing.Api.Modules.Quantum.SuperconductingTwpaReadout;

public sealed class SuperconductingTwpaReadoutEngine : ISuperconductingTwpaReadoutEngine
{
    private const double StandardQuantumLimitPhotons = 0.5; // SQL half-quantum noise

    public SuperconductingTwpaAnalysisResult AnalyzeTwpaPerformance(SuperconductingTwpaTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.AmplifierId))
        {
            throw new ArgumentException("Amplifier ID must be provided.", nameof(telemetry));
        }

        if (telemetry.CenterFrequencyGigahertz <= 0 || telemetry.UsableBandwidthGigahertz <= 0 ||
            telemetry.AddedNoisePhotons < 0)
        {
            throw new ArgumentException("Frequency, bandwidth, and noise photons must be positive.", nameof(telemetry));
        }

        var noiseRelativeToSql = telemetry.AddedNoisePhotons / StandardQuantumLimitPhotons;

        // Number of multiplexed 200 MHz readout channels within bandwidth
        var qubitCapacity = Math.Max(1, (int)Math.Floor((telemetry.UsableBandwidthGigahertz * 1000.0) / 200.0));

        // Dynamic range between noise floor (-140 dBm) and 1-dB compression
        var dynamicRange = Math.Max(0.0, telemetry.InputOneDbCompressionPowerDbm - (-140.0));

        string regime;
        string guidance;
        bool isQuantumLimited = telemetry.AveragePowerGainDecibels >= 18.0 &&
                                telemetry.AddedNoisePhotons <= 1.2 &&
                                telemetry.GainRippleDecibels <= 3.0;

        if (telemetry.AddedNoisePhotons > 3.0)
        {
            regime = "EXCESS_THERMAL_OR_PUMP_PHASE_NOISE_HAZARD";
            guidance = $"Added noise ({telemetry.AddedNoisePhotons:F2} photons = {noiseRelativeToSql:F1}x SQL) significantly exceeds quantum limits. Inspect pump line attenuation and microwave packaging thermal anchoring.";
            isQuantumLimited = false;
        }
        else if (telemetry.AveragePowerGainDecibels < 15.0)
        {
            regime = "INSUFFICIENT_PARAMETRIC_GAIN_PUMP_STARVATION";
            guidance = $"Power gain ({telemetry.AveragePowerGainDecibels:F1} dB) is insufficient for single-shot qubit readout. Increase pump power (currently {telemetry.PumpPowerDbm:F1} dBm) or optimize phase matching frequency.";
            isQuantumLimited = false;
        }
        else if (telemetry.GainRippleDecibels > 4.5)
        {
            regime = "DISPERSION_MISMATCH_STRONG_PHASE_RIPPLE_WARNING";
            guidance = $"High gain ripple ({telemetry.GainRippleDecibels:F1} dB). Resonant phase-matching (RPM) stubs are mismatched with Josephson junction transmission line impedance; recalibrate pump detuning.";
            isQuantumLimited = false;
        }
        else if (telemetry.AveragePowerGainDecibels >= 20.0 && telemetry.AddedNoisePhotons <= 1.0 && telemetry.InputOneDbCompressionPowerDbm >= -105.0)
        {
            regime = "QUANTUM_LIMITED_BROADBAND_PARAMETRIC_AMPLIFICATION_REGIME";
            guidance = $"Near-quantum-limited performance confirmed ({telemetry.AveragePowerGainDecibels:F1} dB gain, n_add = {telemetry.AddedNoisePhotons:F2} photons). Capable of simultaneous multiplexed dispersive readout of {qubitCapacity} superconducting qubits.";
        }
        else
        {
            regime = "NOMINAL_PARAMETRIC_AMPLIFICATION_REGIME";
            guidance = $"TWPA operating with stable gain ({telemetry.AveragePowerGainDecibels:F1} dB) and dynamic range ({dynamicRange:F1} dB).";
        }

        return new SuperconductingTwpaAnalysisResult(
            telemetry.AmplifierId,
            Math.Round(noiseRelativeToSql, 2),
            qubitCapacity,
            Math.Round(dynamicRange, 1),
            regime,
            guidance,
            isQuantumLimited
        );
    }
}
