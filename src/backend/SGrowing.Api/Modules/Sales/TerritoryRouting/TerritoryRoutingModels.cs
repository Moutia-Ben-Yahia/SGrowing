namespace SGrowing.Api.Modules.Sales.TerritoryRouting;

public enum AccountTier
{
    Tier1_Strategic,
    Tier2_Target,
    Tier3_Volume
}

public record TargetAccount(
    string AccountId,
    string CompanyName,
    string Domain,
    string Industry,
    string Country,
    AccountTier Tier,
    double IntentSurgeScore,
    string? AssignedRepId,
    string? AssignedTerritory,
    DateTime LastSurgeAtUtc
);

public record SalesRepCapacity(
    string RepId,
    string RepName,
    string Territory,
    int MaxConcurrentAccounts,
    int ActiveAccounts,
    double QuotaAttainmentPercent
);

public record TerritoryRule(
    string RuleId,
    string TerritoryName,
    List<string> TargetIndustries,
    List<string> Countries,
    AccountTier MinTier
);

public record RoutingAssignmentResult(
    string AccountId,
    string CompanyName,
    string AssignedRepId,
    string AssignedRepName,
    string Territory,
    string RoutingReason,
    DateTime AssignedAtUtc
);

public record IngestSurgeEventRequest(
    string Domain,
    string SignalSource,
    double IntentWeight,
    string TopicKey
);

public record AssignAccountRequest(
    string AccountId,
    bool ForceRebalance = false
);
