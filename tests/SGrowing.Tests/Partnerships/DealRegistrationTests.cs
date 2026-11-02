using SGrowing.Api.Modules.Partnerships.DealRegistration;
using Xunit;

namespace SGrowing.Tests.Partnerships;

public class DealRegistrationTests
{
    private readonly DealRegistrationEngine _engine = new();

    [Fact]
    public void RegisterDeal_NewProspect_Secures90DayExclusivityLock()
    {
        // Arrange
        var request = new RegisterPartnerDealRequest(
            PartnerId: "ptn_cloud_advisors",
            PartnerName: "CloudScale Advisors",
            Tier: PartnerTier.CertifiedSolutionProvider,
            ProspectCompanyName: "Helios Energy AG",
            ProspectDomain: "helios-energy.ch",
            ContactName: "Dr. Klaus Weber",
            ContactEmail: "klaus@helios-energy.ch",
            EstimatedArr: 200000m,
            SolutionScopeDescription: "Deployment of Growth OS across 4 subsidiaries"
        );

        // Act
        var result = _engine.RegisterDeal(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DealStatus.ApprovedLocked, result.Status);
        Assert.Equal(30000m, result.ProjectedPartnerCommission); // 15% of 200k
        Assert.Equal("helios-energy.ch", result.ProspectDomain);
    }

    [Fact]
    public void RegisterDeal_DuplicateDomain_TriggersCollisionRejection()
    {
        // Arrange: Domain already registered in seed
        var request = new RegisterPartnerDealRequest(
            PartnerId: "ptn_another_agency",
            PartnerName: "Other Agency",
            Tier: PartnerTier.RegisteredReferral,
            ProspectCompanyName: "Global Telecom Competitor",
            ProspectDomain: "globaltelecom.de", // Seeded domain
            ContactName: "Someone",
            ContactEmail: "test@globaltelecom.de",
            EstimatedArr: 150000m,
            SolutionScopeDescription: "Another try"
        );

        // Act
        var result = _engine.RegisterDeal(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DealStatus.CollisionRejected, result.Status);
        Assert.Equal(0m, result.ProjectedPartnerCommission);
    }

    [Fact]
    public void ApproveDeal_PendingDeal_UpdatesToApprovedLocked()
    {
        // Arrange
        var request = new ApproveDealRequest("deal_partner_702", "partner-ops@sgrowing.io");

        // Act
        var result = _engine.ApproveDeal(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DealStatus.ApprovedLocked, result.NewStatus);
        Assert.Contains("partner-ops@sgrowing.io", result.ConfirmationMessage);
    }
}
