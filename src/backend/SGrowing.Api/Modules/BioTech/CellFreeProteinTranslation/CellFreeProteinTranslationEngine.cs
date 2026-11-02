using System;

namespace SGrowing.Api.Modules.BioTech.CellFreeProteinTranslation;

public interface ICellFreeProteinTranslationEngine
{
    CellFreeBioreactorAnalysisResult AnalyzeBioreactorKinetics(CellFreeBioreactorTelemetry telemetry);
}

public sealed class CellFreeProteinTranslationEngine : ICellFreeProteinTranslationEngine
{
    private const double MaxElongationRateAaPerSec = 2.5;
    private const double KmAtp = 0.3;
    private const double KmGtp = 0.2;
    private const double KmAa = 0.1;

    public CellFreeBioreactorAnalysisResult AnalyzeBioreactorKinetics(CellFreeBioreactorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.ActiveRibosomeConcentrationMicromolar <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Ribosome concentration must be strictly positive.");
        }

        // Substrate saturations
        double satAtp = Math.Max(0.0, telemetry.AtpConcentrationMillimolar) / (KmAtp + Math.Max(0.0, telemetry.AtpConcentrationMillimolar));
        double satGtp = Math.Max(0.0, telemetry.GtpConcentrationMillimolar) / (KmGtp + Math.Max(0.0, telemetry.GtpConcentrationMillimolar));
        double satAa = Math.Max(0.0, telemetry.AminoAcidsAverageConcentrationMillimolar) / (KmAa + Math.Max(0.0, telemetry.AminoAcidsAverageConcentrationMillimolar));

        // Inorganic phosphate inhibition
        double piInhibition = 1.0;
        if (telemetry.InorganicPhosphateConcentrationMillimolar > 20.0)
        {
            piInhibition = Math.Max(0.05, 1.0 - ((telemetry.InorganicPhosphateConcentrationMillimolar - 20.0) / 35.0));
        }

        // Temperature bell-curve (optimum around 37 degC)
        double tempDelta = telemetry.ReactionTemperatureCelsius - 37.0;
        double tempFactor = Math.Exp(-0.5 * Math.Pow(tempDelta / 5.0, 2.0));

        double elongationRateAaPerSec = MaxElongationRateAaPerSec * satAtp * satGtp * satAa * piInhibition * tempFactor;
        elongationRateAaPerSec = Math.Max(0.01, elongationRateAaPerSec);

        // Synthesis rate: ug/mL/hr = [Ribosomes]_uM * v_elongation * 110 g/mol * 3.6
        double instantaneousYieldPerHour = telemetry.ActiveRibosomeConcentrationMicromolar * elongationRateAaPerSec * 110.0 * 3.6;
        double cumulativeYield = instantaneousYieldPerHour * Math.Max(0.1, telemetry.ElapsedSynthesisHours) * 0.85;

        string regime;
        string recommendation;
        bool isHealthy;

        if (telemetry.InorganicPhosphateConcentrationMillimolar >= 45.0 || elongationRateAaPerSec < 0.50)
        {
            regime = "PHOSPHATE_POISONING_IMMINENT";
            recommendation = "TRIGGER_DIALYSIS_CONTINUOUS_EXCHANGE_BUFFER_PURGE";
            isHealthy = false;
        }
        else if (telemetry.AtpConcentrationMillimolar < 0.50 || telemetry.SecondaryEnergySubstrateConcentrationMillimolar < 5.0)
        {
            regime = "SUBSTRATE_DEPLETION_WARNING";
            recommendation = "FEED_CONCENTRATED_NUCLEOTIDE_AND_AMINO_ACID_COCKTAIL";
            isHealthy = false;
        }
        else
        {
            regime = "TX_TL_STEADY_STATE_OPTIMAL";
            recommendation = "MAINTAIN_STEADY_STATE_INCUBATION_AT_37_CELSIUS";
            isHealthy = true;
        }

        return new CellFreeBioreactorAnalysisResult(
            telemetry.BioreactorRunId,
            telemetry.TargetProteinName,
            Math.Round(elongationRateAaPerSec, 3),
            Math.Round(instantaneousYieldPerHour, 1),
            Math.Round(cumulativeYield, 1),
            regime,
            recommendation,
            isHealthy,
            DateTimeOffset.UtcNow);
    }
}
