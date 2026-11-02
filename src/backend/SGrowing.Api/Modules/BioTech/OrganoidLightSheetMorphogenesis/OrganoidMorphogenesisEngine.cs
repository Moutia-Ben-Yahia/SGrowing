namespace SGrowing.Api.Modules.BioTech.OrganoidLightSheetMorphogenesis;

public interface IOrganoidMorphogenesisEngine
{
    OrganoidMorphogenesisEvaluation AnalyzeMorphogenesis(OrganoidMorphogenesisTelemetry telemetry);
}

public class OrganoidMorphogenesisEngine : IOrganoidMorphogenesisEngine
{
    public OrganoidMorphogenesisEvaluation AnalyzeMorphogenesis(OrganoidMorphogenesisTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Necrotic core fraction = r_core / R_organoid
        double rTotal = Math.Max(1.0, telemetry.OrganoidRadiusMicrons);
        double necroticFraction = telemetry.NecroticCoreRadiusMicrons / rTotal;

        // Morphological integrity score (0 - 10)
        double integrityScore = (telemetry.SphericityIndex * 5.0) + (Math.Min(25, telemetry.TerminalBudMorphogenesisCount) / 25.0 * 5.0);
        integrityScore = Math.Clamp(integrityScore, 0.0, 10.0);

        bool isNecrosisBounded = necroticFraction <= 0.18;
        bool isBranchingActive = telemetry.FractalBranchingDimension >= 1.40 && telemetry.TerminalBudMorphogenesisCount >= 10;
        bool isPhototoxicityOk = telemetry.LightSheetPhotobleachingApoptosisPercent <= 3.5;

        if (necroticFraction > 0.20)
        {
            regimes.Add("ORGANOID_HYPOXIC_NECROTIC_CORE_EXPANSION");
            mitigations.Add("Increase microfluidic perfusion flow rate or transfer organoids to spinning bioreactors with elevated dissolved oxygen tension.");
        }

        if (!isBranchingActive)
        {
            regimes.Add("BRANCHING_MORPHOGENESIS_ARREST");
            mitigations.Add("Supplement differentiation medium with pulsed Wnt-agonist (CHIR99021) and FGF10 to reactivate epithelial branching dynamics.");
        }

        if (!isPhototoxicityOk)
        {
            regimes.Add("LIGHT_SHEET_PHOTOTOXICITY_BLEACHING_SURGE");
            mitigations.Add("Decrease light-sheet cylindrical laser power and increase sCMOS camera exposure time with deconvolution post-processing.");
        }

        bool isCertified = isNecrosisBounded && isBranchingActive && isPhototoxicityOk &&
                           telemetry.DeepTissueCellViabilityPercent >= 85.0 &&
                           telemetry.SphericityIndex >= 0.82;

        return new OrganoidMorphogenesisEvaluation
        {
            OrganoidCultureBatchId = telemetry.OrganoidCultureBatchId,
            NecroticCoreFractionRatio = Math.Round(necroticFraction, 3),
            MorphologicalIntegrityScore = Math.Round(integrityScore, 2),
            IsNecroticCoreHypoxiaBounded = isNecrosisBounded,
            IsBranchingMorphogenesisActive = isBranchingActive,
            IsPhototoxicityControlled = isPhototoxicityOk,
            IsOrganoidBatchQualityCertified = isCertified,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
