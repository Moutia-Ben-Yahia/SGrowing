using System.Collections.Concurrent;
using System.Text;

namespace SGrowing.Api.Modules.AI.Memory;

/// <summary>
/// Contract for multi-tier agent memory management, semantic relevance scoring,
/// TTL expiration pruning, and LLM prompt context injection.
/// </summary>
public interface IAgentMemoryService
{
    /// <summary>
    /// Stores or updates an agent memory fact.
    /// </summary>
    AgentMemoryEntry StoreMemory(StoreMemoryRequest request);

    /// <summary>
    /// Queries active memories using semantic keyword scoring, category filtering, and confidence thresholds.
    /// </summary>
    IReadOnlyList<MemoryQueryResult> QueryMemory(QueryMemoryRequest request);

    /// <summary>
    /// Retrieves all active memories within a specific category for a tenant.
    /// </summary>
    IReadOnlyList<AgentMemoryEntry> GetMemoryByCategory(string tenantId, AgentMemoryCategory category);

    /// <summary>
    /// Retrieves all active memories for a tenant.
    /// </summary>
    IReadOnlyList<AgentMemoryEntry> GetAllMemories(string tenantId);

    /// <summary>
    /// Retrieves a single memory entry by ID.
    /// </summary>
    AgentMemoryEntry? GetMemoryById(string tenantId, string id);

    /// <summary>
    /// Deletes a specific memory entry for a tenant.
    /// </summary>
    bool DeleteMemory(string tenantId, string id);

    /// <summary>
    /// Prunes expired memory entries whose TTL has passed.
    /// </summary>
    int PruneExpired(string tenantId);

    /// <summary>
    /// Formats tenant memories into an LLM system prompt injection string.
    /// </summary>
    string FormatMemoryForPromptInjection(string tenantId, List<AgentMemoryCategory>? categories = null, string? topicQuery = null);

    /// <summary>
    /// Seeds default organizational facts for the specified tenant if not already populated.
    /// </summary>
    void SeedTenantDefaults(string tenantId);
}

/// <summary>
/// Thread-safe, multi-tenant implementation of <see cref="IAgentMemoryService"/>.
/// </summary>
public class AgentMemoryService : IAgentMemoryService
{
    public const string DefaultTenantId = "00000000-0000-0000-0000-000000000001";
    public const string DefaultTestTenantId = "test-tenant";

    // TenantId -> (MemoryId -> AgentMemoryEntry)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, AgentMemoryEntry>> _store = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "in", "on", "at", "to", "for", "with", "by", "of", "is", "are", "was", "were", "be", "this", "that"
    };

    public AgentMemoryService()
    {
        SeedTenantDefaults(DefaultTenantId);
        SeedTenantDefaults(DefaultTestTenantId);
    }

    public AgentMemoryEntry StoreMemory(StoreMemoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId))
            throw new ArgumentException("TenantId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ArgumentException("Key is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Value))
            throw new ArgumentException("Value is required.", nameof(request));

        var tenantStore = _store.GetOrAdd(request.TenantId, _ => new ConcurrentDictionary<string, AgentMemoryEntry>(StringComparer.OrdinalIgnoreCase));

        var now = DateTime.UtcNow;
        DateTime? expiresAt = request.TtlHours.HasValue
            ? now.AddHours(request.TtlHours.Value)
            : null;

        var id = $"mem_{Guid.NewGuid():N}";
        var entry = new AgentMemoryEntry(
            Id: id,
            TenantId: request.TenantId,
            Category: request.Category,
            Key: request.Key.Trim(),
            Value: request.Value.Trim(),
            Confidence: request.Confidence,
            SourceAgent: string.IsNullOrWhiteSpace(request.SourceAgent) ? "SystemArchitectAgent" : request.SourceAgent.Trim(),
            CreatedAtUtc: now,
            ExpiresAtUtc: expiresAt,
            Tags: request.Tags != null ? request.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : new List<string>(),
            Metadata: request.Metadata != null ? new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>()
        );

        tenantStore[id] = entry;
        return entry;
    }

    public IReadOnlyList<MemoryQueryResult> QueryMemory(QueryMemoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TenantId) || !_store.TryGetValue(request.TenantId, out var tenantStore))
        {
            return Array.Empty<MemoryQueryResult>();
        }

        var now = DateTime.UtcNow;
        var activeEntries = tenantStore.Values
            .Where(e => !e.ExpiresAtUtc.HasValue || e.ExpiresAtUtc.Value > now)
            .ToList();

        if (request.Categories != null && request.Categories.Count > 0)
        {
            var categorySet = request.Categories.ToHashSet();
            activeEntries = activeEntries.Where(e => categorySet.Contains(e.Category)).ToList();
        }

        if (request.MinConfidence.HasValue)
        {
            activeEntries = activeEntries.Where(e => e.Confidence >= request.MinConfidence.Value).ToList();
        }

        var query = request.Query?.Trim() ?? string.Empty;
        var limit = request.Limit <= 0 ? 10 : request.Limit;

        if (string.IsNullOrWhiteSpace(query))
        {
            return activeEntries
                .Select(e => new MemoryQueryResult(e, Math.Round(1.0 + GetConfidenceWeight(e.Confidence), 2)))
                .OrderByDescending(r => r.RelevanceScore)
                .ThenByDescending(r => r.Entry.CreatedAtUtc)
                .Take(limit)
                .ToList();
        }

        var queryTokens = Tokenize(query);
        var queryLower = query.ToLowerInvariant();
        var results = new List<MemoryQueryResult>();

        foreach (var entry in activeEntries)
        {
            var score = CalculateRelevanceScore(entry, queryLower, queryTokens, now);
            if (score > 0.0)
            {
                results.Add(new MemoryQueryResult(entry, Math.Round(score, 3)));
            }
        }

        return results
            .OrderByDescending(r => r.RelevanceScore)
            .ThenByDescending(r => r.Entry.CreatedAtUtc)
            .Take(limit)
            .ToList();
    }

    public IReadOnlyList<AgentMemoryEntry> GetMemoryByCategory(string tenantId, AgentMemoryCategory category)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || !_store.TryGetValue(tenantId, out var tenantStore))
        {
            return Array.Empty<AgentMemoryEntry>();
        }

        var now = DateTime.UtcNow;
        return tenantStore.Values
            .Where(e => e.Category == category && (!e.ExpiresAtUtc.HasValue || e.ExpiresAtUtc.Value > now))
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToList();
    }

    public IReadOnlyList<AgentMemoryEntry> GetAllMemories(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || !_store.TryGetValue(tenantId, out var tenantStore))
        {
            return Array.Empty<AgentMemoryEntry>();
        }

        var now = DateTime.UtcNow;
        return tenantStore.Values
            .Where(e => !e.ExpiresAtUtc.HasValue || e.ExpiresAtUtc.Value > now)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToList();
    }

    public AgentMemoryEntry? GetMemoryById(string tenantId, string id)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(id) || !_store.TryGetValue(tenantId, out var tenantStore))
        {
            return null;
        }

        if (tenantStore.TryGetValue(id, out var entry))
        {
            if (entry.ExpiresAtUtc.HasValue && entry.ExpiresAtUtc.Value <= DateTime.UtcNow)
            {
                return null;
            }
            return entry;
        }

        return null;
    }

    public bool DeleteMemory(string tenantId, string id)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(id) || !_store.TryGetValue(tenantId, out var tenantStore))
        {
            return false;
        }

        return tenantStore.TryRemove(id, out _);
    }

    public int PruneExpired(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || !_store.TryGetValue(tenantId, out var tenantStore))
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var expiredKeys = tenantStore.Values
            .Where(e => e.ExpiresAtUtc.HasValue && e.ExpiresAtUtc.Value <= now)
            .Select(e => e.Id)
            .ToList();

        var prunedCount = 0;
        foreach (var key in expiredKeys)
        {
            if (tenantStore.TryRemove(key, out _))
            {
                prunedCount++;
            }
        }

        return prunedCount;
    }

    public string FormatMemoryForPromptInjection(string tenantId, List<AgentMemoryCategory>? categories = null, string? topicQuery = null)
    {
        IReadOnlyList<AgentMemoryEntry> memories;

        if (!string.IsNullOrWhiteSpace(topicQuery))
        {
            var searchResults = QueryMemory(new QueryMemoryRequest(tenantId, topicQuery, categories, Limit: 15));
            memories = searchResults.Select(r => r.Entry).ToList();
        }
        else
        {
            var all = GetAllMemories(tenantId);
            if (categories != null && categories.Count > 0)
            {
                var catSet = categories.ToHashSet();
                memories = all.Where(m => catSet.Contains(m.Category)).ToList();
            }
            else
            {
                memories = all;
            }
        }

        if (memories.Count == 0)
        {
            return "[AI AGENT MEMORY CONTEXT: None available]";
        }

        var sb = new StringBuilder();
        sb.AppendLine("### SYSTEM AGENT MEMORY CONTEXT ###");
        sb.AppendLine("The following verified multi-tier organizational memories and constraints MUST guide your responses:");
        sb.AppendLine();

        var grouped = memories
            .GroupBy(m => m.Category)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var header = group.Key switch
            {
                AgentMemoryCategory.BrandMemory => "Brand Voice & Tone Memory",
                AgentMemoryCategory.BusinessMemory => "Business Rules & Operational Policy Memory",
                AgentMemoryCategory.CampaignMemory => "Campaign & Growth Playbook Memory",
                AgentMemoryCategory.CustomerContext => "Customer Context & Tier SLA Memory",
                AgentMemoryCategory.OperationalFact => "Operational Facts & System Grounding Memory",
                _ => $"{group.Key} Memory"
            };

            sb.AppendLine($"[{header}]");
            foreach (var item in group)
            {
                sb.AppendLine($"- [{item.Key}] (Confidence: {item.Confidence} | Source: {item.SourceAgent})");
                sb.AppendLine($"  {item.Value}");
                if (item.Tags.Count > 0)
                {
                    sb.AppendLine($"  Tags: #{string.Join(" #", item.Tags)}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public void SeedTenantDefaults(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return;

        var tenantStore = _store.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, AgentMemoryEntry>(StringComparer.OrdinalIgnoreCase));
        if (!tenantStore.IsEmpty) return;

        var now = DateTime.UtcNow;

        // 1. Brand Voice
        var brandVoice = new AgentMemoryEntry(
            Id: $"seed_brand_{tenantId.GetHashCode():x8}",
            TenantId: tenantId,
            Category: AgentMemoryCategory.BrandMemory,
            Key: "Brand Voice Guidelines",
            Value: "Direct, culturally resonant, respectful in Arabic & English",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "BrandToneDirectorAgent",
            CreatedAtUtc: now,
            ExpiresAtUtc: null,
            Tags: new List<string> { "brand", "voice", "tone", "arabic", "english" },
            Metadata: new Dictionary<string, string> { ["persona"] = "Executive Growth Director", ["locales"] = "ar-SA,en-US" }
        );
        tenantStore[brandVoice.Id] = brandVoice;

        // 2. Business Rules
        var businessRules = new AgentMemoryEntry(
            Id: $"seed_biz_{tenantId.GetHashCode():x8}",
            TenantId: tenantId,
            Category: AgentMemoryCategory.BusinessMemory,
            Key: "Saudi E-Commerce & Regulatory Rules",
            Value: "Saudi 15% VAT, 24h COD delivery in Riyadh, 3-day return policy",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "ComplianceOfficerAgent",
            CreatedAtUtc: now,
            ExpiresAtUtc: null,
            Tags: new List<string> { "saudi", "vat", "cod", "delivery", "riyadh", "return-policy" },
            Metadata: new Dictionary<string, string> { ["jurisdiction"] = "KSA", ["vatRate"] = "0.15" }
        );
        tenantStore[businessRules.Id] = businessRules;

        // 3. Campaign Memory
        var campaignMemory = new AgentMemoryEntry(
            Id: $"seed_camp_{tenantId.GetHashCode():x8}",
            TenantId: tenantId,
            Category: AgentMemoryCategory.CampaignMemory,
            Key: "Ramadan Campaign Guardrails",
            Value: "Ramadan discount max 25%, ROAS target 3.8x",
            Confidence: MemoryConfidenceLevel.High,
            SourceAgent: "GrowthCampaignPlannerAgent",
            CreatedAtUtc: now,
            ExpiresAtUtc: null,
            Tags: new List<string> { "ramadan", "discount", "roas", "campaign" },
            Metadata: new Dictionary<string, string> { ["maxDiscount"] = "0.25", ["targetRoas"] = "3.8" }
        );
        tenantStore[campaignMemory.Id] = campaignMemory;

        // 4. Customer Context
        var customerContext = new AgentMemoryEntry(
            Id: $"seed_cust_{tenantId.GetHashCode():x8}",
            TenantId: tenantId,
            Category: AgentMemoryCategory.CustomerContext,
            Key: "VIP Customer Engagement SLA",
            Value: "VIP tier requires dedicated WhatsApp support",
            Confidence: MemoryConfidenceLevel.Verified,
            SourceAgent: "CrmConciergeAgent",
            CreatedAtUtc: now,
            ExpiresAtUtc: null,
            Tags: new List<string> { "vip", "whatsapp", "sla", "support" },
            Metadata: new Dictionary<string, string> { ["slaHours"] = "1", ["channel"] = "WhatsApp" }
        );
        tenantStore[customerContext.Id] = customerContext;
    }

    private static List<string> Tokenize(string text)
    {
        return text
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double CalculateRelevanceScore(AgentMemoryEntry entry, string queryLower, List<string> queryTokens, DateTime now)
    {
        double score = 0.0;
        int matches = 0;

        var keyLower = entry.Key.ToLowerInvariant();
        var valLower = entry.Value.ToLowerInvariant();
        var catLower = entry.Category.ToString().ToLowerInvariant();
        var agentLower = entry.SourceAgent.ToLowerInvariant();

        // Exact query substring bonus
        if (keyLower.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
        {
            score += 3.5;
            matches++;
        }

        if (valLower.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
        {
            score += 2.5;
            matches++;
        }

        // Tokenized matches
        foreach (var token in queryTokens)
        {
            bool tokenHit = false;

            if (keyLower.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.8;
                tokenHit = true;
            }

            if (valLower.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.2;
                tokenHit = true;
            }

            if (entry.Tags.Any(t => t.Equals(token, StringComparison.OrdinalIgnoreCase)))
            {
                score += 1.5;
                tokenHit = true;
            }
            else if (entry.Tags.Any(t => t.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                score += 0.8;
                tokenHit = true;
            }

            if (catLower.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.6;
                tokenHit = true;
            }

            if (agentLower.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.4;
                tokenHit = true;
            }

            if (tokenHit)
            {
                matches++;
            }
        }

        if (matches == 0)
        {
            return 0.0;
        }

        // Confidence boost
        score += GetConfidenceWeight(entry.Confidence);

        // Recency boost (within 7 days)
        if ((now - entry.CreatedAtUtc).TotalDays <= 7)
        {
            score += 0.1;
        }

        return score;
    }

    private static double GetConfidenceWeight(MemoryConfidenceLevel confidence)
    {
        return confidence switch
        {
            MemoryConfidenceLevel.Verified => 0.4,
            MemoryConfidenceLevel.High => 0.3,
            MemoryConfidenceLevel.Medium => 0.2,
            MemoryConfidenceLevel.Low => 0.1,
            _ => 0.0
        };
    }
}
