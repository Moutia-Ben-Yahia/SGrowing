using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.FeatureFlags;

public enum CanaryRing
{
    InternalAlpha = 0,
    Ring0EarlyAdopter = 10,
    Ring1Canary = 25,
    Ring2Staged = 50,
    PublicGeneralAvailability = 100
}

public record FeatureFlagDefinition(
    string FlagKey,
    string DisplayName,
    string Description,
    bool IsEnabled,
    int RolloutPercentage, // 0 - 100
    CanaryRing CurrentRing,
    List<string> WhitelistedTenantIds,
    List<string> BlacklistedTenantIds,
    double CanaryErrorRatePercent,
    bool AutoRollbackTriggered,
    DateTime UpdatedAtUtc
);

public record EvaluateFlagRequest(
    string FlagKey,
    string TenantId,
    Dictionary<string, string>? TenantAttributes = null
);

public record EvaluateFlagResult(
    string FlagKey,
    string TenantId,
    bool IsEnabled,
    string EvaluationReason, // "ExplicitWhitelist", "ExplicitBlacklist", "BucketPercentageMatch", "FlagDisabled", "CircuitBreakerRollback"
    int AssignedBucket,
    DateTime EvaluatedAtUtc
);

public record CanaryAdvanceRequest(
    string FlagKey,
    CanaryRing TargetRing
);

public record EmergencyKillRequest(
    string FlagKey,
    string Reason
);
