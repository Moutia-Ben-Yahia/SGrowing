using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Commerce.B2B.ContractPricing;
using Xunit;

namespace SGrowing.Tests.Unit;

public class B2BContractPricingTests
{
    private readonly B2BContractPricingService _service;
    private readonly Guid _defaultTenantId = B2BContractPricingService.DefaultTenantId;
    private readonly Guid _enterpriseAccountId = B2BContractPricingService.EnterpriseRetailGroupId;

    public B2BContractPricingTests()
    {
        _service = new B2BContractPricingService();
    }

    [Theory]
    [InlineData(25, 240, 6250, 6000, 250, 4.0)]
    [InlineData(100, 210, 25000, 21000, 4000, 16.0)]
    [InlineData(250, 180, 62500, 45000, 17500, 28.0)]
    public void CalculatePrice_StandardWholesale_AppliesCorrectVolumeTiers(
        int quantity,
        decimal expectedUnitPrice,
        decimal expectedGross,
        decimal expectedNet,
        decimal expectedDiscountAmount,
        decimal expectedDiscountPercent)
    {
        var request = new CalculateB2BPriceRequest(
            TenantId: _defaultTenantId,
            AccountId: null,
            Sku: "SKU-PERFUME-OUD",
            Quantity: quantity
        );

        var result = _service.CalculatePrice(request);

        result.Should().NotBeNull();
        result.Sku.Should().Be("SKU-PERFUME-OUD");
        result.Quantity.Should().Be(quantity);
        result.UnitPriceSar.Should().Be(expectedUnitPrice);
        result.TotalGrossSar.Should().Be(expectedGross);
        (result.UnitPriceSar * result.Quantity).Should().Be(expectedNet);
        result.DiscountAmountSar.Should().Be(expectedDiscountAmount);
        result.EffectiveDiscountPercent.Should().Be(expectedDiscountPercent);
        result.AppliedPriceList.Should().Be("Standard B2B Wholesale List");
    }

    [Fact]
    public void CalculatePrice_CustomerSpecificOverride_TakesPrecedenceOverDefault()
    {
        // 100 units under default would be 210 SAR, but for EnterpriseRetailGroup it's 190 SAR
        var request = new CalculateB2BPriceRequest(
            TenantId: _defaultTenantId,
            AccountId: _enterpriseAccountId,
            Sku: "SKU-PERFUME-OUD",
            Quantity: 100
        );

        var result = _service.CalculatePrice(request);

        result.Should().NotBeNull();
        result.UnitPriceSar.Should().Be(190m);
        result.EffectiveDiscountPercent.Should().Be(24.0m);
        result.AppliedPriceList.Should().Be("Enterprise Retail Group Contract");
    }

    [Fact]
    public void CalculatePrice_UnknownAccount_FallsBackToDefaultWholesale()
    {
        var nonContractAccount = Guid.NewGuid();
        var request = new CalculateB2BPriceRequest(
            TenantId: _defaultTenantId,
            AccountId: nonContractAccount,
            Sku: "SKU-PERFUME-OUD",
            Quantity: 100
        );

        var result = _service.CalculatePrice(request);

        result.Should().NotBeNull();
        result.UnitPriceSar.Should().Be(210m);
        result.EffectiveDiscountPercent.Should().Be(16.0m);
        result.AppliedPriceList.Should().Be("Standard B2B Wholesale List");
    }

    [Fact]
    public void CreateQuote_ApprovalRouting_AutoApproved_WhenDiscountUnderOrEqual10Percent()
    {
        // 10 units has 4% discount <= 10%
        var request = new CreateB2BQuoteRequest(
            TenantId: _defaultTenantId,
            AccountId: Guid.NewGuid(),
            ContactName: "Tariq Al-Mansoor",
            ContactEmail: "tariq@retailksa.com",
            Items: new List<B2BQuoteLineItemRequest>
            {
                new("SKU-PERFUME-OUD", 10)
            },
            Notes: "Trial wholesale order"
        );

        var quote = _service.CreateQuote(request);

        quote.Should().NotBeNull();
        quote.TotalGrossSar.Should().Be(2500m);
        quote.TotalNetSar.Should().Be(2400m);
        quote.TotalDiscountSar.Should().Be(100m);
        quote.Status.Should().Be(QuoteApprovalStatus.AutoApproved);
        quote.RequiredApproverRole.Should().Be("NONE");
        quote.Items.Should().HaveCount(1);
        quote.Items[0].MarginPercent.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void CreateQuote_ApprovalRouting_PendingManagerApproval_WhenDiscountBetween10And20Percent()
    {
        // 100 units has 16% discount (between 10% and 20%)
        var request = new CreateB2BQuoteRequest(
            TenantId: _defaultTenantId,
            AccountId: Guid.NewGuid(),
            ContactName: "Fatima Zahra",
            ContactEmail: "fatima@gulfretail.com",
            Items: new List<B2BQuoteLineItemRequest>
            {
                new("SKU-PERFUME-OUD", 100)
            },
            Notes: "Q3 replenishment batch"
        );

        var quote = _service.CreateQuote(request);

        quote.Should().NotBeNull();
        quote.TotalGrossSar.Should().Be(25000m);
        quote.TotalNetSar.Should().Be(21000m);
        quote.TotalDiscountSar.Should().Be(4000m);
        quote.Status.Should().Be(QuoteApprovalStatus.PendingManagerApproval);
        quote.RequiredApproverRole.Should().Be("SALES_MANAGER");
    }

    [Fact]
    public void CreateQuote_ApprovalRouting_PendingVpApproval_WhenDiscountOver20Percent()
    {
        // 300 units has 28% discount (> 20%)
        var request = new CreateB2BQuoteRequest(
            TenantId: _defaultTenantId,
            AccountId: Guid.NewGuid(),
            ContactName: "Sultan Al-Otaibi",
            ContactEmail: "sultan@saudidistribution.com",
            Items: new List<B2BQuoteLineItemRequest>
            {
                new("SKU-PERFUME-OUD", 300)
            },
            Notes: "Annual mega contract"
        );

        var quote = _service.CreateQuote(request);

        quote.Should().NotBeNull();
        quote.TotalGrossSar.Should().Be(75000m);
        quote.TotalNetSar.Should().Be(54000m);
        quote.TotalDiscountSar.Should().Be(21000m);
        quote.Status.Should().Be(QuoteApprovalStatus.PendingVpApproval);
        quote.RequiredApproverRole.Should().Be("VP_COMMERCE");
    }

    [Fact]
    public void CreateQuote_WithCustomRequestedPrice_RoutesToVpApprovalWhenHeavyDiscount()
    {
        // Base is 250 SAR, requested is 170 SAR (~32% discount)
        var request = new CreateB2BQuoteRequest(
            TenantId: _defaultTenantId,
            AccountId: Guid.NewGuid(),
            ContactName: "Karim Hassan",
            ContactEmail: "karim@flagshipstores.com",
            Items: new List<B2BQuoteLineItemRequest>
            {
                new("SKU-PERFUME-OUD", 50, RequestedUnitPriceSar: 170m)
            },
            Notes: "Custom concession request"
        );

        var quote = _service.CreateQuote(request);

        quote.Status.Should().Be(QuoteApprovalStatus.PendingVpApproval);
        quote.RequiredApproverRole.Should().Be("VP_COMMERCE");
        quote.Items[0].UnitPriceSar.Should().Be(170m);
    }

    [Fact]
    public void ApproveQuote_Approved_TransitionsStatusToApproved()
    {
        var quote = _service.CreateQuote(new CreateB2BQuoteRequest(
            TenantId: _defaultTenantId,
            AccountId: Guid.NewGuid(),
            ContactName: "Test Approver Target",
            ContactEmail: "test@example.com",
            Items: new List<B2BQuoteLineItemRequest> { new("SKU-PERFUME-OUD", 100) },
            Notes: "Pending approval"
        ));

        quote.Status.Should().Be(QuoteApprovalStatus.PendingManagerApproval);

        var approveResult = _service.ApproveQuote(new ApproveB2BQuoteRequest(
            TenantId: _defaultTenantId,
            QuoteId: quote.QuoteId,
            ApproverEmail: "manager@sgrowing.com",
            ApproverRole: "SALES_MANAGER",
            Approved: true
        ));

        approveResult.Status.Should().Be(QuoteApprovalStatus.Approved);
        approveResult.RequiredApproverRole.Should().Be("COMPLETED");

        var retrieved = _service.GetQuote(_defaultTenantId, quote.QuoteId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(QuoteApprovalStatus.Approved);
    }

    [Fact]
    public void ApproveQuote_Rejected_TransitionsStatusToRejected()
    {
        var quote = _service.CreateQuote(new CreateB2BQuoteRequest(
            TenantId: _defaultTenantId,
            AccountId: Guid.NewGuid(),
            ContactName: "Test Rejection Target",
            ContactEmail: "test@example.com",
            Items: new List<B2BQuoteLineItemRequest> { new("SKU-PERFUME-OUD", 300) },
            Notes: "High discount quote"
        ));

        var rejectResult = _service.ApproveQuote(new ApproveB2BQuoteRequest(
            TenantId: _defaultTenantId,
            QuoteId: quote.QuoteId,
            ApproverEmail: "vp@sgrowing.com",
            ApproverRole: "VP_COMMERCE",
            Approved: false,
            RejectionReason: "Exceeds annual promotional discount reserve."
        ));

        rejectResult.Status.Should().Be(QuoteApprovalStatus.Rejected);
        rejectResult.RequiredApproverRole.Should().Be("REJECTED");
    }

    [Fact]
    public void TenantIsolation_CrossTenantAccessDenied()
    {
        var tenantA = _defaultTenantId;
        var tenantB = Guid.NewGuid();

        // Quote created in Tenant A
        var quoteA = _service.CreateQuote(new CreateB2BQuoteRequest(
            TenantId: tenantA,
            AccountId: Guid.NewGuid(),
            ContactName: "Tenant A Contact",
            ContactEmail: "a@tenant.com",
            Items: new List<B2BQuoteLineItemRequest> { new("SKU-PERFUME-OUD", 20) },
            Notes: "Tenant A quote"
        ));

        // Tenant B cannot retrieve Tenant A's quote
        var crossTenantGet = _service.GetQuote(tenantB, quoteA.QuoteId);
        crossTenantGet.Should().BeNull();

        // Tenant B cannot approve Tenant A's quote
        var actApprove = () => _service.ApproveQuote(new ApproveB2BQuoteRequest(
            TenantId: tenantB,
            QuoteId: quoteA.QuoteId,
            ApproverEmail: "intruder@tenantb.com",
            ApproverRole: "VP_COMMERCE",
            Approved: true
        ));
        actApprove.Should().Throw<KeyNotFoundException>();

        // Tenant B has no price lists initially
        var tenantBPriceLists = _service.GetPriceLists(tenantB);
        tenantBPriceLists.Should().BeEmpty();
    }

    [Fact]
    public void CalculatePrice_InvalidQuantity_ThrowsArgumentException()
    {
        var actZero = () => _service.CalculatePrice(new CalculateB2BPriceRequest(_defaultTenantId, null, "SKU-PERFUME-OUD", 0));
        var actNeg = () => _service.CalculatePrice(new CalculateB2BPriceRequest(_defaultTenantId, null, "SKU-PERFUME-OUD", -10));

        actZero.Should().Throw<ArgumentException>();
        actNeg.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculatePrice_UnknownSku_ThrowsKeyNotFoundException()
    {
        var act = () => _service.CalculatePrice(new CalculateB2BPriceRequest(_defaultTenantId, null, "NON-EXISTENT-SKU", 50));
        act.Should().Throw<KeyNotFoundException>();
    }
}
