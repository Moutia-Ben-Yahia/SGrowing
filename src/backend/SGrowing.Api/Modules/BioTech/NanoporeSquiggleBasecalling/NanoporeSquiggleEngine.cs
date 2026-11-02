namespace SGrowing.Api.Modules.BioTech.NanoporeSquiggleBasecalling;

public interface INanoporeSquiggleEngine
{
    NanoporeSquiggleEvaluation AnalyzeSquiggle(NanoporeSquiggleTelemetry telemetry);
}

public class NanoporeSquiggleEngine : INanoporeSquiggleEngine
{
    public NanoporeSquiggleEvaluation AnalyzeSquiggle(NanoporeSquiggleTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Accuracy from Phred Q: P_error = 10^(-Q/10), Accuracy = 1 - P_error
        double pError = Math.Pow(10.0, -telemetry.MeanPhredQualityScore / 10.0);
        double basecallingAccuracyPercent = Math.Clamp((1.0 - pError) * 100.0, 50.0, 99.99);

        // Processivity score (0.0 to 10.0)
        double processivity = 10.0;
        if (telemetry.HelicaseDwellTimeVarianceRatio > 1.3)
        {
            processivity -= (telemetry.HelicaseDwellTimeVarianceRatio - 1.3) * 8.0;
        }
        if (telemetry.MeanTranslocationVelocityBps < 350.0)
        {
            processivity -= (350.0 - telemetry.MeanTranslocationVelocityBps) * 0.04;
        }
        processivity = Math.Clamp(processivity, 0.5, 10.0);

        bool isBaselineStable = Math.Abs(telemetry.CurrentBaselineDriftPicoAmpsPerMin) <= 2.5 &&
                                telemetry.OpenPoreBaselineCurrentPicoAmps >= 80.0 &&
                                telemetry.OpenPoreBaselineCurrentPicoAmps <= 140.0;

        bool isHelicaseRegular = telemetry.HelicaseDwellTimeVarianceRatio <= 1.5 &&
                                 telemetry.MeanTranslocationVelocityBps >= 320.0 &&
                                 telemetry.MeanTranslocationVelocityBps <= 520.0;

        bool isEpigeneticConfident = telemetry.Epigenetic5mCPredictionConfidencePercent >= 88.0 &&
                                     telemetry.RawSignalSignalToNoiseRatio >= 10.0;

        if (!isBaselineStable)
        {
            regimes.Add("CURRENT_BASELINE_DRIFT_SATURATION");
            mitigations.Add("Execute periodic unblock voltage reversal and replenish electrolyte buffer to stabilize open pore ionic baseline.");
        }

        if (!isHelicaseRegular)
        {
            regimes.Add("HELICASE_STALLING_OR_BACKSTEPPING");
            mitigations.Add("Replenish ATP and cofactor concentrations in sequencing flow cell to ensure uniform enzymatic stepping without back-slips.");
        }

        if (telemetry.MeanPhredQualityScore < 17.0 || telemetry.HomopolymerCompressionArtifactRatePercent > 3.0)
        {
            regimes.Add("BASECALLING_CONFIDENCE_COLLAPSE");
            mitigations.Add("Deploy Transformer-based CTC neural basecaller trained on dual-constriction R10.4.1 squiggles to resolve homopolymer repeats.");
        }

        bool isHighAccuracyReady = isBaselineStable && isHelicaseRegular && isEpigeneticConfident && basecallingAccuracyPercent >= 98.0;

        return new NanoporeSquiggleEvaluation
        {
            ChannelPoreId = telemetry.ChannelPoreId,
            CalculatedBasecallingAccuracyPercent = Math.Round(basecallingAccuracyPercent, 2),
            EstimatedHelicaseProcessivityScore = Math.Round(processivity, 2),
            IsCurrentBaselineStable = isBaselineStable,
            IsHelicaseTranslocationRegular = isHelicaseRegular,
            IsEpigeneticCallingConfident = isEpigeneticConfident,
            IsHighAccuracyBasecallingReady = isHighAccuracyReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
