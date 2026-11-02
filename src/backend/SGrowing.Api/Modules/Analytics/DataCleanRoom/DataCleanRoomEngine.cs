using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Analytics.DataCleanRoom;

public class DataCleanRoomEngine
{
    private readonly ConcurrentDictionary<string, CleanRoomParticipant> _participants = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AudienceCohortDataset> _datasets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<CleanRoomOverlapResult> _results = new();

    public DataCleanRoomEngine()
    {
        SeedCleanRoomData();
    }

    private void SeedCleanRoomData()
    {
        var p1 = new CleanRoomParticipant(
            ParticipantId: "part_acme_brand",
            OrganizationName: "Acme Consumer Brands Group",
            Role: "Advertiser",
            CryptographicSaltId: "salt_acme_sha256_v4",
            Status: "Active"
        );

        var p2 = new CleanRoomParticipant(
            ParticipantId: "part_premium_pub",
            OrganizationName: "Global News & Media Network",
            Role: "Publisher",
            CryptographicSaltId: "salt_pub_sha256_v4",
            Status: "Active"
        );

        _participants[p1.ParticipantId] = p1;
        _participants[p2.ParticipantId] = p2;

        var d1 = new AudienceCohortDataset(
            DatasetId: "ds_acme_high_ltv",
            ParticipantId: p1.ParticipantId,
            CohortName: "Acme 90-Day High LTV Purchasers (First-Party)",
            HashedIdentityCount: 84000,
            EncryptionScheme: "SHA-256 + HMAC Salted Ring",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-5)
        );

        var d2 = new AudienceCohortDataset(
            DatasetId: "ds_pub_tech_readers",
            ParticipantId: p2.ParticipantId,
            CohortName: "Premium Publisher Tech & SaaS Readers",
            HashedIdentityCount: 320000,
            EncryptionScheme: "SHA-256 + HMAC Salted Ring",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-4)
        );

        _datasets[d1.DatasetId] = d1;
        _datasets[d2.DatasetId] = d2;
    }

    public Task<IReadOnlyList<CleanRoomParticipant>> GetParticipantsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<CleanRoomParticipant>>(_participants.Values.ToList());
    }

    public Task<IReadOnlyList<AudienceCohortDataset>> GetDatasetsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AudienceCohortDataset>>(_datasets.Values.OrderByDescending(d => d.HashedIdentityCount).ToList());
    }

    public Task<CleanRoomOverlapResult> ExecuteOverlapMeasurementAsync(RunOverlapQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_datasets.TryGetValue(request.AdvertiserDatasetId, out var advDataset))
        {
            throw new KeyNotFoundException($"Advertiser dataset '{request.AdvertiserDatasetId}' not found.");
        }
        if (!_datasets.TryGetValue(request.PublisherDatasetId, out var pubDataset))
        {
            throw new KeyNotFoundException($"Publisher dataset '{request.PublisherDatasetId}' not found.");
        }

        // Private Set Intersection (PSI) overlap simulation
        var rawMatch = (int)(advDataset.HashedIdentityCount * 0.285); // 28.5% overlap
        var noise = (int)Math.Round((1.0 / Math.Max(0.1, request.DifferentialPrivacyEpsilon)) * 14.0);
        var noisedMatch = rawMatch + noise;

        var matchRate = Math.Round(((double)noisedMatch / advDataset.HashedIdentityCount) * 100, 2);
        var attributedRevenue = noisedMatch * 38.50m; // $38.50 average order value
        var roas = Math.Round((double)(attributedRevenue / 150000m), 2); // against $150k ad spend

        var queryId = $"query_zk_{Guid.NewGuid():N}";
        var result = new CleanRoomOverlapResult(
            QueryId: queryId,
            AdvertiserDatasetId: advDataset.DatasetId,
            PublisherDatasetId: pubDataset.DatasetId,
            RawOverlapMatchCount: rawMatch,
            PrivacyNoisedOverlapCount: noisedMatch,
            MatchRatePercent: matchRate,
            AttributedRevenue: attributedRevenue,
            PrivacyPreservingRoas: Math.Max(1.0, roas),
            EpsilonCost: request.DifferentialPrivacyEpsilon,
            ExecutedAtUtc: DateTime.UtcNow
        );

        _results.Add(result);
        return Task.FromResult(result);
    }

    public Task<CleanRoomSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalIdentities = _datasets.Values.Sum(d => d.HashedIdentityCount);
        var totalEpsilon = _results.Sum(r => r.EpsilonCost);

        var summary = new CleanRoomSummary(
            TotalParticipants: _participants.Count,
            TotalCohortsSecured: _datasets.Count,
            TotalHashedIdentities: totalIdentities,
            TotalQueriesExecuted: _results.Count,
            TotalEpsilonConsumed: Math.Round(totalEpsilon, 2)
        );

        return Task.FromResult(summary);
    }
}
