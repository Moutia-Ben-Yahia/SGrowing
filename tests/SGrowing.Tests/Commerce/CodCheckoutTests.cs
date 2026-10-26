using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CodCheckoutTests
{
    private readonly SGrowingDbContext _db;
    private readonly CodCheckoutService _service;

    public CodCheckoutTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new CodCheckoutService(_db);
    }

    [Fact]
    public async Task GetWilayas_ReturnsCascadedGovernoratesAndDelegations()
    {
        var wilayas = await _service.GetWilayasAsync("TN");

        Assert.NotEmpty(wilayas);
        Assert.Contains(wilayas, w => w.NameFr == "Tunis");
        Assert.Contains(wilayas, w => w.NameFr == "Sousse");

        var tunis = wilayas.First(w => w.NameFr == "Tunis");
        Assert.Contains("La Marsa", tunis.DelegationsJson);
    }

    [Fact]
    public async Task CompleteCodCheckoutLifecycle_WithOtpVerificationAndUpsell_ShouldPlaceOrder()
    {
        var tenantId = Guid.NewGuid();
        var cartJson = "[{\"sku\":\"PERFUME-ROYAL-AMBER\",\"qty\":1,\"price\":89.0}]";

        // 1. Create Checkout Session
        var session = await _service.CreateSessionAsync(tenantId, cartJson, subtotal: 89.0m);
        Assert.NotNull(session);
        Assert.StartsWith("chk_", session.SessionKey);

        // 2. Customer fills address with Wilaya cascade
        var updated = await _service.UpdateCustomerDetailsAsync(
            tenantId,
            session.SessionKey,
            "Mohamed Trabelsi",
            "+21698112233",
            "TN-11",
            "La Marsa",
            "Avenue Habib Bourguiba, Résidence Les Palmiers"
        );
        Assert.Equal("Mohamed Trabelsi", updated.CustomerName);
        Assert.Equal(7m, updated.ShippingFee);
        Assert.Equal(96.0m, updated.TotalAmount); // 89 + 7

        // 3. Send and verify Phone OTP
        var otp = await _service.SendPhoneOtpAsync(tenantId, session.SessionKey);
        Assert.False(string.IsNullOrEmpty(otp));
        Assert.Equal(4, otp.Length);

        var isVerified = await _service.VerifyPhoneOtpAsync(tenantId, session.SessionKey, otp);
        Assert.True(isVerified);

        // 4. Accept 1-Click Post-Purchase Upsell
        var withUpsell = await _service.ApplyUpsellOfferAsync(tenantId, session.SessionKey, "OUD-TRAVEL-ATOMIZER-10ML", 25.0m);
        Assert.Equal("OUD-TRAVEL-ATOMIZER-10ML", withUpsell.UpsellOfferAcceptedSku);
        Assert.Equal(121.0m, withUpsell.TotalAmount); // 89 + 7 + 25

        // 5. Complete COD Order
        var confirmation = await _service.CompleteCodOrderAsync(tenantId, session.SessionKey);
        Assert.NotNull(confirmation);
        Assert.StartsWith("COD-", confirmation.OrderNumber);
        Assert.Equal(121.0m, confirmation.TotalAmountDueOnDoorstep);
        Assert.Equal("Confirmed", confirmation.Status);
    }
}
