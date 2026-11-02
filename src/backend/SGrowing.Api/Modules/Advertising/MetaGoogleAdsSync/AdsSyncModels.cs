namespace SGrowing.Api.Modules.Advertising.MetaGoogleAdsSync;

public record ConversionEvent(
    string EventId,
    string EventName,
    DateTime EventTimeUtc,
    string Platform,
    string? Email = null,
    string? PhoneNumber = null,
    string? ClientIpAddress = null,
    string? UserAgent = null,
    string? Fbp = null,
    string? Fbc = null,
    string? Gclid = null,
    decimal ConversionValue = 0m,
    string Currency = "USD",
    string? OrderId = null,
    Dictionary<string, string>? CustomParameters = null
);

public record CapiUploadRequest(
    string TenantId,
    string AdAccountId,
    string Platform,
    List<ConversionEvent> Events,
    bool TestMode = false
);

public record CapiUploadResult(
    bool Success,
    string Platform,
    int ProcessedEventsCount,
    int AcceptedEventsCount,
    int DeduplicatedEventsCount,
    List<string> GeneratedPayloadHashes,
    string StatusMessage,
    DateTime DispatchedAtUtc
);

public record AudienceHashSyncRequest(
    string TenantId,
    string Platform,
    string AudienceId,
    List<string> RawEmails,
    List<string> RawPhoneNumbers
);

public record AudienceHashSyncResult(
    string AudienceId,
    string Platform,
    int TotalRecords,
    int ValidHashesGenerated,
    List<string> Sha256Hashes,
    DateTime SyncedAtUtc
);

public record CampaignPerformanceRecord(
    string CampaignId,
    string CampaignName,
    string Platform,
    decimal Spend,
    decimal Revenue,
    int Conversions,
    int Impressions,
    int Clicks
);

public record RoasOptimizationRequest(
    string TenantId,
    decimal TotalBudget,
    decimal TargetRoas,
    List<CampaignPerformanceRecord> Campaigns,
    decimal MaxShiftPercentage = 0.25m
);

public record CampaignBudgetRecommendation(
    string CampaignId,
    string CampaignName,
    string Platform,
    decimal CurrentSpend,
    decimal RecommendedSpend,
    decimal CurrentRoas,
    decimal ProjectedRoas,
    string AllocationAction
);

public record RoasOptimizationResult(
    string TenantId,
    decimal CurrentTotalSpend,
    decimal RecommendedTotalSpend,
    decimal BlendedCurrentRoas,
    decimal ProjectedBlendedRoas,
    List<CampaignBudgetRecommendation> Recommendations,
    DateTime EvaluatedAtUtc
);
