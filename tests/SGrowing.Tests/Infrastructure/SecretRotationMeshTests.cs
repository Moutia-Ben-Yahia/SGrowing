using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.SecretRotationMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class SecretRotationMeshTests
{
    private readonly SecretRotationMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsManagedSecretsAndEphemeralCerts()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalManagedSecretsCount.Should().Be(2);
        overview.EphemeralCertsActiveCount.Should().Be(1);
        overview.ZeroDowntimeAttestationPct.Should().Be(100.0);
        overview.ManagedSecrets.Should().HaveCount(2);
        overview.ActiveEphemeralCerts.Should().HaveCount(1);
    }

    [Fact]
    public void IssueEphemeralCert_GeneratesShortLivedX509Certificate()
    {
        var request = new IssueEphemeralCertRequest(
            "CN=edge-proxy-iad-02.mesh.sgrowing.io",
            "EDGE-POP-IAD-02",
            15,
            "KMS-KEY-SOVEREIGN-CA-99"
        );

        var result = _engine.IssueEphemeralCert(request);

        result.Should().NotBeNull();
        result.CertSerialHex.Should().StartWith("0X");
        result.SubjectDistinguishedName.Should().Contain("edge-proxy-iad-02");
        result.X509CertPem.Should().Contain("BEGIN CERTIFICATE");
        result.SovereignSignatureProof.Should().StartWith("SOV-SIG-");
    }

    [Fact]
    public void RotateSecret_ExecutesZeroDowntimeRotationAndEmitsReceipt()
    {
        var request = new RotateSecretRequest(
            "SEC-DB-PG-MASTER",
            true,
            120
        );

        var receipt = _engine.RotateSecret(request);

        receipt.Should().NotBeNull();
        receipt.RotationExecutionId.Should().StartWith("ROT-EXEC-");
        receipt.SecretId.Should().Be("SEC-DB-PG-MASTER");
        receipt.Status.Should().Be("ZERO_DOWNTIME_ROTATION_SUCCESSFUL");
        receipt.HsmVerificationDigest.Should().StartWith("0x");
    }
}
