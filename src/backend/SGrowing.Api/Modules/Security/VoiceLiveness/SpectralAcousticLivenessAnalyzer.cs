namespace SGrowing.Api.Modules.Security.VoiceLiveness;

/// <summary>
/// Digital signal processing analyzer extracting acoustic biomarkers, Harmonic-to-Noise Ratio (HNR),
/// spectral flux transitions, and vocoder artifacts to detect synthetic voice deepfakes.
/// </summary>
public sealed class SpectralAcousticLivenessAnalyzer
{
    private const int FrameSize = 512;
    private const int HopSize = 256;

    /// <summary>
    /// Analyzes raw PCM audio samples to extract spectral and biometric features.
    /// </summary>
    public AudioSpectralFeatures AnalyzeAudio(float[] samples, int sampleRateHz)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (sampleRateHz <= 0)
        {
            throw new ArgumentException("Sample rate must be positive.", nameof(sampleRateHz));
        }

        var durationSec = (double)samples.Length / sampleRateHz;

        if (samples.Length < FrameSize)
        {
            return new AudioSpectralFeatures(
                SampleRateHz: sampleRateHz,
                DurationSeconds: Math.Round(durationSec, 3),
                SpectralFluxMean: 0.0,
                HarmonicToNoiseRatioDb: 0.0,
                HighFrequencyRollOffHz: 0.0,
                PhaseDiscontinuityCount: 0,
                SyntheticVocoderArtifactScore: 0.50
            );
        }

        // 1. Compute Harmonic-to-Noise Ratio (HNR) via auto-correlation
        var hnrDb = CalculateHarmonicToNoiseRatio(samples, sampleRateHz);

        // 2. Compute Spectral Flux across STFT magnitude frames
        var (spectralFlux, highFreqRollOff) = CalculateSpectralMetrics(samples, sampleRateHz);

        // 3. Count abrupt phase discontinuities / splicing clicks
        var phaseDiscontinuities = DetectPhaseDiscontinuities(samples);

        // 4. Calculate Synthetic Vocoder Artifact Score [0.0 = live, 1.0 = synthetic]
        // Neural TTS models typically exhibit:
        // - Abnormally low spectral flux (mel-diffusion over-smoothing)
        // - Low HNR (< 10 dB) or unnaturally rigid pitch periodicity
        // - Sudden artificial cutoff below Nyquist
        var fluxAnomaly = spectralFlux < 0.15 ? 0.35 : 0.05;
        var hnrAnomaly = hnrDb < 10.0 ? 0.35 : (hnrDb > 35.0 ? 0.20 : 0.05);
        var phaseAnomaly = phaseDiscontinuities > 5 ? 0.30 : 0.05;

        var vocoderScore = Math.Clamp(fluxAnomaly + hnrAnomaly + phaseAnomaly, 0.0, 1.0);

        return new AudioSpectralFeatures(
            SampleRateHz: sampleRateHz,
            DurationSeconds: Math.Round(durationSec, 3),
            SpectralFluxMean: Math.Round(spectralFlux, 4),
            HarmonicToNoiseRatioDb: Math.Round(hnrDb, 2),
            HighFrequencyRollOffHz: Math.Round(highFreqRollOff, 1),
            PhaseDiscontinuityCount: phaseDiscontinuities,
            SyntheticVocoderArtifactScore: Math.Round(vocoderScore, 4)
        );
    }

    private static double CalculateHarmonicToNoiseRatio(float[] samples, int sampleRateHz)
    {
        // Pitch period bounds for human voice (60 Hz to 500 Hz)
        var minLag = (int)(sampleRateHz / 500.0);
        var maxLag = (int)(sampleRateHz / 60.0);

        if (samples.Length <= maxLag)
        {
            return 15.0; // Default nominal
        }

        var len = Math.Min(samples.Length - maxLag, 2048);

        // Zero-lag autocorrelation R(0)
        double r0 = 0.0;
        for (var i = 0; i < len; i++)
        {
            r0 += samples[i] * samples[i];
        }

        if (r0 <= 0.00001)
        {
            return 0.0;
        }

        // Search for peak autocorrelation in human pitch range
        double maxR = 0.0;
        for (var lag = minLag; lag < maxLag; lag++)
        {
            double rLag = 0.0;
            for (var i = 0; i < len; i++)
            {
                rLag += samples[i] * samples[i + lag];
            }

            if (rLag > maxR)
            {
                maxR = rLag;
            }
        }

        var harmonicRatio = maxR / r0;
        if (harmonicRatio >= 0.9999) return 35.0;
        if (harmonicRatio <= 0.0001) return 0.0;

        // HNR = 10 * log10(R_max / (R_0 - R_max))
        var noise = r0 - maxR;
        if (noise <= 0.00001) return 35.0;

        var hnr = 10.0 * Math.Log10(maxR / noise);
        return Math.Clamp(hnr, 0.0, 40.0);
    }

    private static (double SpectralFlux, double HighFreqRollOff) CalculateSpectralMetrics(float[] samples, int sampleRateHz)
    {
        var numFrames = (samples.Length - FrameSize) / HopSize;
        if (numFrames < 2)
        {
            return (0.35, sampleRateHz / 2.0);
        }

        var prevMagnitudes = new double[FrameSize / 2];
        var totalFlux = 0.0;
        var rollOffAccumulator = 0.0;

        for (var frame = 0; frame < numFrames; frame++)
        {
            var offset = frame * HopSize;
            var currMagnitudes = new double[FrameSize / 2];

            // Simplified Discrete Cosine / Spectral Magnitude transform
            var totalEnergy = 0.0;
            for (var k = 0; k < FrameSize / 2; k++)
            {
                double sum = 0.0;
                for (var n = 0; n < FrameSize; n += 4)
                {
                    sum += samples[offset + n] * Math.Cos((Math.PI / FrameSize) * (n + 0.5) * k);
                }
                var mag = Math.Abs(sum);
                currMagnitudes[k] = mag;
                totalEnergy += mag * mag;
            }

            if (frame > 0)
            {
                double frameFlux = 0.0;
                for (var k = 0; k < FrameSize / 2; k++)
                {
                    var diff = currMagnitudes[k] - prevMagnitudes[k];
                    if (diff > 0)
                    {
                        frameFlux += diff * diff;
                    }
                }
                totalFlux += Math.Sqrt(frameFlux);
            }

            // High frequency roll-off: frequency below which 85% of energy resides
            var targetEnergy = totalEnergy * 0.85;
            double cumEnergy = 0.0;
            var rollOffBin = FrameSize / 4;
            for (var k = 0; k < FrameSize / 2; k++)
            {
                cumEnergy += currMagnitudes[k] * currMagnitudes[k];
                if (cumEnergy >= targetEnergy)
                {
                    rollOffBin = k;
                    break;
                }
            }

            var freqHz = ((double)rollOffBin / (FrameSize / 2.0)) * (sampleRateHz / 2.0);
            rollOffAccumulator += freqHz;

            Array.Copy(currMagnitudes, prevMagnitudes, currMagnitudes.Length);
        }

        var avgFlux = totalFlux / (numFrames - 1);
        var avgRollOff = rollOffAccumulator / numFrames;

        return (avgFlux, avgRollOff);
    }

    private static int DetectPhaseDiscontinuities(float[] samples)
    {
        var count = 0;
        for (var i = 1; i < samples.Length - 1; i++)
        {
            var delta = Math.Abs(samples[i] - samples[i - 1]);
            // Jump of > 0.80 full-scale within 1 sample is an unnatural splice or phase slip
            if (delta > 0.80f)
            {
                count++;
            }
        }
        return count;
    }
}
