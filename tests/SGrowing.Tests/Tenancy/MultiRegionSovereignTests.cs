using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Tenancy.MultiRegion;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class MultiRegionSovereignTests
{
    private readonly ISovereignDataShardingManager _sovereignManager;
    private readonly IGeoRoutingMeshService _meshService;
    private readonly IHlcReplicationCoordinator _hlcCoordinator;

    public MultiRegionSovereignTests()
    {
        _sovereignManager = new SovereignDataShardingManager();
        _meshService = new GeoRoutingMeshService(_sovereignManager);
        _hlcCoordinator = new HlcReplicationCoordinator(_sovereignManager);
    }

    #region 1. Dependency Injection Registration Tests (No BuildServiceProvider)

    [Fact]
    public void AddMultiRegionSovereignServices_RegistersSovereignDataShardingManager_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddMultiRegionSovereignServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISovereignDataShardingManager) &&
            d.ImplementationType == typeof(SovereignDataShardingManager) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMultiRegionSovereignServices_RegistersGeoRoutingMeshService_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddMultiRegionSovereignServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IGeoRoutingMeshService) &&
            d.ImplementationType == typeof(GeoRoutingMeshService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMultiRegionSovereignServices_RegistersHlcReplicationCoordinator_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddMultiRegionSovereignServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHlcReplicationCoordinator) &&
            d.ImplementationType == typeof(HlcReplicationCoordinator) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMultiRegionSovereignServices_RegistersAllThreeCoreServices_WithoutCallingBuildServiceProvider()
    {
        // Direct ServiceDescriptor assertion on ServiceCollection without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddMultiRegionSovereignServices();

        services.Should().HaveCount(3);
        services.Select(d => d.ServiceType).Should().BeEquivalentTo([
            typeof(ISovereignDataShardingManager),
            typeof(IGeoRoutingMeshService),
            typeof(IHlcReplicationCoordinator)
        ]);
        services.All(d => d.Lifetime == ServiceLifetime.Singleton).Should().BeTrue();
    }

    #endregion

    #region 2. Geo-Proximity Routing Mesh & Geodesics

    [Fact]
    public void ResolveRoute_AlgeriaClientIp_RoutesToAfNorth1AlgiersShard()
    {
        var request = new GeoRouteRequest(ClientIp: "105.101.44.12", CountryCode: null, TenantId: null);

        var decision = _meshService.ResolveRoute(request);

        decision.Should().NotBeNull();
        decision.DetectedCountry.Should().Be("DZ");
        decision.TargetRegion.Should().Be(RegionIdentifier.AfNorth1);
        decision.SelectedShardId.Should().Be("shard-af-north-1-pri");
        decision.ComplianceVerified.Should().BeTrue();
        decision.FallbackApplied.Should().BeFalse();
    }

    [Theory]
    [InlineData("DZ")]
    [InlineData("TN")]
    [InlineData("MA")]
    [InlineData("LY")]
    [InlineData("EG")]
    public void ResolveRoute_NorthAfricanCountries_RoutesToAfNorth1(string countryCode)
    {
        var request = new GeoRouteRequest(ClientIp: "127.0.0.1", CountryCode: countryCode, TenantId: null);

        var decision = _meshService.ResolveRoute(request);

        decision.TargetRegion.Should().Be(RegionIdentifier.AfNorth1);
        decision.SelectedShardId.Should().Be("shard-af-north-1-pri");
        decision.ComplianceVerified.Should().BeTrue();
    }

    [Fact]
    public void ResolveRoute_FranceClientIp_RoutesToEuWest3ParisShard()
    {
        var request = new GeoRouteRequest(ClientIp: "185.24.120.5", CountryCode: null, TenantId: null);

        var decision = _meshService.ResolveRoute(request);

        decision.Should().NotBeNull();
        decision.DetectedCountry.Should().Be("FR");
        decision.TargetRegion.Should().Be(RegionIdentifier.EuWest3);
        decision.SelectedShardId.Should().Be("shard-eu-west-3-pri");
        decision.ComplianceVerified.Should().BeTrue();
        decision.FallbackApplied.Should().BeFalse();
    }

    [Theory]
    [InlineData("FR")]
    [InlineData("DE")]
    [InlineData("ES")]
    [InlineData("IT")]
    [InlineData("GB")]
    [InlineData("NL")]
    public void ResolveRoute_EuropeanCountries_RoutesToEuWest3(string countryCode)
    {
        var request = new GeoRouteRequest(ClientIp: "127.0.0.1", CountryCode: countryCode, TenantId: null);

        var decision = _meshService.ResolveRoute(request);

        decision.TargetRegion.Should().Be(RegionIdentifier.EuWest3);
        decision.SelectedShardId.Should().Be("shard-eu-west-3-pri");
        decision.ComplianceVerified.Should().BeTrue();
    }

    [Fact]
    public void ResolveRoute_UaeClientIp_RoutesToMeCentral1DubaiShard()
    {
        var request = new GeoRouteRequest(ClientIp: "94.200.12.80", CountryCode: null, TenantId: null);

        var decision = _meshService.ResolveRoute(request);

        decision.Should().NotBeNull();
        decision.DetectedCountry.Should().Be("AE");
        decision.TargetRegion.Should().Be(RegionIdentifier.MeCentral1);
        decision.SelectedShardId.Should().Be("shard-me-central-1-pri");
        decision.ComplianceVerified.Should().BeTrue();
        decision.FallbackApplied.Should().BeFalse();
    }

    [Theory]
    [InlineData("AE")]
    [InlineData("SA")]
    [InlineData("QA")]
    [InlineData("KW")]
    [InlineData("BH")]
    [InlineData("OM")]
    public void ResolveRoute_GulfAndMiddleEastCountries_RoutesToMeCentral1(string countryCode)
    {
        var request = new GeoRouteRequest(ClientIp: "127.0.0.1", CountryCode: countryCode, TenantId: null);

        var decision = _meshService.ResolveRoute(request);

        decision.TargetRegion.Should().Be(RegionIdentifier.MeCentral1);
        decision.SelectedShardId.Should().Be("shard-me-central-1-pri");
        decision.ComplianceVerified.Should().BeTrue();
    }

    [Fact]
    public void CalculateHaversineDistanceKm_AlgiersToParis_CalculatesAccurateDistance()
    {
        // Algiers (36.75, 3.06) to Paris (48.86, 2.35) is roughly ~1350 km
        var distance = _meshService.CalculateHaversineDistanceKm(
            GeoRoutingMeshService.AlgiersLatitude,
            GeoRoutingMeshService.AlgiersLongitude,
            GeoRoutingMeshService.ParisLatitude,
            GeoRoutingMeshService.ParisLongitude
        );

        distance.Should().BeInRange(1340.0, 1360.0);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_ParisToDubai_CalculatesAccurateDistance()
    {
        // Paris to Dubai is roughly ~5240 km
        var distance = _meshService.CalculateHaversineDistanceKm(
            GeoRoutingMeshService.ParisLatitude,
            GeoRoutingMeshService.ParisLongitude,
            GeoRoutingMeshService.DubaiLatitude,
            GeoRoutingMeshService.DubaiLongitude
        );

        distance.Should().BeInRange(5200.0, 5300.0);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_SameCoordinates_ReturnsZeroDistance()
    {
        var distance = _meshService.CalculateHaversineDistanceKm(
            GeoRoutingMeshService.AlgiersLatitude,
            GeoRoutingMeshService.AlgiersLongitude,
            GeoRoutingMeshService.AlgiersLatitude,
            GeoRoutingMeshService.AlgiersLongitude
        );

        distance.Should().Be(0.0);
    }

    [Fact]
    public void ResolveCountryToNearestRegion_UnmappedCountry_DefaultsToAfNorth1()
    {
        var region = _meshService.ResolveCountryToNearestRegion("XYZ");
        region.Should().Be(RegionIdentifier.AfNorth1);
    }

    #endregion

    #region 3. Fallback Routing & Outage Simulation

    [Fact]
    public void ResolveRoute_WhenPrimaryAfNorthDegraded_FallsBackToHealthyEuWest()
    {
        try
        {
            // Simulate Algiers shard outage
            _meshService.UpdateShardStatus("shard-af-north-1-pri", isActive: false, healthState: ShardHealthState.Offline);

            var request = new GeoRouteRequest(ClientIp: "105.10.20.30", CountryCode: "DZ", TenantId: null);

            var decision = _meshService.ResolveRoute(request);

            decision.FallbackApplied.Should().BeTrue();
            decision.TargetRegion.Should().Be(RegionIdentifier.EuWest3);
            decision.SelectedShardId.Should().Be("shard-eu-west-3-pri");
            decision.ComplianceVerified.Should().BeTrue();
        }
        finally
        {
            _meshService.ResetShardStatusToDefaults();
        }
    }

    [Fact]
    public void ResolveRoute_WhenPrimaryEuWestOffline_FallsBackToLowestLatencyActiveShard()
    {
        try
        {
            // Simulate Paris shard offline
            _meshService.UpdateShardStatus("shard-eu-west-3-pri", isActive: false, healthState: ShardHealthState.Offline);

            var request = new GeoRouteRequest(ClientIp: "185.10.20.30", CountryCode: "FR", TenantId: null);

            var decision = _meshService.ResolveRoute(request);

            decision.FallbackApplied.Should().BeTrue();
            decision.TargetRegion.Should().Be(RegionIdentifier.AfNorth1);
            decision.SelectedShardId.Should().Be("shard-af-north-1-pri");
            decision.ComplianceVerified.Should().BeTrue();
        }
        finally
        {
            _meshService.ResetShardStatusToDefaults();
        }
    }

    [Fact]
    public void ResolveRoute_WhenAllShardsOffline_AppliesEmergencyFallbackWithNonCompliance()
    {
        try
        {
            _meshService.UpdateShardStatus("shard-af-north-1-pri", isActive: false, healthState: ShardHealthState.Offline);
            _meshService.UpdateShardStatus("shard-eu-west-3-pri", isActive: false, healthState: ShardHealthState.Offline);
            _meshService.UpdateShardStatus("shard-me-central-1-pri", isActive: false, healthState: ShardHealthState.Offline);

            var request = new GeoRouteRequest(ClientIp: "105.1.1.1", CountryCode: "DZ", TenantId: null);

            var decision = _meshService.ResolveRoute(request);

            decision.ComplianceVerified.Should().BeFalse();
            decision.FallbackApplied.Should().BeTrue();
            decision.EstimatedLatencyMs.Should().Be(999.0);
        }
        finally
        {
            _meshService.ResetShardStatusToDefaults();
        }
    }

    [Fact]
    public void UpdateShardStatus_ModifiesLatencyScoreAndHealthState()
    {
        try
        {
            _meshService.UpdateShardStatus("shard-me-central-1-pri", isActive: true, healthState: ShardHealthState.Degraded, latencyScoreMs: 145.5);

            var shard = _meshService.GetShard("shard-me-central-1-pri");

            shard.Should().NotBeNull();
            shard!.HealthState.Should().Be(ShardHealthState.Degraded);
            shard.LatencyScoreMs.Should().Be(145.5);
        }
        finally
        {
            _meshService.ResetShardStatusToDefaults();
        }
    }

    [Fact]
    public void ResetShardStatusToDefaults_RestoresInitialHealthyState()
    {
        _meshService.UpdateShardStatus("shard-af-north-1-pri", isActive: false, healthState: ShardHealthState.Offline);

        _meshService.ResetShardStatusToDefaults();

        var shard = _meshService.GetShard("shard-af-north-1-pri");
        shard.Should().NotBeNull();
        shard!.IsActive.Should().BeTrue();
        shard.HealthState.Should().Be(ShardHealthState.Healthy);
        shard.LatencyScoreMs.Should().Be(12.5);
    }

    #endregion

    #region 4. Sovereign Data Boundary Enforcement (Algerian Law 18-07, GDPR, DIFC)

    [Fact]
    public async Task ValidateReplication_IntraRegionAfNorthToAfNorth_IsAlwaysPermitted()
    {
        var result = await _sovereignManager.ValidateReplicationPermittedAsync(
            SovereignDataShardingManager.AlgerianNationalBankTenantId,
            origin: RegionIdentifier.AfNorth1,
            target: RegionIdentifier.AfNorth1,
            isPii: true,
            entityType: "CitizenAccount",
            entityId: "acc-dz-001"
        );

        result.IsPermitted.Should().BeTrue();
        result.ViolationReason.Should().BeNull();
        result.AuditLogged.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateReplication_AlgerianLaw1807_AbsoluteSovereign_BlocksCrossBorderToEuWest()
    {
        var result = await _sovereignManager.ValidateReplicationPermittedAsync(
            SovereignDataShardingManager.AlgerianNationalBankTenantId,
            origin: RegionIdentifier.AfNorth1,
            target: RegionIdentifier.EuWest3,
            isPii: true,
            entityType: "CitizenNationalIdRecord",
            entityId: "nid-1990-16-004412"
        );

        result.IsPermitted.Should().BeFalse();
        result.ViolationReason.Should().Contain("Algerian Law 18-07");
        result.Jurisdiction.Should().Be(SovereignJurisdiction.Algeria_Law1807);
        result.AuditLogged.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateReplication_AlgerianLaw1807_AbsoluteSovereign_BlocksCrossBorderToMeCentral()
    {
        var result = await _sovereignManager.ValidateReplicationPermittedAsync(
            SovereignDataShardingManager.AlgerianNationalBankTenantId,
            origin: RegionIdentifier.AfNorth1,
            target: RegionIdentifier.MeCentral1,
            isPii: false, // Even non-PII is locked down under AbsoluteSovereign
            entityType: "BankLedgerEntry",
            entityId: "tx-dz-8821"
        );

        result.IsPermitted.Should().BeFalse();
        result.ViolationReason.Should().Contain("Algerian Law 18-07");
        result.AuditLogged.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateReplication_AlgerianLaw1807_RecordsViolationAuditLog()
    {
        _sovereignManager.ClearViolationAuditLogs();

        await _sovereignManager.ValidateReplicationPermittedAsync(
            SovereignDataShardingManager.AlgerianNationalBankTenantId,
            origin: RegionIdentifier.AfNorth1,
            target: RegionIdentifier.EuWest3,
            isPii: true,
            entityType: "CitizenPassport",
            entityId: "pass-2026-dz"
        );

        var auditLogs = _sovereignManager.GetViolationAuditLogs(SovereignDataShardingManager.AlgerianNationalBankTenantId);

        auditLogs.Should().NotBeEmpty();
        auditLogs.First().ViolatedJurisdiction.Should().Be(SovereignJurisdiction.Algeria_Law1807);
        auditLogs.First().AttemptedOrigin.Should().Be(RegionIdentifier.AfNorth1);
        auditLogs.First().AttemptedTarget.Should().Be(RegionIdentifier.EuWest3);
        auditLogs.First().EntityType.Should().Be("CitizenPassport");
    }

    [Fact]
    public async Task ValidateReplication_AlgerianLaw1807_RegionalPreferred_BlocksPiiReplication()
    {
        var customTenantId = Guid.NewGuid();
        var customPolicy = new TenantDataResidencyPolicy(
            TenantId: customTenantId,
            HomeRegion: RegionIdentifier.AfNorth1,
            EnforcedJurisdiction: SovereignJurisdiction.Algeria_Law1807,
            AllowCrossRegionReadReplicas: true,
            DataLocalityStrictness: DataLocalityStrictness.RegionalPreferred,
            ConfiguredAtUtc: DateTime.UtcNow
        );

        await _sovereignManager.RegisterResidencyPolicyAsync(customPolicy);

        // When PII is true -> Blocked
        var piiResult = await _sovereignManager.ValidateReplicationPermittedAsync(
            customTenantId,
            RegionIdentifier.AfNorth1,
            RegionIdentifier.EuWest3,
            isPii: true,
            "CustomerProfile",
            "cust-001"
        );
        piiResult.IsPermitted.Should().BeFalse();
        piiResult.ViolationReason.Should().Contain("PII");

        // When PII is false -> Permitted for RegionalPreferred
        var nonPiiResult = await _sovereignManager.ValidateReplicationPermittedAsync(
            customTenantId,
            RegionIdentifier.AfNorth1,
            RegionIdentifier.EuWest3,
            isPii: false,
            "ProductCatalog",
            "prod-001"
        );
        nonPiiResult.IsPermitted.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateReplication_EuGdpr_AbsoluteSovereign_BlocksReplicationOutsideEu()
    {
        var result = await _sovereignManager.ValidateReplicationPermittedAsync(
            SovereignDataShardingManager.EuEnterpriseHealthTenantId,
            origin: RegionIdentifier.EuWest3,
            target: RegionIdentifier.AfNorth1,
            isPii: true,
            entityType: "MedicalRecord",
            entityId: "mr-9901"
        );

        result.IsPermitted.Should().BeFalse();
        result.ViolationReason.Should().Contain("GDPR");
        result.Jurisdiction.Should().Be(SovereignJurisdiction.Eu_Gdpr);
        result.AuditLogged.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateReplication_UaeDifc_AbsoluteSovereign_BlocksReplicationOutsideMeCentral()
    {
        var customTenantId = Guid.NewGuid();
        var customPolicy = new TenantDataResidencyPolicy(
            TenantId: customTenantId,
            HomeRegion: RegionIdentifier.MeCentral1,
            EnforcedJurisdiction: SovereignJurisdiction.Uae_DIFC,
            AllowCrossRegionReadReplicas: false,
            DataLocalityStrictness: DataLocalityStrictness.AbsoluteSovereign,
            ConfiguredAtUtc: DateTime.UtcNow
        );
        await _sovereignManager.RegisterResidencyPolicyAsync(customPolicy);

        var result = await _sovereignManager.ValidateReplicationPermittedAsync(
            customTenantId,
            origin: RegionIdentifier.MeCentral1,
            target: RegionIdentifier.EuWest3,
            isPii: true,
            entityType: "WealthPortfolio",
            entityId: "wp-7721"
        );

        result.IsPermitted.Should().BeFalse();
        result.ViolationReason.Should().Contain("DIFC");
        result.Jurisdiction.Should().Be(SovereignJurisdiction.Uae_DIFC);
    }

    [Fact]
    public async Task ValidateReplication_GlobalFloating_AllowsCrossRegionReplication()
    {
        var result = await _sovereignManager.ValidateReplicationPermittedAsync(
            SovereignDataShardingManager.GlobalSaaSFloatingTenantId,
            origin: RegionIdentifier.EuWest3,
            target: RegionIdentifier.MeCentral1,
            isPii: false,
            entityType: "AnalyticsAggregate",
            entityId: "agg-4401"
        );

        result.IsPermitted.Should().BeTrue();
        result.ViolationReason.Should().BeNull();
        result.AuditLogged.Should().BeFalse();
    }

    [Fact]
    public void ResolveRoute_WithAbsoluteSovereigntyPolicy_RefusesCrossBorderFallbackOnOutage()
    {
        try
        {
            // Simulate Algiers shard outage
            _meshService.UpdateShardStatus("shard-af-north-1-pri", isActive: false, healthState: ShardHealthState.Offline);

            var request = new GeoRouteRequest(
                ClientIp: "105.10.20.30",
                CountryCode: "DZ",
                TenantId: SovereignDataShardingManager.AlgerianNationalBankTenantId
            );

            var decision = _meshService.ResolveRoute(request);

            // Must NOT failover cross-border to Europe!
            decision.TargetRegion.Should().Be(RegionIdentifier.AfNorth1);
            decision.ComplianceVerified.Should().BeFalse();
            decision.FallbackApplied.Should().BeFalse();
            decision.RoutingReason.Should().Contain("forbids cross-border fallback");
        }
        finally
        {
            _meshService.ResetShardStatusToDefaults();
        }
    }

    [Fact]
    public void ClearViolationAuditLogs_PurgesAuditLogEntries()
    {
        var violation = new SovereignViolationAuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: SovereignDataShardingManager.AlgerianNationalBankTenantId,
            AttemptedOrigin: RegionIdentifier.AfNorth1,
            AttemptedTarget: RegionIdentifier.EuWest3,
            ViolatedJurisdiction: SovereignJurisdiction.Algeria_Law1807,
            EntityType: "TestEntity",
            EntityId: "test-001",
            Reason: "Test Reason",
            TimestampUtc: DateTime.UtcNow
        );

        _sovereignManager.RecordViolation(violation);
        _sovereignManager.GetViolationAuditLogs().Should().NotBeEmpty();

        _sovereignManager.ClearViolationAuditLogs();
        _sovereignManager.GetViolationAuditLogs().Should().BeEmpty();
    }

    #endregion

    #region 5. Hybrid Logical Clock (HLC) Semantics & Causality

    [Fact]
    public void GenerateNextTimestamp_AdvancesPhysicalTimestampMonotonically()
    {
        var t1 = _hlcCoordinator.GenerateNextTimestamp("node-algiers-01");
        var t2 = _hlcCoordinator.GenerateNextTimestamp("node-algiers-01");

        t2.Should().BeGreaterThan(t1);
    }

    [Fact]
    public void GenerateNextTimestamp_WhenPhysicalClockFrozen_IncrementsLogicalCounter()
    {
        const long frozenTime = 1700000000000L;

        var t1 = _hlcCoordinator.GenerateNextTimestamp("node-frozen", physicalOverride: frozenTime);
        var t2 = _hlcCoordinator.GenerateNextTimestamp("node-frozen", physicalOverride: frozenTime);
        var t3 = _hlcCoordinator.GenerateNextTimestamp("node-frozen", physicalOverride: frozenTime);

        t1.PhysicalTimestampMs.Should().Be(frozenTime);
        t2.PhysicalTimestampMs.Should().Be(frozenTime);
        t3.PhysicalTimestampMs.Should().Be(frozenTime);

        t2.LogicalCounter.Should().Be(t1.LogicalCounter + 1);
        t3.LogicalCounter.Should().Be(t2.LogicalCounter + 1);
        (t3 > t2).Should().BeTrue();
        (t2 > t1).Should().BeTrue();
    }

    [Fact]
    public void GenerateNextTimestamp_WhenPhysicalClockAdvances_ResetsLogicalCounter()
    {
        const long frozenTime1 = 1700000000000L;
        const long advancedTime = 1700000005000L;

        _hlcCoordinator.GenerateNextTimestamp("node-adv", physicalOverride: frozenTime1);
        _hlcCoordinator.GenerateNextTimestamp("node-adv", physicalOverride: frozenTime1); // counter increments

        var t3 = _hlcCoordinator.GenerateNextTimestamp("node-adv", physicalOverride: advancedTime);

        t3.PhysicalTimestampMs.Should().Be(advancedTime);
        t3.LogicalCounter.Should().Be(0);
    }

    [Fact]
    public void UpdateOnReceive_WhenRemoteTimestampAhead_AdvancesLocalPhysicalTime()
    {
        const long localTime = 1700000000000L;
        const long remoteTime = 1700000010000L;

        _hlcCoordinator.GenerateNextTimestamp("node-local", physicalOverride: localTime);

        var remoteTimestamp = new HybridLogicalClockRecord(remoteTime, 5, "node-paris-01");
        var updated = _hlcCoordinator.UpdateOnReceive("node-local", remoteTimestamp, physicalOverride: localTime);

        updated.PhysicalTimestampMs.Should().Be(remoteTime);
        updated.LogicalCounter.Should().Be(6); // m.c + 1
        updated.NodeId.Should().Be("node-local");
    }

    [Fact]
    public void UpdateOnReceive_WhenPhysicalTimesMatch_TakesMaxCounterPlusOne()
    {
        const long matchingTime = 1700000000000L;

        var local1 = _hlcCoordinator.GenerateNextTimestamp("node-match", physicalOverride: matchingTime);
        // local counter is 0 or 1
        var remote = new HybridLogicalClockRecord(matchingTime, 10, "node-remote");

        var updated = _hlcCoordinator.UpdateOnReceive("node-match", remote, physicalOverride: matchingTime);

        updated.PhysicalTimestampMs.Should().Be(matchingTime);
        updated.LogicalCounter.Should().Be(11); // max(counter, remote.counter) + 1
    }

    [Fact]
    public void CompareHlc_StrictMonotonicOrdering_PreservesCausality()
    {
        var hlc1 = new HybridLogicalClockRecord(1000, 0, "node-a");
        var hlc2 = new HybridLogicalClockRecord(1000, 1, "node-a");
        var hlc3 = new HybridLogicalClockRecord(1001, 0, "node-a");

        _hlcCoordinator.CompareHlc(hlc1, hlc2).Should().BeNegative();
        _hlcCoordinator.CompareHlc(hlc2, hlc3).Should().BeNegative();
        _hlcCoordinator.CompareHlc(hlc3, hlc1).Should().BePositive();
    }

    [Fact]
    public void CompareHlc_IdenticalPhysicalAndLogical_BreaksTieUsingNodeIdOrdinalComparison()
    {
        var hlcAlgiers = new HybridLogicalClockRecord(2000, 5, "node-algiers-01");
        var hlcParis = new HybridLogicalClockRecord(2000, 5, "node-paris-01");

        // "node-algiers-01" < "node-paris-01" ordinally
        _hlcCoordinator.CompareHlc(hlcAlgiers, hlcParis).Should().BeNegative();
        (hlcAlgiers < hlcParis).Should().BeTrue();
        (hlcParis > hlcAlgiers).Should().BeTrue();
    }

    [Fact]
    public void HlcRecord_TryParse_CorrectlyParsesSerializedHlcString()
    {
        var serialized = "173000500123:42:node-dubai-01";

        var success = HybridLogicalClockRecord.TryParse(serialized, out var record);

        success.Should().BeTrue();
        record.Should().NotBeNull();
        record!.PhysicalTimestampMs.Should().Be(173000500123L);
        record.LogicalCounter.Should().Be(42);
        record.NodeId.Should().Be("node-dubai-01");
        record.ToString().Should().Be(serialized);
    }

    [Fact]
    public void HlcRecord_ComparisonOperators_WorkCorrectly()
    {
        var low = new HybridLogicalClockRecord(100, 0, "a");
        var mid = new HybridLogicalClockRecord(100, 1, "a");
        var high = new HybridLogicalClockRecord(200, 0, "a");

        (low <= mid).Should().BeTrue();
        (mid >= low).Should().BeTrue();
        (high >= mid).Should().BeTrue();
        (low != high).Should().BeTrue();
        (low == new HybridLogicalClockRecord(100, 0, "a")).Should().BeTrue();
    }

    #endregion

    #region 6. Multi-Region Active-Active Conflict Resolution (Last-Write-Wins)

    [Fact]
    public void ResolveConflict_HigherHlcWins_SetsStatusToConflictResolved()
    {
        var hlcOld = new HybridLogicalClockRecord(1000, 0, "node-paris-01");
        var hlcNew = new HybridLogicalClockRecord(1005, 0, "node-algiers-01");

        var oldRecord = new ReplicatedTransactionRecord(
            TxId: "tx-old",
            OriginRegion: RegionIdentifier.EuWest3,
            TargetRegion: RegionIdentifier.AfNorth1,
            ShardId: "shard-af-north-1-pri",
            HlcTimestamp: hlcOld,
            EntityType: "Order",
            EntityId: "ord-100",
            PayloadJson: "{\"status\":\"Pending\"}",
            PayloadSha256: "abc",
            Status: ReplicationStatus.ReplicatedCrossRegion
        );

        var newRecord = new ReplicatedTransactionRecord(
            TxId: "tx-new",
            OriginRegion: RegionIdentifier.AfNorth1,
            TargetRegion: RegionIdentifier.AfNorth1,
            ShardId: "shard-af-north-1-pri",
            HlcTimestamp: hlcNew,
            EntityType: "Order",
            EntityId: "ord-100",
            PayloadJson: "{\"status\":\"Paid\"}",
            PayloadSha256: "def",
            Status: ReplicationStatus.CommittedLocal
        );

        var winner = _hlcCoordinator.ResolveConflict(oldRecord, newRecord);

        winner.TxId.Should().Be("tx-new");
        winner.Status.Should().Be(ReplicationStatus.ConflictResolved);
        winner.PayloadJson.Should().Be("{\"status\":\"Paid\"}");
    }

    [Fact]
    public void ResolveConflict_IdenticalHlcTimestamp_TieBreaksDeterministicallyViaNodeId()
    {
        // Identical physical and logical counters on different nodes
        var hlcA = new HybridLogicalClockRecord(3000, 2, "node-algiers-01");
        var hlcZ = new HybridLogicalClockRecord(3000, 2, "node-paris-01");

        var recordA = new ReplicatedTransactionRecord(
            TxId: "tx-a",
            OriginRegion: RegionIdentifier.AfNorth1,
            TargetRegion: RegionIdentifier.MeCentral1,
            ShardId: "shard-me-central-1-pri",
            HlcTimestamp: hlcA,
            EntityType: "Inventory",
            EntityId: "inv-500",
            PayloadJson: "{\"qty\":10}",
            PayloadSha256: "hashA",
            Status: ReplicationStatus.ReplicatedCrossRegion
        );

        var recordZ = new ReplicatedTransactionRecord(
            TxId: "tx-z",
            OriginRegion: RegionIdentifier.EuWest3,
            TargetRegion: RegionIdentifier.MeCentral1,
            ShardId: "shard-me-central-1-pri",
            HlcTimestamp: hlcZ,
            EntityType: "Inventory",
            EntityId: "inv-500",
            PayloadJson: "{\"qty\":20}",
            PayloadSha256: "hashZ",
            Status: ReplicationStatus.ReplicatedCrossRegion
        );

        // "node-paris-01" > "node-algiers-01"
        var winner = _hlcCoordinator.ResolveConflict(recordA, recordZ);

        winner.TxId.Should().Be("tx-z");
        winner.Status.Should().Be(ReplicationStatus.ConflictResolved);
        winner.PayloadJson.Should().Be("{\"qty\":20}");
    }

    [Fact]
    public async Task ReplicateTransactionAsync_PermittedWrite_StoresInJournalWithSha256()
    {
        _hlcCoordinator.ClearJournal();

        var request = new ReplicationSyncRequest(
            TxId: "tx-sync-01",
            TenantId: SovereignDataShardingManager.DubaiLogisticsHubTenantId,
            OriginRegion: RegionIdentifier.MeCentral1,
            TargetRegion: RegionIdentifier.EuWest3,
            ShardId: "shard-eu-west-3-pri",
            HlcTimestamp: null,
            EntityType: "ShipmentWaypoint",
            EntityId: "shp-8801",
            PayloadJson: "{\"location\":\"Dubai Port\",\"status\":\"Departed\"}",
            PayloadSha256: null,
            IsPii: false
        );

        var response = await _hlcCoordinator.ReplicateTransactionAsync(request);

        response.Success.Should().BeTrue();
        response.Status.Should().Be(ReplicationStatus.ReplicatedCrossRegion);
        response.WinnerRecord.Should().NotBeNull();
        response.WinnerRecord!.PayloadSha256.Should().NotBeNullOrWhiteSpace();
        response.SovereignViolationDetected.Should().BeFalse();

        var journal = _hlcCoordinator.GetTransactionJournal("shp-8801");
        journal.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReplicateTransactionAsync_SovereigntyBreach_IsBlockedAndNotStoredInTargetState()
    {
        _hlcCoordinator.ClearJournal();

        // Attempting to export Algerian citizen data to Europe
        var request = new ReplicationSyncRequest(
            TxId: "tx-illegal-export",
            TenantId: SovereignDataShardingManager.AlgerianNationalBankTenantId,
            OriginRegion: RegionIdentifier.AfNorth1,
            TargetRegion: RegionIdentifier.EuWest3,
            ShardId: "shard-eu-west-3-pri",
            HlcTimestamp: null,
            EntityType: "AlgerianCitizenAccount",
            EntityId: "dz-citizen-4491",
            PayloadJson: "{\"nin\":\"199416000000001\",\"balance\":5000000}",
            PayloadSha256: null,
            IsPii: true
        );

        var response = await _hlcCoordinator.ReplicateTransactionAsync(request);

        response.Success.Should().BeFalse();
        response.SovereignViolationDetected.Should().BeTrue();
        response.Status.Should().Be(ReplicationStatus.CommittedLocal);
        response.Message.Should().Contain("Algerian Law 18-07");
        response.WinnerRecord.Should().BeNull();

        var journal = _hlcCoordinator.GetTransactionJournal("dz-citizen-4491");
        journal.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplicateTransactionAsync_ConcurrentWritesToSameEntity_ResolvesViaLastWriteWins()
    {
        _hlcCoordinator.ClearJournal();

        const long tBase = 1710000000000L;
        var hlcEarlier = new HybridLogicalClockRecord(tBase, 0, "node-paris-01");
        var hlcLater = new HybridLogicalClockRecord(tBase + 100, 0, "node-dubai-01");

        // Write 1: Earlier timestamp
        var request1 = new ReplicationSyncRequest(
            TxId: "tx-1",
            TenantId: SovereignDataShardingManager.GlobalSaaSFloatingTenantId,
            OriginRegion: RegionIdentifier.EuWest3,
            TargetRegion: RegionIdentifier.MeCentral1,
            ShardId: "shard-me-central-1-pri",
            HlcTimestamp: hlcEarlier,
            EntityType: "GlobalCatalogItem",
            EntityId: "item-777",
            PayloadJson: "{\"price\":100}",
            IsPii: false
        );

        var res1 = await _hlcCoordinator.ReplicateTransactionAsync(request1);
        res1.Success.Should().BeTrue();

        // Write 2: Later timestamp writes to the same entity
        var request2 = new ReplicationSyncRequest(
            TxId: "tx-2",
            TenantId: SovereignDataShardingManager.GlobalSaaSFloatingTenantId,
            OriginRegion: RegionIdentifier.MeCentral1,
            TargetRegion: RegionIdentifier.MeCentral1,
            ShardId: "shard-me-central-1-pri",
            HlcTimestamp: hlcLater,
            EntityType: "GlobalCatalogItem",
            EntityId: "item-777",
            PayloadJson: "{\"price\":150}",
            IsPii: false
        );

        var res2 = await _hlcCoordinator.ReplicateTransactionAsync(request2);

        res2.Success.Should().BeTrue();
        res2.Status.Should().Be(ReplicationStatus.ConflictResolved);
        res2.WinnerRecord.Should().NotBeNull();
        res2.WinnerRecord!.TxId.Should().Be("tx-2");
        res2.WinnerRecord.PayloadJson.Should().Be("{\"price\":150}");
    }

    #endregion

    #region 7. Physical Connection Strings & Sharding Endpoints

    [Fact]
    public async Task GetPhysicalConnectionString_WriteQuery_AlwaysRoutesToHomeSovereignPrimary()
    {
        var algerianEndpoint = await _sovereignManager.GetPhysicalConnectionStringAsync(
            SovereignDataShardingManager.AlgerianNationalBankTenantId,
            requestingRegion: RegionIdentifier.EuWest3,
            requiresWrite: true
        );

        algerianEndpoint.Should().Be("postgres://dz-primary.sgrowing.internal:5432/sgrowing_sovereign_dz");

        var euEndpoint = await _sovereignManager.GetPhysicalConnectionStringAsync(
            SovereignDataShardingManager.EuEnterpriseHealthTenantId,
            requestingRegion: RegionIdentifier.AfNorth1,
            requiresWrite: true
        );

        euEndpoint.Should().Be("postgres://fr-primary.sgrowing.internal:5432/sgrowing_sovereign_eu");
    }

    [Fact]
    public async Task GetPhysicalConnectionString_ReadQuery_ReturnsLocalReplicaWhenAllowed()
    {
        // DubaiLogisticsHub allows cross-region read replicas
        var endpoint = await _sovereignManager.GetPhysicalConnectionStringAsync(
            SovereignDataShardingManager.DubaiLogisticsHubTenantId,
            requestingRegion: RegionIdentifier.EuWest3,
            requiresWrite: false
        );

        endpoint.Should().Be("postgres://fr-ro1.sgrowing.internal:5432/sgrowing_ro");
    }

    [Fact]
    public async Task GetPhysicalConnectionString_AbsoluteSovereign_RestrictsReadReplicaToHomeRegion()
    {
        // AlgerianNationalBank disallows cross-region read replicas
        var endpoint = await _sovereignManager.GetPhysicalConnectionStringAsync(
            SovereignDataShardingManager.AlgerianNationalBankTenantId,
            requestingRegion: RegionIdentifier.EuWest3,
            requiresWrite: false
        );

        endpoint.Should().Be("postgres://dz-ro1.sgrowing.internal:5432/sgrowing_ro");
    }

    [Fact]
    public void GetAllShards_ReturnsAllThreeConfiguredRegionsWithMetadata()
    {
        var shards = _meshService.GetAllShards();

        shards.Should().HaveCount(3);
        shards.Select(s => s.Region).Should().BeEquivalentTo([
            RegionIdentifier.AfNorth1,
            RegionIdentifier.EuWest3,
            RegionIdentifier.MeCentral1
        ]);
        shards.All(s => s.IsActive && s.HealthState == ShardHealthState.Healthy).Should().BeTrue();
    }

    #endregion
}
