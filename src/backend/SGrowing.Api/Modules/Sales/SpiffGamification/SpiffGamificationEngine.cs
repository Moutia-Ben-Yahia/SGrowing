using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.SpiffGamification;

public interface ISpiffGamificationEngine
{
    SpiffOverview GetOverview();
    SpiffCampaign CreateCampaign(string name, SpiffRewardType rewardType, double rewardPerDeal, double totalBudget);
    SpiffDealEscrow AwardDealSpiff(string campaignId, string dealId, string salesRepName);
    IReadOnlyList<LeaderboardEntry> GetLeaderboard();
}

public sealed class SpiffGamificationEngine : ISpiffGamificationEngine
{
    private readonly ConcurrentDictionary<string, SpiffCampaign> _campaigns = new();
    private readonly ConcurrentDictionary<string, SpiffDealEscrow> _escrows = new();

    public SpiffGamificationEngine()
    {
        SeedCampaigns();
        SeedInitialEscrows();
    }

    private void SeedCampaigns()
    {
        var c1 = new SpiffCampaign(
            "SPIFF-CAMP-001",
            "Q4 Strategic Enterprise Acceleration Rush",
            SpiffRewardType.CashBonus,
            2500.0,
            100000.0,
            87500.0,
            DateTime.UtcNow.AddDays(-15),
            DateTime.UtcNow.AddDays(45),
            true
        );
        _campaigns.TryAdd(c1.CampaignId, c1);
    }

    private void SeedInitialEscrows()
    {
        var e1 = new SpiffDealEscrow(
            "ESC-001",
            "SPIFF-CAMP-001",
            "DEAL-ENT-901",
            "Alexandra Hayes",
            2500.0,
            DateTime.UtcNow.AddHours(-3),
            "PAID_INSTANT_ESCROW"
        );
        _escrows.TryAdd(e1.EscrowId, e1);
    }

    public SpiffOverview GetOverview()
    {
        var active = _campaigns.Values.Where(c => c.IsActive).ToList();
        var escrows = _escrows.Values.OrderByDescending(e => e.CreatedAtUtc).Take(10).ToList();
        var leaderboard = GetLeaderboard();

        return new SpiffOverview(
            ActiveCampaignsCount: active.Count,
            TotalEscrowFundedUsd: 100000.0,
            TotalSpiffsPaidTodayUsd: 12500.0,
            TopPerformingRepsCount: leaderboard.Count,
            ActiveCampaigns: active,
            RecentEscrows: escrows,
            Leaderboard: leaderboard
        );
    }

    public SpiffCampaign CreateCampaign(string name, SpiffRewardType rewardType, double rewardPerDeal, double totalBudget)
    {
        var campaign = new SpiffCampaign(
            $"SPIFF-CAMP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            name,
            rewardType,
            rewardPerDeal,
            totalBudget,
            totalBudget,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            true
        );
        _campaigns.TryAdd(campaign.CampaignId, campaign);
        return campaign;
    }

    public SpiffDealEscrow AwardDealSpiff(string campaignId, string dealId, string salesRepName)
    {
        double amount = 2500.0;
        if (_campaigns.TryGetValue(campaignId, out var camp))
        {
            amount = camp.RewardAmountPerDeal;
            _campaigns[campaignId] = camp with
            {
                RemainingEscrowUsd = Math.Max(0.0, camp.RemainingEscrowUsd - amount)
            };
        }

        var escrow = new SpiffDealEscrow(
            $"ESC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            campaignId,
            dealId,
            salesRepName,
            amount,
            DateTime.UtcNow,
            "PAID_INSTANT_ESCROW"
        );
        _escrows.TryAdd(escrow.EscrowId, escrow);
        return escrow;
    }

    public IReadOnlyList<LeaderboardEntry> GetLeaderboard()
    {
        return
        [
            new LeaderboardEntry("REP-01", "Alexandra Hayes", 8, 20000.0, "DIAMOND_CHAMPION", 1),
            new LeaderboardEntry("REP-02", "Marcus Chen", 6, 15000.0, "PLATINUM_CLOSER", 2),
            new LeaderboardEntry("REP-03", "Sarah Jenkins", 4, 10000.0, "GOLD_STRIKER", 3)
        ];
    }
}
