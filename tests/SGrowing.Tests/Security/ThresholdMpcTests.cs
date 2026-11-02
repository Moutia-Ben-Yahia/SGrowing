using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.MpcThresholdSigner;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class ThresholdMpcTests
{
    private readonly ThresholdEcdsaMpcEngine _engine = new();

    [Fact]
    public void GenerateThresholdCluster_3of5_AllowsAny3SharesToSignSuccessfully()
    {
        // Arrange: 3-of-5 Threshold Cluster
        var cluster = _engine.GenerateThresholdCluster(thresholdT: 3, totalN: 5);
        cluster.Shares.Should().HaveCount(5);

        var message = "TRANSFER_100_000_000_USDC_TO_ENTERPRISE_TREASURY_ESCROW";

        // Act: Sign using participant shares {1, 2, 3}
        var subset123 = new[] { cluster.Shares[0], cluster.Shares[1], cluster.Shares[2] };
        var signature123 = _engine.SignMessage(cluster.MasterPublicKeyHex, message, subset123);

        // Assert
        signature123.Should().NotBeNull();
        signature123.IsVerified.Should().BeTrue();
        _engine.VerifySignature(signature123, message).Should().BeTrue();
    }

    [Fact]
    public void SignMessage_DifferentSubsetOf3Signers_BothProduceValidVerifiableSignatures()
    {
        // Arrange: 3-of-5 Threshold Cluster
        var cluster = _engine.GenerateThresholdCluster(thresholdT: 3, totalN: 5);
        var message = "ROOT_AUTHORIZATION_DEPLOY_QUANTUM_DEFENSE_SATELLITE";

        // Sign with subset A {1, 3, 5}
        var subsetA = new[] { cluster.Shares[0], cluster.Shares[2], cluster.Shares[4] };
        var sigA = _engine.SignMessage(cluster.MasterPublicKeyHex, message, subsetA);

        // Sign with subset B {2, 3, 4}
        var subsetB = new[] { cluster.Shares[1], cluster.Shares[2], cluster.Shares[3] };
        var sigB = _engine.SignMessage(cluster.MasterPublicKeyHex, message, subsetB);

        // Assert: Both disjoint subsets produce mathematically valid signatures against the same master public key
        sigA.IsVerified.Should().BeTrue();
        sigB.IsVerified.Should().BeTrue();

        _engine.VerifySignature(sigA, message).Should().BeTrue();
        _engine.VerifySignature(sigB, message).Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_AlteredMessage_ReturnsFalse()
    {
        // Arrange
        var cluster = _engine.GenerateThresholdCluster(3, 5);
        var originalMessage = "AUTHENTIC_TREASURY_DISPATCH_ORDER";
        var tamperedMessage = "TAMPERED_TREASURY_DISPATCH_ORDER_MALICIOUS";

        var sig = _engine.SignMessage(cluster.MasterPublicKeyHex, originalMessage, [cluster.Shares[0], cluster.Shares[1], cluster.Shares[2]]);

        // Act & Assert
        _engine.VerifySignature(sig, originalMessage).Should().BeTrue();
        _engine.VerifySignature(sig, tamperedMessage).Should().BeFalse();
    }

    [Fact]
    public void AddMpcThresholdSignerServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMpcThresholdSignerServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(ThresholdEcdsaMpcEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
