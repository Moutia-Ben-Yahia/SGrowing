using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Marketing.AdSpendRedistribution;

public interface IAdSpendRedistributionEngine
{
    AdSpendRedistributionOverview GetOverview();
    ReallocationResult ReallocateBudget(ReallocateBudgetCommand command);
}

public class AdSpendRedistributionEngine : IAdSpendRedistributionEngine
{
    private readonly ConcurrentDictionary<string, AttributionChannelMetric> _channels = new();
    private readonly List<TouchpointJourney> _journeys = new();

    public AdSpendRedistributionEngine()
    {
        SeedData();
    }

    private void SeedData()
    {
        var now = DateTime.UtcNow;

        _journeys.Add(new TouchpointJourney("JRN-101", "Acme Enterprise Corp", 320000m, new() { "ContentSeo", "Webinars", "OutboundSdr", "SocialRetargeting" }, now.AddDays(-2)));
        _journeys.Add(new TouchpointJourney("JRN-102", "Globex Global Logistics", 195000m, new() { "PaidSearch", "ContentSeo", "Webinars", "SocialRetargeting" }, now.AddDays(-3)));
        _journeys.Add(new TouchpointJourney("JRN-103", "Soylent Tech Labs", 480000m, new() { "PartnerEcosystem", "Webinars", "OutboundSdr", "PaidSearch" }, now.AddDays(-5)));
        _journeys.Add(new TouchpointJourney("JRN-104", "Initech Systems", 140000m, new() { "PaidSearch", "SocialRetargeting" }, now.AddDays(-7)));
        _journeys.Add(new TouchpointJourney("JRN-105", "Umbrella BioResearch", 650000m, new() { "ContentSeo", "PartnerEcosystem", "Webinars", "SocialRetargeting" }, now.AddDays(-8)));

        var c1 = new AttributionChannelMetric(
            ChannelId: "Webinars",
            ChannelName: "Executive Interactive Webinars",
            CurrentSpend: 45000m,
            FirstTouchSharePct: 6.2m,
            LastTouchSharePct: 8.5m,
            ShapleySharePct: 29.4m,
            AttributionSkewPct: 20.9m,
            RecommendedSpendShift: +35000m,
            ProjectedRoasLift: 2.8m
        );

        var c2 = new AttributionChannelMetric(
            ChannelId: "SocialRetargeting",
            ChannelName: "LinkedIn & Meta ABM Retargeting",
            CurrentSpend: 85000m,
            FirstTouchSharePct: 12.0m,
            LastTouchSharePct: 41.5m,
            ShapleySharePct: 18.2m,
            AttributionSkewPct: -23.3m,
            RecommendedSpendShift: -30000m,
            ProjectedRoasLift: 1.4m
        );

        var c3 = new AttributionChannelMetric(
            ChannelId: "ContentSeo",
            ChannelName: "High-Intent Product SEO & Whitepapers",
            CurrentSpend: 30000m,
            FirstTouchSharePct: 38.0m,
            LastTouchSharePct: 11.2m,
            ShapleySharePct: 22.1m,
            AttributionSkewPct: 10.9m,
            RecommendedSpendShift: +15000m,
            ProjectedRoasLift: 2.1m
        );

        var c4 = new AttributionChannelMetric(
            ChannelId: "PaidSearch",
            ChannelName: "Google Search (High Intent Brand & Non-Brand)",
            CurrentSpend: 60000m,
            FirstTouchSharePct: 32.5m,
            LastTouchSharePct: 24.8m,
            ShapleySharePct: 16.5m,
            AttributionSkewPct: -8.3m,
            RecommendedSpendShift: -12000m,
            ProjectedRoasLift: 1.1m
        );

        var c5 = new AttributionChannelMetric(
            ChannelId: "PartnerEcosystem",
            ChannelName: "Co-Sell Cloud Marketplace Partners",
            CurrentSpend: 25000m,
            FirstTouchSharePct: 11.3m,
            LastTouchSharePct: 14.0m,
            ShapleySharePct: 13.8m,
            AttributionSkewPct: -0.2m,
            RecommendedSpendShift: +5000m,
            ProjectedRoasLift: 1.9m
        );

        _channels[c1.ChannelId] = c1;
        _channels[c2.ChannelId] = c2;
        _channels[c3.ChannelId] = c3;
        _channels[c4.ChannelId] = c4;
        _channels[c5.ChannelId] = c5;
    }

    public AdSpendRedistributionOverview GetOverview()
    {
        var channelsList = _channels.Values.OrderByDescending(c => c.ShapleySharePct).ToList();
        var totalPipelineArr = _journeys.Sum(j => j.DealArr);
        var totalSpend = channelsList.Sum(c => c.CurrentSpend);
        var blendedRoas = totalSpend > 0 ? (double)(totalPipelineArr / totalSpend) : 0.0;
        var projectedArrUplift = Math.Round(totalPipelineArr * 0.185m, 2);

        return new AdSpendRedistributionOverview(
            TotalPipelineArr: totalPipelineArr,
            TotalMarketingSpend: totalSpend,
            OverallBlendedRoas: Math.Round(blendedRoas, 2),
            Channels: channelsList,
            RecentJourneys: _journeys.OrderByDescending(j => j.ClosedAt).Take(5).ToList(),
            ProjectedArrUplift: projectedArrUplift
        );
    }

    public ReallocationResult ReallocateBudget(ReallocateBudgetCommand command)
    {
        if (!_channels.TryGetValue(command.ChannelId, out var existing))
        {
            throw new KeyNotFoundException($"Channel {command.ChannelId} not found.");
        }

        var prevSpend = existing.CurrentSpend;
        var newSpend = Math.Max(5000m, prevSpend + command.DeltaAmount);
        var expectedRoas = 4.2m + (existing.ProjectedRoasLift * 0.5m);

        var updated = existing with
        {
            CurrentSpend = newSpend,
            RecommendedSpendShift = Math.Max(0m, existing.RecommendedSpendShift - command.DeltaAmount)
        };

        _channels[command.ChannelId] = updated;

        var log = $"Autonomous Budget Rebalancer: Shifted {command.DeltaAmount:C0} to {existing.ChannelName} under {command.RebalanceMode} mode. New monthly budget: {newSpend:C0}.";
        return new ReallocationResult(
            ChannelId: command.ChannelId,
            PreviousSpend: prevSpend,
            NewSpend: newSpend,
            ExpectedRoas: Math.Round(expectedRoas, 2),
            ExecutionLog: log
        );
    }
}
