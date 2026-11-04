using FluentAssertions;
using SGrowing.Api.Modules.Sales.DirectMailMesh;
using Xunit;

namespace SGrowing.Tests.Sales;

public class DirectMailMeshTests
{
    private readonly DirectMailMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsCampaignsAndDispatchedPackages()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveCampaignsCount.Should().Be(2);
        overview.PackagesDispatchedCount.Should().Be(2);
        overview.DeliverySuccessRatePct.Should().BeGreaterThan(95.0);
        overview.PipelineInfluenceRatePct.Should().BeGreaterThan(30.0);
        overview.ActiveCampaigns.Should().HaveCount(2);
        overview.RecentDispatches.Should().HaveCount(2);
    }

    [Fact]
    public void DispatchCampaign_CreatesTrackingNumberAndReceipt()
    {
        var request = new DispatchCampaignRequest(
            "CMP-DIR-01",
            "Elena Rostova (CTO)",
            "Vanguard Logistics AI",
            "742 Evergreen Terrace, Palo Alto, CA",
            "FedEx Priority Overnight",
            "Looking forward to connecting on enterprise AI pipelines."
        );

        var receipt = _engine.DispatchCampaign(request);

        receipt.Should().NotBeNull();
        receipt.DispatchId.Should().StartWith("DSP-");
        receipt.TrackingNumber.Should().StartWith("FDX-");
        receipt.Carrier.Should().Be("FedEx Priority Overnight");
        receipt.ComplianceVerificationCode.Should().StartWith("COMP-OK-");
    }

    [Fact]
    public void VerifyGiftCompliance_CorporatePolicyPassesUnder250()
    {
        var request = new VerifyGiftComplianceRequest(
            "Apex Global Financial",
            "Chief Information Officer",
            145.00m,
            "US_COMMERCIAL"
        );

        var verdict = _engine.VerifyGiftCompliance(request);

        verdict.Should().NotBeNull();
        verdict.IsCompliant.Should().BeTrue();
        verdict.MaxAllowableValueUsd.Should().Be(250.00m);
        verdict.ApplicablePolicyRule.Should().Contain("PASSED");
    }

    [Fact]
    public void VerifyGiftCompliance_UsGovJurisdiction_EnforcesStrict25DollarLimit()
    {
        var compliantReq = new VerifyGiftComplianceRequest(
            "Department of Defense",
            "Program Director",
            20.00m,
            "US_GOV"
        );
        var compliantVerdict = _engine.VerifyGiftCompliance(compliantReq);

        compliantVerdict.Should().NotBeNull();
        compliantVerdict.IsCompliant.Should().BeTrue();
        compliantVerdict.MaxAllowableValueUsd.Should().Be(25.00m);

        var nonCompliantReq = new VerifyGiftComplianceRequest(
            "Department of Transportation",
            "Inspector",
            50.00m,
            "US_GOV"
        );
        var nonCompliantVerdict = _engine.VerifyGiftCompliance(nonCompliantReq);

        nonCompliantVerdict.Should().NotBeNull();
        nonCompliantVerdict.IsCompliant.Should().BeFalse();
        nonCompliantVerdict.MaxAllowableValueUsd.Should().Be(25.00m);
        nonCompliantVerdict.ApplicablePolicyRule.Should().Be("VALUE_EXCEEDS_STATUTORY_MAX_THRESHOLD");
    }

    [Fact]
    public void DispatchCampaign_AppendsToRecentDispatches()
    {
        var request = new DispatchCampaignRequest(
            "CMP-DIR-02",
            "Marcus Vance",
            "CloudSphere",
            "123 Tech Way",
            "UPS 2nd Day Air",
            "Personal note"
        );

        var receipt = _engine.DispatchCampaign(request);

        var overview = _engine.GetOverview();
        overview.RecentDispatches.Should().Contain(d => d.TrackingNumber == receipt.TrackingNumber);
    }

    [Fact]
    public void VerifyGiftCompliance_CommercialJurisdiction_RejectsWhenValueExceeds250()
    {
        var request = new VerifyGiftComplianceRequest(
            "Acme Luxury Retail",
            "VP Merchandising",
            320.00m,
            "US_COMMERCIAL"
        );

        var verdict = _engine.VerifyGiftCompliance(request);

        verdict.Should().NotBeNull();
        verdict.IsCompliant.Should().BeFalse();
        verdict.MaxAllowableValueUsd.Should().Be(250.00m);
        verdict.ApplicablePolicyRule.Should().Be("VALUE_EXCEEDS_STATUTORY_MAX_THRESHOLD");
        verdict.AuditReceiptId.Should().StartWith("AUD-GIFT-");
    }

    [Fact]
    public void DispatchCampaign_WithCustomCarrier_AssignsExactCarrierToRecordAndReceipt()
    {
        var request = new DispatchCampaignRequest(
            "CMP-DIR-01",
            "Nadia Mansour (CMO)",
            "Fintech Apex",
            "Avenue Habib Bourguiba, Tunis",
            "DHL Express Worldwide",
            "Welcome to the high-growth acceleration program."
        );

        var receipt = _engine.DispatchCampaign(request);

        receipt.Carrier.Should().Be("DHL Express Worldwide");
        receipt.EstimatedDeliveryDate.Should().NotBeNullOrWhiteSpace();

        var overview = _engine.GetOverview();
        var record = overview.RecentDispatches.FirstOrDefault(d => d.TrackingNumber == receipt.TrackingNumber);
        record.Should().NotBeNull();
        record!.Carrier.Should().Be("DHL Express Worldwide");
        record.RecipientExecutiveName.Should().Be("Nadia Mansour (CMO)");
    }

    [Fact]
    public void GetOverview_IncludesSeededCampaignAttributes()
    {
        var overview = _engine.GetOverview();

        overview.ActiveCampaigns.Should().Contain(c => c.CampaignId == "CMP-DIR-01" && c.UnitCostUsd == 145.00m);
        overview.ActiveCampaigns.Should().Contain(c => c.CampaignId == "CMP-DIR-02" && c.TriggerStage == "TECHNICAL_VALIDATION_PASSED");
    }
}
