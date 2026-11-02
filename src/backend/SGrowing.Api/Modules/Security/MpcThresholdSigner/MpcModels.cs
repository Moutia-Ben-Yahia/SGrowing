using System.Numerics;

namespace SGrowing.Api.Modules.Security.MpcThresholdSigner;

public sealed record MpcKeyShare(
    int ShareIndex,
    string ShareValueHex,
    string ParticipantId
);

public sealed record MpcKeyCluster(
    string ClusterId,
    int ThresholdT,
    int TotalParticipantsN,
    string MasterPublicKeyHex,
    IReadOnlyList<MpcKeyShare> Shares,
    DateTime CreatedAtUtc
);

public sealed record PartialSignatureContribution(
    string ParticipantId,
    int ShareIndex,
    string PartialSignatureHex,
    string EphemeralPublicRHex
);

public sealed record ThresholdSignature(
    string SignatureId,
    string MessageHashHex,
    string EphemeralRHex,
    string AggregatedSHex,
    string MasterPublicKeyHex,
    IReadOnlyList<int> ParticipatingSignerIndices,
    bool IsVerified,
    DateTime SignedAtUtc
);

public sealed record GenerateClusterRequest(
    int ThresholdT,
    int TotalN
);

public sealed record SignThresholdMessageRequest(
    string MasterPublicKeyHex,
    string MessageText,
    IReadOnlyList<MpcKeyShare> SignerShares
);
