using FluentAssertions;
using SGrowing.Api.Modules.SupplyChain.HsClassification;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

public sealed class HsClassificationTests
{
    private readonly HsTariffClassificationEngine _tariffEngine = new();
    private readonly CrossBorderDutyCalculatorService _dutyCalc;
    private readonly CustomsDeclarationGeneratorService _declGenerator = new();

    public HsClassificationTests()
    {
        _dutyCalc = new CrossBorderDutyCalculatorService(_tariffEngine);
    }

    #region HS Code Classification Tests

    [Fact]
    public void ClassifyProduct_ElectronicsLaptop_SelectsHs847130()
    {
        var candidates = _tariffEngine.ClassifyProduct("Dell Latitude portable laptop notebook computer with keyboard and display");

        candidates.Should().NotBeEmpty();
        var top = candidates[0];
        top.HsCode.Should().Be("8471.30");
        top.ConfidenceScore.Should().BeGreaterThan(0.2);
        top.StandardDutyRatePercent.Should().Be(0.0m);
    }

    [Fact]
    public void ClassifyProduct_AgroFoodOliveOil_SelectsHs150920()
    {
        var candidates = _tariffEngine.ClassifyProduct("Premium Algerian cold-pressed extra virgin olive oil edible culinary");

        candidates.Should().NotBeEmpty();
        var top = candidates[0];
        top.HsCode.Should().Be("1509.20");
        top.StandardDutyRatePercent.Should().Be(15.0m);
        top.VatRatePercent.Should().Be(9.0m);
    }

    #endregion

    #region Landed Cost & Preferential Tariff Tests

    [Fact]
    public void CalculateLandedCost_StandardWtoMfn_ComputesCifDutyAndVat()
    {
        var req = new LandedCostCalculationRequest(
            ProductDescription: "Extra virgin olive oil",
            HsCode: "1509.20",
            FobValueEur: 10000.0m,
            InternationalFreightEur: 1500.0m,
            MarineInsuranceEur: 200.0m,
            OriginCountryIso: "ESP",
            DestinationCountryIso: "DZA",
            TradeAgreement: PreferentialTradeAgreement.NoneWtoMfn,
            HasCertifiedProofOfOrigin: false
        );

        var result = _dutyCalc.CalculateLandedCost(req);

        // CIF = 10000 + 1500 + 200 = 11700 EUR
        result.CifValueEur.Should().Be(11700.00m);
        // Duty = 11700 * 15% = 1755 EUR
        result.CustomsDutyEur.Should().Be(1755.00m);
        // VAT = (11700 + 1755) * 9% = 1210.95 EUR
        result.VatAmountEur.Should().Be(1210.95m);
        // Total landed cost = 11700 + 1755 + 1210.95 = 14665.95 EUR
        result.TotalLandedCostEur.Should().Be(14665.95m);
        result.AppliedTradeAgreement.Should().Be(PreferentialTradeAgreement.NoneWtoMfn);
    }

    [Fact]
    public void CalculateLandedCost_AfCftaWithProofOfOrigin_GrantsZeroTariffDutyExemption()
    {
        var req = new LandedCostCalculationRequest(
            ProductDescription: "Cotton woven textiles",
            HsCode: "5208.11",
            FobValueEur: 20000.0m,
            InternationalFreightEur: 2000.0m,
            MarineInsuranceEur: 300.0m,
            OriginCountryIso: "EGY",
            DestinationCountryIso: "DZA",
            TradeAgreement: PreferentialTradeAgreement.AfCfta,
            HasCertifiedProofOfOrigin: true
        );

        var result = _dutyCalc.CalculateLandedCost(req);

        result.CifValueEur.Should().Be(22300.00m);
        result.ApplicableDutyRatePercent.Should().Be(0.0m, "AfCFTA grants duty exemption with proof of origin.");
        result.CustomsDutyEur.Should().Be(0.0m);
        // VAT = 22300 * 19% = 4237.00 EUR
        result.VatAmountEur.Should().Be(4237.00m);
        result.AppliedTradeAgreement.Should().Be(PreferentialTradeAgreement.AfCfta);
        result.TariffDutyNotes.Should().Contain("AfCFTA");
    }

    [Fact]
    public void CalculateLandedCost_ClaimedAgreementWithoutProofOfOrigin_RevertsToMfnRate()
    {
        var req = new LandedCostCalculationRequest(
            ProductDescription: "Cotton woven textiles",
            HsCode: "5208.11",
            FobValueEur: 20000.0m,
            InternationalFreightEur: 2000.0m,
            MarineInsuranceEur: 300.0m,
            OriginCountryIso: "EGY",
            DestinationCountryIso: "DZA",
            TradeAgreement: PreferentialTradeAgreement.AfCfta,
            HasCertifiedProofOfOrigin: false // Missing documentation!
        );

        var result = _dutyCalc.CalculateLandedCost(req);

        result.ApplicableDutyRatePercent.Should().Be(12.0m, "Without proof of origin, standard MFN tariff applies.");
        result.CustomsDutyEur.Should().BeGreaterThan(0.0m);
        result.AppliedTradeAgreement.Should().Be(PreferentialTradeAgreement.NoneWtoMfn);
        result.TariffDutyNotes.Should().Contain("Proof of origin missing");
    }

    #endregion

    #region Customs Single Administrative Document (SAD) Tests

    [Fact]
    public void GenerateDeclaration_ValidLineItems_GeneratesDocumentWithCryptographicSeal()
    {
        var items = new List<SadLineItem>
        {
            new(1, "8471.30", "Dell Laptops", 250.0, 15000.0m, 0.0m, 2850.0m),
            new(2, "8471.50", "Rack Servers", 450.0, 30000.0m, 0.0m, 5700.0m)
        };

        var sad = _declGenerator.GenerateDeclaration(
            importerTaxId: "DZ-NIF-00998877",
            exporterName: "Dell Europe BV",
            countryOfExport: "NLD",
            countryOfDestination: "DZA",
            customsOfficeCode: "ALG-PORT-01",
            items: items
        );

        sad.Should().NotBeNull();
        sad.DeclarationId.Should().StartWith("SAD-");
        sad.TotalStatisticalValueEur.Should().Be(45000.0m);
        sad.TotalPayableDutyAndTaxesEur.Should().Be(8550.0m);
        sad.DigitalCustomsSealHex.Should().NotBeNullOrWhiteSpace();
        sad.Items.Should().HaveCount(2);
    }

    #endregion
}
