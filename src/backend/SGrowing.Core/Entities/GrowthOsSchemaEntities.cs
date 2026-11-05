namespace SGrowing.Core.Entities;

// ============================================================================
// Schema: customer_data (Domain B3: Customer Data / CDP)
// ============================================================================

public class CustomerIdentityEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string CanonicalProfileId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CustomerProfileEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CanonicalProfileId { get; set; } = string.Empty;
    public string UnifiedTraitsJson { get; set; } = "{}";
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;
}

public class IdentityLinkEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SourceIdentifierJson { get; set; } = "{}";
    public string TargetIdentifierJson { get; set; } = "{}";
    public double ConfidenceScore { get; set; } = 1.0;
    public string MatchReason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class VisitorSessionEntity
{
    public string SessionId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string AnonymousId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public string PageViewsJson { get; set; } = "[]";
    public string UtmParametersJson { get; set; } = "{}";
}

// ============================================================================
// Schema: advertising (Domain C6: Advertising & CAPI Sync)
// ============================================================================

public class CapiConversionEntity
{
    public string EventId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public decimal ConversionValue { get; set; }
    public string Currency { get; set; } = "USD";
    public string PayloadHash { get; set; } = string.Empty;
    public DateTime DispatchedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDeduplicated { get; set; }
}

public class AudienceSyncEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AudienceId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public int ValidHashesCount { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CampaignRoasSnapshotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CampaignId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public decimal Spend { get; set; }
    public decimal Revenue { get; set; }
    public decimal CurrentRoas { get; set; }
    public decimal RecommendedSpend { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

// ============================================================================
// Schema: automation (Domain E1: Workflow DAG & Telemetry)
// ============================================================================

public class WorkflowDagEntity
{
    public string WorkflowId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerNodeId { get; set; } = string.Empty;
    public string NodesJson { get; set; } = "[]";
    public string EdgesJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class WorkflowTelemetryTraceEntity
{
    public string ExecutionId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string WorkflowId { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalDurationMs { get; set; }
    public string StepsJson { get; set; } = "[]";
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}
