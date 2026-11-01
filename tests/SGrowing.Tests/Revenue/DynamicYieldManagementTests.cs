using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Revenue.YieldManagement;
using Xunit;

namespace SGrowing.Tests.Revenue;

public sealed class DynamicYieldManagementTests
{
    private readonly DynamicYieldManagementEngine _yieldEngine = new();
    private readonly PriceElasticityDemandForecaster _elasticityForecaster = new();
    private readonly SpotCapacityAuctioneerService _auctioneer = new();

    private CapacityResourcePool CreateSampleFlightPool(string poolId = "POOL-ALG-ORN-01", int totalUnits = 100, int bookedUnits = 20)
    {
        var classes = new List<FareClassDemandProfile>
        {
            new(FareClassTier.Y_FullFarePremium, FareDzd: 25000m, ForecastedDemandMean: 25.0, ForecastedDemandStdDev: 5.0),
            new(FareClassTier.M_StandardCommercial, FareDzd: 15000m, ForecastedDemandMean: 40.0, ForecastedDemandStdDev: 8.0),
            new(FareClassTier.B_DiscountEconomy, FareDzd: 8000m, ForecastedDemandMean: 50.0, ForecastedDemandStdDev: 10.0)
        };

        var pool = new CapacityResourcePool(
            PoolId: poolId,
            Type: CapacityType.ExpressDeliverySlot,
            TotalCapacityUnits: totalUnits,
            BookedUnits: bookedUnits,
            DepartureOrCutoffUtc: DateTime.UtcNow.AddDays(2),
            FareClasses: classes
        );

        _yieldEngine.RegisterResourcePool(pool);
        return pool;
    }

    #region EMSRb Capacity Protection Tests

    [Fact]
    public void CalculateProtectionLevels_EMSRb_ProtectsHigherYieldTiers()
    {
        var pool = CreateSampleFlightPool();
        var levels = _yieldEngine.CalculateProtectionLevels(pool.PoolId);

        levels.Should().HaveCount(3);
        // Premium class Y should protect a positive allocation against M
        levels[0].Tier.Should().Be(FareClassTier.Y_FullFarePremium);
        levels[0].ProtectedCapacityUnits.Should().BeGreaterThan(15);

        // Combined Y+M should protect even more units against discount class B
        levels[1].Tier.Should().Be(FareClassTier.M_StandardCommercial);
        levels[1].ProtectedCapacityUnits.Should().BeGreaterThan(levels[0].ProtectedCapacityUnits);

        // Lowest class has 0 protection against lower
        levels[2].Tier.Should().Be(FareClassTier.B_DiscountEconomy);
        levels[2].ProtectedCapacityUnits.Should().Be(0);
    }

    [Fact]
    public void EvaluateBooking_FullFare_AcceptedWhenCapacityAvailable()
    {
        var pool = CreateSampleFlightPool(totalUnits: 100, bookedUnits: 50);

        var request = new EvaluateBookingRequest(pool.PoolId, FareClassTier.Y_FullFarePremium, RequestedUnits: 5);
        var result = _yieldEngine.EvaluateBooking(request);

        result.Accepted.Should().BeTrue();
        result.TotalQuoteDzd.Should().Be(25000m * 5);
        result.RemainingAvailableUnits.Should().Be(45);
    }

    [Fact]
    public void EvaluateBooking_ExceedsTotalAvailable_Rejected()
    {
        var pool = CreateSampleFlightPool(totalUnits: 100, bookedUnits: 95); // 5 available

        var request = new EvaluateBookingRequest(pool.PoolId, FareClassTier.Y_FullFarePremium, RequestedUnits: 10);
        var result = _yieldEngine.EvaluateBooking(request);

        result.Accepted.Should().BeFalse();
        result.DecisionReason.Should().Contain("exceed total available");
    }

    [Fact]
    public void EvaluateBooking_DiscountBooking_RejectedWhenEncroachingOnProtectedUnits()
    {
        // 100 total, 50 already booked -> 50 available
        // If higher tiers require protecting 55 units, any discount booking must be rejected!
        var pool = CreateSampleFlightPool(totalUnits: 100, bookedUnits: 55); // 45 available

        // Protection level for Y+M is > 50 units
        var request = new EvaluateBookingRequest(pool.PoolId, FareClassTier.B_DiscountEconomy, RequestedUnits: 5);
        var result = _yieldEngine.EvaluateBooking(request);

        result.Accepted.Should().BeFalse();
        result.DecisionReason.Should().Contain("protect");
    }

    [Fact]
    public void EvaluateBooking_DiscountBooking_AcceptedWhenCapacityBeyondProtectedUnits()
    {
        // 200 total, only 10 booked -> 190 available
        var pool = CreateSampleFlightPool(totalUnits: 200, bookedUnits: 10);

        var request = new EvaluateBookingRequest(pool.PoolId, FareClassTier.B_DiscountEconomy, RequestedUnits: 5);
        var result = _yieldEngine.EvaluateBooking(request);

        result.Accepted.Should().BeTrue();
        result.TotalQuoteDzd.Should().Be(8000m * 5);
    }

    [Fact]
    public void InverseStandardNormalCdf_KnownStandardQuantiles_MatchesExpected()
    {
        // P(Z < 0) = 0.50 => Z = 0
        var zMedian = DynamicYieldManagementEngine.InverseStandardNormalCdf(0.50);
        zMedian.Should().BeInRange(-0.01, 0.01);

        // P(Z < 0.975) ≈ 1.96
        var z975 = DynamicYieldManagementEngine.InverseStandardNormalCdf(0.975);
        z975.Should().BeInRange(1.95, 1.97);

        // P(Z < 0.8413) ≈ 1.0
        var z84 = DynamicYieldManagementEngine.InverseStandardNormalCdf(0.8413);
        z84.Should().BeInRange(0.99, 1.01);
    }

    #endregion

    #region Price Elasticity Tests

    [Fact]
    public void CalculateElasticity_ElasticDemand_ReportsElasticAndComputesOptimalPrice()
    {
        // Price increased from 1000 to 1200 (+20%), Demand dropped from 100 to 70 (-30%)
        // Elasticity = (-30 / 85) / (200 / 1100) = -0.3529 / 0.1818 = -1.94 (Elastic)
        var report = _elasticityForecaster.CalculateElasticity(
            capacityType: CapacityType.ExpressDeliverySlot,
            price1Dzd: 1000m,
            demand1: 100.0,
            price2Dzd: 1200m,
            demand2: 70.0,
            marginalCostDzd: 400m
        );

        report.ElasticityClassification.Should().Be("Elastic");
        report.PointElasticity.Should().BeLessThan(-1.0);
        report.OptimalRevenuePriceDzd.Should().BeGreaterThan(400m);
    }

    [Fact]
    public void CalculateElasticity_InelasticDemand_ReportsInelastic()
    {
        // Price increased from 1000 to 1500 (+50%), Demand dropped from 100 to 95 (-5%)
        var report = _elasticityForecaster.CalculateElasticity(
            capacityType: CapacityType.PalletStorageBay,
            price1Dzd: 1000m,
            demand1: 100.0,
            price2Dzd: 1500m,
            demand2: 95.0,
            marginalCostDzd: 300m
        );

        report.ElasticityClassification.Should().Be("Inelastic");
        report.PointElasticity.Should().BeInRange(-0.95, 0.0);
    }

    [Fact]
    public void CalculateElasticity_InvalidPricesOrDemands_ThrowsArgumentException()
    {
        var act1 = () => _elasticityForecaster.CalculateElasticity(CapacityType.ExpressDeliverySlot, -10m, 100, 20m, 80, 5m);
        act1.Should().Throw<ArgumentException>();

        var act2 = () => _elasticityForecaster.CalculateElasticity(CapacityType.ExpressDeliverySlot, 10m, 0, 20m, 80, 5m);
        act2.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Dutch Spot Auction Tests

    [Fact]
    public void SpotCapacityAuctioneer_CreateAndDecay_ReducesPriceOverTime()
    {
        var lot = _auctioneer.CreateAuctionLot(new CreateSpotAuctionRequest(
            CapacityType: CapacityType.RefrigeratedTransportUnit,
            QuantityUnits: 2,
            StartingPriceDzd: 50000m,
            MinimumFloorPriceDzd: 25000m,
            DecrementDzd: 5000m,
            DecayIntervalSeconds: 60, // Drops 5,000 every 60s
            DurationMinutes: 10
        ));

        lot.CurrentPriceDzd.Should().Be(50000m);
        lot.Status.Should().Be(AuctionLotStatus.Active);

        // Simulate 2 minutes (120s) later: 2 decrements of 5,000 = -10,000 -> 40,000
        var futureTime = lot.AuctionStartedAtUtc.AddSeconds(125);
        var decayedLot = _auctioneer.GetCurrentLotState(lot.LotId, futureTime);

        decayedLot.CurrentPriceDzd.Should().Be(40000m);
    }

    [Fact]
    public void SpotCapacityAuctioneer_DecayReachesFloor_ClampsToFloor()
    {
        var lot = _auctioneer.CreateAuctionLot(new CreateSpotAuctionRequest(
            CapacityType: CapacityType.AirCargoVolumeM3,
            QuantityUnits: 10,
            StartingPriceDzd: 10000m,
            MinimumFloorPriceDzd: 4000m,
            DecrementDzd: 3000m,
            DecayIntervalSeconds: 30,
            DurationMinutes: 5
        ));

        // After 10 intervals (300s), price would be -20,000 without floor clamp
        var futureTime = lot.AuctionStartedAtUtc.AddSeconds(120); // 4 decrements of 3,000 = -12,000
        var decayedLot = _auctioneer.GetCurrentLotState(lot.LotId, futureTime);

        decayedLot.CurrentPriceDzd.Should().Be(4000m); // Clamped to floor
    }

    [Fact]
    public void SpotCapacityAuctioneer_SubmitBid_SettlesAuctionAndRecordsWinner()
    {
        var lot = _auctioneer.CreateAuctionLot(new CreateSpotAuctionRequest(
            CapacityType: CapacityType.ExpressDeliverySlot,
            QuantityUnits: 1,
            StartingPriceDzd: 20000m,
            MinimumFloorPriceDzd: 10000m,
            DecrementDzd: 2000m,
            DecayIntervalSeconds: 60,
            DurationMinutes: 10
        ));

        var bidResult = _auctioneer.SubmitBid(new SubmitAuctionBidRequest(lot.LotId, "BIDDER-ARAMEX-01"));

        bidResult.Status.Should().Be(AuctionLotStatus.Settled);
        bidResult.WinningBidderId.Should().Be("BIDDER-ARAMEX-01");
        bidResult.SettledAtUtc.Should().NotBeNull();

        // Subsequent bid should fail
        var secondBidAct = () => _auctioneer.SubmitBid(new SubmitAuctionBidRequest(lot.LotId, "BIDDER-YALIDINE-02"));
        secondBidAct.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot bid on auction lot*");
    }

    [Fact]
    public void SpotCapacityAuctioneer_CancelLot_MarksCanceled()
    {
        var lot = _auctioneer.CreateAuctionLot(new CreateSpotAuctionRequest(
            CapacityType: CapacityType.PalletStorageBay,
            QuantityUnits: 5,
            StartingPriceDzd: 30000m,
            MinimumFloorPriceDzd: 15000m,
            DecrementDzd: 3000m,
            DecayIntervalSeconds: 60,
            DurationMinutes: 10
        ));

        _auctioneer.CancelLot(lot.LotId).Should().BeTrue();

        var state = _auctioneer.GetCurrentLotState(lot.LotId);
        state.Status.Should().Be(AuctionLotStatus.Canceled);
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddYieldManagementServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddYieldManagementServices();

        services.Should().Contain(d => d.ServiceType == typeof(DynamicYieldManagementEngine) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(PriceElasticityDemandForecaster) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(SpotCapacityAuctioneerService) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
