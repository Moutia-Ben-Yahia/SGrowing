namespace SGrowing.Api.Modules.Sales.QuotaCapacity;

public interface IQuotaCapacityEngine
{
    IReadOnlyList<TerritoryCapacityProfile> GetTerritories();
    TerritoryCapacityProfile? GetTerritory(string territoryId);
    IReadOnlyList<ImbalanceAlert> GetImbalanceAlerts();
    RebalanceResult RebalanceTerritory(RebalanceRequest request);
}

public class QuotaCapacityEngine : IQuotaCapacityEngine
{
    private readonly List<TerritoryCapacityProfile> _territories = new()
    {
        new TerritoryCapacityProfile(
            TerritoryId: "TERR-US-EAST",
            TerritoryName: "US Enterprise East & FinTech",
            Region: "North America",
            AssignedRepsCount: 14,
            TotalAnnualQuotaEur: 28000000m,
            QuotaPerRepEur: 2000000m,
            TotalAddressableMarketEur: 185000000m,
            ActivePipelineEur: 105000000m,
            CurrentPipelineCoverageRatio: 3.75m,
            CapacityStatus: "Balanced",
            RepBurnoutRiskIndex: 22.4m,
            LastUpdatedUtc: DateTime.UtcNow
        ),
        new TerritoryCapacityProfile(
            TerritoryId: "TERR-EMEA-NORTH",
            TerritoryName: "EMEA DACH & Nordics Enterprise",
            Region: "EMEA",
            AssignedRepsCount: 8,
            TotalAnnualQuotaEur: 22000000m,
            QuotaPerRepEur: 2750000m,
            TotalAddressableMarketEur: 140000000m,
            ActivePipelineEur: 52000000m,
            CurrentPipelineCoverageRatio: 2.36m,
            CapacityStatus: "OverCapacity",
            RepBurnoutRiskIndex: 84.6m,
            LastUpdatedUtc: DateTime.UtcNow
        ),
        new TerritoryCapacityProfile(
            TerritoryId: "TERR-APAC-GROWTH",
            TerritoryName: "APAC Strategic Expansion Hub",
            Region: "APAC",
            AssignedRepsCount: 5,
            TotalAnnualQuotaEur: 7500000m,
            QuotaPerRepEur: 1500000m,
            TotalAddressableMarketEur: 95000000m,
            ActivePipelineEur: 42000000m,
            CurrentPipelineCoverageRatio: 5.6m,
            CapacityStatus: "WhitespaceSurge",
            RepBurnoutRiskIndex: 18.2m,
            LastUpdatedUtc: DateTime.UtcNow
        )
    };

    private readonly List<ImbalanceAlert> _alerts = new()
    {
        new ImbalanceAlert(
            AlertId: "ALT-CAP-001",
            TerritoryId: "TERR-EMEA-NORTH",
            TerritoryName: "EMEA DACH & Nordics Enterprise",
            ImbalanceType: "QuotaBurnout",
            Severity: "High",
            RecommendedHeadcountDelta: 3,
            GapAmountEur: 26000000m,
            RecommendedAction: "Add 3 Enterprise Account Executives to lower rep quota target and prevent pipeline decay"
        ),
        new ImbalanceAlert(
            AlertId: "ALT-CAP-002",
            TerritoryId: "TERR-APAC-GROWTH",
            TerritoryName: "APAC Strategic Expansion Hub",
            ImbalanceType: "WhitespaceSurge",
            Severity: "Medium",
            RecommendedHeadcountDelta: 4,
            GapAmountEur: 48000000m,
            RecommendedAction: "Spin off Southeast Asia sub-cluster to capture unworked inbound pipeline"
        )
    };

    public IReadOnlyList<TerritoryCapacityProfile> GetTerritories() => _territories.AsReadOnly();

    public TerritoryCapacityProfile? GetTerritory(string territoryId) =>
        _territories.FirstOrDefault(t => t.TerritoryId == territoryId);

    public IReadOnlyList<ImbalanceAlert> GetImbalanceAlerts() => _alerts.AsReadOnly();

    public RebalanceResult RebalanceTerritory(RebalanceRequest request)
    {
        var existing = _territories.FirstOrDefault(t => t.TerritoryId == request.TerritoryId);
        if (existing == null)
        {
            throw new InvalidOperationException($"Territory {request.TerritoryId} not found");
        }

        var newHeadcount = existing.AssignedRepsCount + request.AddHeadcount;
        var optimizedQuotaPerRep = existing.TotalAnnualQuotaEur / newHeadcount;
        var updated = existing with
        {
            AssignedRepsCount = newHeadcount,
            QuotaPerRepEur = optimizedQuotaPerRep,
            CurrentPipelineCoverageRatio = request.TargetCoverageRatio,
            CapacityStatus = "Balanced",
            RepBurnoutRiskIndex = 28.5m,
            LastUpdatedUtc = DateTime.UtcNow
        };

        var index = _territories.FindIndex(t => t.TerritoryId == request.TerritoryId);
        _territories[index] = updated;

        return new RebalanceResult(
            TerritoryId: request.TerritoryId,
            PreviousHeadcount: existing.AssignedRepsCount,
            NewHeadcount: newHeadcount,
            OptimizedQuotaPerRepEur: optimizedQuotaPerRep,
            ProjectedPipelineCoverageRatio: request.TargetCoverageRatio,
            NewCapacityStatus: "Balanced",
            AuditLogSummary: $"Rebalanced territory {request.TerritoryId} with +{request.AddHeadcount} reps to reach {request.TargetCoverageRatio:F1}x pipeline coverage."
        );
    }
}
