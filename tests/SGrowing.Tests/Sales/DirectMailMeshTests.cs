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
}
