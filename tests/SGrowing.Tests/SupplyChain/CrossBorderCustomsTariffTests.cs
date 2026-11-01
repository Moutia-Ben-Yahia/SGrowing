using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.SupplyChain.Customs;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

/// <summary>
/// Comprehensive unit test suite for Phase 31:
/// Autonomous Cross-Border Customs Clearance, Harmonized System (HS) Auto-Classification &amp; Dynamic Tariffs Engine.
/// </summary>
public class CrossBorderCustomsTariffTests
{
    private readonly HsCodeClassifierEngine _classifier = new();
    private readonly DynamicCustomsTariffService _tariffService;
    private readonly DigitalCustomsDeclarationService _declarationService;

    public CrossBorderCustomsTariffTests()
    {
        _tariffService = new DynamicCustomsTariffService(_classifier);
        _declarationService = new DigitalCustomsDeclarationService(_tariffService);
    }

    #region 1. Harmonized System (HS) Multi-Tier Hierarchical Classification Tests

    [Fact]
    public void ClassifyProduct_CottonTShirt_ReturnsHs610910()
    {
        var request = new ProductClassificationRequest(
            Title: "Men's Classic Cotton T-Shirt Crewneck",
            Category: "Apparel",
            Material: "100% Combed Cotton",
            Description: "Short-sleeved knitted cotton tee for casual wear.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("6109.10");
        result.Category.Should().Be("Apparel/Textiles");
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.80);
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_MensCottonTrousers_ReturnsHs620342()
    {
        var request = new ProductClassificationRequest(
            Title: "Men's Straight Leg Cotton Trousers",
            Category: "Apparel",
            Material: "Cotton Denim Twill",
            Description: "Woven cotton chino trousers for men.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("6203.42");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_LeatherFootwear_ReturnsHs640399()
    {
        var request = new ProductClassificationRequest(
            Title: "Handcrafted Men's Leather Shoes Loafers",
            Category: "Footwear",
            Material: "Calfskin Leather",
            Description: "Formal leather shoes with rubber sole.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("6403.99");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_Smartphone_ReturnsHs851713()
    {
        var request = new ProductClassificationRequest(
            Title: "Pro Max 5G Smartphone 256GB",
            Category: "Electronics",
            Material: "Aluminum and Glass",
            Description: "Cellular mobile phone with touchscreen and LTE/5G wireless modem.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("8517.13");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_PortableLaptop_ReturnsHs847130()
    {
        var request = new ProductClassificationRequest(
            Title: "Ultra-Thin Notebook Laptop 14-inch 16GB RAM",
            Category: "Electronics",
            Material: "Aluminum alloy",
            Description: "Portable automatic data processing computer weighing 1.3 kg.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("8471.30");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_ComputerMonitor_ReturnsHs852852()
    {
        var request = new ProductClassificationRequest(
            Title: "27-inch 4K UHD Computer Monitor Display Screen",
            Category: "Electronics",
            Material: "Plastic and Glass",
            Description: "LED display monitor designed for direct connection to PC via HDMI and DisplayPort.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("8528.52");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_SkincareCreams_ReturnsHs330499()
    {
        var request = new ProductClassificationRequest(
            Title: "Hydrating Anti-Aging Skincare Cream & Face Serum",
            Category: "Beauty",
            Material: "Emulsion with Hyaluronic Acid",
            Description: "Cosmetic facial moisturizer for skin care.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("3304.99");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_Shampoo_ReturnsHs330510()
    {
        var request = new ProductClassificationRequest(
            Title: "Anti-Dandruff Botanical Hair Shampoo 500ml",
            Category: "Beauty",
            Material: "Liquid shampoo preparation",
            Description: "Daily cleansing hair shampoo with natural herbal extracts.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("3305.10");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_PerfumeFragrance_ReturnsHs330300()
    {
        var request = new ProductClassificationRequest(
            Title: "Eau de Parfum Luxury Perfume Vaporisateur 100ml",
            Category: "Beauty",
            Material: "Fragrance and alcohol solution",
            Description: "Fine fragrance perfume toilet water spray.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("3303.00");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_RoastedCoffee_ReturnsHs090121()
    {
        var request = new ProductClassificationRequest(
            Title: "Artisan Organic Roasted Coffee Beans 1kg",
            Category: "Food",
            Material: "100% Arabica Coffee Beans",
            Description: "Medium roasted whole coffee beans not decaffeinated.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("0901.21");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_ChocolateBars_ReturnsHs180632()
    {
        var request = new ProductClassificationRequest(
            Title: "Dark Chocolate Bar 70% Cocoa 100g",
            Category: "Food",
            Material: "Cocoa mass and sugar",
            Description: "Confectionery chocolate slab in solid blocks not filled.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("1806.32");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_NonAlcoholicBeverages_ReturnsHs220299()
    {
        var request = new ProductClassificationRequest(
            Title: "Sparkling Citrus Flavored Energy Drink Beverage 330ml",
            Category: "Beverage",
            Material: "Carbonated water, sugar, and natural flavor",
            Description: "Sweetened non-alcoholic refreshing beverage.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("2202.99");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_WoodenFurniture_ReturnsHs940360()
    {
        var request = new ProductClassificationRequest(
            Title: "Solid Oak Wooden Dining Table and Furniture Set",
            Category: "Furniture",
            Material: "Oak Wood",
            Description: "Home furniture handcrafted wooden table.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("9403.60");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    [Fact]
    public void ClassifyProduct_HandTools_ReturnsHs820559()
    {
        var request = new ProductClassificationRequest(
            Title: "Adjustable Steel Wrench Hand Tools Kit",
            Category: "Hardware",
            Material: "Chrome Vanadium Steel",
            Description: "Manual hand tool spanner for mechanical assembly.");

        var result = _classifier.ClassifyProduct(request);

        result.HsCode.Should().Be("8205.59");
        result.RequiresBrokerReview.Should().BeFalse();
    }

    #endregion

    #region 2. National Tariff Lines & Algerian DAPS Surcharge Tests

    [Fact]
    public void ClassifyProduct_DestinationAlgeria_Resolves10DigitTariffLineAndDapsRates()
    {
        var request = new ProductClassificationRequest(
            Title: "Cotton T-Shirt Short Sleeve",
            Category: "Apparel",
            Material: "Cotton",
            DestinationCountry: "DZ");

        var result = _classifier.ClassifyProduct(request);

        result.NationalTariffLines.Should().NotBeEmpty();
        var dzLine = result.NationalTariffLines.First(l => l.CountryCode == "DZ");
        dzLine.TariffCode.Should().Be("6109.10.10.00");
        dzLine.MfnDutyRate.Should().Be(0.30m);
        dzLine.VatRate.Should().Be(0.19m);
        dzLine.DapsSurchargeRate.Should().Be(0.30m);
    }

    [Fact]
    public void ClassifyProduct_DestinationEu_ResolvesTaric10DigitTariffLine()
    {
        var request = new ProductClassificationRequest(
            Title: "Cotton T-Shirt Short Sleeve",
            Category: "Apparel",
            Material: "Cotton",
            DestinationCountry: "FR"); // France is EU

        var result = _classifier.ClassifyProduct(request);

        var euLine = result.NationalTariffLines.First(l => l.CountryCode == "EU");
        euLine.TariffCode.Should().Be("6109.10.00.10");
        euLine.MfnDutyRate.Should().Be(0.12m);
        euLine.VatRate.Should().Be(0.20m);
    }

    [Fact]
    public void ClassifyProduct_DestinationGcc_Resolves12DigitUnifiedTariffLine()
    {
        var request = new ProductClassificationRequest(
            Title: "Cotton T-Shirt Short Sleeve",
            Category: "Apparel",
            Material: "Cotton",
            DestinationCountry: "AE"); // United Arab Emirates is GCC

        var result = _classifier.ClassifyProduct(request);

        var gccLine = result.NationalTariffLines.First(l => l.CountryCode == "GCC");
        gccLine.TariffCode.Should().Be("6109.10.00.00.00");
        gccLine.MfnDutyRate.Should().Be(0.05m);
        gccLine.VatRate.Should().Be(0.05m);
    }

    [Fact]
    public void ClassifyProduct_DapsRates_VaryByProductType()
    {
        // Perfume: DAPS = 60%
        var perfumeReq = new ProductClassificationRequest("Eau de Parfum Fragrance Spray", DestinationCountry: "DZ");
        var perfumeRes = _classifier.ClassifyProduct(perfumeReq);
        perfumeRes.NationalTariffLines.First(l => l.CountryCode == "DZ").DapsSurchargeRate.Should().Be(0.60m);

        // Laptop: DAPS = 0% (Capital IT equipment)
        var laptopReq = new ProductClassificationRequest("Portable Laptop Computer", DestinationCountry: "DZ");
        var laptopRes = _classifier.ClassifyProduct(laptopReq);
        laptopRes.NationalTariffLines.First(l => l.CountryCode == "DZ").DapsSurchargeRate.Should().Be(0.00m);

        // Roasted Coffee: DAPS = 0% (Essential food)
        var coffeeReq = new ProductClassificationRequest("Roasted Arabica Coffee Beans", DestinationCountry: "DZ");
        var coffeeRes = _classifier.ClassifyProduct(coffeeReq);
        coffeeRes.NationalTariffLines.First(l => l.CountryCode == "DZ").DapsSurchargeRate.Should().Be(0.00m);

        // Wooden Furniture: DAPS = 60%
        var furnitureReq = new ProductClassificationRequest("Solid Wooden Furniture Table", DestinationCountry: "DZ");
        var furnitureRes = _classifier.ClassifyProduct(furnitureReq);
        furnitureRes.NationalTariffLines.First(l => l.CountryCode == "DZ").DapsSurchargeRate.Should().Be(0.60m);
    }

    #endregion

    #region 3. Confidence Scoring & Broker Review Gating Tests

    [Fact]
    public void ClassifyProduct_HighConfidence_AutoApprovedWithoutBrokerReview()
    {
        var request = new ProductClassificationRequest(
            Title: "Premium Organic Cotton T-Shirt",
            Category: "Apparel",
            Material: "100% Organic Cotton",
            Description: "Knitted cotton t-shirt with short sleeves.");

        var result = _classifier.ClassifyProduct(request);

        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.85);
        result.RequiresBrokerReview.Should().BeFalse();
        result.ReviewReason.Should().BeNull();
    }

    [Fact]
    public void ClassifyProduct_LowConfidence_RequiresBrokerReview()
    {
        var request = new ProductClassificationRequest(
            Title: "Generic thing box",
            Category: null,
            Material: null,
            Description: null);

        var result = _classifier.ClassifyProduct(request);

        result.ConfidenceScore.Should().BeLessThan(0.80);
        result.RequiresBrokerReview.Should().BeTrue();
        result.ReviewReason.Should().Contain("below autonomous clearance threshold");
    }

    [Fact]
    public void ClassifyProduct_RestrictedChemicalsKeyword_RequiresBrokerReview()
    {
        var request = new ProductClassificationRequest(
            Title: "Organic Industrial Chemicals for Cleaning",
            Category: "Industrial",
            Material: "Liquid chemical solution");

        var result = _classifier.ClassifyProduct(request);

        result.RequiresBrokerReview.Should().BeTrue();
        result.ReviewReason.Should().Contain("Restricted or dual-use goods trigger detected: 'chemicals'");
    }

    [Fact]
    public void ClassifyProduct_RestrictedDroneKeyword_RequiresBrokerReview()
    {
        var request = new ProductClassificationRequest(
            Title: "Aerial 4K Camera Drone Quadcopter",
            Category: "Electronics",
            Material: "Carbon Fiber");

        var result = _classifier.ClassifyProduct(request);

        result.RequiresBrokerReview.Should().BeTrue();
        result.ReviewReason.Should().Contain("drone");
    }

    [Fact]
    public void ClassifyProduct_RestrictedRadioKeyword_RequiresBrokerReview()
    {
        var request = new ProductClassificationRequest(
            Title: "Long Range Radio Transmitter and Receiver Unit",
            Category: "Communications",
            Material: "Electronics");

        var result = _classifier.ClassifyProduct(request);

        result.RequiresBrokerReview.Should().BeTrue();
        result.ReviewReason.Should().Contain("radio");
    }

    [Fact]
    public void ClassifyProduct_RestrictedMedicalKeyword_RequiresBrokerReview()
    {
        var request = new ProductClassificationRequest(
            Title: "Precision Diagnostic Medical Equipment and Stethoscope",
            Category: "Healthcare",
            Material: "Stainless Steel");

        var result = _classifier.ClassifyProduct(request);

        result.RequiresBrokerReview.Should().BeTrue();
        result.ReviewReason.Should().Contain("medical");
    }

    #endregion

    #region 4. HS Details & Candidate Suggestions Tests

    [Fact]
    public void GetHsCodeDetails_ValidCode_ReturnsCompleteMetadata()
    {
        var details = _classifier.GetHsCodeDetails("6109.10");

        details.Should().NotBeNull();
        details!.HsCode.Should().Be("6109.10");
        details.Heading.Should().Contain("T-shirts");
        details.NationalTariffLines.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void GetHsCodeDetails_NonExistentCode_ReturnsNull()
    {
        var details = _classifier.GetHsCodeDetails("9999.99");
        details.Should().BeNull();
    }

    [Fact]
    public void SuggestHsCandidates_MatchingQuery_ReturnsRankedCandidates()
    {
        var suggestions = _classifier.SuggestHsCandidates("laptop");

        suggestions.Should().NotBeEmpty();
        suggestions[0].HsCode.Should().Be("8471.30");
        suggestions[0].RelevanceScore.Should().BeGreaterThan(0.60);
    }

    #endregion

    #region 5. Tariff & Landed Cost Mathematics Tests

    [Fact]
    public void CalculateLandedTariff_ComputesExactCifValue()
    {
        // FOB = $10,000, Freight = $1,500, Insurance = $250 => CIF = $11,750
        var request = new TariffCalculationRequest(
            FobPrice: 10000m,
            InternationalFreight: 1500m,
            MarineInsurance: 250m,
            DestinationCountry: "US",
            OriginCountry: "CN",
            HsCode: "8205.59");

        var result = _tariffService.CalculateLandedTariff(request);

        result.CifValue.Should().Be(11750.00m);
    }

    [Fact]
    public void CalculateLandedTariff_ComputesExactDutyAmount()
    {
        // CIF = 10,000, CustomDutyRate = 10% => Duty = $1,000
        var request = new TariffCalculationRequest(
            FobPrice: 9000m,
            InternationalFreight: 800m,
            MarineInsurance: 200m,
            DestinationCountry: "US",
            OriginCountry: "CN",
            HsCode: "8205.59",
            CustomDutyRate: 0.10m);

        var result = _tariffService.CalculateLandedTariff(request);

        result.CifValue.Should().Be(10000.00m);
        result.DutyAmount.Should().Be(1000.00m);
        result.EffectiveDutyRate.Should().Be(0.10m);
    }

    [Fact]
    public void CalculateLandedTariff_ComputesExactExciseAmount()
    {
        // Perfume into GCC: CIF = 10,000, Duty = 5% (500), Excise = (CIF + Duty) * 50% = 10,500 * 0.50 = 5,250
        var request = new TariffCalculationRequest(
            FobPrice: 10000m,
            InternationalFreight: 0m,
            MarineInsurance: 0m,
            DestinationCountry: "AE",
            OriginCountry: "FR",
            HsCode: "3303.00",
            CustomDutyRate: 0.05m,
            CustomExciseRate: 0.50m);

        var result = _tariffService.CalculateLandedTariff(request);

        result.DutyAmount.Should().Be(500.00m);
        result.ExciseAmount.Should().Be(5250.00m);
    }

    [Fact]
    public void CalculateLandedTariff_ComputesExactVatAmount()
    {
        // CIF = 1,000, Duty = 300, DAPS = 0, Excise = 0, VAT 19%:
        // Tax base = 1,000 + 300 = 1,300 => VAT = 1,300 * 0.19 = 247.00
        var request = new TariffCalculationRequest(
            FobPrice: 1000m,
            InternationalFreight: 0m,
            MarineInsurance: 0m,
            DestinationCountry: "DZ",
            OriginCountry: "CN",
            HsCode: "8471.30", // Laptops: DAPS 0%, Duty 5%
            CustomDutyRate: 0.30m,
            CustomVatRate: 0.19m);

        var result = _tariffService.CalculateLandedTariff(request);

        result.CifValue.Should().Be(1000.00m);
        result.DutyAmount.Should().Be(300.00m);
        result.VatAmount.Should().Be(247.00m);
    }

    [Fact]
    public void CalculateLandedTariff_TotalLandedCostEqualsExactSumOfAllComponents()
    {
        var request = new TariffCalculationRequest(
            FobPrice: 5000m,
            InternationalFreight: 450m,
            MarineInsurance: 50m,
            DestinationCountry: "DZ",
            OriginCountry: "CN",
            HsCode: "6109.10", // Cotton T-shirts: Duty 30%, DAPS 30%, VAT 19%
            IsCommercial: true);

        var result = _tariffService.CalculateLandedTariff(request);

        var expectedSum = result.CifValue
                          + result.DutyAmount
                          + result.DapsAmount
                          + result.ExciseAmount
                          + result.VatAmount
                          + result.CustomsFeeTotal;

        result.TotalLandedCost.Should().Be(expectedSum);
    }

    #endregion

    #region 6. De Minimis Threshold Exemption Tests

    [Fact]
    public void CheckDeMinimisEligibility_AlgeriaPersonalUnder100Usd_ExemptFromDuty()
    {
        var result = _tariffService.CheckDeMinimisEligibility(
            destinationCountry: "DZ",
            cifValueUsd: 85.00m,
            isCommercial: false);

        result.IsExemptFromDuty.Should().BeTrue();
        result.StatutoryThresholdUsd.Should().Be(100.00m);
        result.DestinationCountry.Should().Be("DZ");
    }

    [Fact]
    public void CheckDeMinimisEligibility_AlgeriaPersonalOver100Usd_NotExempt()
    {
        var result = _tariffService.CheckDeMinimisEligibility(
            destinationCountry: "DZ",
            cifValueUsd: 150.00m,
            isCommercial: false);

        result.IsExemptFromDuty.Should().BeFalse();
        result.LegalBasis.Should().Contain("exceeds statutory threshold");
    }

    [Fact]
    public void CheckDeMinimisEligibility_AlgeriaCommercialUnder100Usd_NotExempt()
    {
        var result = _tariffService.CheckDeMinimisEligibility(
            destinationCountry: "DZ",
            cifValueUsd: 50.00m,
            isCommercial: true);

        result.IsExemptFromDuty.Should().BeFalse();
        result.LegalBasis.Should().Contain("Commercial import into Algeria");
    }

    [Fact]
    public void CheckDeMinimisEligibility_EuUnder150Eur_ExemptFromDutyButSubjectToVat()
    {
        // Under €150 (~$165 USD): exempt from duty, but import VAT is mandatory under IOSS
        var result = _tariffService.CheckDeMinimisEligibility(
            destinationCountry: "DE",
            cifValueUsd: 120.00m);

        result.IsExemptFromDuty.Should().BeTrue();
        result.IsExemptFromVat.Should().BeFalse();
        result.LegalBasis.Should().Contain("import VAT remains mandatory");
    }

    [Fact]
    public void CheckDeMinimisEligibility_EuOver150Eur_SubjectToBothDutyAndVat()
    {
        var result = _tariffService.CheckDeMinimisEligibility(
            destinationCountry: "FR",
            cifValueUsd: 250.00m);

        result.IsExemptFromDuty.Should().BeFalse();
        result.IsExemptFromVat.Should().BeFalse();
    }

    [Fact]
    public void CheckDeMinimisEligibility_GccUnder300Aed_ExemptFromDuty()
    {
        // Under 300 AED (~$81.68 USD)
        var result = _tariffService.CheckDeMinimisEligibility(
            destinationCountry: "AE",
            cifValueUsd: 65.00m);

        result.IsExemptFromDuty.Should().BeTrue();
        result.StatutoryThresholdUsd.Should().Be(81.68m);
    }

    #endregion

    #region 7. Preferential Trade Agreements (PTA) Tests

    [Fact]
    public void EvaluatePtaEligibility_EuAlgeriaWithEur1Certificate_0PercentIndustrialDuty()
    {
        // Cotton T-shirts (HS 6109.10) originating in France imported into Algeria with EUR.1 certificate
        var result = _tariffService.EvaluatePtaEligibility(
            originCountry: "FR",
            destinationCountry: "DZ",
            hsCode: "6109.10",
            hasCertificateOfOrigin: true);

        result.IsPtaApplicable.Should().BeTrue();
        result.AgreementName.Should().Be("EU-Algeria Association Agreement");
        result.PreferentialDutyRate.Should().Be(0.00m);
        result.DutyDiscountPercentage.Should().Be(100.0m);
        result.CertificateOfOriginVerified.Should().BeTrue();
    }

    [Fact]
    public void EvaluatePtaEligibility_EuAlgeriaWithoutCertificate_FallsBackToMfn()
    {
        var result = _tariffService.EvaluatePtaEligibility(
            originCountry: "DE",
            destinationCountry: "DZ",
            hsCode: "6109.10",
            hasCertificateOfOrigin: false);

        result.IsPtaApplicable.Should().BeFalse();
        result.PreferentialDutyRate.Should().Be(result.StandardMfnDutyRate);
        result.DutyDiscountPercentage.Should().Be(0.0m);
        result.EligibilityReason.Should().Contain("missing EUR.1 Movement Certificate");
    }

    [Fact]
    public void EvaluatePtaEligibility_GaftaWithCertificateOfOrigin_0PercentDuty()
    {
        // Shipment between UAE (AE) and Algeria (DZ)
        var result = _tariffService.EvaluatePtaEligibility(
            originCountry: "AE",
            destinationCountry: "DZ",
            hsCode: "6203.42",
            hasCertificateOfOrigin: true);

        result.IsPtaApplicable.Should().BeTrue();
        result.AgreementName.Should().Be("GAFTA (Greater Arab Free Trade Area)");
        result.PreferentialDutyRate.Should().Be(0.00m);
        result.CertificateType.Should().Be("Arab League Certificate of Origin");
    }

    [Fact]
    public void EvaluatePtaEligibility_GaftaWithoutCertificate_FallsBackToMfn()
    {
        var result = _tariffService.EvaluatePtaEligibility(
            originCountry: "SA",
            destinationCountry: "DZ",
            hsCode: "6203.42",
            hasCertificateOfOrigin: false);

        result.IsPtaApplicable.Should().BeFalse();
        result.EligibilityReason.Should().Contain("mandatory Arab League Certificate of Origin was not provided");
    }

    [Fact]
    public void EvaluatePtaEligibility_AfcftaWithCertificateOfOrigin_0PercentDuty()
    {
        // Shipment between Nigeria (NG) and Algeria (DZ)
        var result = _tariffService.EvaluatePtaEligibility(
            originCountry: "NG",
            destinationCountry: "DZ",
            hsCode: "8205.59",
            hasCertificateOfOrigin: true);

        result.IsPtaApplicable.Should().BeTrue();
        result.AgreementName.Should().Be("AfCFTA (African Continental Free Trade Area)");
        result.PreferentialDutyRate.Should().Be(0.00m);
        result.CertificateType.Should().Be("AfCFTA Certificate of Origin");
    }

    [Fact]
    public void EvaluatePtaEligibility_NoApplicablePta_FallsBackToMfn()
    {
        // China (CN) to Algeria (DZ) -> No PTA
        var result = _tariffService.EvaluatePtaEligibility(
            originCountry: "CN",
            destinationCountry: "DZ",
            hsCode: "8471.30",
            hasCertificateOfOrigin: true);

        result.IsPtaApplicable.Should().BeFalse();
        result.AgreementName.Should().Be("WTO Most Favored Nation (MFN)");
    }

    #endregion

    #region 8. Digital Customs Declarations & Cryptography Tests

    [Fact]
    public void CreateDeclaration_SingleAdministrativeDocument_GeneratesSignedDocumentAndQr()
    {
        var declarant = new DeclarantParty("DEC-01", "Global Logistics SARL", "DZ-TAX-1029384", "12 Rue Didouche Mourad, Algiers", "DZ", "customs@globallogistics.dz");
        var consignee = new ConsigneeParty("CON-01", "Algiers Retail SPA", "DZ-TAX-999888", "45 Blvd Colonel Amirouche, Algiers", "DZ");
        var items = new List<DeclarationItem>
        {
            new("ITEM-01", "Cotton Crewneck T-Shirts", "6109.10", 1000, "Units", 10.00m, 10000m, 10500m, "FR", 250m, 220m)
        };

        var request = new CreateDeclarationRequest(
            Type: CustomsDeclarationType.SingleAdministrativeDocument,
            Declarant: declarant,
            Consignee: consignee,
            OriginCountry: "FR",
            DestinationCountry: "DZ",
            Items: items,
            Freight: 1000m,
            Insurance: 200m,
            Currency: "USD");

        var declaration = _declarationService.CreateDeclaration(request);

        declaration.DeclarationId.Should().StartWith("DAU-DZ-");
        declaration.Status.Should().Be(DeclarationStatus.Approved);
        declaration.HmacSignature.Should().NotBeNullOrWhiteSpace();
        declaration.QrPayload.Should().NotBeNullOrWhiteSpace();
        declaration.ItemsHashSha256.Should().HaveLength(64);
        declaration.Metadata.Should().ContainKey("DocumentTitle");
        declaration.Metadata!["DocumentTitle"].Should().Contain("Document Administratif Unique");
    }

    [Fact]
    public void CreateDeclaration_Eur1Certificate_GeneratesPreferentialTradeMetadata()
    {
        var declarant = new DeclarantParty("EXP-01", "Euro Export SAS", "FR-EORI-12345678", "Lyon Industrial Zone", "FR", "export@euroexport.fr");
        var consignee = new ConsigneeParty("IMP-01", "Maghreb Imports EURL", "DZ-TAX-555444", "Oran Port Hub", "DZ");
        var items = new List<DeclarationItem>
        {
            new("ITEM-01", "Men's Cotton Chinos", "6203.42", 500, "Units", 25.00m, 12500m, 13000m, "FR", 300m, 270m)
        };

        var request = new CreateDeclarationRequest(
            Type: CustomsDeclarationType.Eur1CertificateOfOrigin,
            Declarant: declarant,
            Consignee: consignee,
            OriginCountry: "FR",
            DestinationCountry: "DZ",
            Items: items,
            Freight: 500m,
            Insurance: 100m);

        var declaration = _declarationService.CreateDeclaration(request);

        declaration.DeclarationId.Should().StartWith("EUR1-DZ-");
        declaration.Metadata.Should().ContainKey("Box2_PreferentialAgreement");
        declaration.Metadata!["Box2_PreferentialAgreement"].Should().Contain("EU-Algeria Association Agreement");
    }

    [Fact]
    public void CreateDeclaration_AtaCarnet_GeneratesTemporaryAdmissionMetadata()
    {
        var declarant = new DeclarantParty("EXP-02", "Tech Exhibition Ltd", "GB-EORI-998877", "London, UK", "GB", "carnet@techexpo.com");
        var consignee = new ConsigneeParty("IMP-02", "Dubai World Trade Centre", "AE-TAX-112233", "Sheikh Zayed Rd, Dubai", "AE");
        var items = new List<DeclarationItem>
        {
            new("ITEM-01", "Demonstration Computer Monitors", "8528.52", 10, "Units", 800.00m, 8000m, 8200m, "GB", 120m, 100m)
        };

        var request = new CreateDeclarationRequest(
            Type: CustomsDeclarationType.AtaCarnet,
            Declarant: declarant,
            Consignee: consignee,
            OriginCountry: "GB",
            DestinationCountry: "AE",
            Items: items,
            Freight: 300m,
            Insurance: 50m);

        var declaration = _declarationService.CreateDeclaration(request);

        declaration.DeclarationId.Should().StartWith("ATA-AE-");
        declaration.Metadata.Should().ContainKey("CarnetNumber");
        declaration.Metadata.Should().ContainKey("DutySuspensionGuarantee");
    }

    [Fact]
    public void VerifyDeclarationQr_AuthenticQrPayload_ReturnsAuthorized()
    {
        var secretKey = "Customs_Border_Secret_2026";
        var declarant = new DeclarantParty("DEC-01", "Mediterranean Transit", "DZ-TAX-1234", "Algiers", "DZ", "transit@med.dz");
        var consignee = new ConsigneeParty("CON-01", "Algiers Tech", "DZ-TAX-5678", "Algiers", "DZ");
        var items = new List<DeclarationItem>
        {
            new("ITEM-01", "Portable Laptops", "8471.30", 50, "Units", 1000m, 50000m, 51000m, "FR", 100m, 80m)
        };

        var request = new CreateDeclarationRequest(
            Type: CustomsDeclarationType.SingleAdministrativeDocument,
            Declarant: declarant,
            Consignee: consignee,
            OriginCountry: "FR",
            DestinationCountry: "DZ",
            Items: items,
            Freight: 1000m,
            Insurance: 200m,
            SecretSigningKey: secretKey);

        var declaration = _declarationService.CreateDeclaration(request);

        // Verify QR at border
        var borderResult = _declarationService.VerifyDeclarationQr(declaration.QrPayload, secretKey);

        borderResult.IsValid.Should().BeTrue();
        borderResult.DeclarationId.Should().Be(declaration.DeclarationId);
        borderResult.IsExpired.Should().BeFalse();
        borderResult.IsRevoked.Should().BeFalse();
        borderResult.VerificationMessage.Should().Contain("Border transit authorized");
    }

    [Fact]
    public void VerifyDeclarationQr_TamperedLandedValue_RejectsTamperedPayload()
    {
        var secretKey = "Customs_Border_Secret_2026";
        var declarant = new DeclarantParty("DEC-01", "Mediterranean Transit", "DZ-TAX-1234", "Algiers", "DZ", "transit@med.dz");
        var consignee = new ConsigneeParty("CON-01", "Algiers Tech", "DZ-TAX-5678", "Algiers", "DZ");
        var items = new List<DeclarationItem>
        {
            new("ITEM-01", "Cotton T-Shirts", "6109.10", 100, "Units", 10m, 1000m, 1100m, "FR", 20m, 18m)
        };

        var request = new CreateDeclarationRequest(
            Type: CustomsDeclarationType.SingleAdministrativeDocument,
            Declarant: declarant,
            Consignee: consignee,
            OriginCountry: "FR",
            DestinationCountry: "DZ",
            Items: items,
            Freight: 100m,
            Insurance: 20m,
            SecretSigningKey: secretKey);

        var declaration = _declarationService.CreateDeclaration(request);

        // Decode original QR JSON, maliciously tamper landed value from $1,500+ down to $10 to evade tax
        var rawJson = Encoding.UTF8.GetString(Convert.FromBase64String(declaration.QrPayload));
        var node = JsonNode.Parse(rawJson)!.AsObject();
        node["val"] = 10.00m; // Maliciously reduced value!
        var tamperedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(node.ToJsonString()));

        // Border verification
        var borderResult = _declarationService.VerifyDeclarationQr(tamperedBase64, secretKey);

        borderResult.IsValid.Should().BeFalse();
        borderResult.VerificationMessage.Should().Contain("TamperedPayload: Cryptographic signature mismatch");
    }

    [Fact]
    public void VerifyDeclarationQr_WrongSecretKey_RejectsTamperedSignature()
    {
        var declarant = new DeclarantParty("DEC-01", "Air Logistics", "DZ-TAX-1", "Algiers", "DZ", "air@log.dz");
        var consignee = new ConsigneeParty("CON-01", "Imports SARL", "DZ-TAX-2", "Algiers", "DZ");
        var items = new List<DeclarationItem>
        {
            new("ITEM-01", "Chocolate Bars", "1806.32", 100, "Units", 5m, 500m, 550m, "FR", 20m, 18m)
        };

        var request = new CreateDeclarationRequest(
            Type: CustomsDeclarationType.SingleAdministrativeDocument,
            Declarant: declarant,
            Consignee: consignee,
            OriginCountry: "FR",
            DestinationCountry: "DZ",
            Items: items,
            Freight: 50m,
            Insurance: 10m,
            SecretSigningKey: "Correct_Key_123");

        var declaration = _declarationService.CreateDeclaration(request);

        // Verify with invalid secret key
        var borderResult = _declarationService.VerifyDeclarationQr(declaration.QrPayload, "Wrong_Key_999");

        borderResult.IsValid.Should().BeFalse();
        borderResult.VerificationMessage.Should().Contain("TamperedPayload");
    }

    [Fact]
    public void VerifyDeclarationQr_ExpiredDeclaration_RejectsBorderClearance()
    {
        var secretKey = "Customs_Border_Secret_2026";
        var id = "DAU-DZ-EXPIRED1";
        var ts = DateTime.UtcNow.AddDays(-40).ToString("O");
        var exp = DateTime.UtcNow.AddDays(-10).ToString("O"); // Expired 10 days ago!

        var canonical = $"{id}:SingleAdministrativeDocument:6109.10:1000.00:USD:Declarant Inc:DZ:dummyhash:{ts}:{exp}";
        var sigBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(canonical));
        var sig = Convert.ToHexStringLower(sigBytes);

        var qrObject = new JsonObject
        {
            ["id"] = id,
            ["type"] = "SingleAdministrativeDocument",
            ["hs"] = "6109.10",
            ["val"] = 1000.00m,
            ["cur"] = "USD",
            ["dec"] = "Declarant Inc",
            ["dest"] = "DZ",
            ["ts"] = ts,
            ["exp"] = exp,
            ["ihash"] = "dummyhash",
            ["sig"] = sig
        };
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(qrObject.ToJsonString()));

        var borderResult = _declarationService.VerifyDeclarationQr(base64, secretKey);

        borderResult.IsValid.Should().BeFalse();
        borderResult.IsExpired.Should().BeTrue();
        borderResult.VerificationMessage.Should().Contain("DeclarationExpired");
    }

    #endregion

    #region 9. Revocation, Concurrency, and Service Registration Tests

    [Fact]
    public void RevokeDeclaration_MarksDeclarationRevoked_AndRejectsAtBorder()
    {
        var secretKey = "Customs_Border_Secret_2026";
        var declarant = new DeclarantParty("DEC-01", "Transit Express", "DZ-TAX-1", "Algiers", "DZ", "transit@exp.dz");
        var consignee = new ConsigneeParty("CON-01", "Algiers Stores", "DZ-TAX-2", "Algiers", "DZ");
        var items = new List<DeclarationItem>
        {
            new("ITEM-01", "Men's Cotton Trousers", "6203.42", 50, "Units", 30m, 1500m, 1600m, "FR", 30m, 25m)
        };

        var request = new CreateDeclarationRequest(
            Type: CustomsDeclarationType.SingleAdministrativeDocument,
            Declarant: declarant,
            Consignee: consignee,
            OriginCountry: "FR",
            DestinationCountry: "DZ",
            Items: items,
            Freight: 100m,
            Insurance: 20m,
            SecretSigningKey: secretKey);

        var declaration = _declarationService.CreateDeclaration(request);

        // Revoke the declaration
        var revoked = _declarationService.RevokeDeclaration(declaration.DeclarationId, "Fraudulent invoice detected by financial intelligence unit.");
        revoked.Should().BeTrue();

        // Check internal status
        var updated = _declarationService.GetDeclaration(declaration.DeclarationId);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(DeclarationStatus.Revoked);
        updated.RevocationReason.Should().Contain("Fraudulent invoice");

        // Border verification should now fail
        var borderResult = _declarationService.VerifyDeclarationQr(declaration.QrPayload, secretKey);
        borderResult.IsValid.Should().BeFalse();
        borderResult.IsRevoked.Should().BeTrue();
        borderResult.VerificationMessage.Should().Contain("DeclarationRevoked");
    }

    [Fact]
    public void DigitalDeclarationService_ConcurrentSubmissions_ThreadSafe()
    {
        const int concurrentCount = 50;
        var declarant = new DeclarantParty("DEC-CONC", "Concurrent Carrier", "TAX-999", "Port", "DZ", "conc@c.dz");
        var consignee = new ConsigneeParty("CON-CONC", "Concurrent Receiver", "TAX-888", "Warehouse", "DZ");

        var createdIds = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, concurrentCount, i =>
        {
            var items = new List<DeclarationItem>
            {
                new($"ITEM-{i}", $"Item {i} Batch", "6109.10", 10 + i, "Units", 15m, (10 + i) * 15m, ((10 + i) * 15m) + 50m, "FR", 10m, 8m)
            };

            var req = new CreateDeclarationRequest(
                Type: CustomsDeclarationType.SingleAdministrativeDocument,
                Declarant: declarant,
                Consignee: consignee,
                OriginCountry: "FR",
                DestinationCountry: "DZ",
                Items: items,
                Freight: 50m,
                Insurance: 10m);

            var dec = _declarationService.CreateDeclaration(req);
            createdIds.Add(dec.DeclarationId);
        });

        createdIds.Should().HaveCount(concurrentCount);
        createdIds.Distinct().Should().HaveCount(concurrentCount);

        foreach (var id in createdIds)
        {
            var retrieved = _declarationService.GetDeclaration(id);
            retrieved.Should().NotBeNull();
            retrieved!.DeclarationId.Should().Be(id);
        }
    }

    [Fact]
    public void AddCustomsServices_RegistersAllCustomsServicesAsSingletonsWithoutBuildServiceProvider()
    {
        // Explicit boundary requirement: Direct ServiceDescriptor assertion on ServiceCollection without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddCustomsServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHsCodeClassifierEngine) &&
            d.ImplementationType == typeof(HsCodeClassifierEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDynamicCustomsTariffService) &&
            d.ImplementationType == typeof(DynamicCustomsTariffService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDigitalCustomsDeclarationService) &&
            d.ImplementationType == typeof(DigitalCustomsDeclarationService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void CalculateLandedTariff_NegativePrices_ThrowsArgumentOutOfRangeException()
    {
        var act1 = () => _tariffService.CalculateLandedTariff(new TariffCalculationRequest(-100m, 50m, 10m, "DZ", "FR", "6109.10"));
        act1.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*FOB price*");

        var act2 = () => _tariffService.CalculateLandedTariff(new TariffCalculationRequest(100m, -50m, 10m, "DZ", "FR", "6109.10"));
        act2.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Freight*");

        var act3 = () => _tariffService.CalculateLandedTariff(new TariffCalculationRequest(100m, 50m, -10m, "DZ", "FR", "6109.10"));
        act3.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*insurance*");
    }

    #endregion
}
