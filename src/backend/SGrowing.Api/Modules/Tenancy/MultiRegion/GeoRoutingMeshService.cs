using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Tenancy.MultiRegion;

public interface IGeoRoutingMeshService
{
    IReadOnlyList<RegionalShardMetadata> GetAllShards();
    RegionalShardMetadata? GetShard(string shardId);
    RegionalShardMetadata? GetPrimaryShardForRegion(RegionIdentifier region);
    void UpdateShardStatus(string shardId, bool isActive, ShardHealthState healthState, double? latencyScoreMs = null);
    void ResetShardStatusToDefaults();
    RegionIdentifier ResolveCountryToNearestRegion(string countryCode);
    double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2);
    GeoRoutingDecision ResolveRoute(GeoRouteRequest request);
}

public class GeoRoutingMeshService : IGeoRoutingMeshService
{
    private readonly ISovereignDataShardingManager? _sovereigntyManager;
    private readonly ConcurrentDictionary<string, RegionalShardMetadata> _shards = new();

    // Canonical Region Coordinates
    public const double AlgiersLatitude = 36.7538;
    public const double AlgiersLongitude = 3.0588;

    public const double ParisLatitude = 48.8566;
    public const double ParisLongitude = 2.3522;

    public const double DubaiLatitude = 25.2048;
    public const double DubaiLongitude = 55.2708;

    private static readonly Dictionary<string, RegionIdentifier> CountryRegionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // North Africa & Sahel
        ["DZ"] = RegionIdentifier.AfNorth1,
        ["TN"] = RegionIdentifier.AfNorth1,
        ["MA"] = RegionIdentifier.AfNorth1,
        ["LY"] = RegionIdentifier.AfNorth1,
        ["EG"] = RegionIdentifier.AfNorth1,
        ["MR"] = RegionIdentifier.AfNorth1,
        ["NE"] = RegionIdentifier.AfNorth1,
        ["ML"] = RegionIdentifier.AfNorth1,
        ["NG"] = RegionIdentifier.AfNorth1,

        // Europe (EU & UK)
        ["FR"] = RegionIdentifier.EuWest3,
        ["DE"] = RegionIdentifier.EuWest3,
        ["ES"] = RegionIdentifier.EuWest3,
        ["IT"] = RegionIdentifier.EuWest3,
        ["GB"] = RegionIdentifier.EuWest3,
        ["UK"] = RegionIdentifier.EuWest3,
        ["BE"] = RegionIdentifier.EuWest3,
        ["NL"] = RegionIdentifier.EuWest3,
        ["CH"] = RegionIdentifier.EuWest3,
        ["SE"] = RegionIdentifier.EuWest3,
        ["NO"] = RegionIdentifier.EuWest3,
        ["PL"] = RegionIdentifier.EuWest3,
        ["PT"] = RegionIdentifier.EuWest3,
        ["AT"] = RegionIdentifier.EuWest3,
        ["IE"] = RegionIdentifier.EuWest3,

        // Middle East & GCC
        ["AE"] = RegionIdentifier.MeCentral1,
        ["SA"] = RegionIdentifier.MeCentral1,
        ["QA"] = RegionIdentifier.MeCentral1,
        ["OM"] = RegionIdentifier.MeCentral1,
        ["KW"] = RegionIdentifier.MeCentral1,
        ["BH"] = RegionIdentifier.MeCentral1,
        ["JO"] = RegionIdentifier.MeCentral1,
        ["LB"] = RegionIdentifier.MeCentral1,
        ["IQ"] = RegionIdentifier.MeCentral1
    };

    public GeoRoutingMeshService(ISovereignDataShardingManager? sovereigntyManager = null)
    {
        _sovereigntyManager = sovereigntyManager;
        InitializeDefaultShards();
    }

    private void InitializeDefaultShards()
    {
        _shards.Clear();

        var afNorth = new RegionalShardMetadata(
            ShardId: "shard-af-north-1-pri",
            Region: RegionIdentifier.AfNorth1,
            Jurisdiction: SovereignJurisdiction.Algeria_Law1807,
            PrimaryEndpoint: "postgres://dz-primary.sgrowing.internal:5432/sgrowing_sovereign_dz",
            ReadReplicas: ["postgres://dz-ro1.sgrowing.internal:5432/sgrowing_ro", "postgres://oran-ro2.sgrowing.internal:5432/sgrowing_ro"],
            LatencyScoreMs: 12.5,
            IsActive: true,
            HealthState: ShardHealthState.Healthy,
            Latitude: AlgiersLatitude,
            Longitude: AlgiersLongitude
        );

        var euWest = new RegionalShardMetadata(
            ShardId: "shard-eu-west-3-pri",
            Region: RegionIdentifier.EuWest3,
            Jurisdiction: SovereignJurisdiction.Eu_Gdpr,
            PrimaryEndpoint: "postgres://fr-primary.sgrowing.internal:5432/sgrowing_sovereign_eu",
            ReadReplicas: ["postgres://fr-ro1.sgrowing.internal:5432/sgrowing_ro", "postgres://de-ro2.sgrowing.internal:5432/sgrowing_ro"],
            LatencyScoreMs: 18.2,
            IsActive: true,
            HealthState: ShardHealthState.Healthy,
            Latitude: ParisLatitude,
            Longitude: ParisLongitude
        );

        var meCentral = new RegionalShardMetadata(
            ShardId: "shard-me-central-1-pri",
            Region: RegionIdentifier.MeCentral1,
            Jurisdiction: SovereignJurisdiction.Uae_DIFC,
            PrimaryEndpoint: "postgres://uae-primary.sgrowing.internal:5432/sgrowing_sovereign_me",
            ReadReplicas: ["postgres://dxb-ro1.sgrowing.internal:5432/sgrowing_ro", "postgres://auh-ro2.sgrowing.internal:5432/sgrowing_ro"],
            LatencyScoreMs: 24.0,
            IsActive: true,
            HealthState: ShardHealthState.Healthy,
            Latitude: DubaiLatitude,
            Longitude: DubaiLongitude
        );

        _shards[afNorth.ShardId] = afNorth;
        _shards[euWest.ShardId] = euWest;
        _shards[meCentral.ShardId] = meCentral;
    }

    public void ResetShardStatusToDefaults() => InitializeDefaultShards();

    public IReadOnlyList<RegionalShardMetadata> GetAllShards() =>
        _shards.Values.OrderBy(s => s.Region).ToList();

    public RegionalShardMetadata? GetShard(string shardId) =>
        _shards.TryGetValue(shardId, out var shard) ? shard : null;

    public RegionalShardMetadata? GetPrimaryShardForRegion(RegionIdentifier region) =>
        _shards.Values.FirstOrDefault(s => s.Region == region && s.ShardId.EndsWith("-pri"));

    public void UpdateShardStatus(string shardId, bool isActive, ShardHealthState healthState, double? latencyScoreMs = null)
    {
        if (_shards.TryGetValue(shardId, out var existing))
        {
            var updated = existing with
            {
                IsActive = isActive,
                HealthState = healthState,
                LatencyScoreMs = latencyScoreMs ?? existing.LatencyScoreMs
            };
            _shards[shardId] = updated;
        }
    }

    public RegionIdentifier ResolveCountryToNearestRegion(string countryCode)
    {
        if (!string.IsNullOrWhiteSpace(countryCode) &&
            CountryRegionMap.TryGetValue(countryCode.Trim().ToUpperInvariant(), out var region))
        {
            return region;
        }

        // Default to AfNorth1 if country is unmapped
        return RegionIdentifier.AfNorth1;
    }

    public double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);

    public GeoRoutingDecision ResolveRoute(GeoRouteRequest request)
    {
        var clientIp = request.ClientIp?.Trim() ?? "127.0.0.1";
        var country = ResolveCountryFromIpOrRequest(clientIp, request.CountryCode);

        // Check if tenant residency policy governs this route
        TenantDataResidencyPolicy? tenantPolicy = null;
        if (request.TenantId.HasValue && _sovereigntyManager is not null)
        {
            tenantPolicy = _sovereigntyManager.GetResidencyPolicyAsync(request.TenantId.Value).GetAwaiter().GetResult();
        }

        // Determine ideal target region: if tenant is absolute sovereign, home region is mandatory
        RegionIdentifier preferredRegion;
        if (tenantPolicy is not null && tenantPolicy.DataLocalityStrictness == DataLocalityStrictness.AbsoluteSovereign)
        {
            preferredRegion = tenantPolicy.HomeRegion;
        }
        else
        {
            preferredRegion = ResolveCountryToNearestRegion(country);
        }

        var preferredShard = GetPrimaryShardForRegion(preferredRegion);

        // Check if preferred shard is healthy and active
        var isPreferredHealthy = preferredShard is not null &&
                                 preferredShard.IsActive &&
                                 preferredShard.HealthState == ShardHealthState.Healthy;

        if (isPreferredHealthy && preferredShard is not null)
        {
            return new GeoRoutingDecision(
                ClientIp: clientIp,
                DetectedCountry: country,
                TargetRegion: preferredRegion,
                SelectedShardId: preferredShard.ShardId,
                EstimatedLatencyMs: preferredShard.LatencyScoreMs,
                ComplianceVerified: true,
                RoutingReason: $"Primary regional shard {preferredShard.ShardId} selected based on proximity and health.",
                FallbackApplied: false
            );
        }

        // Preferred shard is degraded, offline, or inactive.
        // If tenant has AbsoluteSovereign strictness, cross-border fallback is prohibited by sovereignty laws.
        if (tenantPolicy is not null && tenantPolicy.DataLocalityStrictness == DataLocalityStrictness.AbsoluteSovereign)
        {
            // Fallback to secondary read replica or isolate in sovereign zone
            var sovereignShardId = preferredShard?.ShardId ?? "shard-sovereign-fallback";
            return new GeoRoutingDecision(
                ClientIp: clientIp,
                DetectedCountry: country,
                TargetRegion: preferredRegion,
                SelectedShardId: sovereignShardId,
                EstimatedLatencyMs: (preferredShard?.LatencyScoreMs ?? 20.0) * 2.5,
                ComplianceVerified: false,
                RoutingReason: $"Sovereign policy {tenantPolicy.EnforcedJurisdiction} forbids cross-border fallback. Shard {sovereignShardId} operating in sovereign degraded mode.",
                FallbackApplied: false
            );
        }

        // Fallback for non-strict or public routing: find lowest-latency active shard
        var fallbackShard = _shards.Values
            .Where(s => s.IsActive && s.HealthState != ShardHealthState.Offline)
            .OrderBy(s => s.LatencyScoreMs)
            .FirstOrDefault();

        if (fallbackShard is not null)
        {
            return new GeoRoutingDecision(
                ClientIp: clientIp,
                DetectedCountry: country,
                TargetRegion: fallbackShard.Region,
                SelectedShardId: fallbackShard.ShardId,
                EstimatedLatencyMs: fallbackShard.LatencyScoreMs + 15.0, // inter-region latency penalty
                ComplianceVerified: true,
                RoutingReason: $"Primary shard for {preferredRegion} is unavailable. Fallback applied to lowest-latency healthy shard {fallbackShard.ShardId}.",
                FallbackApplied: true
            );
        }

        // Emergency fallback if all shards are down
        var defaultShard = preferredShard ?? _shards.Values.First();
        return new GeoRoutingDecision(
            ClientIp: clientIp,
            DetectedCountry: country,
            TargetRegion: defaultShard.Region,
            SelectedShardId: defaultShard.ShardId,
            EstimatedLatencyMs: 999.0,
            ComplianceVerified: false,
            RoutingReason: "Critical: All regional shards offline or degraded. Emergency fallback invoked.",
            FallbackApplied: true
        );
    }

    private static string ResolveCountryFromIpOrRequest(string clientIp, string? requestedCountry)
    {
        if (!string.IsNullOrWhiteSpace(requestedCountry))
        {
            return requestedCountry.Trim().ToUpperInvariant();
        }

        // IP range heuristic for demo & edge detection
        if (clientIp.StartsWith("105.") || clientIp.StartsWith("197.") || clientIp.StartsWith("41."))
        {
            return "DZ"; // Algeria / North Africa
        }

        if (clientIp.StartsWith("185.") || clientIp.StartsWith("193.") || clientIp.StartsWith("212.") || clientIp.StartsWith("2.20."))
        {
            return "FR"; // France / EU
        }

        if (clientIp.StartsWith("94.200.") || clientIp.StartsWith("188.135.") || clientIp.StartsWith("194.170.") || clientIp.StartsWith("5.36."))
        {
            return "AE"; // UAE / Middle East
        }

        return "DZ"; // Default baseline
    }
}
