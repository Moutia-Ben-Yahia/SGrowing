using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.ConfidentialEnclave;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class ConfidentialEnclaveAndHsmTests
{
    private readonly ConfidentialEnclaveAttestationEngine _attestationEngine = new();
    private readonly HsmKeyLifecycleCoordinatorService _hsmService = new();
    private readonly MicroVmConfidentialMeshService _meshService;

    public ConfidentialEnclaveAndHsmTests()
    {
        _meshService = new MicroVmConfidentialMeshService(_attestationEngine);
    }

    private EnclaveAttestationReport CreateSampleReport(
        string reportId = "REP-01",
        EnclavePlatform platform = EnclavePlatform.AmdSevSnp,
        string measurement = "4a25b3068e2f896b5d92e597148d4512ec82823a31c6a66d03d15ff1f7cf6df2b8f87f2ab75b7501a3089d8199b50e68",
        int svn = 2,
        string nonce = "nonce1234abcd",
        string chipId = "CHIP-AMD-EPYC-9654-001")
    {
        var launch = new EnclaveLaunchMeasurement(platform, measurement, "FAM-LINUX-ENCLAVE-2026", svn, "HOST-ALGIERS-DC-01");
        var certFp = "cert-sha256-fingerprint-algeria-sovereign-ca";

        var dummyReport = new EnclaveAttestationReport(
            ReportId: reportId,
            Platform: platform,
            Measurement: launch,
            ReportDataHex: nonce,
            ChipId: chipId,
            HardwareSignatureHex: string.Empty,
            CertificateChainFingerprint: certFp,
            IssuedAtUtc: DateTime.UtcNow
        );

        var validSignature = ConfidentialEnclaveAttestationEngine.ComputeExpectedHardwareSignature(dummyReport);

        return dummyReport with { HardwareSignatureHex = validSignature };
    }

    #region Remote Attestation Tests

    [Fact]
    public void VerifyReport_ValidReport_PassesAndIssuesSessionToken()
    {
        var nonce = "FreshNonce2026";
        var report = CreateSampleReport(nonce: nonce);

        var result = _attestationEngine.VerifyReport(report, nonce);

        result.Passed.Should().BeTrue();
        result.FailureReason.Should().BeNull();
        result.EnclaveSessionToken.Should().NotBeNullOrWhiteSpace();
        result.EnclaveSessionToken.Should().StartWith("ENC-SES-");
    }

    [Fact]
    public void VerifyReport_ReplayNonceMismatch_FailsVerification()
    {
        var report = CreateSampleReport(nonce: "OriginalNonce");
        var result = _attestationEngine.VerifyReport(report, "ReplayedDifferentNonce");

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("Nonce mismatch");
        result.EnclaveSessionToken.Should().BeNull();
    }

    [Fact]
    public void VerifyReport_UntrustedPlatform_FailsVerification()
    {
        var policy = new AttestationPolicy("POL-RESTRICTED", [EnclavePlatform.AmdSevSnp], [
            "4a25b3068e2f896b5d92e597148d4512ec82823a31c6a66d03d15ff1f7cf6df2b8f87f2ab75b7501a3089d8199b50e68"
        ], MinimumSvn: 2);

        var engine = new ConfidentialEnclaveAttestationEngine();
        engine.SetPolicy(policy);

        var sgxReport = CreateSampleReport(platform: EnclavePlatform.IntelSgx, nonce: "n1");
        var result = engine.VerifyReport(sgxReport, "n1");

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("is not authorized under active enclave policy");
    }

    [Fact]
    public void VerifyReport_TamperedMeasurementDigest_FailsVerification()
    {
        const string unknownMeasurement = "000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
        var report = CreateSampleReport(measurement: unknownMeasurement, nonce: "n1");

        var result = _attestationEngine.VerifyReport(report, "n1");

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("Untrusted launch measurement");
    }

    [Fact]
    public void VerifyReport_RollbackSvn_FailsVerification()
    {
        // Policy requires minimum SVN 2, report has SVN 1
        var report = CreateSampleReport(svn: 1, nonce: "n1");

        var result = _attestationEngine.VerifyReport(report, "n1");

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("is below required minimum SVN");
    }

    [Fact]
    public void VerifyReport_TamperedHardwareSignature_FailsVerification()
    {
        var report = CreateSampleReport(nonce: "n1");
        var tamperedReport = report with { HardwareSignatureHex = "bad0000000000000000000000000000000000000000000000000000000000000" };

        var result = _attestationEngine.VerifyReport(tamperedReport, "n1");

        result.Passed.Should().BeFalse();
        result.FailureReason.Should().Contain("Hardware processor signature verification failed");
    }

    #endregion

    #region HSM Key Lifecycle Tests

    [Fact]
    public void HsmKeyLifecycle_GenerateAndRotateKey_UpdatesVersionAndStatus()
    {
        var meta1 = _hsmService.GenerateKey("master-db-dek", HsmKeyType.Aes256Gcm, rotationDays: 90);

        meta1.Status.Should().Be(HsmKeyStatus.Active);
        meta1.Version.Should().Be(1);

        var meta2 = _hsmService.RotateKey(meta1.KeyId, rotationDays: 90);

        meta2.Status.Should().Be(HsmKeyStatus.Active);
        meta2.Version.Should().Be(2);

        // Previous key marked rotated
        var oldMeta = _hsmService.GetKeyMetadata(meta1.KeyId);
        oldMeta!.Status.Should().Be(HsmKeyStatus.Rotated);
        oldMeta.RotatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void HsmKeyLifecycle_EmergencyZeroization_WipesKeyMaterial()
    {
        var key = _hsmService.GenerateKey("pqc-signing-root", HsmKeyType.Kyber1024Pqc);

        var wipedCount = _hsmService.EmergencyZeroizeAllKeys();
        wipedCount.Should().BeGreaterThan(0);

        var meta = _hsmService.GetKeyMetadata(key.KeyId);
        meta!.Status.Should().Be(HsmKeyStatus.Zeroized);
    }

    #endregion

    #region Shamir Secret Sharing Tests

    [Fact]
    public void ShamirSecretSharing_3of5_ReconstructsExactOriginalSecret()
    {
        var originalSecret = "48656c6c6f20536f7665726569676e20436f6d707574696e6721"; // "Hello Sovereign Computing!"
        var splitReq = new ShamirSplitRequest(originalSecret, ThresholdM: 3, TotalSharesN: 5);

        var shares = _hsmService.SplitSecret(splitReq);
        shares.Count.Should().Be(5);

        // Reconstruct with any 3 shares (e.g. shares 1, 3, 5)
        var subset = new List<ShamirSecretShare> { shares[0], shares[2], shares[4] };
        var reconstructedHex = _hsmService.ReconstructSecret(new ShamirReconstructRequest(subset));

        reconstructedHex.Should().Be(originalSecret.ToLowerInvariant());
    }

    [Fact]
    public void ShamirSecretSharing_InsufficientSharesLessThanM_ThrowsInvalidOperationException()
    {
        var originalSecret = "aabbccddeeff";
        var shares = _hsmService.SplitSecret(new ShamirSplitRequest(originalSecret, 3, 5));

        // Attempt reconstruction with only 2 shares when 3 are required
        var act = () => _hsmService.ReconstructSecret(new ShamirReconstructRequest([shares[0], shares[1]]));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Insufficient shares provided*");
    }

    [Fact]
    public void ShamirSecretSharing_CorruptedChecksum_ThrowsInvalidOperationException()
    {
        var originalSecret = "112233445566";
        var shares = _hsmService.SplitSecret(new ShamirSplitRequest(originalSecret, 2, 3));

        var corruptedShare = shares[0] with { ShareChecksumHex = "badchecksum1234" };

        var act = () => _hsmService.ReconstructSecret(new ShamirReconstructRequest([corruptedShare, shares[1]]));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Corrupted share checksum*");
    }

    #endregion

    #region Confidential Mesh Service Tests

    [Fact]
    public void MicroVmConfidentialMesh_EstablishChannel_And_EncryptDecrypt_Success()
    {
        var nonce = "MeshSharedNonce2026";
        var reportSource = CreateSampleReport("REP-SRC", nonce: nonce, chipId: "CHIP-01");
        var reportTarget = CreateSampleReport("REP-TGT", nonce: nonce, chipId: "CHIP-02");

        var establishReq = new EstablishMeshChannelRequest(
            SourceEnclaveId: "VM-ENC-ORAN-01",
            TargetEnclaveId: "VM-ENC-ALGIERS-02",
            SourceReport: reportSource,
            TargetReport: reportTarget,
            SharedChallengeNonceHex: nonce
        );

        var channel = _meshService.EstablishChannel(establishReq);

        channel.Should().NotBeNull();
        channel.MutualAttestationVerified.Should().BeTrue();
        channel.IsActive.Should().BeTrue();

        // Encrypt message
        var originalPlaintext = Encoding.UTF8.GetBytes("Confidential Transaction Payload: 10,000,000 DZD");
        var encrypted = _meshService.EncryptMeshMessage(channel.ChannelId, originalPlaintext);

        encrypted.Should().NotBeNull();
        encrypted.Length.Should().BeGreaterThan(originalPlaintext.Length);

        // Decrypt message
        var decrypted = _meshService.DecryptMeshMessage(channel.ChannelId, encrypted);
        var decryptedText = Encoding.UTF8.GetString(decrypted);

        decryptedText.Should().Be("Confidential Transaction Payload: 10,000,000 DZD");
    }

    [Fact]
    public void MicroVmConfidentialMesh_TamperedCiphertext_ThrowsCryptographicException()
    {
        var nonce = "MeshNonceTest";
        var reportSource = CreateSampleReport("R1", nonce: nonce);
        var reportTarget = CreateSampleReport("R2", nonce: nonce);

        var channel = _meshService.EstablishChannel(new EstablishMeshChannelRequest("V1", "V2", reportSource, reportTarget, nonce));

        var plaintext = Encoding.UTF8.GetBytes("Secret Data");
        var ciphertext = _meshService.EncryptMeshMessage(channel.ChannelId, plaintext);

        // Tamper with payload byte
        ciphertext[^1] ^= 0xFF;

        var act = () => _meshService.DecryptMeshMessage(channel.ChannelId, ciphertext);
        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddConfidentialEnclaveServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddConfidentialEnclaveServices();

        services.Should().Contain(d => d.ServiceType == typeof(ConfidentialEnclaveAttestationEngine) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(HsmKeyLifecycleCoordinatorService) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(MicroVmConfidentialMeshService) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
