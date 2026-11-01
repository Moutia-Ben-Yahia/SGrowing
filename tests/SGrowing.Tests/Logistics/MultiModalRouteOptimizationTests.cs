using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Logistics.Routing;
using Xunit;

namespace SGrowing.Tests.Logistics;

public sealed class MultiModalRouteOptimizationTests
{
    private readonly DynamicWeatherHazardRadarService _radar = new();
    private readonly MultiModalRouteOptimizationEngine _engine;
    private readonly EvFleetEnergyRoutingService _evService = new();

    public MultiModalRouteOptimizationTests()
    {
        _engine = new MultiModalRouteOptimizationEngine(_radar);
    }

    #region GeoCoordinate & Distance Tests

    [Fact]
    public void GeoCoordinate_DistanceToKm_AlgiersToOran_MatchesHaversineDistance()
    {
        var algiers = new GeoCoordinate(36.7538, 3.0588);
        var oran = new GeoCoordinate(35.6987, -0.6349);

        var dist = algiers.DistanceToKm(oran);

        // Approximate straight-line distance between Algiers and Oran is ~350-370 km
        dist.Should().BeInRange(350.0, 375.0);
    }

    #endregion

    #region Weather Hazard Radar Tests

    [Fact]
    public void DynamicWeatherHazardRadar_RegisterAndRetrieve_ReturnsActiveHazards()
    {
        var hazard = new WeatherHazardAlert(
            HazardId: "HAZ-SIROCCO-01",
            HazardType: WeatherHazardType.SiroccoSandstorm,
            Severity: HazardSeverity.Warning,
            AffectedCenter: new GeoCoordinate(34.8504, 5.7280), // Biskra
            RadiusKm: 120.0,
            SpeedReductionFactor: 0.50,
            ReportedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(4),
            CorridorName: "RN3 Biskra to Hassi Messaoud"
        );

        _radar.RegisterHazard(hazard);
        var active = _radar.GetActiveHazards();

        active.Should().ContainSingle(h => h.HazardId == "HAZ-SIROCCO-01");
        _radar.RevokeHazard("HAZ-SIROCCO-01").Should().BeTrue();
        _radar.GetActiveHazards().Should().BeEmpty();
    }

    [Fact]
    public void DynamicWeatherHazardRadar_ExpiredHazard_IsFilteredOut()
    {
        var expiredHazard = new WeatherHazardAlert(
            HazardId: "HAZ-EXPIRED",
            HazardType: WeatherHazardType.FlashFlood,
            Severity: HazardSeverity.CriticalBlocked,
            AffectedCenter: new GeoCoordinate(36.0, 3.0),
            RadiusKm: 50.0,
            SpeedReductionFactor: 0.0,
            ReportedAtUtc: DateTime.UtcNow.AddHours(-10),
            ExpiresAtUtc: DateTime.UtcNow.AddHours(-2),
            CorridorName: "Old Flood"
        );

        _radar.RegisterHazard(expiredHazard);
        var active = _radar.GetActiveHazards();

        active.Should().BeEmpty();
    }

    [Fact]
    public void DynamicWeatherHazardRadar_EvaluateEdgeHazard_BlockedHighway_ReturnsBlockedZeroSpeed()
    {
        var blockedHazard = new WeatherHazardAlert(
            HazardId: "HAZ-FLOOD-CHIFFA",
            HazardType: WeatherHazardType.FlashFlood,
            Severity: HazardSeverity.CriticalBlocked,
            AffectedCenter: new GeoCoordinate(36.7538, 3.0588), // Directly on Algiers
            RadiusKm: 50.0,
            SpeedReductionFactor: 0.0,
            ReportedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(6),
            CorridorName: "Algiers Ring Road"
        );

        _radar.RegisterHazard(blockedHazard);

        var edge = new RoutingEdge("E1", "NODE-ALG", "NODE-ORN", TransportMode.RoadFreight, 415.0, 90.0, 45m, 1.0, 110.0);
        var nodeFrom = new RoutingNode("NODE-ALG", "Algiers", new GeoCoordinate(36.7538, 3.0588), true, [TransportMode.RoadFreight]);
        var nodeTo = new RoutingNode("NODE-ORN", "Oran", new GeoCoordinate(35.6987, -0.6349), true, [TransportMode.RoadFreight]);

        var eval = _radar.EvaluateEdgeHazard(edge, nodeFrom, nodeTo);

        eval.IsImpaired.Should().BeTrue();
        eval.IsBlocked.Should().BeTrue();
        eval.EffectiveSpeedKmh.Should().Be(0.0);
        eval.HazardId.Should().Be("HAZ-FLOOD-CHIFFA");
    }

    #endregion

    #region Multi-Modal Route Optimization Tests

    [Fact]
    public void MultiModalRouteOptimization_SameOriginAndDestination_ReturnsZeroDurationRoute()
    {
        var req = new CalculateRouteRequest("NODE-ALG", "NODE-ALG", OptimizationGoal.FastestTime);
        var plan = _engine.CalculateRoute(req);

        plan.TotalDistanceKm.Should().Be(0.0);
        plan.TotalDurationHours.Should().Be(0.0);
        plan.Legs.Should().BeEmpty();
    }

    [Fact]
    public void MultiModalRouteOptimization_FastestTime_SelectsRoadDirectForAlgiersToOran()
    {
        var req = new CalculateRouteRequest("NODE-ALG", "NODE-ORN", OptimizationGoal.FastestTime);
        var plan = _engine.CalculateRoute(req);

        plan.Should().NotBeNull();
        plan.Legs.Should().NotBeEmpty();
        plan.TotalDistanceKm.Should().BeGreaterThan(300.0);
        plan.Legs[0].Mode.Should().Be(TransportMode.RoadFreight); // 90 km/h road is faster than 75 km/h rail or 32 km/h sea
    }

    [Fact]
    public void MultiModalRouteOptimization_LowestCost_PrefersMaritimeOrRail()
    {
        var req = new CalculateRouteRequest("NODE-ALG", "NODE-ORN", OptimizationGoal.LowestCost);
        var plan = _engine.CalculateRoute(req);

        plan.Should().NotBeNull();
        // Coastal maritime (12 DZD/km) or Rail (22 DZD/km) should beat Road (45 DZD/km)
        plan.Legs[0].Mode.Should().BeOneOf(TransportMode.CoastalMaritime, TransportMode.RailFreight);
    }

    [Fact]
    public void MultiModalRouteOptimization_LowestCarbon_PrefersRailOverRoad()
    {
        var req = new CalculateRouteRequest("NODE-ALG", "NODE-ORN", OptimizationGoal.LowestCarbon);
        var plan = _engine.CalculateRoute(req);

        // Rail (28 g/ton-km) and Maritime (18 g/ton-km) emit vastly less CO2 than Road (110 g/ton-km)
        plan.Legs[0].Mode.Should().BeOneOf(TransportMode.CoastalMaritime, TransportMode.RailFreight);
        plan.TotalCarbonKg.Should().BeLessThan(200.0);
    }

    [Fact]
    public void MultiModalRouteOptimization_WeatherBlockedHighway_ReroutesViaRailOrSea()
    {
        // Block road from Algiers to Oran with severe flash flood hazard on road network
        var floodHazard = new WeatherHazardAlert(
            HazardId: "HAZ-HWY-BLOCK",
            HazardType: WeatherHazardType.FlashFlood,
            Severity: HazardSeverity.CriticalBlocked,
            AffectedCenter: new GeoCoordinate(36.7538, 3.0588),
            RadiusKm: 60.0,
            SpeedReductionFactor: 0.0,
            ReportedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: DateTime.UtcNow.AddHours(5),
            CorridorName: "RN4 Chiffa Gorges",
            ApplicableMode: TransportMode.RoadFreight
        );
        _radar.RegisterHazard(floodHazard);

        var req = new CalculateRouteRequest("NODE-ALG", "NODE-ORN", OptimizationGoal.FastestTime, AvoidWeatherHazards: true);
        var plan = _engine.CalculateRoute(req);

        plan.WeatherReroutingApplied.Should().BeTrue();
        plan.Legs.Should().NotBeEmpty();
        plan.Legs[0].Mode.Should().NotBe(TransportMode.RoadFreight); // Successfully rerouted to Rail or Sea!
    }

    [Fact]
    public void MultiModalRouteOptimization_InvalidOrigin_ThrowsArgumentException()
    {
        var req = new CalculateRouteRequest("NODE-NON-EXISTENT", "NODE-ORN");
        var act = () => _engine.CalculateRoute(req);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*not found in transport network*");
    }

    #endregion

    #region EV Fleet Energy Routing Tests

    [Fact]
    public void EvFleetEnergyRouting_NominalRoadRoute_ConsumesBattery()
    {
        var baseRoute = _engine.CalculateRoute(new CalculateRouteRequest("NODE-ALG", "NODE-ORN", OptimizationGoal.FastestTime));
        var vehicle = new EvFleetEnergyProfile(
            VehicleId: "EV-TRUCK-001",
            BatteryCapacityKwh: 600.0,
            CurrentSoCKwh: 600.0, // 100% full long-haul battery
            EmptyWeightKg: 8000.0,
            PayloadWeightKg: 10000.0,
            RegenerativeEfficiencyRatio: 0.70,
            AuxiliaryPowerKw: 2.5,
            AmbientTemperatureCelsius: 22.0
        );

        var plan = _evService.CalculateEvEnergyPlan(new CalculateEvEnergyRouteRequest(baseRoute, vehicle));

        plan.Should().NotBeNull();
        plan.TotalEnergyConsumedKwh.Should().BeGreaterThan(50.0);
        plan.EndingSoCPercent.Should().BeLessThan(100.0);
        plan.RangeDepletedRisk.Should().BeFalse();
    }

    [Fact]
    public void EvFleetEnergyRouting_HighAmbientTemperature_ConsumesExtraAuxiliaryPower()
    {
        var baseRoute = _engine.CalculateRoute(new CalculateRouteRequest("NODE-ALG", "NODE-ORN", OptimizationGoal.FastestTime));
        
        var moderateTempVehicle = new EvFleetEnergyProfile(
            VehicleId: "EV-MODERATE",
            BatteryCapacityKwh: 500.0,
            CurrentSoCKwh: 500.0,
            EmptyWeightKg: 8000.0,
            PayloadWeightKg: 5000.0,
            AmbientTemperatureCelsius: 20.0
        );

        var extremeHeatVehicle = new EvFleetEnergyProfile(
            VehicleId: "EV-HEAT",
            BatteryCapacityKwh: 500.0,
            CurrentSoCKwh: 500.0,
            EmptyWeightKg: 8000.0,
            PayloadWeightKg: 5000.0,
            AmbientTemperatureCelsius: 48.0 // Extreme desert heatwave
        );

        var planModerate = _evService.CalculateEvEnergyPlan(new CalculateEvEnergyRouteRequest(baseRoute, moderateTempVehicle));
        var planHeat = _evService.CalculateEvEnergyPlan(new CalculateEvEnergyRouteRequest(baseRoute, extremeHeatVehicle));

        planHeat.TotalEnergyConsumedKwh.Should().BeGreaterThan(planModerate.TotalEnergyConsumedKwh);
    }

    [Fact]
    public void EvFleetEnergyRouting_LowInitialBattery_SchedulesChargingStops()
    {
        var baseRoute = _engine.CalculateRoute(new CalculateRouteRequest("NODE-ALG", "NODE-ORN", OptimizationGoal.FastestTime));
        var lowBatteryVehicle = new EvFleetEnergyProfile(
            VehicleId: "EV-LOW",
            BatteryCapacityKwh: 300.0,
            CurrentSoCKwh: 60.0, // Only 20%
            EmptyWeightKg: 8000.0,
            PayloadWeightKg: 10000.0
        );

        var plan = _evService.CalculateEvEnergyPlan(new CalculateEvEnergyRouteRequest(baseRoute, lowBatteryVehicle, MinimumReserveSoCPercent: 15.0));

        plan.RequiredChargingStops.Should().NotBeEmpty();
        plan.RequiredChargingStops[0].EnergyChargedKwh.Should().BeGreaterThan(0.0);
        plan.RequiredChargingStops[0].ChargingCostDzd.Should().BeGreaterThan(0m);
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddRouteOptimizationServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddRouteOptimizationServices();

        services.Should().Contain(d => d.ServiceType == typeof(DynamicWeatherHazardRadarService) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(MultiModalRouteOptimizationEngine) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(EvFleetEnergyRoutingService) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
