using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.SecretRotationMesh;

public sealed record SecretRotationOverview(
    int TotalManagedSecretsCount,
    int EphemeralCertsActiveCount,
    int RotationsCompletedLast30Days,
    double ZeroDowntimeAttestationPct,
    IReadOnlyList<ManagedSecretProfile> ManagedSecrets,
    IReadOnlyList<EphemeralCertRecord> ActiveEphemeralCerts
);

public sealed record ManagedSecretProfile(
    string SecretId,
    string SecretName,
    string TargetServiceType, // PostgreSQL Replica, Stripe Webhook Key, Redis Cluster, AWS IAM Role
    int RotationCadenceDays,
    DateTime LastRotatedAt,
    DateTime NextScheduledRotation,
    string MultiCloudHsmSyncStatus
);

public sealed record EphemeralCertRecord(
    string CertSerialHex,
    string SubjectDistinguishedName,
    string TargetEdgeNode,
    int ValidDurationMinutes,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    string Status
);

public sealed record IssueEphemeralCertRequest(
    string SubjectDistinguishedName,
    string TargetEdgeNode,
    int ValidDurationMinutes,
    string SovereignCaKeyId
);

public sealed record EphemeralCertResult(
    string CertSerialHex,
    string SubjectDistinguishedName,
    string X509CertPem,
    DateTime ExpiresAt,
    string SovereignSignatureProof
);

public sealed record RotateSecretRequest(
    string SecretId,
    bool ForceDualCredentialOverlap,
    int OverlapGracePeriodMinutes
);

public sealed record SecretRotationReceipt(
    string RotationExecutionId,
    string SecretId,
    string VersionActive,
    string VersionDeprecated,
    string Status,
    string HsmVerificationDigest,
    DateTime Timestamp
);
