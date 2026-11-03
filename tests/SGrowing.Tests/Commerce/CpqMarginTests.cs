using SGrowing.Api.Modules.Commerce.CpqMargin;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CpqMarginTests
{
    private readonly CpqMarginEngine _engine = new();

    [Fact]
    public void SeededQuote_ReturnsHealthyMarginAndRepApproved()
    {
        var quote = _engine.GetQuoteById("quote_ent_901");

        Assert.NotNull(quote);
        Assert.Equal("AeroDynamics Global", quote.CustomerName);
        Assert.True(quote.BlendedGrossMarginPercentage >= 75.0);
        Assert.Equal(DiscountApprovalTier.StandardRepApproved, quote.ApprovalTier);
        Assert.NotEmpty(quote.CryptographicAuditHash);
        Assert.Equal(3, quote.YearRamps.Count);
    }

    [Fact]
    public void AggressiveDiscountBreachingMarginHurdle_IsRejected()
    {
        var request = new QuotePricingRequest(
            "quote_unprofitable",
            "BrokeCorp",
            1,
            50000m,
            60.0m, // 60% discount -> 20k Net ACV
            18000m, // COGS = 23k -> Negative profit!
            5000m
        );

        var result = _engine.CalculateQuotePricing(request);

        Assert.Equal(DiscountApprovalTier.RejectedMarginBreach, result.ApprovalTier);
        Assert.Contains("Rejected", result.GuardrailRecommendation);
    }

    [Fact]
    public void ModerateHighDiscount_RequiresVpOrCfoApproval()
    {
        var request = new QuotePricingRequest(
            "quote_vp_tier",
            "BigScale Corp",
            3,
            200000m,
            22.0m, // 22% discount -> VP Sales
            25000m,
            5000m
        );

        var result = _engine.CalculateQuotePricing(request);

        Assert.Equal(DiscountApprovalTier.VpSalesApproved, result.ApprovalTier);
        Assert.True(result.BlendedGrossMarginPercentage >= 75.0);
    }
}
