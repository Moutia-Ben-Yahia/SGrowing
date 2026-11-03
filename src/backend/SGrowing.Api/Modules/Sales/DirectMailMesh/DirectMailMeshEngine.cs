using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Sales.DirectMailMesh;

public sealed class DirectMailMeshEngine
{
    private readonly ConcurrentDictionary<string, DirectMailCampaign> _campaigns = new();
    private readonly ConcurrentDictionary<string, DispatchedPackageRecord> _dispatches = new();
    private readonly ConcurrentBag<DispatchReceipt> _receiptsHistory = new();

    public DirectMailMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var c1 = new DirectMailCampaign(
            "CMP-DIR-01",
            "Fortune 500 CIO VIP Concierge Box",
            "ENTERPRISE_TIER_1",
            "Custom Hardcover Growth Playbook + Smart Ember Mug",
            145.00m,
            "OPPORTUNITY_QUALIFIED_DISCOVERY",
            "ACTIVE"
        );

        var c2 = new DirectMailCampaign(
            "CMP-DIR-02",
            "Mid-Market VP Engineering Welcome Kit",
            "GROWTH_TIER_2",
            "Bespoke Mechanical Keycap Set + Founder Letter",
            85.00m,
            "TECHNICAL_VALIDATION_PASSED",
            "ACTIVE"
        );

        _campaigns[c1.CampaignId] = c1;
        _campaigns[c2.CampaignId] = c2;

        var d1 = new DispatchedPackageRecord(
            "FDX-9988-7711-22",
            c1.CampaignId,
            "Sarah Jenkins (Chief Information Officer)",
            "Apex Global Financial",
            "FedEx Priority Overnight",
            "DELIVERED_SIGNED",
            DateTime.UtcNow.AddDays(-2)
        );

        var d2 = new DispatchedPackageRecord(
            "UPS-1122-3344-55",
            c2.CampaignId,
            "Marcus Vance (VP Engineering)",
            "CloudSphere Systems",
            "UPS 2nd Day Air",
            "IN_TRANSIT_OUT_FOR_DELIVERY",
            DateTime.UtcNow.AddDays(-1)
        );

        _dispatches[d1.TrackingNumber] = d1;
        _dispatches[d2.TrackingNumber] = d2;
    }

    public DirectMailOverview GetOverview()
    {
        var cmpList = _campaigns.Values.ToList();
        var dispList = _dispatches.Values.ToList();

        return new DirectMailOverview(
            cmpList.Count,
            dispList.Count,
            98.6,
            42.8,
            cmpList,
            dispList
        );
    }

    public DispatchReceipt DispatchCampaign(DispatchCampaignRequest request)
    {
        var tracking = $"FDX-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var dispatchId = $"DSP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var record = new DispatchedPackageRecord(
            tracking,
            request.CampaignId,
            request.RecipientExecutiveName,
            request.CompanyName,
            request.Carrier,
            "LABEL_CREATED_SCHEDULED_PICKUP",
            DateTime.UtcNow
        );

        _dispatches[tracking] = record;

        var receipt = new DispatchReceipt(
            dispatchId,
            tracking,
            request.Carrier,
            DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"),
            $"COMP-OK-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            DateTime.UtcNow
        );

        _receiptsHistory.Add(receipt);
        return receipt;
    }

    public GiftComplianceVerdict VerifyGiftCompliance(VerifyGiftComplianceRequest request)
    {
        var maxLimit = string.Equals(request.Jurisdiction, "US_GOV", StringComparison.OrdinalIgnoreCase) ? 25.00m : 250.00m;
        var isCompliant = request.GiftValueUsd <= maxLimit;

        return new GiftComplianceVerdict(
            request.RecipientCompany,
            isCompliant,
            maxLimit,
            isCompliant ? "CORPORATE_POLICY_SEC_7B_PASSED" : "VALUE_EXCEEDS_STATUTORY_MAX_THRESHOLD",
            $"AUD-GIFT-{Guid.NewGuid():N}"[..10].ToUpperInvariant()
        );
    }
}
