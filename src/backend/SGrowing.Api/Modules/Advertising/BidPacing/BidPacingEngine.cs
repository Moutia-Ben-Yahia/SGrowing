using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.BidPacing;

public interface IBidPacingEngine
{
    List<CampaignPacingRecord> GetCampaigns();
    PacingAdjustmentResult AdjustPacing(AdjustBidPacingRequest request);
}

public class BidPacingEngine : IBidPacingEngine
{
    private readonly ConcurrentDictionary<string, CampaignPacingRecord> _campaigns = new(StringComparer.OrdinalIgnoreCase);

    public BidPacingEngine()
    {
        // Seed campaign pacing
        var c1 = new CampaignPacingRecord("camp_abm_enterprise_01", "Fortune 500 ABM Intent Surge", "LinkedIn Marketing", 2500m, 1450m, 58.0, 1.25, PacingMode.AcceleratedHighIntent, 18400);
        var c2 = new CampaignPacingRecord("camp_google_high_cpc_02", "B2B Cloud Migration Intent", "Google Ads DSP", 3500m, 3200m, 91.4, 0.85, PacingMode.ThrottledOverSpend, 24100);
        var c3 = new CampaignPacingRecord("camp_meta_lookalike_03", "FinTech SaaS Buying Committee", "Meta Ads", 1800m, 900m, 50.0, 1.0, PacingMode.EvenThroughoutDay, 31200);

        _campaigns[c1.CampaignId] = c1;
        _campaigns[c2.CampaignId] = c2;
        _campaigns[c3.CampaignId] = c3;
    }

    public List<CampaignPacingRecord> GetCampaigns() => _campaigns.Values.OrderBy(c => c.CampaignId).ToList();

    public PacingAdjustmentResult AdjustPacing(AdjustBidPacingRequest request)
    {
        if (!_campaigns.TryGetValue(request.CampaignId, out var existing))
        {
            throw new KeyNotFoundException($"Campaign {request.CampaignId} not found.");
        }

        var updated = existing with
        {
            CurrentBidMultiplier = request.NewBidMultiplier,
            Mode = request.NewMode
        };

        _campaigns[request.CampaignId] = updated;

        return new PacingAdjustmentResult(
            request.CampaignId,
            updated.CurrentBidMultiplier,
            updated.Mode,
            $"Pacing for '{existing.CampaignName}' adjusted to {request.NewBidMultiplier:F2}x with mode '{request.NewMode}'."
        );
    }
}
