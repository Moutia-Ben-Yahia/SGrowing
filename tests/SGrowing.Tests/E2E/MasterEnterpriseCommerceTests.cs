using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Api.Modules.Security;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterEnterpriseCommerceTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SGrowingDbContext _db;

    public MasterEnterpriseCommerceTests()
    {
        var services = new ServiceCollection();
        var dbName = "MasterCommerceDb_" + Guid.NewGuid();
        services.AddDbContext<SGrowingDbContext>(options => options.UseInMemoryDatabase(dbName));

        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<SGrowingDbContext>();
    }

    [Fact]
    public async Task MasterEnterpriseCommerceE2E_FullLifecycle_SucceedsSeamlessly()
    {
        var tenantId = Guid.NewGuid();

        // 1. Setup PIM Catalog & Webhook Subscribers
        var catalog = new ProductCatalogService(_db);
        var product = await catalog.CreateProductAsync(
            tenantId, "Enterprise Growth Plan", "growth-plan", "Omnichannel AI suite", ProductType.Subscription, 499m);
        var variant = await catalog.AddVariantAsync(
            tenantId, product.Id, "GRW-ANNUAL", "Annual B2B Plan", 4990m, inventoryStock: 100);

        var zapierSub = WebhookSubscription.Create(
            tenantId, "https://hooks.zapier.com/sgrowing/revenue", "zapier_secret_key", "DealWonEvent,OrderPaidEvent");
        await _db.WebhookSubscriptions.AddAsync(zapierSub);
        await _db.SaveChangesAsync();

        // 2. Ingest Abandoned Cart from Shopify Storefront
        var shopifyReceiver = new ShopifyWebhookReceiver(_db, NullLogger<ShopifyWebhookReceiver>.Instance);
        var cartToken = "cart_abandoned_e2e";
        var checkoutPayload = $@"{{
            ""id"": ""{cartToken}"",
            ""email"": ""director@enterprise.com"",
            ""total_price"": ""4990.00"",
            ""abandoned_checkout_url"": ""https://shop.sgrowing.com/checkouts/{cartToken}"",
            ""completed_at"": null
        }}";

        var webhookResult = await shopifyReceiver.ProcessWebhookAsync(tenantId, "checkouts/create", checkoutPayload);
        Assert.True(webhookResult.Success);

        // 3. Automated Abandoned Cart Recovery Scanner
        var recoveryService = new AbandonedCartRecoveryService(_db, NullLogger<AbandonedCartRecoveryService>.Instance);
        var cart = await _db.AbandonedCartRecords.FirstAsync(c => c.ExternalCartId == cartToken);
        typeof(AbandonedCartRecord).GetProperty("DetectedAtUtc")!.SetValue(cart, DateTime.UtcNow.AddHours(-3));
        await _db.SaveChangesAsync();

        var dispatchedRecovery = await recoveryService.TriggerRecoveryScanAsync(tenantId, TimeSpan.FromMinutes(60));
        Assert.Single(dispatchedRecovery);
        Assert.Equal("director@enterprise.com", dispatchedRecovery[0].CustomerEmail);

        // 4. In-Chat CPQ Quote Proposal Generation
        var contact = Contact.Create(tenantId, "Eleanor", "Vance", "director@enterprise.com", "+15554443333");
        var deal = Deal.Create(tenantId, contact.Id, "Enterprise Expansion Deal", 4990m, DealStage.Proposal);
        await _db.Contacts.AddAsync(contact);
        await _db.Deals.AddAsync(deal);
        await _db.SaveChangesAsync();

        var cpq = new InChatCpqService(_db, NullLogger<InChatCpqService>.Instance);
        var quote = await cpq.CreateQuoteAsync(
            tenantId,
            contact.Id,
            new List<CreateQuoteLineItemRequest>
            {
                new() { ProductId = product.Id, VariantId = variant.Id, Description = variant.Name, Quantity = 1, UnitPrice = 4990m }
            },
            dealId: deal.Id,
            discountPercent: 10m);

        Assert.Equal(4491m, quote.TotalAmount); // 4990 - 10% = 4491

        // 5. Customer Accepts Quote -> Deal Stage automatically closed WON!
        var acceptedQuote = await cpq.AcceptQuoteAsync(tenantId, quote.Id);
        Assert.Equal(QuoteStatus.Accepted, acceptedQuote.Status);

        var wonDeal = await _db.Deals.FindAsync(deal.Id);
        Assert.Equal(DealStage.Won, wonDeal!.Stage);

        // 6. Stripe Payment Webhook converts Quote to Order
        var stripeHandler = new StripePaymentWebhookHandler(_db, NullLogger<StripePaymentWebhookHandler>.Instance);
        var stripePayload = $@"{{
            ""type"": ""checkout.session.completed"",
            ""data"": {{
                ""object"": {{
                    ""amount_total"": 449100,
                    ""metadata"": {{
                        ""quoteId"": ""{quote.Id}""
                    }}
                }}
            }}
        }}";

        var stripeResult = await stripeHandler.ProcessStripeEventAsync(tenantId, stripePayload);
        Assert.True(stripeResult.Handled);
        Assert.Equal(4491m, stripeResult.AmountReceived);

        var finalizedQuote = await _db.QuoteProposals.FindAsync(quote.Id);
        Assert.Equal(QuoteStatus.ConvertedToOrder, finalizedQuote!.Status);

        // 7. Outbound Webhook Mesh Dispatches Events to Zapier
        var webhookDispatcher = new OutboundWebhookDispatcherService(_db, NullLogger<OutboundWebhookDispatcherService>.Instance);
        var dispatchedSubscribers = await webhookDispatcher.DispatchOutboundWebhooksAsync(
            tenantId, "DealWonEvent", $"{{\"dealId\":\"{deal.Id}\",\"amount\":4491}}");
        Assert.Equal(1, dispatchedSubscribers);

        // 8. Realtime Voice Duplex Session Update Command
        var voiceBridge = new OpenAiRealtimeBridgeService();
        var sessionCmd = voiceBridge.BuildSessionUpdateCommand(new RealtimeSessionConfig
        {
            Voice = "coral",
            Instructions = "SGrowing Realtime voice assistant"
        });
        Assert.Contains("session.update", sessionCmd);

        // 9. Customer invokes GDPR Right to be Forgotten
        var privacyService = new CustomerPrivacyService(_db, NullLogger<CustomerPrivacyService>.Instance);
        var erasureResult = await privacyService.ExecuteRightToBeForgottenAsync(tenantId, "director@enterprise.com");

        Assert.True(erasureResult.Success);
        Assert.Equal(1, erasureResult.ErasedContactsCount);

        var anonymizedContact = await _db.Contacts.FindAsync(contact.Id);
        Assert.Equal("Anonymized", anonymizedContact!.FirstName);
        Assert.Contains("anonymized_", anonymizedContact.Email);
    }
}
