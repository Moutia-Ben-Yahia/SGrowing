using System;

namespace SGrowing.Api.Modules.BioTech.AcidMineDrainageBioleaching;

public sealed class AcidMineDrainageBioleachingEngine : IAcidMineDrainageBioleachingEngine
{
    public BioleachingAnalysisResult AnalyzeBioleachingProcess(BioleachingTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var cellDensity = Math.Max(1.0, telemetry.BacterialCellDensityCellsPerMl);
        var logDensity = Math.Log10(cellDensity);
        var ehRatio = Math.Max(0.1, telemetry.OxidationReductionPotentialMv / 700.0);

        // Arrhenius-like bell curve for mesophilic bacteria centered at 38 C
        var tempDiff = telemetry.TemperatureCelsius - 38.0;
        var tempEfficiency = Math.Max(0.05, Math.Exp(-0.5 * Math.Pow(tempDiff / 10.0, 2)));

        var baseKinetics = Math.Max(1.0, 18.0 * (logDensity - 6.0) * ehRatio * tempEfficiency);
        var leachingRate = Math.Clamp(baseKinetics, 0.5, 125.0);

        // Jarosite precipitation risk: high pH (>2.0), high Fe3+/Fe2+, high sulfate
        var phTerm = Math.Max(0.0, telemetry.SolutionPh - 1.8) * 0.7;
        var feRatioTerm = Math.Max(0.0, telemetry.FerricToFerrousRatio - 25.0) * 0.015;
        var sulfateTerm = Math.Max(0.0, telemetry.SulfateIonConcentrationGramsPerLiter - 45.0) * 0.012;
        var jarositeRisk = Math.Clamp(phTerm + feRatioTerm + sulfateTerm, 0.0, 1.0);

        // Cumulative extraction
        var netKinetics = leachingRate * (1.0 - 0.45 * jarositeRisk);
        var copperExtraction = Math.Clamp(40.0 + netKinetics * 0.55 + telemetry.FeedChalcopyriteGradePercent * 3.5, 5.0, 98.5);

        // AMD neutralization demand: kg lime (Ca(OH)2) per m3
        var acidityFactor = Math.Max(0.1, 3.0 - Math.Min(3.0, telemetry.SolutionPh)) * 4.5;
        var sulfateFactor = telemetry.SulfateIonConcentrationGramsPerLiter * 0.045;
        var limeDemand = acidityFactor + sulfateFactor;

        string regime;
        string recommendation;
        bool isOptimal;

        if (telemetry.DissolvedOxygenMilligramsPerLiter < 1.5)
        {
            regime = "ANOXIC_BACTERIAL_SUFFOCATION";
            recommendation = "Dissolved oxygen below critical microbial threshold (1.5 mg/L). Increase sparger aeration rate and impeller agitation speed.";
            isOptimal = false;
        }
        else if (telemetry.SolutionPh > 2.3 || jarositeRisk > 0.70)
        {
            regime = "JAROSITE_SURFACE_PASSIVATION_HAZARD";
            recommendation = "pH elevated above 2.3 or jarosite saturation threshold exceeded. Dose concentrated sulfuric acid (H2SO4) to prevent ore passivation.";
            isOptimal = false;
        }
        else if (telemetry.OxidationReductionPotentialMv < 550.0 || telemetry.FerricToFerrousRatio < 3.0)
        {
            regime = "SUBOPTIMAL_REDOX_POTENTIAL";
            recommendation = "Low redox potential (< 550 mV) indicates stagnant Fe2+ oxidation. Inoculate active Acidithiobacillus culture and augment nutrient salts.";
            isOptimal = false;
        }
        else if (telemetry.TemperatureCelsius > 52.0 || telemetry.TemperatureCelsius < 20.0)
        {
            regime = "THERMAL_MICROBIAL_INHIBITION";
            recommendation = "Bioreactor temperature outside mesophilic operating envelope (20-50 C). Modulate cooling/heating jacket circulation.";
            isOptimal = false;
        }
        else
        {
            regime = "STEADY_STATE_CHEMOLITHOAUTOTROPHIC_BIOLEACHING";
            recommendation = "Chemolithoautotrophic bio-leaching at peak catalytic efficiency. Ore dissolution and copper extraction proceeding optimally.";
            isOptimal = true;
        }

        return new BioleachingAnalysisResult(
            BioreactorId: telemetry.BioreactorId,
            BioleachingKineticsRateMgPerLiterPerHour: leachingRate,
            CumulativeCopperExtractionPercent: copperExtraction,
            JarositePrecipitationRiskScore: jarositeRisk,
            AcidMineDrainageNeutralizationDemandKgLimePerM3: limeDemand,
            BioleachingRegime: regime,
            ProcessControlRecommendation: recommendation,
            IsProcessOptimal: isOptimal
        );
    }
}
