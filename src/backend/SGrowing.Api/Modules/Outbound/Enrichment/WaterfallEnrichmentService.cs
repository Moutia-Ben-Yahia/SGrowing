using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Outbound.Enrichment;

public class WaterfallEnrichmentService : IWaterfallEnrichmentService
{
    private readonly SGrowingDbContext _db;

    public WaterfallEnrichmentService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<WaterfallEnrichmentCascade> CreateCascadeAsync(
        Guid tenantId,
        string name,
        List<string> providers,
        string targetDataType = "EmailAndPhone",
        decimal minConfidence = 0.80m)
    {
        var providersJson = JsonSerializer.Serialize(providers);
        var cascade = WaterfallEnrichmentCascade.Create(tenantId, name, providersJson, targetDataType, minConfidence);
        _db.WaterfallEnrichmentCascades.Add(cascade);
        await _db.SaveChangesAsync();
        return cascade;
    }

    public async Task<WaterfallEnrichmentResultDto> ExecuteWaterfallEnrichmentAsync(
        Guid tenantId,
        string prospectQuery,
        string? companyDomain = null,
        Guid? cascadeId = null,
        Guid? contactId = null)
    {
        var providers = new List<string> { "Hunter", "Apollo", "Dropcontact", "Prospeo" };
        var minConfidence = 0.80m;

        if (cascadeId.HasValue)
        {
            var cascade = await _db.WaterfallEnrichmentCascades
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == cascadeId.Value);

            if (cascade != null)
            {
                var customList = JsonSerializer.Deserialize<List<string>>(cascade.ProviderChainJson);
                if (customList != null && customList.Count > 0)
                {
                    providers = customList;
                }
                minConfidence = cascade.MinConfidenceScore;
            }
        }

        var stepsTrail = new List<EnrichmentStepDetailDto>();
        var domain = !string.IsNullOrWhiteSpace(companyDomain)
            ? companyDomain.Trim().ToLowerInvariant()
            : "enterprise.com";

        var sanitizedName = prospectQuery.Trim().ToLowerInvariant().Replace(" ", ".");
        var totalSw = Stopwatch.StartNew();

        string? winningProvider = null;
        decimal winningConfidence = 0m;
        string? verifiedEmail = null;
        string? verifiedPhone = null;
        string? jobTitle = "VP of Growth & Operations";
        string? companyName = char.ToUpperInvariant(domain.Split('.')[0][0]) + domain.Split('.')[0][1..];

        for (int i = 0; i < providers.Count; i++)
        {
            var provider = providers[i];
            var stepIndex = i + 1;
            var stepLatency = 120 + (i * 45); // simulated provider latency ms

            // Simulated provider hit rates:
            // Hunter hits on step 1 (60% chance)
            // Apollo hits on step 2 (80% chance)
            // Dropcontact hits on step 3 (90% chance)
            bool isProviderMatch = (i == 0 && sanitizedName.Length % 2 == 0) || (i > 0);
            decimal score = isProviderMatch ? Math.Min(0.98m, 0.78m + (stepIndex * 0.06m)) : 0.45m;

            if (isProviderMatch && score >= minConfidence)
            {
                winningProvider = provider;
                winningConfidence = score;
                verifiedEmail = $"{sanitizedName}@{domain}";
                verifiedPhone = $"+216 71 {new Random(prospectQuery.GetHashCode()).Next(100, 999)} {new Random(prospectQuery.GetHashCode()).Next(100, 999)}";

                stepsTrail.Add(new EnrichmentStepDetailDto(stepIndex, provider, "Matched", score, stepLatency));

                var log = EnrichmentExecutionLog.Create(
                    tenantId,
                    prospectQuery,
                    provider,
                    stepIndex,
                    "Matched",
                    score,
                    1,
                    stepLatency,
                    JsonSerializer.Serialize(new { Email = verifiedEmail, Phone = verifiedPhone, Domain = domain }),
                    contactId);

                _db.EnrichmentExecutionLogs.Add(log);
                break;
            }
            else
            {
                stepsTrail.Add(new EnrichmentStepDetailDto(stepIndex, provider, "Missed - Cascading", score, stepLatency));

                var missLog = EnrichmentExecutionLog.Create(
                    tenantId,
                    prospectQuery,
                    provider,
                    stepIndex,
                    "FallbackTriggered",
                    score,
                    0,
                    stepLatency,
                    "{}",
                    contactId);

                _db.EnrichmentExecutionLogs.Add(missLog);
            }
        }

        await _db.SaveChangesAsync();
        totalSw.Stop();

        SGrowingTelemetry.WaterfallEnrichmentsExecutedCounter.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId.ToString()),
            new KeyValuePair<string, object?>("provider", winningProvider ?? "None"));

        return new WaterfallEnrichmentResultDto(
            winningProvider != null,
            prospectQuery,
            winningProvider,
            stepsTrail.Count,
            winningConfidence,
            verifiedEmail,
            verifiedPhone,
            jobTitle,
            companyName,
            winningProvider != null ? 1 : 0,
            totalSw.ElapsedMilliseconds,
            stepsTrail);
    }

    public async Task<List<EnrichmentExecutionLog>> GetExecutionLogsAsync(Guid tenantId, int limit = 50)
    {
        return await _db.EnrichmentExecutionLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.ExecutedAtUtc)
            .Take(limit)
            .ToListAsync();
    }
}
