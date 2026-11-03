using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.Uid2Tokenizer;

public record Uid2Token(
    string AdvertisingToken,
    string RefreshToken,
    DateTime GeneratedAt,
    DateTime ExpiresAt,
    string IdentityHashNormalized,
    string SaltBucketId,
    string TokenStatus // "Active", "Refreshing", "Revoked"
);

public record FirstPartyIdentityNode(
    string IdentityGraphId,
    string CanonicalEmailSha256,
    string PhoneSha256,
    string FirstPartyCookieId,
    string MobileAdvertisingId, // IDFA / GAID
    int TotalStitchedSessionsCount,
    DateTime LastSeenAt,
    bool Uid2TokenActive
);

public record Uid2TokenizerOverview(
    int TotalStitchedIdentities,
    int ActiveAdvertisingTokensCount,
    double TokenRefreshComplianceRatePct,
    int DailyStitchedTouchpoints,
    IReadOnlyList<FirstPartyIdentityNode> Identities,
    IReadOnlyList<Uid2Token> RecentTokens
);

public record GenerateTokenCommand(
    string RawEmail,
    string RawPhone,
    string SaltBucketKey
);

public record GenerateTokenResult(
    string AdvertisingToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string IdentityHash,
    bool IsEuidSupported
);

public record StitchIdentityCommand(
    string AnonymousCookieId,
    string VerifiedEmail,
    string MobileAdId
);

public record StitchIdentityResult(
    string IdentityGraphId,
    string UnifiedProfileId,
    int MergedSessionCount,
    string ActiveUid2Token,
    string StitchReceipt
);
