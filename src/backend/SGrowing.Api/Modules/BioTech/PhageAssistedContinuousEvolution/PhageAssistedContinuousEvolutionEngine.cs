using System;

namespace SGrowing.Api.Modules.BioTech.PhageAssistedContinuousEvolution;

public sealed class PhageAssistedContinuousEvolutionEngine : IPhageAssistedContinuousEvolutionEngine
{
    public PaceAnalysisResult AnalyzeContinuousEvolution(PaceTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var netGrowth = telemetry.PhageReplicationRatePerHour - telemetry.DilutionRatePerHour;

        var inducerConc = Math.Max(0.0, telemetry.MutagenesisInducerConcentrationMicroMolar);
        var mutationsPerKb = 0.05 + (inducerConc / 50.0) * 0.65;

        var stringency = Math.Clamp(telemetry.SelectionStringencyScore, 0.1, 10.0);
        var growthFactor = Math.Max(0.1, 1.0 + 0.25 * Math.Clamp(netGrowth, -1.0, 2.0));
        var velocity = stringency * mutationsPerKb * growthFactor;

        string regime;
        string guidance;
        bool isOptimal;

        if (telemetry.DilutionRatePerHour > telemetry.PhageReplicationRatePerHour)
        {
            regime = "LAGOON_WASHOUT_EXTINCTION_RISK";
            guidance = "Lagoon dilution rate exceeds phage replication kinetics. Throttling influent peristaltic pump to prevent viral population washout.";
            isOptimal = false;
        }
        else if (inducerConc > 250.0 || telemetry.HostCellSurvivalRatePercent < 60.0)
        {
            regime = "HOST_MUTAGENIC_BURDEN_TOXICITY";
            guidance = "Mutagenesis plasmid (MP6) overexpression causing fatal host cell genome degradation. Reduce arabinose/doxycycline inducer dosing.";
            isOptimal = false;
        }
        else if (telemetry.PhageTiterPfuPerMilliliter < 1e5)
        {
            regime = "EXCESSIVE_SELECTION_STRINGENCY_COLLAPSE";
            guidance = "Phage titer dropped below critical viable pool (10^5 PFU/mL). Lower selection circuit promoter stringency or inject ancestral viral stock.";
            isOptimal = false;
        }
        else
        {
            regime = "CONTINUOUS_STEADY_STATE_DIRECTED_EVOLUTION";
            guidance = "PACE lagoon operating in steady-state evolutionary trajectory. High mutagenesis coupled to robust gene III selection drive.";
            isOptimal = true;
        }

        return new PaceAnalysisResult(
            LagoonId: telemetry.LagoonId,
            NetPopulationGrowthPerHour: netGrowth,
            EstimatedMutationsPerKilobase: mutationsPerKb,
            EvolutionaryAdaptationVelocityScore: velocity,
            EvolutionRegime: regime,
            BioreactorActionGuidance: guidance,
            IsEvolutionOptimal: isOptimal
        );
    }
}
