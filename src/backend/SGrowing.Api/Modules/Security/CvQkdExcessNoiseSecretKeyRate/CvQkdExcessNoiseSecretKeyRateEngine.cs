using System;

namespace SGrowing.Api.Modules.Security.CvQkdExcessNoiseSecretKeyRate;

public interface ICvQkdExcessNoiseSecretKeyRateEngine
{
    CvQkdAnalysisResult AnalyzeQuantumChannel(CvQkdTelemetry telemetry);
}

public sealed class CvQkdExcessNoiseSecretKeyRateEngine : ICvQkdExcessNoiseSecretKeyRateEngine
{
    public CvQkdAnalysisResult AnalyzeQuantumChannel(CvQkdTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.FiberDistanceKilometers <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Fiber distance must be strictly positive.");
        }

        // Transmittance T = 10^(-alpha * L / 10)
        double totalLossDb = telemetry.FiberAttenuationDbPerKm * telemetry.FiberDistanceKilometers;
        double channelTransmittance = Math.Pow(10.0, -totalLossDb / 10.0);
        channelTransmittance = Math.Clamp(channelTransmittance, 1e-5, 1.0);

        double eta = Math.Clamp(telemetry.DetectorQuantumEfficiency, 0.1, 1.0);
        double va = Math.Max(0.5, telemetry.AliceModulationVarianceSnu);
        double vElec = Math.Max(0.001, telemetry.ElectronicNoiseVarianceSnu);

        // Excess noise extracted from total noise variance
        double expectedNominalNoise = 1.0 + (eta * channelTransmittance * va) + vElec;
        double excessNoiseSnu = (telemetry.MeasuredTotalNoiseVarianceSnu - expectedNominalNoise) / (eta * channelTransmittance);
        excessNoiseSnu = Math.Max(0.0, excessNoiseSnu);

        // Mutual Information I_AB = 0.5 * log2(V_B / V_B|A)
        double vb = (eta * channelTransmittance * (va + 1.0 + excessNoiseSnu)) + (1.0 - eta) + vElec;
        double vbGivenA = (eta * channelTransmittance * (1.0 + excessNoiseSnu)) + (1.0 - eta) + vElec;
        double mutualInformationBits = 0.5 * Math.Log2(vb / vbGivenA);

        // Holevo bound on Eve's information chi_BE
        // As excess noise crosses 0.015 SNU, Eve's bound exceeds beta * I_AB
        double beta = Math.Clamp(telemetry.ReconciliationEfficiencyBeta, 0.70, 0.99);
        double holevoBoundEveBits = (0.55 * Math.Log2(1.0 + (excessNoiseSnu / 0.010)) * (1.0 - channelTransmittance))
                                   + (25.0 * Math.Pow(excessNoiseSnu, 1.5));

        double secretKeyRateBitsPerPulse = (beta * mutualInformationBits) - holevoBoundEveBits;
        secretKeyRateBitsPerPulse = Math.Max(0.0, secretKeyRateBitsPerPulse);

        double pulseRateHz = Math.Max(1.0, telemetry.PulseRepetitionRateMhz) * 1e6;
        double throughputKbps = (secretKeyRateBitsPerPulse * pulseRateHz) / 1000.0;

        string status;
        string action;
        bool isSecure;

        if (excessNoiseSnu >= 0.015 || secretKeyRateBitsPerPulse <= 0.0001)
        {
            status = "EAVESDROPPING_TAMPER_SUSPECTED";
            action = "ABORT_QUANTUM_KEY_EXCHANGE_AND_REROUTE_FIBER_PATH";
            isSecure = false;
        }
        else if (excessNoiseSnu >= 0.008)
        {
            status = "PHASE_DRIFT_WARNING";
            action = "INCREASE_PILOT_TONE_HOMODYNE_PHASE_CORRECTION_CADENCE";
            isSecure = true;
        }
        else
        {
            status = "QUANTUM_CHANNEL_SECURE";
            action = "MAINTAIN_HIGH_RATE_CRYPTOGRAPHIC_KEY_DISTRIBUTION";
            isSecure = true;
        }

        return new CvQkdAnalysisResult(
            telemetry.FiberLinkId,
            Math.Round(channelTransmittance, 4),
            Math.Round(excessNoiseSnu, 4),
            Math.Round(mutualInformationBits, 4),
            Math.Round(holevoBoundEveBits, 4),
            Math.Round(secretKeyRateBitsPerPulse, 5),
            Math.Round(throughputKbps, 2),
            status,
            action,
            isSecure,
            DateTimeOffset.UtcNow);
    }
}
