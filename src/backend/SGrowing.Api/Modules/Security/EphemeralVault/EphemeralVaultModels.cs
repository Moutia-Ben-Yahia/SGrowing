using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Security.EphemeralVault;

public enum AccessScope
{
    DbReadOnly,
    ProdDeploy,
    CustomerPiiAccess,
    ApiAdmin
}

public enum TokenStatus
{
    ActiveValid,
    RevokedEmergency,
    ExpiredTtl
}

public record EphemeralToken(
    string TokenId,
    string RequestedByUserId,
    AccessScope Scope,
    string Reason,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    TokenStatus Status,
    string SecretValuePrefix,
    string CryptographicSignature
);

public record RequestEphemeralTokenRequest(
    string RequestedByUserId,
    AccessScope Scope,
    string JustificationReason,
    int DurationMinutes
);

public record RevocationResult(
    bool Success,
    string TokenId,
    string RevokedBy,
    DateTime RevokedAtUtc,
    string Message
);
