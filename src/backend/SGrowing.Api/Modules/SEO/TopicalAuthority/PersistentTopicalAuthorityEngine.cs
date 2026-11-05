using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO.TopicalAuthority;

public record TopicClusterDto(
    string ClusterId,
    string PillarTopic,
    string TargetKeyword,
    double AuthorityScore,
    int TotalSubArticles,
    int MissingSubTopicsCount,
    IReadOnlyList<string> CoveredEntities,
    IReadOnlyList<string> RecommendedExpansions
);

public interface IPersistentTopicalAuthorityEngine
{
    Task<TopicalClusterEntity> UpsertClusterAsync(
        Guid tenantId,
        string clusterId,
        string pillarTopic,
        string targetKeyword,
        double authorityScore,
        int totalSubArticles,
        int missingSubTopicsCount,
        IEnumerable<string> coveredEntities,
        IEnumerable<string> recommendedExpansions,
        CancellationToken ct = default);

    Task<IReadOnlyList<TopicClusterDto>> GetClustersAsync(Guid tenantId, CancellationToken ct = default);
}

public class PersistentTopicalAuthorityEngine : IPersistentTopicalAuthorityEngine
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<PersistentTopicalAuthorityEngine>? _logger;
    private readonly ConcurrentDictionary<string, TopicalClusterEntity> _l1Cache = new();

    public PersistentTopicalAuthorityEngine(
        SGrowingDbContext db,
        ILogger<PersistentTopicalAuthorityEngine>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TopicalClusterEntity> UpsertClusterAsync(
        Guid tenantId,
        string clusterId,
        string pillarTopic,
        string targetKeyword,
        double authorityScore,
        int totalSubArticles,
        int missingSubTopicsCount,
        IEnumerable<string> coveredEntities,
        IEnumerable<string> recommendedExpansions,
        CancellationToken ct = default)
    {
        var existing = await _db.TopicClusters
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ClusterId == clusterId, ct);

        var coveredJson = JsonSerializer.Serialize(coveredEntities);
        var expansionsJson = JsonSerializer.Serialize(recommendedExpansions);

        if (existing != null)
        {
            existing.PillarTopic = pillarTopic;
            existing.TargetKeyword = targetKeyword;
            existing.TopicalAuthorityScore = authorityScore;
            existing.TotalSubArticles = totalSubArticles;
            existing.MissingSubTopicsCount = missingSubTopicsCount;
            existing.CoveredEntitiesJson = coveredJson;
            existing.RecommendedClusterExpansionsJson = expansionsJson;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new TopicalClusterEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ClusterId = clusterId,
                PillarTopic = pillarTopic,
                TargetKeyword = targetKeyword,
                TopicalAuthorityScore = authorityScore,
                TotalSubArticles = totalSubArticles,
                MissingSubTopicsCount = missingSubTopicsCount,
                CoveredEntitiesJson = coveredJson,
                RecommendedClusterExpansionsJson = expansionsJson,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            await _db.TopicClusters.AddAsync(existing, ct);
        }

        await _db.SaveChangesAsync(ct);

        _l1Cache[$"{tenantId}_{clusterId}"] = existing;
        _logger?.LogInformation("Upserted topic cluster {ClusterId} for tenant {TenantId}. Authority Score: {Score}", clusterId, tenantId, authorityScore);
        return existing;
    }

    public async Task<IReadOnlyList<TopicClusterDto>> GetClustersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entities = await _db.TopicClusters
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.TopicalAuthorityScore)
            .ToListAsync(ct);

        return entities.Select(e => new TopicClusterDto(
            ClusterId: e.ClusterId,
            PillarTopic: e.PillarTopic,
            TargetKeyword: e.TargetKeyword,
            AuthorityScore: e.TopicalAuthorityScore,
            TotalSubArticles: e.TotalSubArticles,
            MissingSubTopicsCount: e.MissingSubTopicsCount,
            CoveredEntities: JsonSerializer.Deserialize<List<string>>(e.CoveredEntitiesJson) ?? new List<string>(),
            RecommendedExpansions: JsonSerializer.Deserialize<List<string>>(e.RecommendedClusterExpansionsJson) ?? new List<string>()
        )).ToList();
    }
}
