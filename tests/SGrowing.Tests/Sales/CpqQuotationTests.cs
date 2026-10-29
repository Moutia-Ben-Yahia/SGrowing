using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Sales;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Sales;

public class CpqQuotationTests
{
    private static (SGrowingDbContext Db, CpqQuotationService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"SGrowing_CPQ_{Guid.NewGuid()}")
            .Options;
        var db = new SGrowingDbContext(options);
        var service = new CpqQuotationService(db);
        return (db, service);
    }

    [Fact]
    public async Task VolumeTierEvaluation_CalculatesCorrectDiscountedUnitPrice()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Standard enterprise price book has ENTERPRISE-CRM-LIC:
        // 1-9: $120.00 (BasePrice)
        // 10-49: $100.00
        // 50-99: $85.00
        // 100+: $70.00

        // Act 1: 5 units (below 10 -> Base price $120.00)
        var quote1 = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Acme Corporation",
            CustomerEmail: "procurement@acme.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Standard 30 Days",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 5)
            }
        ));

        // Act 2: 25 units (in 10-49 tier -> $100.00)
        var quote2 = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Acme Corporation",
            CustomerEmail: "procurement@acme.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Standard 30 Days",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 25)
            }
        ));

        // Act 3: 75 units (in 50-99 tier -> $85.00)
        var quote3 = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Acme Corporation",
            CustomerEmail: "procurement@acme.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Standard 30 Days",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 75)
            }
        ));

        // Act 4: 150 units (in 100+ tier -> $70.00)
        var quote4 = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Acme Corporation",
            CustomerEmail: "procurement@acme.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Standard 30 Days",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 150)
            }
        ));

        // Assert
        Assert.Equal(120.0m, quote1.LineItems[0].UnitPrice);
        Assert.Equal(600.0m, quote1.LineItems[0].LineTotal);

        Assert.Equal(100.0m, quote2.LineItems[0].UnitPrice);
        Assert.Equal(2500.0m, quote2.LineItems[0].LineTotal);

        Assert.Equal(85.0m, quote3.LineItems[0].UnitPrice);
        Assert.Equal(6375.0m, quote3.LineItems[0].LineTotal);

        Assert.Equal(70.0m, quote4.LineItems[0].UnitPrice);
        Assert.Equal(10500.0m, quote4.LineItems[0].LineTotal);
    }

    [Fact]
    public async Task MarginPercentageMath_CalculatesLineAndAggregateQuoteGrossMargin()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // ENTERPRISE-CRM-LIC: cost = 30.00, price for 10 units = 100.00.
        // Line Net = 10 * 100 = 1000. Line COGS = 10 * 30 = 300.
        // Line Margin% = (1000 - 300) / 1000 * 100 = 70.00%
        //
        // CUSTOM-INTEGRATION-HR: cost = 120.00, price for 5 units = 150.00.
        // Line Net = 5 * 150 = 750. Line COGS = 5 * 120 = 600.
        // Line Margin% = (750 - 600) / 750 * 100 = 20.00%
        //
        // Aggregate:
        // Total NetRevenue = 1000 + 750 = 1750.
        // Total COGS = 300 + 600 = 900.
        // Overall Margin% = (1750 - 900) / 1750 * 100 = 850 / 1750 * 100 = 48.57%

        // Act
        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "FinTech Global",
            CustomerEmail: "ops@fintech.io",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Standard Enterprise Terms",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(15),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 10),
                new("CUSTOM-INTEGRATION-HR", 5)
            }
        ));

        // Assert
        Assert.Equal(2, quote.LineItems.Count);
        Assert.Equal(70.0m, quote.LineItems[0].LineMarginPercentage);
        Assert.Equal(20.0m, quote.LineItems[1].LineMarginPercentage);

        Assert.Equal(1750.0m, quote.GrandTotal);
        Assert.Equal(48.57m, quote.OverallMarginPercentage);
        Assert.False(quote.RequiresManagerApproval); // Overall margin is 48.57% >= 25% policy threshold
    }

    [Fact]
    public async Task PolicyThresholdGuard_BlocksQuoteSending_WhenMarginBelowThreshold()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // CUSTOM-INTEGRATION-HR has base cost $120.00, base price $150.00.
        // Base margin is 20%, which is below the default 25% policy threshold!
        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Low Margin Client LLC",
            CustomerEmail: "buyer@lowmargin.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Net 30",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("CUSTOM-INTEGRATION-HR", 10) // 10 * 150 = 1500, cost 10 * 120 = 1200. Margin = 20.0% < 25.0%
            }
        ));

        // Assert: Flagged for manager approval
        Assert.True(quote.RequiresManagerApproval);
        Assert.Equal(20.0m, quote.OverallMarginPercentage);

        // Act & Assert: Attempting to send unapproved quote throws InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendQuoteAsync(tenantId, quote.Id));
        Assert.Contains("requires manager approval", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20.0%", ex.Message);
    }

    [Fact]
    public async Task PolicyThresholdGuard_FlagsWhenDiscountExceedsPolicy()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // ENTERPRISE-CRM-LIC has 70% margin, but customer requests 30% discount (exceeds 20% max discount limit)
        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Discount Demander Corp",
            CustomerEmail: "sales@demander.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Net 30",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 10, DiscountPercentage: 30.0m)
            }
        ));

        // Assert
        Assert.True(quote.RequiresManagerApproval);
        Assert.Equal(30.0m, quote.LineItems[0].DiscountPercentage);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendQuoteAsync(tenantId, quote.Id));
        Assert.Contains("requires manager approval", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManagerApprovalWorkflow_EnablesQuoteDispatch_AfterApproval()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Edge Case Corp",
            CustomerEmail: "finance@edgecase.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Standard Enterprise",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("CUSTOM-INTEGRATION-HR", 5) // Margin 20% < 25%
            }
        ));

        Assert.True(quote.RequiresManagerApproval);

        // Step 1: Request approval
        var requested = await service.RequestApprovalAsync(tenantId, quote.Id, "Strategic account expansion discount approved by VP");
        Assert.Equal(CpqQuoteStatus.PendingApproval, requested.Status);

        // Step 2: Manager approval
        var approved = await service.ApproveQuoteAsync(tenantId, quote.Id, "sarah.vp@sgrowing.io", "Approved for Q4 strategic pilot");
        Assert.Equal("sarah.vp@sgrowing.io", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAtUtc);
        Assert.False(approved.RequiresManagerApproval);

        // Step 3: Now sending to client succeeds
        var sent = await service.SendQuoteAsync(tenantId, quote.Id);
        Assert.Equal(CpqQuoteStatus.SentToClient, sent.Status);
    }

    [Fact]
    public async Task QuoteToOrderConversion_GeneratesConfirmedOrder_AndUpdatesDealToWon()
    {
        // Arrange
        var (db, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // Create a Deal in CRM
        var deal = Deal.Create(
            tenantId: tenantId,
            contactId: contactId,
            title: "Q4 Enterprise Expansion Deal",
            amount: 5000.0m,
            stage: DealStage.Proposal
        );
        db.Deals.Add(deal);
        await db.SaveChangesAsync();

        // Create and populate quote
        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "SaaS Scaleup Inc.",
            CustomerEmail: "billing@scaleup.io",
            DealId: deal.Id,
            Currency: "USD",
            TermsAndConditions: "Net 15 Days",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 20), // 20 * 100 = 2000
                new("AUTONOMOUS-AI-SEAT", 5)    // 5 * 210 = 1050
            }
        ));

        // Send to client
        await service.SendQuoteAsync(tenantId, quote.Id);

        // Act: Client accepts quote
        var result = await service.AcceptQuoteAsync(tenantId, quote.Id);

        // Assert: Conversion Result
        Assert.NotNull(result);
        Assert.Equal(quote.Id, result.QuoteId);
        Assert.Equal(deal.Id, result.DealId);
        Assert.Equal("Won", result.DealStage);
        Assert.Equal(3050.0m, result.TotalAmount);
        Assert.StartsWith("ORD-CPQ-", result.OrderNumber);

        // Assert: Deal stage updated in database
        var updatedDeal = await db.Deals.FindAsync(deal.Id);
        Assert.NotNull(updatedDeal);
        Assert.Equal(DealStage.Won, updatedDeal.Stage);
        Assert.Equal(3050.0m, updatedDeal.Amount);

        // Assert: Confirmed Order created in database
        var confirmedOrder = await db.CodOrders.FindAsync(result.OrderId);
        Assert.NotNull(confirmedOrder);
        Assert.Equal(CodOrderStatus.Confirmed, confirmedOrder.Status);
        Assert.Equal(3050.0m, confirmedOrder.TotalAmount);
        Assert.Contains("ENTERPRISE-CRM-LIC", confirmedOrder.ItemsJson);
    }

    [Fact]
    public async Task TamperEvidentChecksum_ValidatesHash_AndDetectsModifications()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Secure Defense Systems",
            CustomerEmail: "sec@defense.gov",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Strict DOD Clearance Required",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(60),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 10),
                new("DEDICATED-IP-POOL", 2)
            }
        ));

        // Act 1: Initial checksum verification
        var validation1 = service.ValidateChecksum(quote);
        Assert.True(validation1.IsValid);
        Assert.Equal(quote.ChecksumSha256, validation1.ComputedChecksum);

        // Act 2: Tamper with line item price without re-sealing
        var originalPrice = quote.LineItems[0].UnitPrice;
        quote.LineItems[0].UnitPrice = 1.00m; // Maliciously lowered price
        var validation2 = service.ValidateChecksum(quote);

        // Assert 2: Tamper detected
        Assert.False(validation2.IsValid);
        Assert.NotEqual(quote.ChecksumSha256, validation2.ComputedChecksum);

        // Restore price
        quote.LineItems[0].UnitPrice = originalPrice;
        Assert.True(service.ValidateChecksum(quote).IsValid);

        // Act 3: Tamper with Terms & Conditions
        quote.TermsAndConditions = "Malicious terms substituted!";
        var validation3 = service.ValidateChecksum(quote);
        Assert.False(validation3.IsValid);
    }

    [Fact]
    public async Task PdfPreview_GeneratesFullHtmlAndExpirationData()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: customerId,
            CustomerName: "Global Logistics Hub",
            CustomerEmail: "procurement@globallogistics.com",
            DealId: null,
            Currency: "USD",
            TermsAndConditions: "Payment within 30 days. Standard enterprise warranty applies.",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(25),
            LineItems: new List<QuoteLineItemInput>
            {
                new("ENTERPRISE-CRM-LIC", 15),
                new("DEDICATED-IP-POOL", 5)
            }
        ));

        // Act
        var preview = await service.GetPdfPreviewAsync(tenantId, quote.Id);

        // Assert
        Assert.NotNull(preview);
        Assert.Equal(quote.QuoteNumber, preview.QuoteNumber);
        Assert.Equal("SGrowing Technologies Inc.", preview.CompanyName);
        Assert.Equal(25, preview.DaysUntilExpiration);
        Assert.NotEmpty(preview.FormattedHtmlPreview);
        Assert.Contains(quote.ChecksumSha256, preview.FormattedHtmlPreview);
        Assert.Contains("ENTERPRISE-CRM-LIC", preview.FormattedHtmlPreview);
        Assert.Contains("DEDICATED-IP-POOL", preview.FormattedHtmlPreview);
    }

    [Fact]
    public async Task PriceBookCreation_SupportsCustomTiersAndMultiCurrency()
    {
        // Arrange
        var (_, service) = CreateService();
        var tenantId = Guid.NewGuid();

        // Act: Create EMEA Price Book in EUR with custom tiered pricing
        var created = await service.CreatePriceBookAsync(tenantId, new CreatePriceBookRequest(
            Name: "EMEA 2026 Tiered Price Book",
            Currency: "EUR",
            IsDefault: true,
            Entries: new List<PriceBookEntryDto>
            {
                new(
                    ProductSku: "EMEA-AI-SEAT",
                    ProductName: "EMEA AI Agent License",
                    BasePrice: 200.0m,
                    CostPrice: 50.0m,
                    VolumeTiers: new List<VolumeTierDto>
                    {
                        new(1, 9, 200.0m),
                        new(10, 49, 160.0m),
                        new(50, null, 120.0m)
                    }
                )
            }
        ));

        // Assert
        Assert.NotNull(created);
        Assert.Equal("EMEA 2026 Tiered Price Book", created.Name);
        Assert.Equal("EUR", created.Currency);
        Assert.True(created.IsDefault);
        Assert.Single(created.PriceBookEntries);

        // Verify quote created with this price book gets evaluated properly
        var quote = await service.CreateQuoteAsync(tenantId, new CreateQuoteRequest(
            TenantId: tenantId,
            CustomerId: Guid.NewGuid(),
            CustomerName: "Parisian Retailers",
            CustomerEmail: "achats@parisretail.fr",
            DealId: null,
            Currency: "EUR",
            TermsAndConditions: "30 jours fin de mois",
            ExpirationDateUtc: DateTime.UtcNow.AddDays(30),
            LineItems: new List<QuoteLineItemInput>
            {
                new("EMEA-AI-SEAT", 60) // in 50+ tier -> 120.0 EUR
            },
            PriceBookId: created.Id
        ));

        Assert.Equal(120.0m, quote.LineItems[0].UnitPrice);
        Assert.Equal(7200.0m, quote.GrandTotal);
        Assert.Equal(58.33m, quote.OverallMarginPercentage); // (7200 - 3000) / 7200 = 58.33%
    }
}
