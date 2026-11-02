using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Finance.CreditRiskUnderwriting;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class CreditRiskTests
{
    private readonly AlgorithmicCreditRiskEngine _engine = new();

    [Fact]
    public void UnderwriteInvoice_SolventCorporateDebtor_ApprovesSafeZoneWith90PercentAdvance()
    {
        // Arrange: Blue-chip corporate debtor with strong balance sheet
        var financials = new CorporateFinancialStatement(
            CompanyTaxId: "DZ-TAX-101",
            LegalEntityName: "Sonatrach Enterprise Logistics",
            WorkingCapitalEur: 5_000_000.0m,
            RetainedEarningsEur: 12_000_000.0m,
            EbitOperatingIncomeEur: 4_000_000.0m,
            TotalAssetsEur: 20_000_000.0m,
            BookValueOfEquityEur: 14_000_000.0m,
            TotalLiabilitiesEur: 6_000_000.0m,
            HistoricalDsoDays: 32
        );

        var application = new InvoiceFactoringApplication(
            InvoiceId: "INV-2026-999",
            CreditorTenantId: "TENANT-VENDOR-01",
            DebtorTaxId: "DZ-TAX-101",
            InvoiceFaceValueEur: 100_000.0m,
            IssueDateUtc: DateTime.UtcNow.AddDays(-10),
            DueDateUtc: DateTime.UtcNow.AddDays(50), // 50 days tenor
            DebtorFinancials: financials
        );

        // Act
        var result = _engine.UnderwriteInvoice(application);

        // Assert
        result.Should().NotBeNull();
        result.AltmanZScore.Should().BeGreaterThan(2.90);
        result.Zone.Should().Be(SolvencyZone.SafeZone);
        result.IsApproved.Should().BeTrue();
        result.ApprovedAdvanceRatePercent.Should().Be(90.0);
        result.AdvanceAmountEur.Should().Be(90_000.0m);
        result.EstimatedProbabilityOfDefault.Should().BeLessThan(0.10);
        result.DiscountFeeEur.Should().BeGreaterThan(0m);
        result.NetDisbursedCashEur.Should().Be(result.AdvanceAmountEur - result.DiscountFeeEur);
    }

    [Fact]
    public void UnderwriteInvoice_ModerateFinancials_ApprovesGreyZoneWith80PercentAdvance()
    {
        // Arrange: Mid-market company in grey zone (Z-score between 1.23 and 2.90)
        var financials = new CorporateFinancialStatement(
            CompanyTaxId: "DZ-TAX-MID",
            LegalEntityName: "MidMarket Manufacturing SPA",
            WorkingCapitalEur: 200_000.0m,
            RetainedEarningsEur: 150_000.0m,
            EbitOperatingIncomeEur: 100_000.0m,
            TotalAssetsEur: 2_000_000.0m,
            BookValueOfEquityEur: 600_000.0m,
            TotalLiabilitiesEur: 1_400_000.0m
        );

        var application = new InvoiceFactoringApplication(
            InvoiceId: "INV-MID-01",
            CreditorTenantId: "TENANT-02",
            DebtorTaxId: "DZ-TAX-MID",
            InvoiceFaceValueEur: 50_000.0m,
            IssueDateUtc: DateTime.UtcNow.AddDays(-5),
            DueDateUtc: DateTime.UtcNow.AddDays(40),
            DebtorFinancials: financials
        );

        // Act
        var result = _engine.UnderwriteInvoice(application);

        // Assert
        result.Zone.Should().Be(SolvencyZone.GreyZone);
        result.ApprovedAdvanceRatePercent.Should().Be(80.0);
        result.AdvanceAmountEur.Should().Be(40_000.0m);
        result.IsApproved.Should().BeTrue();
    }

    [Fact]
    public void UnderwriteInvoice_InsolventDebtor_RejectsFactoring()
    {
        // Arrange: Insolvent company with negative working capital, negative EBIT, and high leverage
        var insolvent = new CorporateFinancialStatement(
            CompanyTaxId: "DZ-TAX-FAIL",
            LegalEntityName: "Distressed Retail SARL",
            WorkingCapitalEur: -500_000.0m,
            RetainedEarningsEur: -300_000.0m,
            EbitOperatingIncomeEur: -150_000.0m,
            TotalAssetsEur: 1_000_000.0m,
            BookValueOfEquityEur: 50_000.0m,
            TotalLiabilitiesEur: 950_000.0m
        );

        var application = new InvoiceFactoringApplication(
            InvoiceId: "INV-FAIL-01",
            CreditorTenantId: "TENANT-03",
            DebtorTaxId: "DZ-TAX-FAIL",
            InvoiceFaceValueEur: 25_000.0m,
            IssueDateUtc: DateTime.UtcNow.AddDays(-2),
            DueDateUtc: DateTime.UtcNow.AddDays(30),
            DebtorFinancials: insolvent
        );

        // Act
        var result = _engine.UnderwriteInvoice(application);

        // Assert
        result.AltmanZScore.Should().BeLessThan(0.60);
        result.Zone.Should().Be(SolvencyZone.DistressZone);
        result.IsApproved.Should().BeFalse();
        result.AdvanceAmountEur.Should().Be(0m);
        result.UnderwritingNotes.Should().Contain("UNDERWRITING REJECTED");
    }

    [Fact]
    public void AddCreditRiskUnderwritingServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCreditRiskUnderwritingServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(AlgorithmicCreditRiskEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
