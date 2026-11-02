namespace SGrowing.Api.Modules.BioTech.MetabolicFluxOptimizer;

public class MetabolicFluxOptimizerEngine
{
    public FedBatchOptimizationResult OptimizeFedBatch(FedBatchOptimizationRequest request)
    {
        var t = request.Telemetry;
        var p = request.Parameters ?? new MetabolicKineticParameters();

        double s = Math.Max(0.001, t.GlucoseConcentrationGPerL);
        double sInhibition = (s * s) / p.SubstrateInhibitionKi;

        // Monod-Haldane specific growth rate (mu, 1/hr)
        double mu = Math.Round(p.MaxSpecificGrowthRateMuMax * (s / (p.MonodConstantKs + s + sInhibition)), 4);

        // Optimal substrate feed rate (F = mu * X * V / (Y_xs * S_feed))
        double targetMu = Math.Min(mu, 0.20); // Safe setpoint to avoid Crabtree effect
        double requiredFeedRateLitersPerHour = Math.Round((targetMu * t.BiomassConcentrationGPerL * request.CurrentVesselVolumeLiters) / (p.YieldBiomassOnSubstrateYxs * request.FeedStockConcentrationGPerL), 3);

        // Oxygen enrichment demand
        double oxygenDeficit = Math.Max(0.0, p.TargetDissolvedOxygenPercent - t.DissolvedOxygenPercent);
        double oxygenEnrichment = Math.Round(oxygenDeficit * 1.5, 1);

        // Predicted biomass next hour: X(t+1) = X(t) * exp(mu * 1 hr)
        double predictedBiomass = Math.Round(t.BiomassConcentrationGPerL * Math.Exp(mu * 1.0), 2);

        string growthPhase;
        string warning;

        if (t.GlucoseConcentrationGPerL > 5.0)
        {
            growthPhase = "Substrate Overload";
            warning = "Crabtree Effect Alert: Elevated glucose triggers acetate by-product toxicity; cut feed rate.";
        }
        else if (t.DissolvedOxygenPercent < 15.0)
        {
            growthPhase = "Micro-Aerobic Strain";
            warning = "Anoxia Warning: dO2 < 15% induces anaerobic mixed-acid fermentation; ramp agitation.";
        }
        else if (t.GlucoseConcentrationGPerL < 0.05)
        {
            growthPhase = "Substrate Starvation";
            warning = "Carbon Starvation: Glucose depleted; initiate fed-batch feed ramp immediately.";
        }
        else
        {
            growthPhase = "Exponential Growth Phase";
            warning = "Metabolic Equilibrium: Balanced carbon uptake without overflow metabolites.";
        }

        return new FedBatchOptimizationResult(
            VesselId: t.VesselId,
            CurrentGrowthRateMu: mu,
            OptimalFeedRateLitersPerHour: requiredFeedRateLitersPerHour,
            OxygenEnrichmentPercent: oxygenEnrichment,
            PredictedBiomassAccumulationGPerL: predictedBiomass,
            GrowthPhase: growthPhase,
            MetabolicWarning: warning
        );
    }
}
