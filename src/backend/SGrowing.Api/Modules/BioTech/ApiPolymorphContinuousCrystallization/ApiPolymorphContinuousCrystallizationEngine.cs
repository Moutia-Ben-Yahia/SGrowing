using System;

namespace SGrowing.Api.Modules.BioTech.ApiPolymorphContinuousCrystallization;

public interface IApiPolymorphContinuousCrystallizationEngine
{
    ApiCrystallizerAnalysisResult AnalyzeCrystallizationState(ApiCrystallizerTelemetry telemetry);
}

public sealed class ApiPolymorphContinuousCrystallizationEngine : IApiPolymorphContinuousCrystallizationEngine
{
    public ApiCrystallizerAnalysisResult AnalyzeCrystallizationState(ApiCrystallizerTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.EquilibriumFormISolubilityGramsPerLiter <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Solubility must be strictly positive.");
        }

        double supersaturationRatioS = telemetry.SolutionConcentrationGramsPerLiter / telemetry.EquilibriumFormISolubilityGramsPerLiter;

        double formIPurityPercent = Math.Clamp(100.0 - Math.Max(0.0, telemetry.MeasuredMetastableFormIiMassFractionPercent), 0.0, 100.0);

        // Ostwald transformation rate: Rate = 0.25 * (S - 1.0) * (Form_II / 10.0) (%/min)
        double drivingForce = Math.Max(0.0, supersaturationRatioS - 1.0);
        double transformationRate = 0.25 * drivingForce * (Math.Max(0.0, telemetry.MeasuredMetastableFormIiMassFractionPercent) / 10.0);

        string regime;
        string recommendation;
        bool isCompliant;

        if (telemetry.FilterDifferentialPressureBar > 1.5 || telemetry.ImpellerTorqueSurgePercent > 25.0)
        {
            regime = "CRYSTALLIZER_FOULING_ENCRUSTATION";
            recommendation = "INITIATE_SOLVENT_FLUSH_AND_REDUCE_COOLING_JACKET_RATE";
            isCompliant = false;
        }
        else if (telemetry.MeasuredMetastableFormIiMassFractionPercent > 5.0 || supersaturationRatioS > 1.80)
        {
            regime = "UNDESIRED_POLYMORPH_METASTABLE_SURGE";
            recommendation = "PULSE_ULTRASONIC_SEEDING_AND_INCREASE_SLURRY_RESIDENCE_TIME";
            isCompliant = false;
        }
        else if (formIPurityPercent >= 98.0 && supersaturationRatioS >= 1.10 && supersaturationRatioS <= 1.65)
        {
            regime = "FORM_I_STEADY_STATE_HIGH_PURITY";
            recommendation = "MAINTAIN_CONTINUOUS_MSMPR_WITHDRAWAL_CADENCE";
            isCompliant = true;
        }
        else
        {
            regime = "LOW_SUPERSATURATION_SLOW_GROWTH";
            recommendation = "RAMP_UP_EVAPORATION_OR_REDUCE_TEMPERATURE";
            isCompliant = true;
        }

        return new ApiCrystallizerAnalysisResult(
            telemetry.CrystallizerBatchId,
            Math.Round(supersaturationRatioS, 3),
            Math.Round(formIPurityPercent, 2),
            Math.Round(transformationRate, 4),
            regime,
            recommendation,
            isCompliant,
            DateTimeOffset.UtcNow);
    }
}
