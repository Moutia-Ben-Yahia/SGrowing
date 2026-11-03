using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Commerce.InfluencerEscrowV2;

public interface IInfluencerEscrowV2Engine
{
    List<InfluencerCampaign> GetCampaigns();
    InfluencerCampaign? GetCampaign(string campaignId);
    InfluencerCampaign ReleasePayout(ReleaseMilestonePayoutRequest request);
    InfluencerCampaign AttributeConversion(AttributeConversionRequest request);
}

public class InfluencerEscrowV2Engine : IInfluencerEscrowV2Engine
{
    private readonly ConcurrentDictionary<string, InfluencerCampaign> _campaigns = new(StringComparer.OrdinalIgnoreCase);

    public InfluencerEscrowV2Engine()
    {
        // Seed campaigns
        var c1 = new InfluencerCampaign(
            CampaignId: "inf_camp_201",
            CreatorHandle: "@AlexTechFinOps",
            Platform: "YouTube Tech",
            TotalEscrowLockedUsd: 15000m,
            ReleasedPayoutUsd: 5000m,
            AttributedConversions: 84,
            AttributedGmvUsd: 142000m,
            FraudRiskScore: 3.5,
            IsActive: true
        );

        var c2 = new InfluencerCampaign(
            CampaignId: "inf_camp_202",
            CreatorHandle: "@SarahGrowthLead",
            Platform: "LinkedIn ThoughtLeader",
            TotalEscrowLockedUsd: 25000m,
            ReleasedPayoutUsd: 10000m,
            AttributedConversions: 140,
            AttributedGmvUsd: 310000m,
            FraudRiskScore: 2.1,
            IsActive: true
        );

        var c3 = new InfluencerCampaign(
            CampaignId: "inf_camp_203",
            CreatorHandle: "@DevOpsDaily",
            Platform: "X / Twitter",
            TotalEscrowLockedUsd: 8000m,
            ReleasedPayoutUsd: 0m,
            AttributedConversions: 18,
            AttributedGmvUsd: 36000m,
            FraudRiskScore: 5.8,
            IsActive: true
        );

        _campaigns[c1.CampaignId] = c1;
        _campaigns[c2.CampaignId] = c2;
        _campaigns[c3.CampaignId] = c3;
    }

    public List<InfluencerCampaign> GetCampaigns()
    {
        return _campaigns.Values.OrderByDescending(c => c.AttributedGmvUsd).ToList();
    }

    public InfluencerCampaign? GetCampaign(string campaignId)
    {
        _campaigns.TryGetValue(campaignId, out var camp);
        return camp;
    }

    public InfluencerCampaign ReleasePayout(ReleaseMilestonePayoutRequest request)
    {
        if (!_campaigns.TryGetValue(request.CampaignId, out var camp))
        {
            throw new KeyNotFoundException($"Campaign {request.CampaignId} not found.");
        }

        decimal maxAvailable = camp.TotalEscrowLockedUsd - camp.ReleasedPayoutUsd;
        decimal toRelease = Math.Clamp(request.AmountToReleaseUsd, 0m, maxAvailable);

        var updated = camp with
        {
            ReleasedPayoutUsd = camp.ReleasedPayoutUsd + toRelease
        };

        _campaigns[request.CampaignId] = updated;
        return updated;
    }

    public InfluencerCampaign AttributeConversion(AttributeConversionRequest request)
    {
        if (!_campaigns.TryGetValue(request.CampaignId, out var camp))
        {
            throw new KeyNotFoundException($"Campaign {request.CampaignId} not found.");
        }

        if (request.IsFraudSuspected)
        {
            var fraudFlagged = camp with
            {
                FraudRiskScore = Math.Min(100.0, camp.FraudRiskScore + 12.0)
            };
            _campaigns[request.CampaignId] = fraudFlagged;
            return fraudFlagged;
        }

        var updated = camp with
        {
            AttributedConversions = camp.AttributedConversions + 1,
            AttributedGmvUsd = camp.AttributedGmvUsd + request.OrderGmvUsd
        };

        _campaigns[request.CampaignId] = updated;
        return updated;
    }
}
