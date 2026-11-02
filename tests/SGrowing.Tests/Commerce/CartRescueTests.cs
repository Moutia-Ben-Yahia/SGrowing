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
}
