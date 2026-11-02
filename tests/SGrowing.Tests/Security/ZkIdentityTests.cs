using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.ZkIdentityVerification;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class ZkIdentityTests
{
    private readonly ZkSelectiveAttributeDisclosureEngine _engine = new();

    [Fact]
    public void GenerateAndVerifyZkRangeProof_AgeAtLeast21_ProvesTrueWithoutRevealingActualAge()
    {
        // Arrange: Subject is 29 years old. Proves Age >= 21 without revealing 29.
        long actualAge = 29;
        long requiredAgeThreshold = 21;

        var (_, blindingFactor) = _engine.CommitAttribute(actualAge);

        // Act: Prover generates non-interactive zero-knowledge range proof
        var proof = _engine.GenerateZkRangeProof(
            actualValue: actualAge,
            blindingFactorR: blindingFactor,
            thresholdValue: requiredAgeThreshold,
            claimType: AttributeClaimType.AgeAtLeast21
        );

        // Verifier checks proof without learning actualAge (29)
        var verification = _engine.VerifyZkRangeProof(proof);

        // Assert
        proof.Should().NotBeNull();
        proof.ThresholdValue.Should().Be(21);
        proof.CommitmentHex.Should().NotBeEmpty();

        verification.IsProofMathematicallyValid.Should().BeTrue();
        verification.AuditSummary.Should().Contain("ZERO-KNOWLEDGE ATTESTATION VERIFIED");
    }

    [Fact]
    public void GenerateZkRangeProof_ActualValueBelowThreshold_ThrowsInvalidOperationException()
    {
        // Arrange: Underage user (age 17) attempting to prove Age >= 21
        long actualAge = 17;
        var (_, blindingFactor) = _engine.CommitAttribute(actualAge);

        // Act & Assert
        var act = () => _engine.GenerateZkRangeProof(actualAge, blindingFactor, 21, AttributeClaimType.AgeAtLeast21);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not satisfy threshold*");
    }

    [Fact]
    public void VerifyZkRangeProof_TamperedThreshold_ReturnsFalse()
    {
        // Arrange: Valid proof for Age >= 18
        var (c, r) = _engine.CommitAttribute(20);
        var proof = _engine.GenerateZkRangeProof(20, r, 18, AttributeClaimType.AgeAtLeast18);

        // Attacker tampers with proof to claim threshold 25 instead of 18
        var tamperedProof = proof with { ThresholdValue = 25 };

        // Act
        var result = _engine.VerifyZkRangeProof(tamperedProof);

        // Assert
        result.IsProofMathematicallyValid.Should().BeFalse();
        result.AuditSummary.Should().Contain("VERIFICATION FAILED");
    }

    [Fact]
    public void AddZkIdentityVerificationServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddZkIdentityVerificationServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(ZkSelectiveAttributeDisclosureEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
