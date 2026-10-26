using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class ProductCatalogAndCpqTests
{
    private readonly SGrowingDbContext _db;
    private readonly ProductCatalogService _catalog;
    private readonly InChatCpqService _cpq;
    private readonly StripePaymentWebhookHandler _stripeHandler;

    public ProductCatalogAndCpqTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _catalog = new ProductCatalogService(_db);
        _cpq = new InChatCpqService(_db);
        _stripeHandler = new StripePaymentWebhookHandler(_db);
    }

    [Fact]
    public async Task ProductCatalog_CreatesProductAndVariantsProperly()
    {
        var tenantId = Guid.NewGuid();
        var product = await _catalog.CreateProductAsync(
            tenantId,
            "SGrowing Omnicommunication Enterprise",
            "sgrowing-enterprise",
            "Full omnichannel suite with autonomous AI SDR and softphone",
            ProductType.Subscription,
            basePrice: 999m,
            currency: "USD");

        var variant = await _catalog.AddVariantAsync(
            tenantId,
            product.Id,
            sku: "SGR-ENT-50SEATS",
            name: "50 Seats / Annual Commitment",
            price: 11988m,
            inventoryStock: 50);

        Assert.NotNull(product);
        Assert.Equal("sgrowing-enterprise", product.Slug);
        Assert.Equal("SGR-ENT-50SEATS", variant.SKU);

        var products = await _catalog.GetProductsAsync(tenantId);
        Assert.Single(products);

        var variants = await _catalog.GetVariantsAsync(tenantId, product.Id);
        Assert.Single(variants);
    }

    [Fact]
    public async Task InChatCpq_CalculatesTotalsAndAcceptanceClosesDeal()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Pre-create deal
        var deal = Deal.Create(tenantId, contactId, "Global Expansion CRM Deal", 25000m, DealStage.Proposal);
        await _db.Deals.AddAsync(deal);
        await _db.SaveChangesAsync();

        var prodId = Guid.NewGuid();
        var lineItems = new List<CreateQuoteLineItemRequest>
        {
            new() { ProductId = prodId, Description = "SGrowing Core License", Quantity = 10, UnitPrice = 100m }, // 1000
            new() { ProductId = prodId, Description = "AI Voice Streams Add-on", Quantity = 2, UnitPrice = 250m }   // 500 -> subtotal = 1500
        };

        // 10% discount, 5% tax => 1500 - 150 = 1350, tax = 67.50 => total = 1417.50
        var quote = await _cpq.CreateQuoteAsync(
            tenantId,
            contactId,
            lineItems,
            dealId: deal.Id,
            discountPercent: 10m,
            taxPercent: 5m);

        Assert.Equal(1500m, quote.Subtotal);
        Assert.Equal(1417.50m, quote.TotalAmount);
        Assert.Equal(QuoteStatus.Draft, quote.Status);

        // Generate in-chat message
        var message = await _cpq.GenerateInChatQuoteMessageAsync(tenantId, quote.Id);
        Assert.Contains(quote.QuoteNumber, message);
        Assert.Contains("SGrowing Core License", message);
        Assert.Contains("1,417.50", message);
        Assert.Equal(QuoteStatus.Sent, quote.Status);

        // Customer accepts quote
        var accepted = await _cpq.AcceptQuoteAsync(tenantId, quote.Id);
        Assert.Equal(QuoteStatus.Accepted, accepted.Status);

        // Check Deal auto-closed WON!
        var refreshedDeal = await _db.Deals.FindAsync(deal.Id);
        Assert.Equal(DealStage.Won, refreshedDeal!.Stage);

        // Check Outbox event
        var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "QuoteAcceptedEvent");
        Assert.NotNull(outbox);
        Assert.Contains(quote.QuoteNumber, outbox.Payload);
    }

    [Fact]
    public async Task StripeWebhook_ProcessesCheckoutSessionAndMarksOrderPaid()
    {
        var tenantId = Guid.NewGuid();
        var quote = QuoteProposal.Create(tenantId, Guid.NewGuid(), "QT-2026-TEST", "USD");
        quote.TotalAmount = 500m;
        await _db.QuoteProposals.AddAsync(quote);
        await _db.SaveChangesAsync();

        var stripeJson = $@"{{
            ""type"": ""checkout.session.completed"",
            ""data"": {{
                ""object"": {{
                    ""amount_total"": 50000,
                    ""metadata"": {{
                        ""quoteId"": ""{quote.Id}""
                    }}
                }}
            }}
        }}";

        var result = await _stripeHandler.ProcessStripeEventAsync(tenantId, stripeJson);

        Assert.True(result.Handled);
        Assert.Equal("QT-2026-TEST", result.OrderId);
        Assert.Equal(500m, result.AmountReceived);

        var refreshedQuote = await _db.QuoteProposals.FindAsync(quote.Id);
        Assert.Equal(QuoteStatus.ConvertedToOrder, refreshedQuote!.Status);

        var outbox = await _db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "OrderPaidEvent");
        Assert.NotNull(outbox);
        Assert.Contains("500", outbox.Payload);
    }
}
