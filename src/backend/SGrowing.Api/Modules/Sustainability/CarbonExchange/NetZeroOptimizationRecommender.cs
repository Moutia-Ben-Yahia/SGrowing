namespace SGrowing.Api.Modules.Sustainability.CarbonExchange;

/// <summary>
/// Autonomous marginal abatement cost curve (MACC) optimizer recommending
/// prioritized decarbonization actions ranked by Euro per tonne CO2e abated.
/// </summary>
public sealed class NetZeroOptimizationRecommender
{
    public IReadOnlyList<DecarbonizationRecommendation> GenerateRecommendations(GhgAssessmentResult assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        var list = new List<DecarbonizationRecommendation>();

        // 1. Scope 2 Grid Electricity -> On-site Solar PPA
        if (assessment.Scope2TotalKgCo2e > 500.0)
        {
            var s2Tonnes = assessment.Scope2TotalKgCo2e / 1000.0;
            var abatementTonnes = s2Tonnes * 0.85; // 85% abatement potential
            var cost = (decimal)(abatementTonnes * 28.0); // ~28 EUR/tonne levelized solar cost
            list.Add(new DecarbonizationRecommendation(
                ActionTitle: "Deploy Rooftop Solar PPA for Distribution Centers",
                Description: "Transition warehouse grid consumption to commercial rooftop solar with storage buffer.",
                PotentialAbatementTonnesCo2e: Math.Round(abatementTonnes, 2),
                EstimatedCostEur: Math.Round(cost, 2),
                AbatementCostEurPerTonne: 28.00m,
                Priority: "High"
            ));
        }

        // 2. Scope 3 Road Freight -> Intermodal Rail / Short-Sea Shipping
        if (assessment.Scope3TotalKgCo2e > 1000.0)
        {
            var s3Tonnes = assessment.Scope3TotalKgCo2e / 1000.0;
            var abatementTonnes = s3Tonnes * 0.74; // ~74% reduction shifting diesel road to rail/maritime
            var cost = (decimal)(abatementTonnes * 42.50);
            list.Add(new DecarbonizationRecommendation(
                ActionTitle: "Modal Shift: Long-Haul Highway Freight to Rail Corridor",
                Description: "Shift arterial freight legs (>400km) to electrified rail freight network.",
                PotentialAbatementTonnesCo2e: Math.Round(abatementTonnes, 2),
                EstimatedCostEur: Math.Round(cost, 2),
                AbatementCostEurPerTonne: 42.50m,
                Priority: "High"
            ));
        }

        // 3. Scope 1 Fleet Diesel -> Commercial EV Delivery Vans
        if (assessment.Scope1TotalKgCo2e > 500.0)
        {
            var s1Tonnes = assessment.Scope1TotalKgCo2e / 1000.0;
            var abatementTonnes = s1Tonnes * 0.80; // ~80% lifecycle reduction
            var cost = (decimal)(abatementTonnes * 65.0);
            list.Add(new DecarbonizationRecommendation(
                ActionTitle: "Last-Mile Delivery Fleet Electrification (EV)",
                Description: "Replace internal combustion diesel vans with electric courier fleet.",
                PotentialAbatementTonnesCo2e: Math.Round(abatementTonnes, 2),
                EstimatedCostEur: Math.Round(cost, 2),
                AbatementCostEurPerTonne: 65.00m,
                Priority: "Medium"
            ));
        }

        // 4. Residual emissions offset via Gold Standard certified nature-based removals
        var residualTonnes = assessment.NetEmissionsKgCo2e / 1000.0;
        if (residualTonnes > 0.1)
        {
            var offsetCost = (decimal)(residualTonnes * 35.0);
            list.Add(new DecarbonizationRecommendation(
                ActionTitle: "Procure Gold Standard Verified Carbon Removals",
                Description: "Retire high-durability afforestation and soil carbon removal certificates for unavoidable residual emissions.",
                PotentialAbatementTonnesCo2e: Math.Round(residualTonnes, 2),
                EstimatedCostEur: Math.Round(offsetCost, 2),
                AbatementCostEurPerTonne: 35.00m,
                Priority: "Immediate"
            ));
        }

        return list.OrderBy(r => r.AbatementCostEurPerTonne).ToList();
    }
}
