namespace SGrowing.Api.Modules.Analytics.DataCleanRoom;

public record CleanRoomParticipant(
    string ParticipantId,
    string OrganizationName,
    string Role,
    string CryptographicSaltId,
    string Status
);

public record AudienceCohortDataset(
    string DatasetId,
    string ParticipantId,
    string CohortName,
    int HashedIdentityCount,
    string EncryptionScheme,
    DateTime CreatedAtUtc
);

public record RunOverlapQueryRequest(
    string AdvertiserDatasetId,
    string PublisherDatasetId,
    double DifferentialPrivacyEpsilon = 0.5
);

public record CleanRoomOverlapResult(
    string QueryId,
    string AdvertiserDatasetId,
    string PublisherDatasetId,
    int RawOverlapMatchCount,
    int PrivacyNoisedOverlapCount,
    double MatchRatePercent,
    decimal AttributedRevenue,
    double PrivacyPreservingRoas,
    double EpsilonCost,
    DateTime ExecutedAtUtc
);

public record CleanRoomSummary(
    int TotalParticipants,
    int TotalCohortsSecured,
    int TotalHashedIdentities,
    int TotalQueriesExecuted,
    double TotalEpsilonConsumed
);
