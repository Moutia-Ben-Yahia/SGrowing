using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Logistics.MaritimeEcoRouting;
using Xunit;

namespace SGrowing.Tests.Logistics;

public sealed class MaritimeEcoRoutingTests
{
    private readonly MaritimeEcoRoutingEngine _engine = new();

    [Fact]
    public void ComputeEcoRoute_AlgiersToMarseille_CalculatesFuelConsumptionAndCiiGrade()
    {
        // Arrange: Mediterranean route Algiers (36.75°N, 3.06°E) to Marseille (43.30°N, 5.37°E) ~ 420 NM
        var ownVessel = new AisVesselTelemetry(
            Mmsi: "605123456",
            VesselName: "Tariq Ibn Ziyad",
            Position: new GeoCoordinate(36.75, 3.06),
            SpeedKnots: 20.0,
            CourseOverGroundDegrees: 18.0,
            DeadweightTons: 25000.0
        );

        var destination = new GeoCoordinate(43.30, 5.37);

        var weather = new SeaWeatherCondition(
            SignificantWaveHeightMeters: 1.5,
            OceanCurrentSpeedKnots: 0.8,
            OceanCurrentDirectionDegrees: 45.0,
            WindSpeedKnots: 15.0
        );

        var request = new EcoRoutingRequest(
            OwnVessel: ownVessel,
            Destination: destination,
            NearbyAisTargets: [],
            Weather: weather,
            VlsfoFuelPricePerTonUsd: 620.0m
        );

        // Act
        var plan = _engine.ComputeEcoRouteAndCollisionRisks(request);

        // Assert
        plan.Should().NotBeNull();
        plan.DistanceNauticalMiles.Should().BeInRange(380.0, 450.0);
        plan.RecommendedSpeedKnots.Should().BeLessThan(20.0, "Eco speed should apply slow-steaming");
        plan.EstimatedFuelBurnMetricTons.Should().BeGreaterThan(3.0);
        plan.EstimatedFuelCostUsd.Should().BeGreaterThan(0m);
        plan.CiiScoreGramsCo2PerTonNm.Should().BeGreaterThan(0.0);
        plan.CiiRatingGrade.Should().MatchRegex("^[A-E]$");
    }

    [Fact]
    public void EvaluateCollisionRisk_CrossingVesselWithLowCpa_TriggersColregsStarboardAdvisory()
    {
        // Arrange: Own vessel heading North (000°) at 18 knots, Target vessel crossing from starboard (045°) heading West (270°) at 16 knots
        var own = new AisVesselTelemetry(
            Mmsi: "OWN-01",
            VesselName: "Ever Given",
            Position: new GeoCoordinate(35.0, 10.0),
            SpeedKnots: 18.0,
            CourseOverGroundDegrees: 0.0, // Heading North
            DeadweightTons: 100000.0
        );

        // Target vessel is 3 NM East, 2 NM North, heading West directly on collision course
        var target = new AisVesselTelemetry(
            Mmsi: "TARGET-01",
            VesselName: "Cosco Shipping",
            Position: new GeoCoordinate(35.033, 10.06), // ~2 NM North, 3 NM East
            SpeedKnots: 18.0,
            CourseOverGroundDegrees: 270.0, // Heading West directly across own bow
            DeadweightTons: 90000.0
        );

        var request = new EcoRoutingRequest(
            OwnVessel: own,
            Destination: new GeoCoordinate(38.0, 10.0),
            NearbyAisTargets: [target],
            Weather: new SeaWeatherCondition(1.0, 0.0, 0.0, 10.0),
            VlsfoFuelPricePerTonUsd: 600m
        );

        // Act
        var plan = _engine.ComputeEcoRouteAndCollisionRisks(request);

        // Assert
        plan.CollisionRisks.Should().ContainSingle();
        var risk = plan.CollisionRisks[0];
        risk.CollisionRiskDetected.Should().BeTrue();
        risk.DistanceCpaNauticalMiles.Should().BeLessThan(1.5);
        risk.TimeCpaMinutes.Should().BeGreaterThan(0.0);
        risk.RecommendedCourseAdjustmentDegrees.Should().Be(25.0);
        risk.ColregsAdvisory.Should().Contain("COLREGS RULE 16");
    }

    [Fact]
    public void EvaluateCollisionRisk_DivergingVessel_ReportsClearAirspaceWaterway()
    {
        // Arrange: Target vessel already passed and opening distance (diverging)
        var own = new AisVesselTelemetry("OWN", "Own", new GeoCoordinate(35.0, 10.0), 15.0, 0.0, 20000.0);
        var targetDiverging = new AisVesselTelemetry("TARGET", "Target", new GeoCoordinate(34.8, 10.0), 12.0, 180.0, 20000.0); // Heading South behind us

        var request = new EcoRoutingRequest(own, new GeoCoordinate(38.0, 10.0), [targetDiverging], new SeaWeatherCondition(1.0, 0, 0, 5), 600m);

        // Act
        var plan = _engine.ComputeEcoRouteAndCollisionRisks(request);

        // Assert
        plan.CollisionRisks[0].CollisionRiskDetected.Should().BeFalse();
        plan.CollisionRisks[0].ColregsAdvisory.Should().Contain("CLEAR");
    }

    [Fact]
    public void AddMaritimeEcoRoutingServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMaritimeEcoRoutingServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(MaritimeEcoRoutingEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
