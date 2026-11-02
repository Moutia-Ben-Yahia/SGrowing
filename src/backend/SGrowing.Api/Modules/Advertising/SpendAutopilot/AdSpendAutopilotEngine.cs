using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.SpendAutopilot;

public class AdSpendAutopilotEngine
{
    private readonly ConcurrentDictionary<string, AdCampaignChannel> _campaigns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<BidShadingTelemetry> _shadingLogs = new();

    public AdSpendAutopilotEngine()
    {
        SeedCampaigns();
    }

    private void SeedCampaigns()
    {
        var c1 = new AdCampaignChannel(
            CampaignId: "cmp_google_pmax",
            CampaignName: "Enterprise Tier 1 Google Search PMax",
            Channel: "GoogleSearch",
            DailyBudget: 3500m,
            CurrentSpend: 2840m,
            TargetCpa: 45m,
            CurrentCpa: 38.50m,
            Roas: 4.82,
            Status: "Optimized"
        );

        var c2 = new AdCampaignChannel(
            CampaignId: "cmp_meta_retargeting",
            CampaignName: "Meta DPA Mid-Funnel Retargeting",
            Channel: "MetaFeed",
            DailyBudget: 2200m,
            CurrentSpend: 1950m,
            TargetCpa: 55m,
            CurrentCpa: 52.10m,
            Roas: 3.45,
            Status: "Active"
        );

        var c3 = new AdCampaignChannel(
            CampaignId: "cmp_tiktok_topview",
            CampaignName: "TikTok Gen-Z Viral Creator Sparks",
            Channel: "TikTokVideo",
            DailyBudget: 1500m,
            CurrentSpend: 1480m,
            TargetCpa: 30m,
            CurrentCpa: 68.20m, // CPA spike anomaly!
            Roas: 1.15,
            Status: "AnomalyPaused"
        );

        _campaigns[c1.CampaignId] = c1;
        _campaigns[c2.CampaignId] = c2;
        _campaigns[c3.CampaignId] = c3;

        _shadingLogs.Add(new BidShadingTelemetry(c1.CampaignId, 4.20m, 3.45m, 0.75m, 86.4, DateTime.UtcNow.AddMinutes(-5)));
        _shadingLogs.Add(new BidShadingTelemetry(c2.CampaignId, 3.10m, 2.60m, 0.50m, 82.1, DateTime.UtcNow.AddMinutes(-2)));
    }

    public Task<IReadOnlyList<AdCampaignChannel>> GetCampaignsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AdCampaignChannel>>(_campaigns.Values.OrderByDescending(c => c.Roas).ToList());
    }

    public Task<IReadOnlyList<BudgetRebalanceRecommendation>> GetRebalanceRecommendationsAsync(CancellationToken ct = default)
    {
        var recommendations = new List<BudgetRebalanceRecommendation>();
        var campaigns = _campaigns.Values.ToList();

        var highPerformer = campaigns.OrderByDescending(c => c.Roas).FirstOrDefault();
        var underPerformer = campaigns.Where(c => c.Status != "AnomalyPaused").OrderBy(c => c.Roas).FirstOrDefault();

        if (highPerformer != null && underPerformer != null && highPerformer.CampaignId != underPerformer.CampaignId)
        {
            var realloc = Math.Round(underPerformer.DailyBudget * 0.25m, 0);
            recommendations.Add(new BudgetRebalanceRecommendation(
                SourceCampaignId: underPerformer.CampaignId,
                TargetCampaignId: highPerformer.CampaignId,
                ReallocatedAmount: realloc,
                ExpectedRoasLiftPercent: 14.8,
                Reasoning: $"Shift ${realloc} daily budget from lower-performing {underPerformer.CampaignName} (ROAS {underPerformer.Roas}x) to {highPerformer.CampaignName} (ROAS {highPerformer.Roas}x)."
            ));
        }

        return Task.FromResult<IReadOnlyList<BudgetRebalanceRecommendation>>(recommendations);
    }

    public Task<AdCampaignChannel> RebalanceBudgetAsync(RebalanceBudgetRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_campaigns.TryGetValue(request.SourceCampaignId, out var source))
        {
            throw new KeyNotFoundException($"Source campaign '{request.SourceCampaignId}' not found.");
        }
        if (!_campaigns.TryGetValue(request.TargetCampaignId, out var target))
        {
            throw new KeyNotFoundException($"Target campaign '{request.TargetCampaignId}' not found.");
        }

        var updatedSource = source with { DailyBudget = Math.Max(100m, source.DailyBudget - request.Amount) };
        var updatedTarget = target with { DailyBudget = target.DailyBudget + request.Amount, Status = "Optimized" };

        _campaigns[source.CampaignId] = updatedSource;
        _campaigns[target.CampaignId] = updatedTarget;

        return Task.FromResult(updatedTarget);
    }

    public Task<BidShadingTelemetry> ExecuteBidShadingAsync(string campaignId, decimal rawBid, CancellationToken ct = default)
    {
        if (!_campaigns.TryGetValue(campaignId, out var campaign))
        {
            throw new KeyNotFoundException($"Campaign '{campaignId}' not found.");
        }

        // Shave 16% off raw bid while preserving >82% win rate in 1st price auction
        var shadedBid = Math.Round(rawBid * 0.84m, 2);
        var savings = rawBid - shadedBid;

        var log = new BidShadingTelemetry(
            CampaignId: campaign.CampaignId,
            RawWinningBid: rawBid,
            ShadedBidPrice: shadedBid,
            SavingsAmount: savings,
            AuctionWinRatePercent: 84.5,
            TimestampUtc: DateTime.UtcNow
        );

        _shadingLogs.Add(log);
        return Task.FromResult(log);
    }

    public Task<AutopilotEngineSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var campaigns = _campaigns.Values.ToList();
        var totalBudget = campaigns.Sum(c => c.DailyBudget);
        var totalSpend = campaigns.Sum(c => c.CurrentSpend);
        var totalSavings = _shadingLogs.Sum(l => l.SavingsAmount) * 2400m; // extrapolated daily savings volume
        var blendedRoas = campaigns.Count > 0 ? campaigns.Average(c => c.Roas) : 0;
        var paused = campaigns.Count(c => c.Status == "AnomalyPaused");

        var summary = new AutopilotEngineSummary(
            TotalDailyBudget: totalBudget,
            TotalCurrentSpend: totalSpend,
            TotalSavingsFromBidShading: Math.Round(totalSavings, 2),
            BlendedRoas: Math.Round(blendedRoas, 2),
            TotalActiveCampaigns: campaigns.Count(c => c.Status != "AnomalyPaused"),
            AnomalyPausedCount: paused
        );

        return Task.FromResult(summary);
    }
}
