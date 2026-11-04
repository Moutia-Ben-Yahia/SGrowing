using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Security.SecretVaultMesh;
using Xunit;

namespace SGrowing.Tests.Security;

public class SecretVaultMeshTests
{
    private readonly SecretVaultMeshEngine _engine = new();

    [Fact]
    public void GetSecrets_ReturnsInitialSeededSecrets()
    {
        var secrets = _engine.GetSecrets();

        secrets.Should().NotBeNull();
        secrets.Should().HaveCount(4);
        secrets.Should().Contain(s => s.SecretId == "sec_db_master_01" && s.Type == SecretType.DatabaseMasterCredentials);
        secrets.Should().Contain(s => s.SecretId == "sec_jwt_ed25519_02" && s.Type == SecretType.JwtSigningKey);
    }

    [Fact]
    public void RotateSecret_IncrementsVersionAndResetsAge()
    {
        var secretId = "sec_jwt_ed25519_02";
        var before = _engine.GetSecrets().Find(s => s.SecretId == secretId)!;

        var rotated = _engine.RotateSecret(new RotateSecretRequest(secretId));

        rotated.Should().NotBeNull();
        rotated.Version.Should().Be(before.Version + 1);
        rotated.AgeInDays.Should().Be(0);
        rotated.Phase.Should().Be(RotationPhase.ActivePrimary);
        rotated.LastRotatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RotateSecret_ThrowsWhenSecretNotFound()
    {
        var action = () => _engine.RotateSecret(new RotateSecretRequest("non_existent_key"));

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void ZeroizeSecret_SetsPhaseToZeroizedPurged()
    {
        var secretId = "sec_stripe_live_04";

        var result = _engine.ZeroizeSecret(new ZeroizeSecretRequest(secretId));

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("zeroized and purged");

        var secret = _engine.GetSecrets().Find(s => s.SecretId == secretId)!;
        secret.Phase.Should().Be(RotationPhase.ZeroizedPurged);
    }

    [Fact]
    public void ZeroizeSecret_ReturnsFalseWhenNotFound()
    {
        var result = _engine.ZeroizeSecret(new ZeroizeSecretRequest("ghost_secret_id"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public void RotateSecret_CaseInsensitiveSecretId_RotatesSuccessfully()
    {
        // Vault keys are configured with StringComparer.OrdinalIgnoreCase
        var uppercaseSecretId = "SEC_DB_MASTER_01";
        var before = _engine.GetSecrets().Find(s => s.SecretId.Equals(uppercaseSecretId, StringComparison.OrdinalIgnoreCase))!;

        var rotated = _engine.RotateSecret(new RotateSecretRequest(uppercaseSecretId));

        rotated.Should().NotBeNull();
        rotated.Version.Should().Be(before.Version + 1);
        rotated.Phase.Should().Be(RotationPhase.ActivePrimary);
    }

    [Fact]
    public void RotateSecret_MultipleRotations_IncrementsVersionProgressively()
    {
        var secretId = "sec_tls_wildcard_03";
        var initial = _engine.GetSecrets().Find(s => s.SecretId == secretId)!;

        var rot1 = _engine.RotateSecret(new RotateSecretRequest(secretId));
        var rot2 = _engine.RotateSecret(new RotateSecretRequest(secretId));

        rot1.Version.Should().Be(initial.Version + 1);
        rot2.Version.Should().Be(initial.Version + 2);
        rot2.AgeInDays.Should().Be(0);
    }

    [Fact]
    public void ZeroizeSecret_ThenRotate_RevivesPhaseToActivePrimary()
    {
        var secretId = "sec_stripe_live_04";

        var zeroizeResult = _engine.ZeroizeSecret(new ZeroizeSecretRequest(secretId));
        zeroizeResult.Success.Should().BeTrue();

        var zeroizedSecret = _engine.GetSecrets().Find(s => s.SecretId == secretId)!;
        zeroizedSecret.Phase.Should().Be(RotationPhase.ZeroizedPurged);

        var revived = _engine.RotateSecret(new RotateSecretRequest(secretId));
        revived.Phase.Should().Be(RotationPhase.ActivePrimary);
        revived.AgeInDays.Should().Be(0);
    }
}
