using FluentAssertions;
using SGrowing.Api.Modules.Billing.DunningEngine;
using Xunit;

namespace SGrowing.Tests.Billing;

public class DunningBillingTests
{
    private readonly DunningBillingEngine _engine = new();

    [Fact]
    public void CalculateTax_AppliesEuVatReverseCharge_WhenValidVatProvided()
    {
        // Act
        var result = _engine.CalculateTax(1000m, "DE", "DE123456789");

        // Assert
        result.TaxRatePercent.Should().Be(0.0m);
        result.TaxAmount.Should().Be(0.0m);
        result.TotalAmount.Should().Be(1000m);
        result.ExemptionReason.Should().Be(TaxExemptionReason.EuViesReverseCharge);
    }

    [Fact]
    public void CalculateTax_AppliesStandardTax_WhenDomesticOrNoVat()
    {
        // Act 1: US State Tax
        var usTax = _engine.CalculateTax(2000m, "US", null);
        usTax.TaxRatePercent.Should().Be(8.25m);
        usTax.TaxAmount.Should().Be(165m);
        usTax.TotalAmount.Should().Be(2165m);

        // Act 2: UK VAT
        var ukTax = _engine.CalculateTax(1000m, "GB", null);
        ukTax.TaxRatePercent.Should().Be(20.0m);
        ukTax.TaxAmount.Should().Be(200m);
        ukTax.TotalAmount.Should().Be(1200m);
    }

    [Fact]
    public void GenerateInvoice_ProducesSha256DigitalSealAndPastDueStatus()
    {
        // Arrange
        var request = new CreateInvoiceRequest(
            TenantId: "tenant_bill_01",
            CustomerId: "cust_acme_corp",
            Subtotal: 5000m,
            Currency: "USD",
            CountryCode: "US"
        );

        // Act
        var invoice = _engine.GenerateInvoice(request);

        // Assert
        invoice.Should().NotBeNull();
        invoice.Total.Should().Be(5412.50m);
        invoice.Status.Should().Be(InvoiceStatus.PastDue);
        invoice.DigitalSealHash.Should().HaveLength(64);
    }

    [Fact]
    public void ProcessDunningRetry_ProgressesThroughEscalationAndRecoversOnSuccess()
    {
        // Arrange
        var invoice = _engine.GenerateInvoice(new CreateInvoiceRequest(
            TenantId: "tenant_bill_02",
            CustomerId: "cust_beta_tech",
            Subtotal: 1000m,
            Currency: "EUR",
            CountryCode: "DE",
            VatNumber: "DE987654321"
        ));

        // Act 1: Attempt 1 fails -> Email warning
        var step1 = _engine.ProcessDunningRetry(new ProcessDunningRetryRequest("tenant_bill_02", invoice.InvoiceId, false));
        step1.AttemptNumber.Should().Be(1);
        step1.Status.Should().Be(InvoiceStatus.PastDue);
        step1.NextAction.Should().Contain("Day 3 payment retry");

        // Act 2: Attempt 2 succeeds -> Paid
        var step2 = _engine.ProcessDunningRetry(new ProcessDunningRetryRequest("tenant_bill_02", invoice.InvoiceId, true));
        step2.AttemptNumber.Should().Be(2);
        step2.Status.Should().Be(InvoiceStatus.Paid);
        step2.RecoveredAmount.Should().Be(1000m);

        // Assert audit log
        var logs = _engine.GetDunningAuditLog(invoice.InvoiceId);
        logs.Should().HaveCount(2);
        logs[0].Succeeded.Should().BeFalse();
        logs[1].Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ProcessDunningRetry_NonExistentInvoice_ThrowsKeyNotFoundException()
    {
        var act = () => _engine.ProcessDunningRetry(new ProcessDunningRetryRequest("tenant_unknown", "inv_unknown_999", false));
        act.Should().Throw<KeyNotFoundException>().WithMessage("*not found*");
    }

    [Fact]
    public void ProcessDunningRetry_ThreeConsecutiveFailures_TransitionsToUncollectibleAndDowngradesTier()
    {
        var invoice = _engine.GenerateInvoice(new CreateInvoiceRequest(
            TenantId: "tenant_bill_03",
            CustomerId: "cust_failing_corp",
            Subtotal: 3000m,
            Currency: "USD",
            CountryCode: "US"
        ));

        // Fail 1: Email warning
        var step1 = _engine.ProcessDunningRetry(new ProcessDunningRetryRequest("tenant_bill_03", invoice.InvoiceId, false));
        step1.AttemptNumber.Should().Be(1);
        step1.Status.Should().Be(InvoiceStatus.PastDue);

        // Fail 2: SMS warning
        var step2 = _engine.ProcessDunningRetry(new ProcessDunningRetryRequest("tenant_bill_03", invoice.InvoiceId, false));
        step2.AttemptNumber.Should().Be(2);
        step2.Status.Should().Be(InvoiceStatus.PastDue);
        step2.NextAction.Should().Contain("Day 7 final notice");

        // Fail 3: Downgrade tier & Uncollectible
        var step3 = _engine.ProcessDunningRetry(new ProcessDunningRetryRequest("tenant_bill_03", invoice.InvoiceId, false));
        step3.AttemptNumber.Should().Be(3);
        step3.Status.Should().Be(InvoiceStatus.Uncollectible);
        step3.NextAction.Should().Contain("downgraded to Free tier");

        var updatedInvoice = _engine.GetInvoice("tenant_bill_03", invoice.InvoiceId);
        updatedInvoice.Should().NotBeNull();
        updatedInvoice!.Status.Should().Be(InvoiceStatus.Uncollectible);
    }

    [Fact]
    public void GetInvoice_NonExistentInvoice_ReturnsNull()
    {
        var inv = _engine.GetInvoice("tenant_none", "inv_none_404");
        inv.Should().BeNull();
    }

    [Fact]
    public void ListInvoices_IsolatesInvoicesByTenantAndOrdersByDueDate()
    {
        var tenantA = "tenant_multi_inv_a";
        var tenantB = "tenant_multi_inv_b";

        _engine.GenerateInvoice(new CreateInvoiceRequest(tenantA, "cust_1", 100m, "USD", "US"));
        _engine.GenerateInvoice(new CreateInvoiceRequest(tenantA, "cust_2", 200m, "USD", "US"));
        _engine.GenerateInvoice(new CreateInvoiceRequest(tenantB, "cust_3", 300m, "USD", "US"));

        var listA = _engine.ListInvoices(tenantA);
        var listB = _engine.ListInvoices(tenantB);

        listA.Should().HaveCount(2);
        listB.Should().HaveCount(1);
        listA.Should().AllSatisfy(i => i.TenantId.Should().Be(tenantA));
        listB.Should().AllSatisfy(i => i.TenantId.Should().Be(tenantB));
    }

    [Fact]
    public void CalculateTax_CanadaAndAustralia_AppliesRegionalRates()
    {
        var caTax = _engine.CalculateTax(1000m, "CA", null);
        caTax.TaxRatePercent.Should().Be(13.0m);
        caTax.TaxAmount.Should().Be(130.0m);
        caTax.TaxJurisdiction.Should().Contain("Canada Harmonized Sales Tax");

        var auTax = _engine.CalculateTax(1000m, "AU", null);
        auTax.TaxRatePercent.Should().Be(10.0m);
        auTax.TaxAmount.Should().Be(100.0m);
        auTax.TaxJurisdiction.Should().Contain("Australia GST");
    }

    [Fact]
    public void GetDunningAuditLog_UnknownInvoice_ReturnsEmptyList()
    {
        var logs = _engine.GetDunningAuditLog("inv_no_history_999");
        logs.Should().NotBeNull();
        logs.Should().BeEmpty();
    }
}
