using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Sales.TerritoryRebalancing;

public interface ITerritoryRebalancingEngine
{
    TerritoryRebalancingOverview GetOverview();
    SimulateRebalanceResult SimulateRebalance(SimulateRebalanceCommand command);
    ApplyRebalanceResult ApplyRebalance(ApplyRebalanceCommand command);
}

public class TerritoryRebalancingEngine : ITerritoryRebalancingEngine
{
    private readonly List<TerritoryWorkloadMetrics> _territories =
    [
        new("TERR-NA-EAST", "North America East", "REP-101", "Sarah Jenkins", 48, 4850000m, 1.42, 118.5),
        new("TERR-NA-WEST", "North America West", "REP-102", "Michael Chang", 32, 2900000m, 0.88, 89.2),
        new("TERR-EMEA-CENTRAL", "EMEA Central", "REP-103", "Elena Rostova", 52, 5100000m, 1.55, 124.0),
        new("TERR-APAC-SOUTH", "APAC South", "REP-104", "Kenji Takahashi", 24, 1850000m, 0.65, 72.4)
    ];

    private readonly List<RebalanceRecommendation> _recommendations =
    [
        new("ACC-9041", "Apex Logistics Global", "REP-103", "Elena Rostova", "REP-104", "Kenji Takahashi", "High APAC branch density; balances EMEA overload (1.55 -> 1.25)", -19.3),
        new("ACC-8812", "Beacon Health Systems", "REP-101", "Sarah Jenkins", "REP-102", "Michael Chang", "West coast HQ relocation; balances East overload (1.42 -> 1.15)", -18.7),
        new("ACC-7740", "CloudVanguard AI", "REP-101", "Sarah Jenkins", "REP-102", "Michael Chang", "Tech vertical specialization and pipeline equalization", -12.4)
    ];

    public TerritoryRebalancingOverview GetOverview()
    {
        var totalAccounts = _territories.Sum(t => t.AccountCount);
        var totalReps = _territories.Count;
        var giniIndex = 0.38; // Initial unequal distribution
        var efficiencyGain = 24.6;

        return new TerritoryRebalancingOverview(
            totalAccounts,
            totalReps,
            giniIndex,
            efficiencyGain,
            _territories,
            _recommendations
        );
    }

    public SimulateRebalanceResult SimulateRebalance(SimulateRebalanceCommand command)
    {
        var simId = $"SIM-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var simulatedGini = command.Strategy switch
        {
            "Equilibrium" => 0.12,
            "GeoProximity" => 0.18,
            _ => 0.15
        };

        var expectedImprovement = Math.Round((0.38 - simulatedGini) * 100 * 0.95, 1);

        return new SimulateRebalanceResult(
            simId,
            command.Strategy,
            0.38,
            simulatedGini,
            _recommendations.Count,
            expectedImprovement,
            _recommendations
        );
    }

    public ApplyRebalanceResult ApplyRebalance(ApplyRebalanceCommand command)
    {
        var planId = $"REBAL-PLAN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var effectiveDate = DateTime.UtcNow.AddDays(command.GracePeriodDays > 0 ? command.GracePeriodDays : 14);

        return new ApplyRebalanceResult(
            planId,
            "Scheduled",
            _recommendations.Count,
            effectiveDate,
            $"AUDIT-HASH-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}"
        );
    }
}
