namespace SGrowing.Api.Modules.Advertising.CrossDeviceSync;

public enum IdentifierType
{
    HashedEmailSha256,
    HashedPhoneSha256,
    MobileAdIdGaid,
    MobileAdIdIdfa,
    Uid2Token,
    IpSubnetFingerprint
}

public record DeviceIdentityNode(
    string IdentifierValue,
    IdentifierType Type,
    DateTime FirstSeenUtc,
    DateTime LastActiveUtc,
    int ConfidenceScore
);

public record IdentityCluster(
    string ClusterId,
    string PrimaryHouseholdId,
    List<DeviceIdentityNode> Nodes,
    int TotalLinkedDevices,
    decimal OverallGraphConfidence,
    DateTime UpdatedAtUtc
);

public record ResolveIdentityRequest(
    string PrimaryIdentifier,
    IdentifierType PrimaryType,
    List<string> AssociatedIdentifiers,
    string? HouseholdHint
);

public record OfflineConversionReceipt(
    string TransactionId,
    string StoreOrPosId,
    string CustomerHashedEmail,
    string? CustomerHashedPhone,
    decimal PurchaseAmount,
    string Currency,
    List<string> PurchasedSkuCodes,
    DateTime TransactionTimestampUtc
);

public record MatchedConversionEvent(
    string MatchId,
    string TransactionId,
    string ClusterId,
    string AttributedCampaignId,
    string AttributedChannel,
    int LookbackWindowDays,
    decimal AttributionWeight,
    bool ForwardedToMetaCapi,
    bool ForwardedToGoogleOci,
    bool ForwardedToTikTokEventsApi,
    DateTime MatchedAtUtc
);

public record SyncMatchMetrics(
    int TotalReceiptsIngested,
    int TotalMatchedConversions,
    decimal MatchRatePercentage,
    decimal TotalAttributedGrossRevenue,
    int ActiveIdentityClusters,
    DateTime LastSyncUtc
);
