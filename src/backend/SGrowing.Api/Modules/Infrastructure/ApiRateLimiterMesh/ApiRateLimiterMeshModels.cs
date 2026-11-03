using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.ApiRateLimiterMesh;

public sealed record ApiRateLimiterOverview(
    int TotalActiveBucketsCount,
    int QuarantinedIpCount,
    double CurrentTarpitDelayMs,
    long TotalRequestsEvaluated24h,
    IReadOnlyList<ClientBucketProfile> ActiveBuckets,
    IReadOnlyList<QuarantinedIpRecord> QuarantinedIps
);

public sealed record ClientBucketProfile(
    string ClientId,
    string Tier,
    int MaxCapacityTokens,
    int AvailableTokens,
    double RefillRateTokensPerSecond,
    DateTime LastRefillAt
);

public sealed record QuarantinedIpRecord(
    string IpAddress,
    string Reason,
    double EntropyScore,
    DateTime QuarantinedAt,
    DateTime ExpiresAt,
    string EdgeSyncStatus
);

public sealed record ConsumeTokenRequest(
    string ClientId,
    string RequestIp,
    string ApiPath,
    int TokensToConsume
);

public sealed record RateLimitVerdict(
    string DecisionId,
    string ClientId,
    string Verdict, // ALLOW, TARPIT_DELAY, REJECT_TOO_MANY_REQUESTS
    int RemainingTokens,
    int TarpitDelayMs,
    int RetryAfterSeconds,
    string DistributedNodeId
);

public sealed record QuarantineIpRequest(
    string IpAddress,
    string Reason,
    double EntropyScore,
    int QuarantineDurationMinutes
);

public sealed record QuarantineReceipt(
    string ReceiptId,
    string IpAddress,
    string Status,
    DateTime ExpiresAt,
    string MultiCloudSyncHash
);
