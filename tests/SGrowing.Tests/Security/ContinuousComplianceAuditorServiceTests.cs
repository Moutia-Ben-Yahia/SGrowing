using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SGrowing.Api.Modules.Security;
using Xunit;

namespace SGrowing.Tests.Security;

public class ContinuousComplianceAuditorServiceTests
{
    private readonly ContinuousComplianceAuditorService _service;
    private const string TestHmacSecret = "Test-Compliance-Auditor-Secret-Key-2026-XYZ!";

    public ContinuousComplianceAuditorServiceTests()
    {
        _service = new ContinuousComplianceAuditorService(
            merkleAuditLedger: null,
            rateLimiter: null,
            idempotencyService: null,
            hmacSecret: TestHmacSecret
        );
    }

    [Fact]
    public void Constructor_Initializes25BaselineControls_AcrossAllFiveTscCategories()
    {
        // Act
        var controls = _service.GetControls();

        // Assert
        controls.Should().HaveCount(25);
        controls.Count(c => c.Category == TrustServicesCategory.Security).Should().Be(5);
        controls.Count(c => c.Category == TrustServicesCategory.Availability).Should().Be(5);
        controls.Count(c => c.Category == TrustServicesCategory.Confidentiality).Should().Be(5);
        controls.Count(c => c.Category == TrustServicesCategory.ProcessingIntegrity).Should().Be(5);
        controls.Count(c => c.Category == TrustServicesCategory.Privacy).Should().Be(5);
    }

    [Fact]
    public void Constructor_SeedsRichInitialEvidence_ForControls()
    {
        // Act
        var evidenceList = _service.GetEvidenceList(limit: 100);

        // Assert
        evidenceList.Should().NotBeEmpty();
        evidenceList.Count.Should().BeGreaterThanOrEqualTo(25);

        // Ensure every category has at least 5 evidence items
        foreach (TrustServicesCategory cat in Enum.GetValues<TrustServicesCategory>())
        {
            evidenceList.Count(e => e.Category == cat).Should().BeGreaterThanOrEqualTo(5);
        }
    }

    [Fact]
    public void GetScorecard_CalculatesAccurateReadinessScore_Formula()
    {
        // Act
        var scorecard = _service.GetScorecard();

        // Assert
        scorecard.TotalControls.Should().Be(25);
        scorecard.OverallReadinessScore.Should().BeInRange(0.0, 100.0);

        // Score formula: (Compliant * 1.0 + Warning * 0.5) / TotalControls * 100
        double expectedScore = Math.Round(((scorecard.CompliantControls * 1.0) + (scorecard.WarningControls * 0.5)) / scorecard.TotalControls * 100.0, 1);
        scorecard.OverallReadinessScore.Should().Be(expectedScore);
        scorecard.LastEvaluatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetScorecard_AssignsCorrectCertificationTier_BasedOnScore()
    {
        // Act
        var scorecard = _service.GetScorecard();

        // Assert
        if (scorecard.OverallReadinessScore >= 95.0)
        {
            scorecard.CertificationTier.Should().Contain("Tier 1: Audit-Ready");
        }
        else if (scorecard.OverallReadinessScore >= 85.0)
        {
            scorecard.CertificationTier.Should().Contain("Tier 2: Substantially Compliant");
        }
    }

    [Fact]
    public void GetScorecard_CalculatesCategoryBreakdown_ForEveryTscDomain()
    {
        // Act
        var scorecard = _service.GetScorecard();

        // Assert
        scorecard.Categories.Should().HaveCount(5);
        scorecard.Categories.Select(c => c.Category).Should().BeEquivalentTo(Enum.GetValues<TrustServicesCategory>());

        foreach (var cat in scorecard.Categories)
        {
            cat.TotalControls.Should().Be(5);
            cat.ReadinessScore.Should().BeInRange(0.0, 100.0);
            cat.CategoryName.Should().NotBeNullOrWhiteSpace();
            cat.EvidenceCount.Should().BeGreaterThanOrEqualTo(5);
        }
    }

    [Fact]
    public void GetControls_WithoutFilter_ReturnsAllControlsOrderedById()
    {
        // Act
        var controls = _service.GetControls();

        // Assert
        controls.Should().HaveCount(25);
        controls.Should().BeInAscendingOrder(c => c.Id);
    }

    [Theory]
    [InlineData(TrustServicesCategory.Security, "SEC-")]
    [InlineData(TrustServicesCategory.Availability, "AVL-")]
    [InlineData(TrustServicesCategory.Confidentiality, "CNF-")]
    [InlineData(TrustServicesCategory.ProcessingIntegrity, "PRC-")]
    [InlineData(TrustServicesCategory.Privacy, "PRV-")]
    public void GetControls_WithCategoryFilter_ReturnsOnlyMatchingControls(TrustServicesCategory category, string expectedPrefix)
    {
        // Act
        var controls = _service.GetControls(category);

        // Assert
        controls.Should().HaveCount(5);
        controls.Should().OnlyContain(c => c.Category == category);
        controls.Should().OnlyContain(c => c.Id.StartsWith(expectedPrefix));
    }

    [Fact]
    public void GetControl_WithValidId_ReturnsExpectedControlDetails()
    {
        // Act
        var control = _service.GetControl("SEC-CC6.1-01");

        // Assert
        control.Should().NotBeNull();
        control!.Id.Should().Be("SEC-CC6.1-01");
        control.Code.Should().Be("CC6.1");
        control.Category.Should().Be(TrustServicesCategory.Security);
        control.Title.Should().Contain("Merkle Tree");
        control.StandardMapping.Should().Contain("SOC 2 CC6.1");
    }

    [Fact]
    public void GetControl_WithInvalidId_ReturnsNull()
    {
        // Act
        var control = _service.GetControl("NON-EXISTENT-CONTROL-999");

        // Assert
        control.Should().BeNull();
    }

    [Fact]
    public void GetEvidenceList_RespectsLimitAndFilters()
    {
        // Act
        var limited = _service.GetEvidenceList(limit: 3);
        var secEvidence = _service.GetEvidenceList(category: TrustServicesCategory.Security);
        var singleControlEvidence = _service.GetEvidenceList(controlId: "SEC-CC6.1-01");

        // Assert
        limited.Should().HaveCount(3);
        secEvidence.Should().OnlyContain(e => e.Category == TrustServicesCategory.Security);
        singleControlEvidence.Should().OnlyContain(e => e.ControlId == "SEC-CC6.1-01");
    }

    [Fact]
    public void GetEvidence_WithValidId_ReturnsSpecificRecord()
    {
        // Arrange
        var allEvidence = _service.GetEvidenceList(limit: 1);
        var targetId = allEvidence.First().EvidenceId;

        // Act
        var evidence = _service.GetEvidence(targetId);

        // Assert
        evidence.Should().NotBeNull();
        evidence!.EvidenceId.Should().Be(targetId);
        evidence.ProofDigest.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateEvidenceRecord_GeneratesDeterministicSha256Digest()
    {
        // Arrange
        var payload = new { metric = "ZeroTamper", counter = 42 };
        string expectedJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        string expectedDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedJson))).ToLowerInvariant();

        // Act
        var record = _service.CreateEvidenceRecord(
            "SEC-CC6.1-01",
            TrustServicesCategory.Security,
            "Integrity Test",
            "UnitTestSubsystem",
            ComplianceControlStatus.Compliant,
            payload,
            "AuditorBot"
        );

        // Assert
        record.ProofDigest.Should().Be(expectedDigest);
        record.ProofDigest.Length.Should().Be(64);
        record.Status.Should().Be(ComplianceControlStatus.Compliant);
    }

    [Fact]
    public void RecordCustomEvidence_UpdatesControlStatusAndEvidenceDigest()
    {
        // Arrange
        var customEv = _service.CreateEvidenceRecord(
            "SEC-CC7.1-05",
            TrustServicesCategory.Security,
            "Critical CVE Detected In Container Base Image",
            "SbomScanner",
            ComplianceControlStatus.NonCompliant,
            new { cveId = "CVE-2026-9999", severity = "CRITICAL" },
            "SecOpsAutomator"
        );

        // Act
        _service.RecordCustomEvidence(customEv);
        var updatedControl = _service.GetControl("SEC-CC7.1-05");

        // Assert
        updatedControl.Should().NotBeNull();
        updatedControl!.CurrentStatus.Should().Be(ComplianceControlStatus.NonCompliant);
        updatedControl.LastEvidenceId.Should().Be(customEv.EvidenceId);
        updatedControl.LastEvidenceDigest.Should().Be(customEv.ProofDigest);
    }

    [Fact]
    public async Task RunEvidenceHarvestSweepAsync_CollectsNewEvidenceAcrossDomains()
    {
        // Arrange
        int initialEvidenceCount = _service.GetEvidenceList(limit: 1000).Count;

        // Act
        var result = await _service.RunEvidenceHarvestSweepAsync("ContinuousComplianceDaemon");

        // Assert
        result.Should().NotBeNull();
        result.CollectedEvidenceCount.Should().BeGreaterThanOrEqualTo(5);
        result.SweepLogs.Should().NotBeEmpty();
        result.SweepLogs.Should().Contain(l => l.Contains("CC6.1 Merkle tree"));
        result.SweepLogs.Should().Contain(l => l.Contains("A1.2 Rate limiter"));
        result.SweepLogs.Should().Contain(l => l.Contains("C1.1 AES-256-GCM"));
        result.SweepLogs.Should().Contain(l => l.Contains("PI1.1 Idempotency"));
        result.SweepLogs.Should().Contain(l => l.Contains("P3.1 Customer erasure"));

        int postSweepCount = _service.GetEvidenceList(limit: 1000).Count;
        postSweepCount.Should().Be(initialEvidenceCount + result.CollectedEvidenceCount);
    }

    [Fact]
    public void GenerateSealedVaultPackage_CreatesCertifiedJsonLdPackage()
    {
        // Act
        var package = _service.GenerateSealedVaultPackage(
            auditorOrg: "AICPA / Schellman & Co.",
            sealedBy: "CISO Security Officer",
            validityDays: 120
        );

        // Assert
        package.Should().NotBeNull();
        package.PackageId.Should().NotBeEmpty();
        package.AuditorOrganization.Should().Be("AICPA / Schellman & Co.");
        package.SealedBy.Should().Be("CISO Security Officer");
        package.CertifiedStandard.Should().Contain("SOC 2 Type II / ISO/IEC 27001");
        package.ExpiresAtUtc.Should().BeAfter(package.GeneratedAtUtc);
        package.IntegrityDigest.Should().NotBeNullOrWhiteSpace();
        package.CryptographicSeal.Should().NotBeNullOrWhiteSpace();

        // Inspect JSON-LD text
        package.JsonLdDocument.Should().Contain("\"type\": \"ComplianceAuditorVaultPackage\"");
        package.JsonLdDocument.Should().Contain("https://www.w3.org/ns/credentials/v2");
        package.JsonLdDocument.Should().Contain("trustServicesCriteria");
        package.JsonLdDocument.Should().Contain("HmacSha256Signature2026");
    }

    [Fact]
    public void GenerateSealedVaultPackage_DeterministicManifestDigest()
    {
        // Act
        var pkg1 = _service.GenerateSealedVaultPackage();

        // Assert
        pkg1.Manifest.ManifestDigest.Should().Be(pkg1.IntegrityDigest);
        pkg1.Manifest.EvidenceItems.Should().NotBeEmpty();
        pkg1.Manifest.EvidenceItems.Should().BeInAscendingOrder(e => e.ControlId);
    }

    [Fact]
    public void VerifyVaultPackage_ReturnsValid_ForUntamperedPackage()
    {
        // Arrange
        var package = _service.GenerateSealedVaultPackage();

        // Act
        var verification = _service.VerifyVaultPackage(package);

        // Assert
        verification.IsValid.Should().BeTrue();
        verification.DigestMatch.Should().BeTrue();
        verification.SignatureValid.Should().BeTrue();
        verification.VerificationMessage.Should().Contain("passed cryptographic tamper verification");
    }

    [Fact]
    public void VerifyVaultPackage_ByPackageId_SucceedsForStoredPackage()
    {
        // Arrange
        var package = _service.GenerateSealedVaultPackage();

        // Act
        var verification = _service.VerifyVaultPackage(package.PackageId);

        // Assert
        verification.IsValid.Should().BeTrue();
        verification.PackageId.Should().Be(package.PackageId);
    }

    [Fact]
    public void VerifyVaultPackage_ReturnsFalse_ForNonExistentPackageId()
    {
        // Act
        var nonExistentId = Guid.NewGuid();
        var verification = _service.VerifyVaultPackage(nonExistentId);

        // Assert
        verification.IsValid.Should().BeFalse();
        verification.VerificationMessage.Should().Contain("does not exist in the auditor vault");
    }

    [Fact]
    public void VerifyVaultPackage_DetectsTamper_WhenEvidenceItemModified()
    {
        // Arrange
        var package = _service.GenerateSealedVaultPackage();
        var evidenceItems = new List<ComplianceEvidenceRecord>(package.Manifest.EvidenceItems);

        // Tamper with the first evidence item
        var original = evidenceItems[0];
        evidenceItems[0] = original with
        {
            Title = "Tampered Fake Evidence Record Inserted By Malicious Actor",
            ProofDigest = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
        };

        var tamperedManifest = package.Manifest with { EvidenceItems = evidenceItems };
        var tamperedPackage = package with { Manifest = tamperedManifest };

        // Act
        var verification = _service.VerifyVaultPackage(tamperedPackage);

        // Assert
        verification.IsValid.Should().BeFalse();
        verification.DigestMatch.Should().BeFalse();
        verification.VerificationMessage.Should().Contain("Tamper detected: Manifest integrity digest mismatch");
    }

    [Fact]
    public void VerifyVaultPackage_DetectsTamper_WhenManifestDigestModified()
    {
        // Arrange
        var package = _service.GenerateSealedVaultPackage();
        var tamperedPackage = package with
        {
            IntegrityDigest = "0000000000000000000000000000000000000000000000000000000000000000"
        };

        // Act
        var verification = _service.VerifyVaultPackage(tamperedPackage);

        // Assert
        verification.IsValid.Should().BeFalse();
        verification.VerificationMessage.Should().Contain("Tamper detected");
    }

    [Fact]
    public void VerifyVaultPackage_DetectsTamper_WhenCryptographicSealAltered()
    {
        // Arrange
        var package = _service.GenerateSealedVaultPackage();
        var tamperedPackage = package with
        {
            CryptographicSeal = "bad_seal_hash_1234567890abcdef1234567890abcdef1234567890abcdef"
        };

        // Act
        var verification = _service.VerifyVaultPackage(tamperedPackage);

        // Assert
        verification.IsValid.Should().BeFalse();
        verification.SignatureValid.Should().BeFalse();
        verification.VerificationMessage.Should().Contain("Cryptographic signature mismatch");
    }

    [Fact]
    public void GetVaultPackages_ReturnsHistoricallySealedPackages_OrderedByDate()
    {
        // Arrange
        var pkg1 = _service.GenerateSealedVaultPackage("Auditor 1");
        var pkg2 = _service.GenerateSealedVaultPackage("Auditor 2");

        // Act
        var allPackages = _service.GetVaultPackages();

        // Assert
        allPackages.Should().Contain(p => p.PackageId == pkg1.PackageId);
        allPackages.Should().Contain(p => p.PackageId == pkg2.PackageId);
        allPackages.Should().BeInDescendingOrder(p => p.GeneratedAtUtc);
    }

    [Fact]
    public void ThreadSafety_ConcurrentEvidenceCollectionAndSweep()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Concurrently run sweeps and query scorecards and evidence
        for (int i = 0; i < 15; i++)
        {
            int index = i;
            tasks.Add(Task.Run(async () =>
            {
                if (index % 3 == 0)
                {
                    await _service.RunEvidenceHarvestSweepAsync($"Actor-{index}");
                }
                else if (index % 3 == 1)
                {
                    var scorecard = _service.GetScorecard();
                    scorecard.OverallReadinessScore.Should().BeInRange(0.0, 100.0);
                }
                else
                {
                    var evidence = _service.GetEvidenceList(limit: 50);
                    evidence.Should().NotBeEmpty();
                }
            }));
        }

        // Assert
        Func<Task> act = () => Task.WhenAll(tasks);
        act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentVaultPackageGeneration()
    {
        // Arrange
        var packages = new List<AuditorVaultPackage>();
        var tasks = new List<Task>();

        // Act - Concurrently generate sealed vault packages
        for (int i = 0; i < 8; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(() =>
            {
                var pkg = _service.GenerateSealedVaultPackage($"Auditor-Firm-{idx}");
                lock (packages)
                {
                    packages.Add(pkg);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        packages.Should().HaveCount(8);
        packages.Select(p => p.PackageId).Distinct().Should().HaveCount(8);

        foreach (var pkg in packages)
        {
            var verification = _service.VerifyVaultPackage(pkg);
            verification.IsValid.Should().BeTrue();
        }
    }
}
