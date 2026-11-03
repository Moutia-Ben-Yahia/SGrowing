using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.RetailCoopDsp;

public sealed class RetailCoopDspEngine
{
    private readonly ConcurrentDictionary<string, RetailerCleanRoomPartnerRecord> _cleanRooms = new();
    private readonly ConcurrentDictionary<string, OffSiteCoopCampaignRecord> _campaigns = new();
    private readonly ConcurrentBag<CleanRoomMatchReceipt> _matchReceipts = new();

    public RetailCoopDspEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var cr1 = new RetailerCleanRoomPartnerRecord(
            "CR-WALMART-01",
            "Walmart Luminate / Connect Clean Room",
            "Snowflake Clean Rooms",
            145000000,
            98.8,
            "ACTIVE_LINKED"
        );

        var cr2 = new RetailerCleanRoomPartnerRecord(
            "CR-AMAZON-02",
            "Amazon Marketing Cloud (AMC) Audience Hub",
            "AWS Clean Rooms",
            210000000,
            99.2,
            "ACTIVE_LINKED"
        );

        _cleanRooms[cr1.CleanRoomPartnerId] = cr1;
        _cleanRooms[cr2.CleanRoomPartnerId] = cr2;

        var cmp1 = new OffSiteCoopCampaignRecord(
            "COOP-CMP-801",
            "BioActive Organic Nutrients",
            cr1.CleanRoomPartnerId,
            120000.00m,
            80000.00m,
            40000.00m,
            "Verified Past 90-Day Organic Beverage Buyers",
            "Connected TV (The Trade Desk) & High-Impact Display",
            4.65,
            "PACING_OPTIMAL"
        );

        var cmp2 = new OffSiteCoopCampaignRecord(
            "COOP-CMP-802",
            "HyperHydrate Electrolytes",
            cr2.CleanRoomPartnerId,
            75000.00m,
            50000.00m,
            25000.00m,
            "Amazon Prime Active Sports & Fitness Shoppers",
            "Open Web Video & Programmatic Audio",
            5.20,
            "PACING_OPTIMAL"
        );

        _campaigns[cmp1.CampaignId] = cmp1;
        _campaigns[cmp2.CampaignId] = cmp2;
    }

    public RetailCoopDspOverview GetOverview()
    {
        var crList = _cleanRooms.Values.ToList();
        var campList = _campaigns.Values.ToList();
        var totalSpend = campList.Sum(c => c.TotalCoopBudgetUsd);
        var blendedRoas = campList.Count > 0 ? campList.Average(c => c.ProjectedRoasMultiplier) : 4.8;

        return new RetailCoopDspOverview(
            crList.Count,
            campList.Count,
            totalSpend,
            blendedRoas,
            crList,
            campList
        );
    }

    public OffSiteCoopCampaignRecord LaunchCoopCampaign(LaunchCoopCampaignRequest request)
    {
        var campaignId = $"COOP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var retailerCredit = request.BrandBudgetUsd * (request.RetailerContributionPct / 100.0m);
        var totalBudget = request.BrandBudgetUsd + retailerCredit;

        var campaign = new OffSiteCoopCampaignRecord(
            campaignId,
            request.BrandName,
            request.RetailPartnerId,
            totalBudget,
            request.BrandBudgetUsd,
            retailerCredit,
            request.TargetAudienceSegment,
            request.Channels,
            4.95,
            "ACTIVATED_COOP_BIDDING"
        );

        _campaigns[campaignId] = campaign;
        return campaign;
    }

    public CleanRoomMatchReceipt ExecuteCleanRoomMatch(ExecuteCleanRoomMatchRequest request)
    {
        var receiptId = $"RCP-CR-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var proof = $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}";
        var matched = (int)(request.HashedCandidateIdentifiersCount * 0.785);
        var matchRate = (double)matched / Math.Max(1, request.HashedCandidateIdentifiersCount) * 100.0;

        var receipt = new CleanRoomMatchReceipt(
            receiptId,
            request.RetailPartnerId,
            request.HashedCandidateIdentifiersCount,
            matched,
            matchRate,
            proof,
            DateTime.UtcNow
        );

        _matchReceipts.Add(receipt);
        return receipt;
    }
}
