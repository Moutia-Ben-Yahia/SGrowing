using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Security.SecretVaultMesh;

public enum SecretType
{
    DatabaseMasterCredentials,
    JwtSigningKey,
    TlsPrivateKey,
    StripeApiKey
}

public enum RotationPhase
{
    ActivePrimary,
    DeprecatedSecondary,
    ZeroizedPurged
}

public record VaultSecretEntry(
    string SecretId,
    string Name,
    SecretType Type,
    int Version,
    RotationPhase Phase,
    DateTime LastRotatedAtUtc,
    int AgeInDays
);

public record RotateSecretRequest(
    string SecretId
);

public record ZeroizeSecretRequest(
    string SecretId
);

public record ZeroizationResult(
    bool Success,
    string SecretId,
    string Message
);
