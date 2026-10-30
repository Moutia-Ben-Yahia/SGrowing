using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using SGrowing.Api.Modules.Security;
using Xunit;

namespace SGrowing.Tests.Security;

public class MerkleAuditLedgerServiceTests
{
    private readonly MerkleAuditLedgerService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public MerkleAuditLedgerServiceTests()
    {
        _service = new MerkleAuditLedgerService("Test_Secure_HMAC_Key_2026_XYZ!");
    }

    [Fact]
    public void ComputeLeafHash_IsDeterministic_ForSameInputs()
    {
        // Arrange
        var timestamp = new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc);
        string email = "auditor@sgrowing.io";
        string action = "RoleEscalation";
        string resource = "RBAC/Roles";
        string payloadHash = _service.ComputePayloadHash("{\"role\":\"Admin\"}");

        // Act
        string hash1 = _service.ComputeLeafHash(_tenantId, timestamp, email, action, resource, payloadHash);
        string hash2 = _service.ComputeLeafHash(_tenantId, timestamp, email, action, resource, payloadHash);

        // Assert
        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Length.Should().Be(64);
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeLeafHash_Changes_WhenAnyFieldModified()
    {
        // Arrange
        var timestamp = new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc);
        string email = "auditor@sgrowing.io";
        string action = "RoleEscalation";
        string resource = "RBAC/Roles";
        string payloadHash = _service.ComputePayloadHash("{\"role\":\"Admin\"}");

        string baseHash = _service.ComputeLeafHash(_tenantId, timestamp, email, action, resource, payloadHash);

        // Act & Assert
        string diffActor = _service.ComputeLeafHash(_tenantId, timestamp, "attacker@evil.com", action, resource, payloadHash);
        diffActor.Should().NotBe(baseHash);

        string diffAction = _service.ComputeLeafHash(_tenantId, timestamp, email, "UserLogin", resource, payloadHash);
        diffAction.Should().NotBe(baseHash);

        string diffResource = _service.ComputeLeafHash(_tenantId, timestamp, email, action, "Orders", payloadHash);
        diffResource.Should().NotBe(baseHash);

        string diffTime = _service.ComputeLeafHash(_tenantId, timestamp.AddSeconds(1), email, action, resource, payloadHash);
        diffTime.Should().NotBe(baseHash);

        string diffPayload = _service.ComputeLeafHash(_tenantId, timestamp, email, action, resource, _service.ComputePayloadHash("{\"role\":\"SuperRoot\"}"));
        diffPayload.Should().NotBe(baseHash);
    }

    [Fact]
    public void BuildMerkleTree_WithPowerOfTwoLeaves_CalculatesExpectedRoot()
    {
        // Arrange: 4 distinct leaves
        var leaves = new List<string>
        {
            ComputeSha256("leaf_1"),
            ComputeSha256("leaf_2"),
            ComputeSha256("leaf_3"),
            ComputeSha256("leaf_4")
        };

        // Manual expected root:
        // Level 1: H(leaf_1 + leaf_2), H(leaf_3 + leaf_4)
        // Level 2: H(Level1[0] + Level1[1])
        string p0 = ComputeSha256(leaves[0] + leaves[1]);
        string p1 = ComputeSha256(leaves[2] + leaves[3]);
        string expectedRoot = ComputeSha256(p0 + p1);

        // Act
        var result = _service.BuildMerkleTree(leaves);

        // Assert
        result.LeafCount.Should().Be(4);
        result.Levels.Count.Should().Be(3);
        result.RootHash.Should().Be(expectedRoot);
    }

    [Fact]
    public void BuildMerkleTree_WithOddNumberOfLeaves_DuplicatesLastLeafAndSucceeds()
    {
        // Arrange: 3 leaves (odd)
        var leaves = new List<string>
        {
            ComputeSha256("leaf_alpha"),
            ComputeSha256("leaf_beta"),
            ComputeSha256("leaf_gamma")
        };

        // Manual expected root with odd duplication of leaf_gamma:
        // Level 1: H(alpha + beta), H(gamma + gamma)
        // Level 2: H(Level1[0] + Level1[1])
        string p0 = ComputeSha256(leaves[0] + leaves[1]);
        string p1 = ComputeSha256(leaves[2] + leaves[2]);
        string expectedRoot = ComputeSha256(p0 + p1);

        // Act
        var result = _service.BuildMerkleTree(leaves);

        // Assert
        result.LeafCount.Should().Be(3);
        result.RootHash.Should().Be(expectedRoot);
    }

    [Fact]
    public void BuildMerkleTree_WithSingleLeaf_ReturnsLeafAsRoot()
    {
        // Arrange
        var singleLeaf = ComputeSha256("only_one_leaf");
        var leaves = new List<string> { singleLeaf };

        // Act
        var result = _service.BuildMerkleTree(leaves);

        // Assert
        result.LeafCount.Should().Be(1);
        result.RootHash.Should().Be(singleLeaf);
        result.TotalNodeCount.Should().Be(1);
    }

    [Fact]
    public void BuildMerkleTree_WithEmptyLeaves_ReturnsGenesisHash()
    {
        // Arrange
        var leaves = new List<string>();

        // Act
        var result = _service.BuildMerkleTree(leaves);

        // Assert
        result.LeafCount.Should().Be(0);
        result.RootHash.Should().Be(MerkleAuditLedgerService.GenesisHash);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task InclusionProof_GeneratesAndVerifiesSuccessfully_ForEveryLeafInTree(int leafCount)
    {
        // Arrange: fresh service instance to isolate
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();
        var eventIds = new List<Guid>();

        for (int i = 0; i < leafCount; i++)
        {
            var ev = await svc.RecordAuditEventAsync(
                tenantId: tenant,
                actorId: Guid.NewGuid(),
                actorEmail: $"user{i}@test.com",
                action: $"Action_{i}",
                resource: $"Resource_{i}",
                metadataJson: $"{{\"idx\":{i}}}"
            );
            eventIds.Add(ev.Id);
        }

        // Seal the batch
        var epoch = await svc.SealCurrentEpochAsync(tenant);

        // Act & Assert: For every single leaf in this tree, get its proof and verify
        for (int i = 0; i < eventIds.Count; i++)
        {
            var proof = await svc.GetInclusionProofAsync(eventIds[i]);

            proof.TotalLeaves.Should().Be(leafCount);
            proof.MerkleRoot.Should().Be(epoch.MerkleRoot);
            proof.LeafIndex.Should().Be(i);

            bool isValid = svc.VerifyInclusionProof(proof.LeafHash, proof.ProofPath, proof.MerkleRoot);
            isValid.Should().BeTrue($"proof for leaf index {i} in a {leafCount}-leaf tree should verify");
        }
    }

    [Fact]
    public async Task VerifyInclusionProof_Fails_WhenLeafHashIsTampered()
    {
        // Arrange
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        var ev1 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "a@test.com", "Login", "Auth", "{}");
        var ev2 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "b@test.com", "Export", "Data", "{}");

        var epoch = await svc.SealCurrentEpochAsync(tenant);
        var proof = await svc.GetInclusionProofAsync(ev1.Id);

        // Act: Tamper leaf hash
        string forgedLeafHash = ComputeSha256("forged_fake_leaf_hash");
        bool verified = svc.VerifyInclusionProof(forgedLeafHash, proof.ProofPath, epoch.MerkleRoot);

        // Assert
        verified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyInclusionProof_Fails_WhenSiblingHashIsTampered()
    {
        // Arrange
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        for (int i = 0; i < 4; i++)
        {
            await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), $"u{i}@test.com", "Write", "Db", $"{{\"n\":{i}}}");
        }

        var epoch = await svc.SealCurrentEpochAsync(tenant);
        var events = await svc.GetEventsAsync(tenant);
        var proof = await svc.GetInclusionProofAsync(events[0].Id);

        // Act: tamper the first sibling hash in the proof path
        var corruptedProofPath = proof.ProofPath.Select((step, idx) =>
            idx == 0
                ? step with { SiblingHash = ComputeSha256("tampered_sibling_hash") }
                : step
        ).ToList();

        bool verified = svc.VerifyInclusionProof(proof.LeafHash, corruptedProofPath, epoch.MerkleRoot);

        // Assert
        verified.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyInclusionProof_Fails_WhenProofDirectionIsInverted()
    {
        // Arrange
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        var ev1 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "u1@test.com", "Act1", "Res1", "{}");
        var ev2 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "u2@test.com", "Act2", "Res2", "{}");

        var epoch = await svc.SealCurrentEpochAsync(tenant);
        var proof = await svc.GetInclusionProofAsync(ev1.Id);

        // Act: Invert the proof direction
        var invertedPath = proof.ProofPath.Select(s => s with
        {
            Direction = s.Direction == MerkleProofDirection.Left ? MerkleProofDirection.Right : MerkleProofDirection.Left
        }).ToList();

        bool verified = svc.VerifyInclusionProof(proof.LeafHash, invertedPath, epoch.MerkleRoot);

        // Assert
        verified.Should().BeFalse();
    }

    [Fact]
    public async Task RecordAuditEventAsync_ComputesCorrectPayloadAndLeafHashes()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var actorId = Guid.NewGuid();
        string email = "compliance@sgrowing.io";
        string action = "FactoringDrawn";
        string resource = "Finance/Factoring";
        string metadata = "{\"amount\":10000,\"currency\":\"EUR\"}";

        // Act
        var recorded = await _service.RecordAuditEventAsync(
            _tenantId, actorId, email, action, resource, metadata, timestamp);

        // Assert
        recorded.Should().NotBeNull();
        recorded.PayloadHash.Should().Be(_service.ComputePayloadHash(metadata));
        recorded.LeafHash.Should().Be(_service.ComputeLeafHash(_tenantId, timestamp, email, action, resource, recorded.PayloadHash));
        recorded.IsSealed.Should().BeFalse();
        recorded.EpochId.Should().BeNull();
    }

    [Fact]
    public async Task SealCurrentEpochAsync_BatchesUnsealedEvents_IntoImmutableEpoch()
    {
        // Arrange
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        var ev1 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "a@a.com", "Login", "Auth");
        var ev2 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "b@b.com", "RoleEscalate", "Rbac");
        var ev3 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "c@c.com", "ApiKey", "Keys");

        // Act
        var epoch = await svc.SealCurrentEpochAsync(tenant, "SecOps Lead");

        // Assert
        epoch.Should().NotBeNull();
        epoch.EpochNumber.Should().Be(1);
        epoch.EventCount.Should().Be(3);
        epoch.SealedBy.Should().Be("SecOps Lead");
        epoch.MerkleRoot.Length.Should().Be(64);
        epoch.IsSealed.Should().BeTrue();

        // Check that recorded events are now sealed and carry EpochId
        var sealedEvents = await svc.GetEventsAsync(tenant, isSealed: true);
        sealedEvents.Should().HaveCount(3);
        sealedEvents.Should().AllSatisfy(e =>
        {
            e.IsSealed.Should().BeTrue();
            e.EpochId.Should().Be(epoch.Id);
            e.EpochNumber.Should().Be(1);
        });

        // Querying unsealed should now be empty
        var remainingUnsealed = await svc.GetEventsAsync(tenant, isSealed: false);
        remainingUnsealed.Should().BeEmpty();
    }

    [Fact]
    public async Task SealCurrentEpochAsync_GeneratesHmacSignature_AndChainedHash()
    {
        // Arrange
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        // Epoch 1
        await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "e1@a.com", "Action1", "Res1");
        var epoch1 = await svc.SealCurrentEpochAsync(tenant);

        // Epoch 2
        await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "e2@b.com", "Action2", "Res2");
        var epoch2 = await svc.SealCurrentEpochAsync(tenant);

        // Assert
        epoch1.PreviousEpochChainedHash.Should().Be(MerkleAuditLedgerService.GenesisHash);
        epoch1.ChainedHash.Should().Be(ComputeSha256(epoch1.MerkleRoot + MerkleAuditLedgerService.GenesisHash));
        epoch1.EpochSignature.Should().NotBeNullOrWhiteSpace();

        // Epoch 2 should chain to Epoch 1's ChainedHash
        epoch2.PreviousEpochChainedHash.Should().Be(epoch1.ChainedHash);
        epoch2.ChainedHash.Should().Be(ComputeSha256(epoch2.MerkleRoot + epoch1.ChainedHash));
        epoch2.EpochNumber.Should().Be(2);
    }

    [Fact]
    public async Task SealCurrentEpochAsync_Throws_WhenNoUnsealedEventsAvailable()
    {
        // Arrange: fresh tenant with 0 events
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        // Act & Assert
        var act = async () => await svc.SealCurrentEpochAsync(tenant);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No unsealed events available*");
    }

    [Fact]
    public async Task SimulateTamper_DetectsUnauthorizedMetadataChange_ProducesRootMismatch_AndInvalidatesProof()
    {
        // Arrange
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        var ev1 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "user@co.tn", "Transfer", "Banking", "{\"amount\":500}");
        var ev2 = await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "user@co.tn", "Transfer", "Banking", "{\"amount\":1200}");
        var epoch = await svc.SealCurrentEpochAsync(tenant);

        // Act: Adversary maliciously modifies ev1 payload to alter the transfer amount
        string tamperedPayload = "{\"amount\":999999,\"forged_by\":\"Adversary_APT\"}";
        var simulation = await svc.SimulateTamperAsync(ev1.Id, tamperedPayload);

        // Assert
        simulation.Should().NotBeNull();
        simulation.RootMismatchDetected.Should().BeTrue();
        simulation.ProofVerificationFailed.Should().BeTrue();
        simulation.IntegrityBroken.Should().BeTrue();
        simulation.OriginalMerkleRoot.Should().Be(epoch.MerkleRoot);
        simulation.TamperedMerkleRoot.Should().NotBe(simulation.OriginalMerkleRoot);
        simulation.TamperAuditVerdict.Should().Contain("TAMPER_DETECTED");
        simulation.TamperedPayloadHash.Should().NotBe(simulation.OriginalPayloadHash);
    }

    [Fact]
    public async Task GenerateComplianceManifest_ProducesCompleteAuditDto_WithValidHashesAndGuide()
    {
        // Arrange
        var svc = new MerkleAuditLedgerService();
        var tenant = Guid.NewGuid();

        await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "ciso@company.com", "MfaPolicy", "Security", "{\"enforced\":true}");
        await svc.RecordAuditEventAsync(tenant, Guid.NewGuid(), "admin@company.com", "KeyRotation", "Vault", "{\"keyId\":\"k-99\"}");
        var epoch = await svc.SealCurrentEpochAsync(tenant, "Compliance Bot v1");

        // Act
        var manifest = await svc.GenerateComplianceManifestAsync(tenant, epoch.Id);

        // Assert
        manifest.Should().NotBeNull();
        manifest.ManifestVersion.Should().Be("2026.1-cryptographic-merkle-audit");
        manifest.ComplianceStandard.Should().Contain("SOC2");
        manifest.EpochNumber.Should().Be(1);
        manifest.MerkleRoot.Should().Be(epoch.MerkleRoot);
        manifest.EpochSignature.Should().Be(epoch.EpochSignature);
        manifest.ChainedHash.Should().Be(epoch.ChainedHash);
        manifest.Events.Should().HaveCount(2);
        manifest.Events[0].Action.Should().Be("MfaPolicy");
        manifest.Events[1].Action.Should().Be("KeyRotation");
        manifest.VerificationGuide.Should().NotBeNull();
        manifest.VerificationGuide.StandardsCompliance.Should().Contain("SOC 2");
        manifest.VerificationGuide.StandardsCompliance.Should().Contain("GDPR");
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
