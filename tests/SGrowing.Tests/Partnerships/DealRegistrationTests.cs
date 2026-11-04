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

    [Fact]
    public void RegisterDeal_NullRequest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.RegisterDeal(null!));
    }

    [Fact]
    public void RegisterDeal_RegisteredReferralTier_Calculates10PercentCommission()
    {
        var request = new RegisterPartnerDealRequest(
            PartnerId: "ptn_ref_01",
            PartnerName: "Referral Partner",
            Tier: PartnerTier.RegisteredReferral,
            ProspectCompanyName: "Alpha Beta Biotech",
            ProspectDomain: "alphabetabiotech.ch",
            ContactName: "Sarah Connor",
            ContactEmail: "sarah@alphabetabiotech.ch",
            EstimatedArr: 100000m,
            SolutionScopeDescription: "Referral intake test"
        );

        var result = _engine.RegisterDeal(request);

        Assert.NotNull(result);
        Assert.Equal(DealStatus.ApprovedLocked, result.Status);
        Assert.Equal(10000m, result.ProjectedPartnerCommission); // 10%
    }

    [Fact]
    public void RegisterDeal_PremierGlobalIntegratorTier_Calculates25PercentCommission()
    {
        var request = new RegisterPartnerDealRequest(
            PartnerId: "ptn_premier_02",
            PartnerName: "PwC Enterprise Cloud",
            Tier: PartnerTier.PremierGlobalIntegrator,
            ProspectCompanyName: "Omega FinTech Holdings",
            ProspectDomain: "omegafintech.uk",
            ContactName: "James Bond",
            ContactEmail: "jbond@omegafintech.uk",
            EstimatedArr: 500000m,
            SolutionScopeDescription: "Global roll-out integration"
        );

        var result = _engine.RegisterDeal(request);

        Assert.NotNull(result);
        Assert.Equal(DealStatus.ApprovedLocked, result.Status);
        Assert.Equal(125000m, result.ProjectedPartnerCommission); // 25% of 500k
    }

    [Fact]
    public void RegisterDeal_NormalizesDomainCaseAndWhitespace()
    {
        var request1 = new RegisterPartnerDealRequest(
            PartnerId: "ptn_norm_01",
            PartnerName: "Norm Agency",
            Tier: PartnerTier.RegisteredReferral,
            ProspectCompanyName: "Unique Enterprise",
            ProspectDomain: "  Unique-Enterprise.Com  ",
            ContactName: "Alice",
            ContactEmail: "alice@unique-enterprise.com",
            EstimatedArr: 50000m,
            SolutionScopeDescription: "Normalization test"
        );

        var result1 = _engine.RegisterDeal(request1);
        Assert.Equal("unique-enterprise.com", result1.ProspectDomain);
        Assert.Equal(DealStatus.ApprovedLocked, result1.Status);

        // Attempt second registration with mixed case
        var request2 = request1 with { ProspectDomain = "UNIQUE-ENTERPRISE.COM" };
        var result2 = _engine.RegisterDeal(request2);
        Assert.Equal(DealStatus.CollisionRejected, result2.Status);
    }

    [Fact]
    public void ApproveDeal_NullRequest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.ApproveDeal(null!));
    }

    [Fact]
    public void ApproveDeal_NonExistentDealId_ThrowsKeyNotFoundException()
    {
        var request = new ApproveDealRequest("deal_nonexistent_999", "admin@sgrowing.io");
        Assert.Throws<KeyNotFoundException>(() => _engine.ApproveDeal(request));
    }

    [Fact]
    public void GetAllDeals_ReturnsSeededAndNewlyRegisteredDealsInDescendingOrder()
    {
        var deals = _engine.GetAllDeals();
        Assert.NotEmpty(deals);
        Assert.True(deals.Count >= 2);

        // Ensure descending order by RegisteredAtUtc
        for (int i = 0; i < deals.Count - 1; i++)
        {
            Assert.True(deals[i].RegisteredAtUtc >= deals[i + 1].RegisteredAtUtc);
        }
    }
}
