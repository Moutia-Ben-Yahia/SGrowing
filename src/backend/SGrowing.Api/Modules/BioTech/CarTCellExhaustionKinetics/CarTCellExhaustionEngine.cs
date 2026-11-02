namespace SGrowing.Api.Modules.BioTech.CarTCellExhaustionKinetics;

public interface ICarTCellExhaustionEngine
{
    CarTExhaustionAssessment EvaluateExhaustion(CarTExhaustionEvaluationRequest request);
}

public class CarTCellExhaustionEngine : ICarTCellExhaustionEngine
{
    public CarTExhaustionAssessment EvaluateExhaustion(CarTExhaustionEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var lim = request.Limits;

        // 1. Multi-checkpoint Exhaustion Hazard Index (EHI):
        // Weighted composite of inhibitory receptors (PD-1, LAG-3, TIM-3) plus chronic antigen, offset by T_SCM stemness
        double checkpointBurden = (0.35 * tel.Pd1PositiveFraction) +
                                 (0.35 * tel.Lag3PositiveFraction) +
                                 (0.30 * tel.Tim3PositiveFraction);

        double exhaustionHazardIndex = checkpointBurden + (0.20 * tel.ChronicAntigenLoadIndex) - (0.35 * tel.StemCellMemoryFraction);
        exhaustionHazardIndex = Math.Clamp(exhaustionHazardIndex, 0.05, 0.98);

        // 2. In-vivo persistence half-life projection in days:
        // High T_SCM and low EHI correlate with multi-month clonal persistence
        double baseHalfLife = 28.0 * (tel.StemCellMemoryFraction / 0.25);
        double persistenceDays = baseHalfLife * Math.Max(0.1, 1.0 - (exhaustionHazardIndex * 0.75));
        persistenceDays = Math.Clamp(persistenceDays, 2.0, 120.0);

        bool terminalExhaustion = exhaustionHazardIndex >= lim.MaxAllowableTerminalExhaustionIndex ||
                                 tel.StemCellMemoryFraction < lim.MinAllowableStemCellMemoryFraction;

        string state;
        string directive;

        if (terminalExhaustion)
        {
            state = "TERMINAL_EXHAUSTION_DYSFUNCTION";
            directive = $"INTERVENE_IL7_IL15: Exhaustion hazard index ({exhaustionHazardIndex:F2}) breaches safety threshold. High co-expression of LAG-3/TIM-3 and collapsed stem-cell memory pool. Transition patient protocol to low-dose IL-7/IL-15 support and consider CRISPR-Cas9 disruption of TOX/NR4A master regulators.";
        }
        else if (exhaustionHazardIndex >= 0.35)
        {
            state = "EARLY_TRANSITIONAL_EXHAUSTION";
            directive = $"MONITOR_CYTOKINES: Moderate inhibitory receptor elevation ({checkpointBurden:F2}). IL-2 production ({tel.Il2SecretionPicogramsPerMl:F0} pg/mL) intact. Track clonal expansion kinetics.";
        }
        else
        {
            state = "POTENT_PERSISTENT_MEMORY";
            directive = $"MAINTAIN_SURVEILLANCE: T_SCM stemness ({tel.StemCellMemoryFraction * 100:F1}%) and cytolytic vigor optimal. Estimated in-vivo persistence ({persistenceDays:F0} days) provides durable anti-tumor surveillance.";
        }

        return new CarTExhaustionAssessment(
            BatchId: tel.BatchId,
            ExhaustionHazardIndex: Math.Round(exhaustionHazardIndex, 3),
            EstimatedPersistenceHalfLifeDays: Math.Round(persistenceDays, 1),
            TerminalExhaustionRiskDetected: terminalExhaustion,
            ImmunologicalPhenotypeState: state,
            RecommendedCytokineInterventionDirective: directive
        );
    }
}
