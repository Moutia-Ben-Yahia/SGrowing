using FluentAssertions;
using SGrowing.Api.Modules.Sales.Proposals;
using Xunit;

namespace SGrowing.Tests.Sales;

public class B2BProposalTests
{
    private readonly B2BProposalEngine _engine = new();
    private const string TenantId = "tenant-enterprise-01";
    private const string CustomerCompany = "Acme Technologies Inc.";
    private const string CustomerEmail = "procurement@acme-tech.com";

    [Fact]
    public void CalculateQuote_WithValidItemsAndDiscount_ComputesSubtotalDiscountTaxAndMarginCorrectly()
    {
        // Arrange: 
        // Item 1: 10 units @ $100 = $1,000, 10% line discount ($100), net $900, cost $50/unit ($500)
        // Item 2: 5 units @ $200 = $1,000, 0% discount, net $1,000, cost $80/unit ($400)
        var items = new List<QuoteLineItem>
        {
            new("SKU-CORE", "Core Growth License", 10, 100m, 10m, 900m, 50m),
            new("SKU-ADDON", "AI Workflow Addon", 5, 200m, 0m, 1000m, 80m)
        };

        // Custom discount: 5% on remaining after line discount ($1,900 * 5% = $95)
        var request = new CalculateQuoteRequest(TenantId, CustomerCompany, items, CustomDiscountPercent: 5m);

        // Act
        var result = _engine.CalculateQuote(request);

        // Assert
        result.Subtotal.Should().Be(2000m);
        result.DiscountTotal.Should().Be(195m); // 100 line discount + 95 custom discount
        var netPreTax = 2000m - 195m; // 1805m
        result.TaxAmount.Should().Be(Math.Round(netPreTax * 0.10m, 2)); // 180.50m
        result.TotalAmount.Should().Be(netPreTax + result.TaxAmount); // 1985.50m
        result.TotalCost.Should().Be(900m); // 10*50 + 5*80

        var grossProfit = netPreTax - 900m; // 905m
        var expectedMargin = Math.Round((grossProfit / netPreTax) * 100m, 2); // 50.14%
        result.MarginPercent.Should().Be(expectedMargin);
        result.MarginApproved.Should().BeTrue();
        result.MarginWarning.Should().BeEmpty();
    }

    [Fact]
    public void CalculateQuote_WhenMarginBelow35PercentFloor_EnforcesGuardrailAndSetsWarning()
    {
        // Arrange: High-cost low-price scenario
        // 10 units @ $100 = $1,000, cost $75/unit ($750), 10% discount = $900 net
        // Margin = (900 - 750) / 900 = 16.67% (< 35%)
        var items = new List<QuoteLineItem>
        {
            new("SKU-HARDWARE", "Edge Sensor Unit", 10, 100m, 10m, 900m, 75m)
        };

        var request = new CalculateQuoteRequest(TenantId, CustomerCompany, items, CustomDiscountPercent: 0m);

        // Act
        var result = _engine.CalculateQuote(request);

        // Assert
        result.MarginPercent.Should().Be(16.67m);
        result.MarginApproved.Should().BeFalse();
        result.MarginWarning.Should().Contain("below the required floor threshold of 35.00%");
    }

    [Fact]
    public void CreateProposal_StoresDraftProposal_AndRetrievesViaGetProposal()
    {
        // Arrange
        var items = new List<QuoteLineItem>
        {
            new("SKU-SaaS-ENT", "Enterprise Growth Subscription", 12, 500m, 0m, 6000m, 150m)
        };

        // Act
        var proposal = _engine.CreateProposal(TenantId, CustomerCompany, CustomerEmail, items, discountPercent: 0m);

        // Assert
        proposal.Should().NotBeNull();
        proposal.Id.Should().NotBeEmpty();
        proposal.TenantId.Should().Be(TenantId);
        proposal.CustomerCompany.Should().Be(CustomerCompany);
        proposal.CustomerEmail.Should().Be(CustomerEmail);
        proposal.Status.Should().Be(ProposalStatus.Draft);
        proposal.SignedPdfHash.Should().BeNull();
        proposal.SignerName.Should().BeNull();
        proposal.SignedAtUtc.Should().BeNull();
        proposal.TotalAmount.Should().Be(6600m); // 6000 + 10% VAT (600)

        var retrieved = _engine.GetProposal(TenantId, proposal.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(proposal.Id);
        retrieved.CustomerCompany.Should().Be(CustomerCompany);
    }

    [Fact]
    public void SignProposal_GeneratesSha256DigitalSeal_AndTransitionsToSigned()
    {
        // Arrange
        var items = new List<QuoteLineItem>
        {
            new("SKU-ANNUAL", "Annual Platform License", 1, 10000m, 0m, 10000m, 2000m)
        };
        var proposal = _engine.CreateProposal(TenantId, CustomerCompany, CustomerEmail, items);

        var signRequest = new SignProposalRequest(
            ProposalId: proposal.Id,
            TenantId: TenantId,
            SignerName: "Jane Doe (VP of Procurement)",
            SignerEmail: "jane.doe@acme-tech.com",
            SignerIp: "203.0.113.42"
        );

        // Act
        var signed = _engine.SignProposal(signRequest);

        // Assert
        signed.Status.Should().Be(ProposalStatus.Signed);
        signed.SignerName.Should().Be("Jane Doe (VP of Procurement)");
        signed.SignedAtUtc.Should().NotBeNull();
        signed.SignedPdfHash.Should().NotBeNullOrWhiteSpace();
        signed.SignedPdfHash!.Length.Should().Be(64); // SHA-256 hex string is 64 characters

        // Verify retrieval reflects signed status
        var persisted = _engine.GetProposal(TenantId, proposal.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ProposalStatus.Signed);
        persisted.SignedPdfHash.Should().Be(signed.SignedPdfHash);
    }

    [Fact]
    public void SignProposal_WithNonexistentProposal_ThrowsKeyNotFoundException()
    {
        // Arrange
        var signRequest = new SignProposalRequest(
            ProposalId: Guid.NewGuid(),
            TenantId: TenantId,
            SignerName: "Unknown User",
            SignerEmail: "unknown@company.com",
            SignerIp: "127.0.0.1"
        );

        // Act & Assert
        var act = () => _engine.SignProposal(signRequest);
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage($"*{signRequest.ProposalId}*");
    }

    [Fact]
    public void RecordUsage_And_GenerateUsageInvoice_CalculatesTieredOveragesCorrectly()
    {
        // Arrange: period from 1 hour ago to 1 hour from now
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow.AddHours(1);

        // Pricing rules:
        // ApiRequests: over 100k, $0.001/req. Consume 150k -> overage 50k * 0.001 = $50.00
        _engine.RecordUsage(new RecordUsageRequest(TenantId, UsageMetricType.ApiRequests, 90_000));
        _engine.RecordUsage(new RecordUsageRequest(TenantId, UsageMetricType.ApiRequests, 60_000)); // Total = 150,000

        // CapiConversions: over 50k, $0.002/event. Consume 75k -> overage 25k * 0.002 = $50.00
        _engine.RecordUsage(new RecordUsageRequest(TenantId, UsageMetricType.CapiConversions, 75_000));

        // AiTokens: over 1M, $0.005 per 1k tokens. Consume 3M -> overage 2M / 1k * 0.005 = $10.00
        _engine.RecordUsage(new RecordUsageRequest(TenantId, UsageMetricType.AiTokens, 3_000_000));

        // ActiveContacts: over 25k, $0.01 per contact. Consume 30k -> overage 5k * 0.01 = $50.00
        _engine.RecordUsage(new RecordUsageRequest(TenantId, UsageMetricType.ActiveContacts, 30_000));

        // Act
        var invoice = _engine.GenerateUsageInvoice(TenantId, start, end);

        // Assert
        invoice.Should().NotBeNull();
        invoice.TenantId.Should().Be(TenantId);
        invoice.PeriodStartUtc.Should().Be(start);
        invoice.PeriodEndUtc.Should().Be(end);

        invoice.ConsumedUnits[UsageMetricType.ApiRequests].Should().Be(150_000);
        invoice.ConsumedUnits[UsageMetricType.CapiConversions].Should().Be(75_000);
        invoice.ConsumedUnits[UsageMetricType.AiTokens].Should().Be(3_000_000);
        invoice.ConsumedUnits[UsageMetricType.ActiveContacts].Should().Be(30_000);

        invoice.OverageCharges[UsageMetricType.ApiRequests].Should().Be(50.00m);
        invoice.OverageCharges[UsageMetricType.CapiConversions].Should().Be(50.00m);
        invoice.OverageCharges[UsageMetricType.AiTokens].Should().Be(10.00m);
        invoice.OverageCharges[UsageMetricType.ActiveContacts].Should().Be(50.00m);

        // Total = 50 + 50 + 10 + 50 = 160.00
        invoice.TotalOverageAmount.Should().Be(160.00m);
    }

    [Fact]
    public void GenerateUsageInvoice_WhenUsageWithinIncludedQuotas_ReturnsZeroOverageCharges()
    {
        // Arrange
        const string quietTenant = "tenant-startup-tier";
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow.AddDays(1);

        _engine.RecordUsage(new RecordUsageRequest(quietTenant, UsageMetricType.ApiRequests, 25_000));
        _engine.RecordUsage(new RecordUsageRequest(quietTenant, UsageMetricType.CapiConversions, 10_000));
        _engine.RecordUsage(new RecordUsageRequest(quietTenant, UsageMetricType.AiTokens, 500_000));
        _engine.RecordUsage(new RecordUsageRequest(quietTenant, UsageMetricType.ActiveContacts, 5_000));

        // Act
        var invoice = _engine.GenerateUsageInvoice(quietTenant, start, end);

        // Assert
        invoice.ConsumedUnits[UsageMetricType.ApiRequests].Should().Be(25_000);
        invoice.ConsumedUnits[UsageMetricType.CapiConversions].Should().Be(10_000);
        invoice.ConsumedUnits[UsageMetricType.AiTokens].Should().Be(500_000);
        invoice.ConsumedUnits[UsageMetricType.ActiveContacts].Should().Be(5_000);

        invoice.OverageCharges[UsageMetricType.ApiRequests].Should().Be(0.00m);
        invoice.OverageCharges[UsageMetricType.CapiConversions].Should().Be(0.00m);
        invoice.OverageCharges[UsageMetricType.AiTokens].Should().Be(0.00m);
        invoice.OverageCharges[UsageMetricType.ActiveContacts].Should().Be(0.00m);

        invoice.TotalOverageAmount.Should().Be(0.00m);
    }
}
