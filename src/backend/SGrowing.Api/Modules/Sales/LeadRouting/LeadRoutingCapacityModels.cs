namespace SGrowing.Api.Modules.Sales.LeadRouting;

public enum RoutingRuleType
{
    RoundRobin,
    WeightedQuota,
    SkillBased,
    TerritoryGeo
}

public enum SdrAvailabilityStatus
{
    Online,
    Busy,
    Away,
    Offline
}

public enum LeadSlaBreachStatus
{
    WithinSla,
    Warning,
    Breached,
    Reassigned
}

public record SdrRepProfile(
    Guid SdrId,
    Guid TenantId,
    string RepName,
    string RepEmail,
    SdrAvailabilityStatus Status,
    int MaxCapacity,
    int CurrentActiveLeads,
    decimal QuotaWeight,
    List<string> AssignedTerritories,
    List<string> Skills,
    DateTime LastAssignedAtUtc
);

public record LeadRoutingRule(
    Guid RuleId,
    Guid TenantId,
    string RuleName,
    RoutingRuleType RuleType,
    bool IsActive,
    int SlaTimeoutMinutes,
    string TargetTerritory,
    List<string> RequiredSkills
);

public record LeadAssignmentRecord(
    Guid AssignmentId,
    Guid TenantId,
    Guid LeadId,
    string LeadName,
    string LeadEmail,
    string CompanyName,
    string Territory,
    Guid SdrId,
    string SdrName,
    DateTime AssignedAtUtc,
    DateTime SlaDueAtUtc,
    LeadSlaBreachStatus SlaStatus,
    DateTime? FirstContactedAtUtc,
    Guid? ReassignedFromSdrId
);

public record RouteLeadRequest(
    Guid TenantId,
    Guid LeadId,
    string LeadName,
    string LeadEmail,
    string CompanyName,
    string Territory,
    List<string> RequiredSkills,
    int EstimatedDealSizeSar
);

public record UpdateSdrStatusRequest(
    Guid TenantId,
    Guid SdrId,
    SdrAvailabilityStatus NewStatus,
    int? NewMaxCapacity
);

public record LogContactAttemptRequest(
    Guid TenantId,
    Guid AssignmentId,
    string Channel,
    string Notes
);

public record LeadRoutingOverviewReport(
    Guid TenantId,
    int TotalSdrCount,
    int OnlineSdrCount,
    int TotalActiveRoutedLeads,
    int SlaBreachesToday,
    double AverageFirstContactMinutes,
    List<SdrRepProfile> SdrRoster,
    List<LeadAssignmentRecord> PendingAssignments
);
