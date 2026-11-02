using SGrowing.Api.Modules.Growth.Affiliates;

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
}
