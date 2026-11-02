using System;

namespace SGrowing.Api.Modules.BioTech.CrisprCas12aCollateralCleavage;

public interface ICrisprCas12aCollateralCleavageEngine
{
    Cas12aAssayResult AnalyzeAssayDiagnostics(Cas12aAssayTelemetry telemetry);
}

public sealed class CrisprCas12aCollateralCleavageEngine : ICrisprCas12aCollateralCleavageEngine
{
    private const double BaseKcatPerSec = 1250.0;
    private const double KmReporterMicromolar = 1.5;

    public Cas12aAssayResult AnalyzeAssayDiagnostics(Cas12aAssayTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        double darkNoise = Math.Max(1.0, telemetry.DarkNoiseStandardDeviationRfu);
        double deltaFluorescence = Math.Max(0.0, telemetry.MeasuredFluorescenceRfu - telemetry.BaselineBackgroundRfu);
        double snr = deltaFluorescence / darkNoise;

        // Active fraction based on Kd (assuming 10 nM typical reaction target RNP concentration)
        double activeFraction = 10.0 / (10.0 + Math.Max(0.1, telemetry.TargetBindingAffinityKdNanomolar));

        // Mismatch penalties
        double seedFactor = Math.Pow(0.08, Math.Max(0, telemetry.SeedRegionMismatchCount));
        int nonSeedMismatches = Math.Max(0, telemetry.TotalMismatchCount - telemetry.SeedRegionMismatchCount);
        double nonSeedFactor = Math.Pow(0.65, nonSeedMismatches);
        double overallMismatchFactor = seedFactor * nonSeedFactor;

        double effectiveKcat = BaseKcatPerSec * overallMismatchFactor;
        double substrate = Math.Max(0.01, telemetry.ReporterProbeConcentrationMicromolar);

        // Michaelis-Menten trans-cleavage velocity (RFU generation equivalent)
        double transCleavageVelocity = (effectiveKcat * activeFraction * substrate) / (KmReporterMicromolar + substrate);

        // Time to positive calculation (target threshold SNR = 3.0)
        double targetPositiveDeltaF = 3.0 * darkNoise;
        double estimatedTimeToPositiveMinutes;

        if (snr >= 3.0)
        {
            estimatedTimeToPositiveMinutes = telemetry.ElapsedReactionMinutes;
        }
        else if (telemetry.FluorescenceSlopeRfuPerMinute > 0.1)
        {
            double remainingRfu = targetPositiveDeltaF - deltaFluorescence;
            estimatedTimeToPositiveMinutes = telemetry.ElapsedReactionMinutes + (remainingRfu / telemetry.FluorescenceSlopeRfuPerMinute);
        }
        else
        {
            estimatedTimeToPositiveMinutes = 999.0;
        }

        string verdict;
        string action;
        bool isConfirmed;

        if (snr >= 5.0 && telemetry.SeedRegionMismatchCount == 0 && telemetry.FluorescenceSlopeRfuPerMinute >= 15.0)
        {
            verdict = "POSITIVE_PATHOGEN_DETECTED";
            action = "CONFIRM_VIRAL_INFECTION_AND_DISPATCH_HEALTHCARE_ALERT";
            isConfirmed = true;
        }
        else if ((snr >= 2.5 && telemetry.SeedRegionMismatchCount >= 1) || (telemetry.TotalMismatchCount >= 2 && snr >= 2.5))
        {
            verdict = "SUSPECT_OFF_TARGET_INTERFERENCE";
            action = "FLAG_PROBABLE_OFF_TARGET_CROSS_REACTIVITY";
            isConfirmed = false;
        }
        else if (snr < 2.0)
        {
            verdict = "NEGATIVE_SAMPLE";
            action = "MARK_SAMPLE_NEGATIVE_AND_LOG_QUALITY_METRICS";
            isConfirmed = false;
        }
        else
        {
            verdict = "INDETERMINATE_INCONCLUSIVE";
            action = "EXTEND_INCUBATION_AND_REPEAT_FLUORESCENCE_READ";
            isConfirmed = false;
        }

        return new Cas12aAssayResult(
            telemetry.AssayId,
            telemetry.TargetViralPathogenId,
            Math.Round(snr, 2),
            Math.Round(transCleavageVelocity, 2),
            Math.Round(estimatedTimeToPositiveMinutes, 1),
            verdict,
            action,
            isConfirmed,
            DateTimeOffset.UtcNow);
    }
}
