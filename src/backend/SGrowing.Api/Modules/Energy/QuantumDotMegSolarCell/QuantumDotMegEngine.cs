namespace SGrowing.Api.Modules.Energy.QuantumDotMegSolarCell;

public interface IQuantumDotMegEngine
{
    QuantumDotMegAssessment EvaluateMegPerformance(QuantumDotMegEvaluationRequest request);
}

public class QuantumDotMegEngine : IQuantumDotMegEngine
{
    public QuantumDotMegAssessment EvaluateMegPerformance(QuantumDotMegEvaluationRequest request)
    {
        var p = request.Parameters;
        var thresh = request.Thresholds;

        // 1. Ratio of incident photon energy to quantum dot bandgap:
        double energyRatio = p.IncidentPhotonEnergyElectronVolts / Math.Max(0.2, p.BandgapEnergyElectronVolts);
        bool megActive = energyRatio >= thresh.MegEnergyThresholdMultiplier;

        // 2. Carrier multiplication quantum yield:
        // Above 2.5 * Eg, each additional bandgap chunk can generate a secondary exciton
        double quantumYield;
        if (megActive)
        {
            double excessEnergyChunks = energyRatio - 2.0;
            double megEfficiencyFactor = 0.42; // Characteristic slope for colloidal PbSe/PbS QDs
            quantumYield = 1.0 + Math.Clamp(excessEnergyChunks * megEfficiencyFactor, 0.05, 1.80);
        }
        else
        {
            quantumYield = 1.0;
        }

        // 3. Auger recombination penalty:
        // Multiple excitons confined in tiny ~4nm dot recombine rapidly if not extracted
        double augerLifetimePs = Math.Max(5.0, p.AugerRecombinationRatePicoseconds);
        double augerPenaltyPercent = megActive ? Math.Clamp(85.0 / augerLifetimePs, 0.5, 12.0) : 0.0;

        // 4. Power Conversion Efficiency (PCE):
        // Base Shockley-Queisser single junction baseline scaled by MEG yield and collection
        double basePcePercent = 27.5;
        double effectiveYieldMultiplier = (quantumYield * p.CarrierCollectionEfficiency) * (1.0 - (augerPenaltyPercent / 100.0));
        double finalPcePercent = Math.Clamp(basePcePercent * effectiveYieldMultiplier, 12.0, 44.5);

        string regime;
        string directive;

        if (quantumYield >= 1.25)
        {
            regime = "MULTIPLE_EXCITON_ACTIVE_HIGH_YIELD";
            directive = $"BYPASS_SHOCKLEY_QUEISSER: MEG confirmed with {quantumYield * 100:F0}% quantum yield ({finalPcePercent:F1}% PCE). Maintain ultrafast carrier extraction layers to outpace {augerLifetimePs:F0} ps Auger recombination.";
        }
        else if (megActive)
        {
            regime = "MARGINAL_MEG_ONSET";
            directive = $"TUNE_DOT_DIAMETER: Photon energy ({p.IncidentPhotonEnergyElectronVolts:F2} eV) is near MEG threshold ({thresh.MegEnergyThresholdMultiplier * p.BandgapEnergyElectronVolts:F2} eV). Reduce QD diameter to ~3.5 nm to shift quantum confinement bandgap.";
        }
        else
        {
            regime = "STANDARD_SINGLE_EXCITON";
            directive = "SUB_MEG_REGIME: Incident photon energy below 2.5x Eg threshold. Cell operates in standard single-exciton thermodynamic regime.";
        }

        return new QuantumDotMegAssessment(
            CellId: p.CellId,
            MegThresholdExceeded: megActive,
            CarrierMultiplicationQuantumYield: Math.Round(quantumYield, 3),
            PowerConversionEfficiencyPercent: Math.Round(finalPcePercent, 2),
            AugerRecombinationPenaltyPercent: Math.Round(augerPenaltyPercent, 2),
            SolarCellEfficiencyRegime: regime,
            OptimizationDirective: directive
        );
    }
}
