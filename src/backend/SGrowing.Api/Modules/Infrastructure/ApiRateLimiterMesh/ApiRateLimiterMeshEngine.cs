using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.ApiRateLimiterMesh;

public sealed class ApiRateLimiterMeshEngine
{
    private readonly ConcurrentDictionary<string, ClientBucketProfile> _buckets = new();
    private readonly ConcurrentDictionary<string, QuarantinedIpRecord> _quarantined = new();
    private readonly ConcurrentBag<QuarantineReceipt> _quarantineHistory = new();

    public ApiRateLimiterMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var b1 = new ClientBucketProfile("CLI-ENT-ACME-01", "ENTERPRISE_UNLIMITED", 50000, 48500, 500.0, DateTime.UtcNow);
        var b2 = new ClientBucketProfile("CLI-SCALE-TECH-02", "GROWTH_SCALE", 10000, 9200, 100.0, DateTime.UtcNow);
        var b3 = new ClientBucketProfile("CLI-STARTER-APP-03", "STARTER_TIER", 1000, 45, 10.0, DateTime.UtcNow);

        _buckets[b1.ClientId] = b1;
        _buckets[b2.ClientId] = b2;
        _buckets[b3.ClientId] = b3;

        var q1 = new QuarantinedIpRecord(
            "185.220.101.5",
            "Distributed Credential Stuffing & High-Entropy Header Scrape",
            0.985,
            DateTime.UtcNow.AddMinutes(-30),
            DateTime.UtcNow.AddHours(2),
            "SYNCED_ALL_EDGE_POPS"
        );

        _quarantined[q1.IpAddress] = q1;
    }

    public ApiRateLimiterOverview GetOverview()
    {
        var bucketsList = _buckets.Values.ToList();
        var quarantinedList = _quarantined.Values.ToList();

        return new ApiRateLimiterOverview(
            bucketsList.Count,
            quarantinedList.Count,
            250.0,
            14500000,
            bucketsList,
            quarantinedList
        );
    }

    public RateLimitVerdict ConsumeToken(ConsumeTokenRequest request)
    {
        // Check if IP is quarantined
        if (_quarantined.TryGetValue(request.RequestIp, out var qRecord) && qRecord.ExpiresAt > DateTime.UtcNow)
        {
            return new RateLimitVerdict(
                $"VER-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                request.ClientId,
                "REJECT_TOO_MANY_REQUESTS",
                0,
                0,
                300,
                "EDGE-NODE-FRA-01"
            );
        }

        var bucket = _buckets.TryGetValue(request.ClientId, out var b) ? b : _buckets.Values.First();

        if (bucket.AvailableTokens < request.TokensToConsume)
        {
            return new RateLimitVerdict(
                $"VER-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                bucket.ClientId,
                "TARPIT_DELAY",
                bucket.AvailableTokens,
                500,
                5,
                "EDGE-NODE-IAD-02"
            );
        }

        var remaining = bucket.AvailableTokens - request.TokensToConsume;
        var updated = bucket with { AvailableTokens = remaining, LastRefillAt = DateTime.UtcNow };
        _buckets[bucket.ClientId] = updated;

        return new RateLimitVerdict(
            $"VER-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            bucket.ClientId,
            "ALLOW",
            remaining,
            0,
            0,
            "EDGE-NODE-SJC-04"
        );
    }

    public QuarantineReceipt QuarantineIp(QuarantineIpRequest request)
    {
        var record = new QuarantinedIpRecord(
            request.IpAddress,
            request.Reason,
            request.EntropyScore,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(request.QuarantineDurationMinutes),
            "SYNCING_EDGE_NODES"
        );

        _quarantined[request.IpAddress] = record;

        var receipt = new QuarantineReceipt(
            $"QRC-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            request.IpAddress,
            "QUARANTINED_ACTIVE",
            record.ExpiresAt,
            $"0x{Guid.NewGuid():N}"[..16]
        );

        _quarantineHistory.Add(receipt);
        return receipt;
    }
}
