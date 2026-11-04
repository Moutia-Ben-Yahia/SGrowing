using SGrowing.Api.Modules.Growth.Affiliates;
using Xunit;

namespace SGrowing.Tests.Growth;

public class AffiliateLoyaltyTests
{
    private readonly IAffiliateLoyaltyEngine _engine = new AffiliateLoyaltyEngine();

    [Fact]
    public void ListAffiliates_ReturnsSeededPartners()
    {
        var partners = _engine.ListAffiliates();

        Assert.NotEmpty(partners);
        Assert.Contains(partners, p => p.ReferralCode == "ALPHA20");
        Assert.Contains(partners, p => p.ParentPartnerId != null);
    }

    [Fact]
    public void RegisterAffiliate_CreatesValidPartnerWithCode()
    {
        var request = new CreateAffiliateRequest(
            Name: "Growth Hackers Weekly",
            Email: "team@growthweekly.io",
            Tier1RatePercent: 25.0m,
            Tier2RatePercent: 7.5m
        );

        var partner = _engine.RegisterAffiliate(request);

        Assert.NotNull(partner);
        Assert.Equal("Growth Hackers Weekly", partner.Name);
        Assert.NotEmpty(partner.ReferralCode);
        Assert.Equal(25.0m, partner.Tier1RatePercent);
    }

    [Fact]
    public void ProcessReferralConversion_CalculatesMultiTierCommissionsAndLoyaltyPoints()
    {
        var request = new TrackConversionRequest(
            ReferralCode: "DEVREL10",
            CustomerId: "cust_beta_corp",
            CustomerEmail: "finance@betacorp.com",
            OrderAmountUsd: 1000.00m
        );

        var result = _engine.ProcessReferralConversion(request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(150.00m, result.DirectCommissionUsd); // 15% of $1000
        Assert.Equal(50.00m, result.ParentCommissionUsd);  // 5% parent tier 2 commission
        Assert.Equal(1000, result.AwardedLoyaltyPoints);

        var loyalty = _engine.GetOrCreateLoyaltyAccount("finance@betacorp.com");
        Assert.Equal(1000, loyalty.TotalPoints);
        Assert.Equal(1000.00m, loyalty.LifetimeSpendUsd);
    }

    [Fact]
    public void ProcessReferralConversion_BlocksSelfReferral()
    {
        var request = new TrackConversionRequest(
            ReferralCode: "ALPHA20",
            CustomerId: "cust_self",
            CustomerEmail: "partners@alphagrowth.io", // Matches partner email!
            OrderAmountUsd: 500.00m
        );

        Assert.Throws<InvalidOperationException>(() => _engine.ProcessReferralConversion(request));
    }

    [Fact]
    public void ProcessReferralConversion_ThrowsKeyNotFoundException_WhenReferralCodeDoesNotExist()
    {
        var request = new TrackConversionRequest(
            ReferralCode: "NONEXISTENT_99",
            CustomerId: "cust_unknown",
            CustomerEmail: "buyer@unknown.com",
            OrderAmountUsd: 250.00m
        );

        var ex = Assert.Throws<KeyNotFoundException>(() => _engine.ProcessReferralConversion(request));
        Assert.Contains("NONEXISTENT_99", ex.Message);
    }

    [Fact]
    public void ProcessReferralConversion_AdvancesLoyaltyTier_AcrossSilverGoldAndPlatinum()
    {
        var testEmail = $"vip_{Guid.NewGuid():N}@enterprise.com";

        // Bronze -> Silver (>= $5,000)
        _engine.ProcessReferralConversion(new TrackConversionRequest(
            ReferralCode: "ALPHA20",
            CustomerId: "cust_vip",
            CustomerEmail: testEmail,
            OrderAmountUsd: 6000.00m
        ));
        var accountSilver = _engine.GetOrCreateLoyaltyAccount(testEmail);
        Assert.Equal(LoyaltyTier.Silver, accountSilver.Tier);

        // Silver -> Gold (>= $15,000 total: 6,000 + 10,000 = 16,000)
        _engine.ProcessReferralConversion(new TrackConversionRequest(
            ReferralCode: "ALPHA20",
            CustomerId: "cust_vip",
            CustomerEmail: testEmail,
            OrderAmountUsd: 10000.00m
        ));
        var accountGold = _engine.GetOrCreateLoyaltyAccount(testEmail);
        Assert.Equal(LoyaltyTier.Gold, accountGold.Tier);

        // Gold -> Platinum (>= $50,000 total: 16,000 + 35,000 = 51,000)
        _engine.ProcessReferralConversion(new TrackConversionRequest(
            ReferralCode: "ALPHA20",
            CustomerId: "cust_vip",
            CustomerEmail: testEmail,
            OrderAmountUsd: 35000.00m
        ));
        var accountPlatinum = _engine.GetOrCreateLoyaltyAccount(testEmail);
        Assert.Equal(LoyaltyTier.Platinum, accountPlatinum.Tier);
        Assert.Equal(51000, accountPlatinum.TotalPoints);
    }

    [Fact]
    public void RunAntiFraudAudit_ReturnsAuditedReportWithComplianceWarnings()
    {
        var report = _engine.RunAntiFraudAudit();

        Assert.NotNull(report);
        Assert.True(report.TotalTransactionsAudited >= 12);
        Assert.Equal(0, report.FlaggedCount);
        Assert.NotEmpty(report.AuditWarnings);
        Assert.Contains(report.AuditWarnings, w => w.Contains("circular parent-child"));
    }

    [Fact]
    public void RegisterAffiliate_AssignsParentPartnerIdAndTracksInList()
    {
        var parentRequest = new CreateAffiliateRequest(
            Name: "Master Syndicate",
            Email: "syndicate@network.io",
            Tier1RatePercent: 20.0m,
            Tier2RatePercent: 4.0m
        );
        var parent = _engine.RegisterAffiliate(parentRequest);

        var childRequest = new CreateAffiliateRequest(
            Name: "Sub Influencer Node",
            Email: "node@network.io",
            Tier1RatePercent: 12.0m,
            Tier2RatePercent: 0.0m,
            ParentPartnerId: parent.PartnerId
        );
        var child = _engine.RegisterAffiliate(childRequest);

        Assert.NotNull(child.ParentPartnerId);
        Assert.Equal(parent.PartnerId, child.ParentPartnerId);

        var list = _engine.ListAffiliates();
        Assert.Contains(list, a => a.PartnerId == child.PartnerId);
    }
}
