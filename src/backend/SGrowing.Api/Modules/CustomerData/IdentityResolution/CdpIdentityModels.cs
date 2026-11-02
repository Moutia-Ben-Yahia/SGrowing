namespace SGrowing.Api.Modules.CustomerData.IdentityResolution;

public enum IdentityType
{
    AnonymousCookie,
    DeviceId,
    Email,
    PhoneNumber,
    CustomerId,
    CrmContactId
}

public record IdentityIdentifier(
    IdentityType Type,
    string Value
);

public record IdentityLink(
    IdentityIdentifier SourceIdentifier,
    IdentityIdentifier TargetIdentifier,
    double ConfidenceScore,
    string MatchReason,
    DateTime LinkedAtUtc
);

public record VisitorSessionRecord(
    string SessionId,
    string AnonymousId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string? IpAddress,
    string? UserAgent,
    List<string> PageViews,
    Dictionary<string, string> UtmParameters
);

public record StitchIdentityRequest(
    string TenantId,
    string AnonymousId,
    IdentityIdentifier KnownIdentifier,
    string? SessionId = null,
    Dictionary<string, string>? AdditionalTraits = null
);

public record StitchIdentityResult(
    bool Success,
    string CanonicalProfileId,
    bool WasMerged,
    int LinkedIdentifiersCount,
    double OverallConfidence,
    List<string> MergedSessionIds,
    string AuditMessage,
    DateTime ProcessedAtUtc
);

public record ResolvedCustomerProfile(
    string CanonicalProfileId,
    string TenantId,
    List<IdentityIdentifier> Identifiers,
    List<IdentityLink> Links,
    List<string> AssociatedSessionIds,
    Dictionary<string, string> UnifiedTraits,
    DateTime FirstSeenUtc,
    DateTime LastActiveUtc
);

public record IdentityResolutionQuery(
    string TenantId,
    IdentityIdentifier Identifier
);
