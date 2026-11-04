using System.Collections.Generic;
using SGrowing.Api.Modules.Commerce.CartRescue;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CartRescueTests
{
    private readonly CartRescueEngine _engine = new();

    [Fact]
    public void EvaluateCart_HighValueHighMargin_RecommendsGiftBundle()
    {
        // Arrange
        var request = new EvaluateCartRescueRequest(
            CartId: "cart_eval_99",
            CustomerEmail: "buyer@enterprise.corp",
            CustomerPhone: "+14155552671",
            Items: new List<CartItem>
            {
                new("SKU-CHAIR-01", "Ergonomic Mesh Chair", 2, 350.00m, 62.0m)
            },
            MinutesSinceAbandonment: 45
        );

        // Act
        var result = _engine.EvaluateCart(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(700.00m, result.TotalCartValue);
        Assert.Equal(IncentiveStrategy.HighMarginFreeGiftBundle, result.RecommendedIncentive);
        Assert.Equal("WhatsApp", result.PreferredChannel);
        Assert.True(result.ExpectedGrossProfitLift > 200m);
    }

    [Fact]
    public void GetActiveRescues_ReturnsSeededPipelines()
    {
        // Act
        var rescues = _engine.GetActiveRescues();

        // Assert
        Assert.NotNull(rescues);
        Assert.True(rescues.Count >= 2);
        Assert.Contains(rescues, r => r.CurrentStatus == "Recovered");
    }

    [Fact]
    public void DispatchNudge_ValidRescue_UpdatesStatusAndGeneratesMessage()
    {
        // Arrange
        var request = new DispatchCartNudgeRequest("rescue_cart_8801", "WhatsApp");

        // Act
        var result = _engine.DispatchNudge(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Dispatched);
        Assert.Contains("rescue_cart_8801", result.RescueId);
        Assert.Contains("FreeExpeditedShipping", result.OutboundMessageText);
    }

    [Fact]
    public void EvaluateCart_MidValueCart_RecommendsFreeExpeditedShipping()
    {
        var request = new EvaluateCartRescueRequest(
            CartId: "cart_mid_val",
            CustomerEmail: "shopper@domain.com",
            CustomerPhone: "+21698000111",
            Items: new List<CartItem>
            {
                new("SKU-LAMP-01", "Nordic Floor Lamp", 1, 260.00m, 45.0m)
            },
            MinutesSinceAbandonment: 15
        );

        var result = _engine.EvaluateCart(request);

        Assert.NotNull(result);
        Assert.Equal(260.00m, result.TotalCartValue);
        Assert.Equal(IncentiveStrategy.FreeExpeditedShipping, result.RecommendedIncentive);
        Assert.Equal("WhatsApp", result.PreferredChannel);
        Assert.Contains("Guaranteed Priority Courier", result.IncentiveDescription);
    }

    [Fact]
    public void EvaluateCart_RecentAbandonmentUnder30Min_RecommendsZeroDiscountValueNudge()
    {
        var request = new EvaluateCartRescueRequest(
            CartId: "cart_low_recent",
            CustomerEmail: "recent@domain.com",
            CustomerPhone: null, // No phone -> Email
            Items: new List<CartItem>
            {
                new("SKU-BOOK-01", "AI Strategy Book", 2, 45.00m, 60.0m)
            },
            MinutesSinceAbandonment: 12
        );

        var result = _engine.EvaluateCart(request);

        Assert.NotNull(result);
        Assert.Equal(90.00m, result.TotalCartValue);
        Assert.Equal(IncentiveStrategy.ZeroDiscountValueNudge, result.RecommendedIncentive);
        Assert.Equal("Email", result.PreferredChannel);
        Assert.Contains("Low-Stock Reservation", result.IncentiveDescription);
    }

    [Fact]
    public void EvaluateCart_LongerAbandonment_RecommendsTimeLockedCashbackEscrow()
    {
        var request = new EvaluateCartRescueRequest(
            CartId: "cart_low_older",
            CustomerEmail: "older@domain.com",
            CustomerPhone: "+21650112233",
            Items: new List<CartItem>
            {
                new("SKU-SHIRT-01", "Linen Shirt", 1, 80.00m, 50.0m)
            },
            MinutesSinceAbandonment: 90
        );

        var result = _engine.EvaluateCart(request);

        Assert.NotNull(result);
        Assert.Equal(80.00m, result.TotalCartValue);
        Assert.Equal(IncentiveStrategy.TimeLockedCashbackEscrow, result.RecommendedIncentive);
        Assert.Contains("Instant Wallet Credit", result.IncentiveDescription);
    }

    [Fact]
    public void DispatchNudge_UnknownRescueId_ThrowsKeyNotFoundException()
    {
        var request = new DispatchCartNudgeRequest("non_existent_rescue_id", "Email");
        Assert.Throws<KeyNotFoundException>(() => _engine.DispatchNudge(request));
    }

    [Fact]
    public void EvaluateCart_EmptyItemsList_EvaluatesWithZeroTotalAndDefaultMargin()
    {
        var request = new EvaluateCartRescueRequest(
            CartId: "cart_empty_01",
            CustomerEmail: "empty@example.com",
            CustomerPhone: "+1234567890",
            Items: new List<CartItem>(),
            MinutesSinceAbandonment: 10
        );

        var result = _engine.EvaluateCart(request);

        Assert.NotNull(result);
        Assert.Equal(0.00m, result.TotalCartValue);
        Assert.Equal(IncentiveStrategy.ZeroDiscountValueNudge, result.RecommendedIncentive);
        Assert.Equal(0.00m, result.ExpectedGrossProfitLift);
    }
}
