namespace SGrowing.Tests.Security;

using FluentAssertions;
using SGrowing.Api.Modules.Security.RingSignatureIdentity;
using Xunit;

public class RingSignatureIdentityTests
{
    private readonly RingSignatureIdentityEngine _engine = new();

    [Fact]
    public void VerifyRingSignature_ValidClosedRing_AttestsAnonymousMembership()
    {
        string message = "Whistleblower Attestation: Compliance Violation in Procurement";
        var ringKeys = new List<string>
        {
            "pk_ed25519_01abcdef0123456789abcdef0123456789",
            "pk_ed25519_02bcdef0123456789abcdef0123456789a",
            "pk_ed25519_03cdef0123456789abcdef0123456789ab",
            "pk_ed25519_04def0123456789abcdef0123456789abc"
        };
        string keyImage = "ki_99887766554433221100aabbccddeeff";

        var token = _engine.GenerateValidSyntheticRingSignature(message, ringKeys, keyImage);
        var request = new RingVerificationRequest(message, token);

        var result = _engine.VerifyRingSignature(request);

        result.IsSignatureValid.Should().BeTrue();
        result.IsDoubleSpent.Should().BeFalse();
        result.RingSize.Should().Be(4);
        result.AnonymitySetTier.Should().Be("MINIMAL_UNDER_8");
        result.VerificationStatus.Should().Contain("VALID_ANONYMOUS_SIGNATURE");
    }

    [Fact]
    public void VerifyRingSignature_DoubleSpentKeyImage_RejectsSignature()
    {
        string message = "Vote: Proposal #42";
        var ringKeys = new List<string>
        {
            "pk_01", "pk_02", "pk_03", "pk_04", "pk_05", "pk_06", "pk_07", "pk_08"
        };
        string spentKeyImage = "ki_ALREADY_SPENT_IN_LEDGER";

        var token = _engine.GenerateValidSyntheticRingSignature(message, ringKeys, spentKeyImage);
        var request = new RingVerificationRequest(message, token, KnownSpentKeyImages: [spentKeyImage]);

        var result = _engine.VerifyRingSignature(request);

        result.IsSignatureValid.Should().BeFalse();
        result.IsDoubleSpent.Should().BeTrue();
        result.AnonymitySetTier.Should().Be("ROBUST_8_TO_15");
        result.VerificationStatus.Should().Contain("Key image has already been spent");
    }

    [Fact]
    public void VerifyRingSignature_ForgedChallengeScalar_RejectsMathematicalClosure()
    {
        string message = "Sovereign Audit Document";
        var ringKeys = new List<string> { "pk_1", "pk_2", "pk_3" };
        string keyImage = "ki_genuine";

        var validToken = _engine.GenerateValidSyntheticRingSignature(message, ringKeys, keyImage);
        // Tamper with initial challenge c1
        var forgedToken = validToken with { InitialChallengeC1Hex = "00112233445566778899aabbccddeeff" };

        var request = new RingVerificationRequest(message, forgedToken);
        var result = _engine.VerifyRingSignature(request);

        result.IsSignatureValid.Should().BeFalse();
        result.VerificationStatus.Should().Contain("Ring mathematical closure validation failed");
    }

    [Fact]
    public void VerifyRingSignature_SmallRingUnderThree_RejectsInsufficientAnonymity()
    {
        string message = "Test Message";
        var ringKeys = new List<string> { "pk_1", "pk_2" }; // Only 2 participants (insufficient ring anonymity)
        string keyImage = "ki_test";

        var token = new RingSignatureToken("digest", keyImage, ringKeys, ["r1", "r2"], "c1");
        var request = new RingVerificationRequest(message, token);

        var result = _engine.VerifyRingSignature(request);

        result.IsSignatureValid.Should().BeFalse();
        result.VerificationStatus.Should().Contain("at least 3 participants");
    }
}
