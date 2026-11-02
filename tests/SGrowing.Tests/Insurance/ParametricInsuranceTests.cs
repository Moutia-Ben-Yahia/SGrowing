using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Insurance.ParametricUnderwriting;
using Xunit;

namespace SGrowing.Tests.Insurance;

public sealed class ParametricInsuranceTests
{
    private readonly ParametricCatastropheOracleEngine _engine = new();

    [Fact]
    public void AdjudicateEvent_EarthquakeAboveTriggerNearEpicenter_CalculatesAutomatedPayout()
    {
        // Arrange: Boumerdes, Algeria earthquake scenario (Epicenter 36.75°N, 3.48°E, Magnitude 6.8)
        var oracleEvent = new CatastropheOracleEvent(
            EventId: "EQ-2026-ALG-01",
            Peril: CatastropheRiskType.EarthquakeRichter,
            EpicenterLocation: new AssetGeoLocation(36.75, 3.48),
            RawIntensityValue: 6.8,
            OracleSource: "USGS-CRAAG-SEISMIC-NETWORK",
            CryptographicSignatureHex: "A1B2C3D4E5F6",
            OccurredAtUtc: DateTime.UtcNow
        );

        // Insured warehouse located 12km from epicenter
        var policy = new InsuredAssetPolicy(
            PolicyId: "POL-EARTHQUAKE-01",
            TenantId: "TENANT-LOGISTICS-DZ",
            AssetName: "Boumerdes Central Distribution Hub",
            Location: new AssetGeoLocation(36.76, 3.35), // ~11.8 km distance
            Peril: CatastropheRiskType.EarthquakeRichter,
            TriggerThreshold: 5.5,
            CatastrophicCeiling: 6.8,
            MaxSumInsuredEur: 2_000_000.0m,
            AnnualPremiumEur: 45_000.0m
        );

        var request = new AdjudicateEventRequest(oracleEvent, [policy]);

        // Act
        var settlements = _engine.AdjudicateEvent(request);

        // Assert
        settlements.Should().ContainSingle();
        var s = settlements[0];
        s.PayoutTriggered.Should().BeTrue();
        s.DistanceToEpicenterKm.Should().BeInRange(10.0, 15.0);
        s.EffectiveLocalIntensity.Should().BeGreaterThan(5.5);
        s.SettlementAmountEur.Should().BeGreaterThan(1_000_000.0m);
        s.AdjudicationSummary.Should().Contain("PARAMETRIC PAYOUT APPROVED");
    }

    [Fact]
    public void AdjudicateEvent_DistantAsset_AttenuatesBelowThresholdAndRejectsPayout()
    {
        // Arrange: Same 6.8 earthquake in Boumerdes, but asset in Oran (~380 km away)
        var oracleEvent = new CatastropheOracleEvent(
            EventId: "EQ-2026-ALG-01",
            Peril: CatastropheRiskType.EarthquakeRichter,
            EpicenterLocation: new AssetGeoLocation(36.75, 3.48),
            RawIntensityValue: 6.8,
            OracleSource: "USGS",
            CryptographicSignatureHex: "FF00AA",
            OccurredAtUtc: DateTime.UtcNow
        );

        var distantPolicy = new InsuredAssetPolicy(
            PolicyId: "POL-ORAN-01",
            TenantId: "TENANT-ORAN",
            AssetName: "Oran Cold Storage Facility",
            Location: new AssetGeoLocation(35.69, -0.63), // ~380km away
            Peril: CatastropheRiskType.EarthquakeRichter,
            TriggerThreshold: 5.0,
            CatastrophicCeiling: 6.5,
            MaxSumInsuredEur: 1_500_000.0m,
            AnnualPremiumEur: 25_000.0m
        );

        // Act
        var settlements = _engine.AdjudicateEvent(new AdjudicateEventRequest(oracleEvent, [distantPolicy]));

        // Assert
        settlements.Should().ContainSingle();
        var s = settlements[0];
        s.PayoutTriggered.Should().BeFalse();
        s.DistanceToEpicenterKm.Should().BeGreaterThan(300.0);
        s.EffectiveLocalIntensity.Should().BeLessThan(2.0, "Exponential attenuation attenuates ground motion over 300km.");
        s.SettlementAmountEur.Should().Be(0m);
        s.AdjudicationSummary.Should().Contain("NO PAYOUT");
    }

    [Fact]
    public void AuditSolvencyAdequacy_CompliantReserves_ReturnsCompliantResult()
    {
        // Arrange
        var policy1 = new InsuredAssetPolicy("P1", "T1", "A1", new(36.0, 3.0), CatastropheRiskType.EarthquakeRichter, 5.0, 7.0, 4_000_000m, 50000m);
        var policy2 = new InsuredAssetPolicy("P2", "T2", "A2", new(36.0, 3.0), CatastropheRiskType.TropicalCycloneWindKmh, 100.0, 180.0, 6_000_000m, 75000m);
        // Total exposure = 10,000,000 EUR -> Stress PML (25%) = 2,500,000 EUR
        // Solvency II 150% threshold requires capital >= 3,750,000 EUR

        var request = new AuditSolvencyRequest(
            PoolId: "SOLVENCY-POOL-01",
            CapitalReservesEur: 4_500_000.0m, // 4.5M EUR > 3.75M EUR -> Solvency Ratio = 180%
            ActivePolicies: [policy1, policy2]
        );

        // Act
        var audit = _engine.AuditSolvencyAdequacy(request);

        // Assert
        audit.TotalSumInsuredExposureEur.Should().Be(10_000_000m);
        audit.StressTestPmlEur.Should().Be(2_500_000m);
        audit.SolvencyRatioPercent.Should().Be(180.0);
        audit.IsSolvencyIiCompliant.Should().BeTrue();
    }

    [Fact]
    public void AddParametricInsuranceServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddParametricInsuranceServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(ParametricCatastropheOracleEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
