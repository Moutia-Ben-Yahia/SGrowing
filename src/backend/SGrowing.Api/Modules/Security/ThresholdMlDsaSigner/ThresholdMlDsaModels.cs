namespace SGrowing.Api.Modules.Security.ThresholdMlDsaSigner;

public record ParticipantRound1Commitment(
    int ParticipantId,
    string HighBitsVectorW1Hex
);

public record ParticipantRound2Response(
    int ParticipantId,
    string ResponseVectorZHex,
    bool PassedRejectionSampling
);

public record ThresholdMlDsaSigningSessionRequest(
    string Message,
    int ThresholdRequired, // e.g. t = 3 of n = 5
    IReadOnlyList<ParticipantRound1Commitment> Round1Commitments,
    IReadOnlyList<ParticipantRound2Response> Round2Responses,
    string MasterPublicKeyHex
);

public record ThresholdMlDsaSignatureResult(
    string SessionId,
    bool SignatureValid,
    int ParticipantsContributed,
    string AggregatedSignatureTokenHex,
    string QuantumSecurityLevel, // "ML_DSA_65_NIST_LEVEL_3", "ML_DSA_87_NIST_LEVEL_5"
    string VerificationStatus
);
