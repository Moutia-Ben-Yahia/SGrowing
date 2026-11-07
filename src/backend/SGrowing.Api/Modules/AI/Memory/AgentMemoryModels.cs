using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.AI.Memory;

/// <summary>
/// Defines categories for multi-tier agent memory engine (Domain J1 & PRD 6.5).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentMemoryCategory
{
    BrandMemory,
    BusinessMemory,
    CampaignMemory,
    CustomerContext,
    OperationalFact
}

/// <summary>
/// Represents confidence and verification grading for organizational facts stored by agents.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemoryConfidenceLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Verified = 4
}

/// <summary>
/// An immutable entry stored in the multi-tier agent memory store.
/// </summary>
public record AgentMemoryEntry(
    string Id,
    string TenantId,
    AgentMemoryCategory Category,
    string Key,
    string Value,
    MemoryConfidenceLevel Confidence,
    string SourceAgent,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    List<string> Tags,
    Dictionary<string, string> Metadata
);

/// <summary>
/// Request payload to persist a new memory fact or organizational rule into the agent memory store.
/// </summary>
public record StoreMemoryRequest(
    string TenantId,
    AgentMemoryCategory Category,
    string Key,
    string Value,
    MemoryConfidenceLevel Confidence,
    string SourceAgent,
    int? TtlHours = null,
    List<string>? Tags = null,
    Dictionary<string, string>? Metadata = null
);

/// <summary>
/// Request payload to query agent memories with keyword scoring, category filtering, and confidence thresholds.
/// </summary>
public record QueryMemoryRequest(
    string TenantId,
    string Query,
    List<AgentMemoryCategory>? Categories = null,
    MemoryConfidenceLevel? MinConfidence = null,
    int Limit = 10
);

/// <summary>
/// A query search result containing the matched memory entry and its calculated relevance score.
/// </summary>
public record MemoryQueryResult(
    AgentMemoryEntry Entry,
    double RelevanceScore
);
