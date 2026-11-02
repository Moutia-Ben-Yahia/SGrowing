using FluentAssertions;
using SGrowing.Api.Modules.Sustainability.CarbonExchange;
using Xunit;

namespace SGrowing.Tests.Sustainability;

public sealed class CarbonExchangeTests
{
    private readonly GhgEmissionsCalculatorEngine _calcEngine = new();
    private readonly RecTokenizationAndMatchingService _matchingService = new();
    private readonly NetZeroOptimizationRecommender _recommender = new();

    #region GHG Emissions Calculation Tests

    [Fact]
    public void CalculateFootprint_ValidInputs_ComputesScope123AndCbamTariff()
    {
        var req = new CalculateFootprintRequest(
            TenantId: "TENANT-ORAN-LOGISTICS",
            DieselLitersBurned: 1000.0,
            GridElectricityKwh: 10000.0,
            GridRecCoverageRatio: 0.50, // 50% renewable PPA coverage
            FreightTonKilometers: 50000.0,
            FreightMode: TransportModeEmissions.DieselTruck,
            EuEtsPriceEurPerTonne: 85.0,
            EmbeddedEmissionsBenchmarkTonne: 5.0
        );

        var result = _calcEngine.CalculateFootprint(req);

        result.Should().NotBeNull();
        result.TenantId.Should().Be("TENANT-ORAN-LOGISTICS");

        // Scope 1: 1000L * 2.68 kg/L = 2680 kg
        result.Scope1TotalKgCo2e.Should().BeApproximately(2680.0, 0.01);

        // Scope 2 gross: 10000 * 0.475 = 4750 kg; 50% offset = 2375 kg; net = 2375 kg
        result.Scope2TotalKgCo2e.Should().BeApproximately(4750.0, 0.01);
        result.Scope2RecOffsetKgCo2e.Should().BeApproximately(2375.0, 0.01);

        // Scope 3: 50000 * 0.095 = 4750 kg
        result.Scope3TotalKgCo2e.Should().BeApproximately(4750.0, 0.01);

        // Net emissions = 2680 + 2375 + 4750 = 9805 kg = 9.805 tonnes
        result.NetEmissionsKgCo2e.Should().BeApproximately(9805.0, 0.01);

        // CBAM: excess = 9.805 - 5.0 = 4.805 tonnes; tax = 4.805 * 85.0 = 408.425 -> 408.43 EUR
        result.CbamTaxExposureEur.Should().BeApproximately(408.43, 0.05);
        result.LineItems.Should().HaveCount(3);
    }

    [Fact]
    public void CalculateFootprint_SubBenchmarkEmissions_ZeroCbamExposure()
    {
        var req = new CalculateFootprintRequest(
            TenantId: "TENANT-CLEAN-RAIL",
            DieselLitersBurned: 0.0,
            GridElectricityKwh: 1000.0,
            GridRecCoverageRatio: 1.0, // 100% green
            FreightTonKilometers: 10000.0,
            FreightMode: TransportModeEmissions.FreightRail,
            EuEtsPriceEurPerTonne: 85.0,
            EmbeddedEmissionsBenchmarkTonne: 10.0 // higher than net footprint
        );

        var result = _calcEngine.CalculateFootprint(req);

        result.Scope2RecOffsetKgCo2e.Should().Be(result.Scope2TotalKgCo2e);
        result.CbamTaxExposureEur.Should().Be(0.0);
    }

    #endregion

    #region REC Order Book Matching Tests

    [Fact]
    public void RecOrderBook_CrossingOrders_ExecutesMatchedTrade()
    {
        // Seller places 100 tonnes GoldStandard at 30 EUR/t
        var (sellOrder, sellMatches) = _matchingService.PlaceOrder(
            tenantId: "SELLER-SOLAR-PARK",
            side: OrderBookSide.Sell,
            quantityTonnes: 100.0,
            limitPriceEur: 30.00m,
            standard: CreditCertificateStandard.GoldStandard
        );

        sellMatches.Should().BeEmpty();
        sellOrder.Status.Should().Be(TradeStatus.Open);

        // Buyer places 60 tonnes at 32 EUR/t (crosses price)
        var (buyOrder, buyMatches) = _matchingService.PlaceOrder(
            tenantId: "BUYER-GREEN-FREIGHT",
            side: OrderBookSide.Buy,
            quantityTonnes: 60.0,
            limitPriceEur: 32.00m,
            standard: CreditCertificateStandard.GoldStandard
        );

        buyMatches.Should().HaveCount(1);
        var trade = buyMatches[0];
        trade.MatchedTonnes.Should().Be(60.0);
        trade.ExecutionPriceEurPerTonne.Should().Be(30.00m); // Maker price
        trade.BuyerTenantId.Should().Be("BUYER-GREEN-FREIGHT");
        trade.SellerTenantId.Should().Be("SELLER-SOLAR-PARK");

        buyOrder.Status.Should().Be(TradeStatus.Filled);
        buyOrder.FilledQuantityTonnes.Should().Be(60.0);

        _matchingService.GetExecutedTrades().Should().ContainSingle();
    }

    [Fact]
    public void RecOrderBook_SelfTrading_IsPreventedFromMatching()
    {
        // Seller places order
        _matchingService.PlaceOrder(
            tenantId: "TENANT-SAME",
            side: OrderBookSide.Sell,
            quantityTonnes: 50.0,
            limitPriceEur: 25.00m,
            standard: CreditCertificateStandard.IrecRenewableEnergy
        );

        // Same tenant places matching buy order
        var (buyOrder, buyMatches) = _matchingService.PlaceOrder(
            tenantId: "TENANT-SAME",
            side: OrderBookSide.Buy,
            quantityTonnes: 50.0,
            limitPriceEur: 28.00m,
            standard: CreditCertificateStandard.IrecRenewableEnergy
        );

        buyMatches.Should().BeEmpty("Tenants cannot wash-trade with themselves.");
        buyOrder.Status.Should().Be(TradeStatus.Open);
    }

    #endregion

    #region Certificate Minting & Retirement Tests

    [Fact]
    public void MintAndRetireCertificate_SuccessAndPreventsDoubleRetirement()
    {
        var cert = _matchingService.MintCertificate(
            standard: CreditCertificateStandard.GoldStandard,
            originFacility: "Hassi R'Mel Solar Concentrator",
            generationType: "Concentrated Solar Power",
            megawattHours: 500.0,
            co2eOffsetTonnes: 237.5,
            issuerCountry: "DZA",
            vintageYear: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        );

        cert.IsRetired.Should().BeFalse();
        cert.ProofHashHex.Should().NotBeNullOrWhiteSpace();

        // Retire certificate for corporate Scope 2 claims
        var receipt = _matchingService.RetireCertificate(
            certificateId: cert.CertificateId,
            tenantId: "TENANT-CORPORATE-HQ",
            reason: "2026 Q3 Scope 2 Neutralization"
        );

        receipt.Should().NotBeNull();
        receipt.CertificateId.Should().Be(cert.CertificateId);
        receipt.RetiredTonnes.Should().Be(237.5);
        receipt.ImmutableAuditProofHex.Should().NotBeNullOrWhiteSpace();

        // Attempting second retirement must throw InvalidOperationException
        var act = () => _matchingService.RetireCertificate(cert.CertificateId, "TENANT-ROGUE", "Double count attempt");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been retired*");
    }

    #endregion

    #region Decarbonization Recommendations Tests

    [Fact]
    public void GenerateRecommendations_HighEmissions_YieldsRankedMaccInterventions()
    {
        var assessment = new GhgAssessmentResult(
            AssessmentId: "GHG-MOCK",
            TenantId: "TENANT-HEAVY-INDUSTRIAL",
            TimestampUtc: DateTime.UtcNow,
            Scope1TotalKgCo2e: 15000.0,
            Scope2TotalKgCo2e: 20000.0,
            Scope3TotalKgCo2e: 35000.0,
            TotalGrossEmissionsKgCo2e: 70000.0,
            Scope2RecOffsetKgCo2e: 0.0,
            NetEmissionsKgCo2e: 70000.0,
            CbamTaxExposureEur: 5500.0,
            LineItems: []
        );

        var recommendations = _recommender.GenerateRecommendations(assessment);

        recommendations.Should().NotBeEmpty();
        // Should be ordered by lowest abatement cost first
        recommendations.Should().BeInAscendingOrder(r => r.AbatementCostEurPerTonne);
        recommendations.Select(r => r.ActionTitle).Should().ContainMatch("*Solar*");
        recommendations.Select(r => r.ActionTitle).Should().ContainMatch("*Rail*");
    }

    #endregion
}
