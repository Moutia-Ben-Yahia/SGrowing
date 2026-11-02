using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.AudienceSync;

public enum DspPlatform
{
    GoogleCustomerMatch,
    MetaCustomAudience,
    TikTokAudienceManager,
    LinkedInMatchedAudience
}

public enum SyncAction
{
    Add,
    Remove
}

public record AudienceSyncRecord(
    string RawIdentifier, // Email or Phone
    string CohortName,
    SyncAction Action
);

public record BatchAudienceSyncRequest(
    DspPlatform Platform,
    string ExternalAudienceId,
    List<AudienceSyncRecord> Records
);

public record BatchAudienceSyncResult(
    string SyncJobId,
    DspPlatform Platform,
    string ExternalAudienceId,
    int ProcessedCount,
    int HashedCount,
    double SyncLatencyMs,
    bool Success,
    string Status,
    DateTime SyncedAtUtc
);

public record DspConnectorStatus(
    DspPlatform Platform,
    string ConnectionName,
    bool IsConnected,
    int TotalSyncedAudiences,
    int ActiveHashedRecords,
    double AverageLatencyMs,
    DateTime LastSyncUtc
);

public record RetryFailedSyncRequest(
    string SyncJobId
);
