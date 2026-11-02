namespace SGrowing.Api.Modules.BioTech.MicrobialElectrosynthesis;

public class MicrobialElectrosynthesisEngine
{
    private const double FaradayConstant = 96485.33; // C/mol e-
    private const double AcetateMolarMass = 60.052;   // g/mol
    private const int ElectronsPerMoleAcetate = 8;

    public MicrobialElectrosynthesisAnalysisResult AnalyzeBiocathode(MicrobialElectrosynthesisTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.ReactorId))
        {
            throw new ArgumentException("Reactor ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.CathodeCurrentDensityAmpsPerM2 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Current density must be positive.");
        }

        if (telemetry.ElectrodeSurfaceAreaM2 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Electrode surface area must be positive.");
        }

        // Calculate Faradaic Efficiency for Acetate:
        // 2 CO2 + 8 H+ + 8 e- -> CH3COOH + 2 H2O
        // 12,853.6 Coulombs per gram of acetate
        // Standard laboratory specific electrode area a_s ~ 0.005 m^2 per Liter
        const double specificAreaM2PerLiter = 0.005;
        double actualCurrentPerLiter = telemetry.CathodeCurrentDensityAmpsPerM2 * specificAreaM2PerLiter;
        double faradaicCurrentPerLiter = (telemetry.AcetateProductionRateGramsPerLiterDay * 12853.6) / 86400.0;

        double faradaicEfficiency = Math.Clamp((faradaicCurrentPerLiter / Math.Max(actualCurrentPerLiter, 1e-6)) * 100.0, 0.0, 100.0);
        double abioticHydrogenLoss = Math.Clamp(telemetry.OffGasHydrogenFraction * 100.0, 0.0, 100.0);

        bool isBiofilmDetachedRisk = telemetry.CathodePotentialVoltsVsShe <= -0.80 || telemetry.BiofilmCoveragePercent < 65.0;

        string regime;
        string command;
        string summary;

        if (isBiofilmDetachedRisk)
        {
            regime = "BIOFILM_SHEAR_DETACHMENT_ALERT";
            command = "EMERGENCY_POTENTIAL_RELAXATION";
            summary = $"BIOFILM DETACHMENT RISK: Cathode poised too cathodic ({telemetry.CathodePotentialVoltsVsShe:F2} V vs SHE) or biofilm coverage collapsed to {telemetry.BiofilmCoveragePercent:F1}%. Intense H2 micro-bubbling shearing {telemetry.BacterialStrain} biofilm. Relaxing potential anodically by 100 mV.";
        }
        else if (abioticHydrogenLoss >= 35.0 || faradaicEfficiency < 60.0)
        {
            regime = "HIGH_HYDROGEN_EVOLUTION_WASTE";
            command = "SHIFT_POTENTIAL_ANODIC_50MV";
            summary = $"SUBOPTIMAL CO2 REDUCTION: Faradaic efficiency dropped to {faradaicEfficiency:F1}% while abiotic H2 off-gas reached {abioticHydrogenLoss:F1}%. Shifting cathode potential anodically by 50 mV to favor direct extracellular electron transfer.";
        }
        else
        {
            regime = "OPTIMAL_ACETOGENIC_MES";
            command = "MAINTAIN_CATHODE_POISE";
            summary = $"OPTIMAL ELECTROMICROBIAL CONVERSION: High Faradaic efficiency ({faradaicEfficiency:F1}%) producing {telemetry.AcetateProductionRateGramsPerLiterDay:F2} g/L/day acetate with {telemetry.BiofilmCoveragePercent:F1}% {telemetry.BacterialStrain} coverage.";
        }

        return new MicrobialElectrosynthesisAnalysisResult(
            telemetry.ReactorId,
            telemetry.BacterialStrain,
            Math.Round(faradaicEfficiency, 1),
            Math.Round(abioticHydrogenLoss, 1),
            isBiofilmDetachedRisk,
            regime,
            command,
            summary);
    }
}
