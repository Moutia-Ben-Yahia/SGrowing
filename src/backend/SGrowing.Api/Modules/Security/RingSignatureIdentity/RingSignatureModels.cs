namespace SGrowing.Api.Modules.Security.RingSignatureIdentity;

public record RingParticipantPublicKey(
    string ParticipantId,
    string PublicKeyHex
);

public record RingSignatureToken(
    string MessageDigestSha256,
    string KeyImageHex,
    IReadOnlyList<string> RingPublicKeys,
    IReadOnlyList<string> ResponseScalarsRHex,
    string InitialChallengeC1Hex
);

public record RingVerificationRequest(
    string Message,
    RingSignatureToken Signature,
    IReadOnlyList<string>? KnownSpentKeyImages = null
);

public record RingVerificationResult(
    bool IsSignatureValid,
    bool IsDoubleSpent,
    int RingSize,
    string AnonymitySetTier, // "EXCEPTIONAL_16_PLUS", "ROBUST_8_TO_15", "MINIMAL_UNDER_8"
    string KeyImageHex,
    string VerificationStatus
);
