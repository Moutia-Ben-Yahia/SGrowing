namespace SGrowing.Tests.Security;

using FluentAssertions;
using SGrowing.Api.Modules.Security.ZkIrisBiometrics;
using Xunit;

public class ZkIrisBiometricTests
{
    private readonly ZkIrisBiometricEngine _engine = new();

    [Fact]
    public void VerifyProof_ValidProofUnderThreshold_ConfirmsBiometricMatch()
    {
        string subjectDid = "did:ion:EiD3IrisBiometricSubject99281";
        string nonce = "NONCE-CHALLENGE-98124";
        double hammingDistance = 0.18; // Close match
        string bitstream = "0a1f4b8c9d0e1f2a";

        string proof = _engine.GenerateProofSignature(subjectDid, nonce, hammingDistance, bitstream);

        var request = new ZkIrisProofRequest(
            SubjectDid: subjectDid,
            VerifierChallengeNonce: nonce,
            ProofOfMatchPiHex: proof,
            ClaimedHammingDistance: hammingDistance,
            BitsComparedCount: 1840,
            EncryptedDifferenceBitstreamHex: bitstream
        );

        var result = _engine.VerifyProof(request);

        result.ProofValid.Should().BeTrue();
        result.BiometricMatchConfirmed.Should().BeTrue();
        result.MatchConfidenceTier.Should().Be("EXCEPTIONAL_MATCH");
        result.FractionalHammingDistance.Should().Be(0.18);
        result.VerificationStatus.Should().Contain("VERIFIED: Iris code zero-knowledge match");
    }

    [Fact]
    public void VerifyProof_TamperedProofSignature_RejectsAttestation()
    {
        string subjectDid = "did:ion:EiD3IrisBiometricSubject99281";
        string nonce = "NONCE-CHALLENGE-98124";

        var request = new ZkIrisProofRequest(
            SubjectDid: subjectDid,
            VerifierChallengeNonce: nonce,
            ProofOfMatchPiHex: "tampered_bad_proof_signature_hex_123456",
            ClaimedHammingDistance: 0.15,
            BitsComparedCount: 1500,
            EncryptedDifferenceBitstreamHex: "0a1f4b8c"
        );

        var result = _engine.VerifyProof(request);

        result.ProofValid.Should().BeFalse();
        result.BiometricMatchConfirmed.Should().BeFalse();
        result.MatchConfidenceTier.Should().Be("CRYPTOGRAPHIC_PROOF_TAMPERED");
        result.VerificationStatus.Should().Contain("Zero-knowledge polynomial proof failed attestation");
    }

    [Fact]
    public void VerifyProof_InsufficientEntropyBits_RejectsProof()
    {
        string subjectDid = "did:ion:EiD3IrisBiometricSubject99281";
        string nonce = "NONCE-CHALLENGE-98124";

        var request = new ZkIrisProofRequest(
            SubjectDid: subjectDid,
            VerifierChallengeNonce: nonce,
            ProofOfMatchPiHex: "proof",
            ClaimedHammingDistance: 0.15,
            BitsComparedCount: 450, // Less than 1000 minimum bits
            EncryptedDifferenceBitstreamHex: "0a1f"
        );

        var result = _engine.VerifyProof(request);

        result.ProofValid.Should().BeFalse();
        result.MatchConfidenceTier.Should().Be("INSUFFICIENT_ENTROPY");
        result.VerificationStatus.Should().Contain("below required entropy floor");
    }

    [Fact]
    public void VerifyProof_DifferentIndividualAboveThreshold_AttestsNonMatch()
    {
        string subjectDid = "did:ion:EiD3IrisBiometricSubject99281";
        string nonce = "NONCE-CHALLENGE-98124";
        double hammingDistance = 0.49; // Two different individuals
        string bitstream = "ffeeddccbbaa";

        string proof = _engine.GenerateProofSignature(subjectDid, nonce, hammingDistance, bitstream);

        var request = new ZkIrisProofRequest(
            SubjectDid: subjectDid,
            VerifierChallengeNonce: nonce,
            ProofOfMatchPiHex: proof,
            ClaimedHammingDistance: hammingDistance,
            BitsComparedCount: 1600,
            EncryptedDifferenceBitstreamHex: bitstream
        );

        var result = _engine.VerifyProof(request);

        result.ProofValid.Should().BeTrue();
        result.BiometricMatchConfirmed.Should().BeFalse();
        result.MatchConfidenceTier.Should().Be("REJECTED_DIFFERENT_INDIVIDUAL");
        result.VerificationStatus.Should().Contain("NON_MATCH");
    }
}
