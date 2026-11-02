namespace SGrowing.Api.Modules.Security.ZkIrisBiometrics;

public record IrisCodeCommitment(
    string SubjectDid,
    string TemplateCommitmentHex,
    int UnmaskedBitCount,
    string SaltHex
);

public record ZkIrisProofRequest(
    string SubjectDid,
    string VerifierChallengeNonce,
    string ProofOfMatchPiHex,
    double ClaimedHammingDistance,
    int BitsComparedCount,
    string EncryptedDifferenceBitstreamHex
);

public record ZkIrisVerificationResult(
    string SubjectDid,
    bool ProofValid,
    double FractionalHammingDistance,
    bool BiometricMatchConfirmed,
    string MatchConfidenceTier,
    string SecurityProofFingerprint,
    string VerificationStatus
);
