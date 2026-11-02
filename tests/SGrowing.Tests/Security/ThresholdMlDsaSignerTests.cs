namespace SGrowing.Tests.Security;

using FluentAssertions;
using SGrowing.Api.Modules.Security.ThresholdMlDsaSigner;
using Xunit;

public class ThresholdMlDsaSignerTests
{
    private readonly ThresholdMlDsaSignerEngine _engine = new();

    [Fact]
    public void AggregateSignature_QuorumReachedWithValidShares_ProducesValidSignature()
    {
        string message = "Sovereign Treasury Transfer Authorization: TX-99214";
        string masterPk = "pk_mldsa65_master_key_lattice_dilithium";

        // 3-of-5 threshold signing
        var r1 = new List<ParticipantRound1Commitment>
        {
            new(1, "w1_highbits_share_node1"),
            new(2, "w1_highbits_share_node2"),
            new(3, "w1_highbits_share_node3")
        };

        var r2 = new List<ParticipantRound2Response>
        {
            new(1, "z_response_vector_node1", PassedRejectionSampling: true),
            new(2, "z_response_vector_node2", PassedRejectionSampling: true),
            new(3, "z_response_vector_node3", PassedRejectionSampling: true)
        };

        var request = new ThresholdMlDsaSigningSessionRequest(message, ThresholdRequired: 3, r1, r2, masterPk);
        var result = _engine.AggregateAndVerifyThresholdSignature(request);

        result.SignatureValid.Should().BeTrue();
        result.ParticipantsContributed.Should().Be(3);
        result.AggregatedSignatureTokenHex.Should().NotBeEmpty();
        result.QuantumSecurityLevel.Should().Contain("FIPS 204");
        result.VerificationStatus.Should().Contain("VALID_THRESHOLD_SIGNATURE");
    }

    [Fact]
    public void AggregateSignature_InsufficientQuorumShares_RejectsAggregation()
    {
        string message = "Document Review";
        var r1 = new List<ParticipantRound1Commitment> { new(1, "w1_node1") };
        var r2 = new List<ParticipantRound2Response> { new(1, "z_node1", PassedRejectionSampling: true) };

        // Requires 3 shares, only 1 provided
        var request = new ThresholdMlDsaSigningSessionRequest(message, ThresholdRequired: 3, r1, r2, "master_pk");
        var result = _engine.AggregateAndVerifyThresholdSignature(request);

        result.SignatureValid.Should().BeFalse();
        result.ParticipantsContributed.Should().Be(1);
        result.VerificationStatus.Should().Contain("Insufficient threshold quorum");
    }

    [Fact]
    public void AggregateSignature_FailedRejectionSampling_ExcludesLeakingNode()
    {
        string message = "Payload Data";
        var r1 = new List<ParticipantRound1Commitment>
        {
            new(1, "w1_1"),
            new(2, "w1_2"),
            new(3, "w1_3")
        };

        // Node 2 failed rejection sampling (norm overflow would leak secret)
        var r2 = new List<ParticipantRound2Response>
        {
            new(1, "z_1", PassedRejectionSampling: true),
            new(2, "z_2", PassedRejectionSampling: false), // Failed rejection sampling
            new(3, "z_3", PassedRejectionSampling: true)
        };

        // Requires 3, but only 2 passed rejection sampling
        var request = new ThresholdMlDsaSigningSessionRequest(message, ThresholdRequired: 3, r1, r2, "pk");
        var result = _engine.AggregateAndVerifyThresholdSignature(request);

        result.SignatureValid.Should().BeFalse();
        result.ParticipantsContributed.Should().Be(2);
        result.VerificationStatus.Should().Contain("rejection sampling");
    }

    [Fact]
    public void AggregateSignature_DeterministicSignatureToken_BindsMessageAndMasterKey()
    {
        var r1 = new List<ParticipantRound1Commitment> { new(1, "w1"), new(2, "w2") };
        var r2 = new List<ParticipantRound2Response> { new(1, "z1", true), new(2, "z2", true) };

        var resA = _engine.AggregateAndVerifyThresholdSignature(new ThresholdMlDsaSigningSessionRequest("MSG_A", 2, r1, r2, "PK"));
        var resB = _engine.AggregateAndVerifyThresholdSignature(new ThresholdMlDsaSigningSessionRequest("MSG_B", 2, r1, r2, "PK"));

        resA.AggregatedSignatureTokenHex.Should().NotBe(resB.AggregatedSignatureTokenHex);
    }
}
