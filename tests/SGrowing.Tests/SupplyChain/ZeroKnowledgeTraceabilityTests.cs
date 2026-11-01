using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.SupplyChain.ZkTraceability;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

public sealed class ZeroKnowledgeTraceabilityTests
{
    private readonly ZeroKnowledgeOriginProofEngine _zkEngine = new();
    private readonly SanctionComplianceScreeningService _sanctionService = new();
    private readonly VerifiableBOMProvenanceService _provenanceService = new();

    #region Origin Proof & Commitments Tests

    [Fact]
    public void ComputeCommitment_Deterministic_ProducesConsistentHash()
    {
        var commitment1 = _zkEngine.ComputeCommitment(125000.50m, "salt1234abcd", TradeZone.DomesticAlgeria);
        var commitment2 = _zkEngine.ComputeCommitment(125000.50m, "salt1234abcd", TradeZone.DomesticAlgeria);

        commitment1.CommitmentHash.Should().Be(commitment2.CommitmentHash);
        commitment1.CommittedValueDzd.Should().Be(125000.50m);
    }

    [Fact]
    public void ComputeCommitment_DifferentSalts_ProduceDistinctHashes()
    {
        var commitment1 = _zkEngine.ComputeCommitment(1000m, "salt_A", TradeZone.AfCFTA);
        var commitment2 = _zkEngine.ComputeCommitment(1000m, "salt_B", TradeZone.AfCFTA);

        commitment1.CommitmentHash.Should().NotBe(commitment2.CommitmentHash);
    }

    [Fact]
    public void ComputeBlindedCommitmentRoot_ValidManifest_ProducesValidSha256Root()
    {
        var items = new List<BomComponentItem>
        {
            new("CMP-1", "Olive Oil Extract", 100m, 500m, 10, TradeZone.DomesticAlgeria, "hash1", "sanct1"),
            new("CMP-2", "Glass Bottle Packaging", 20m, 200m, 10, TradeZone.AfCFTA, "hash2", "sanct2")
        };
        var manifest = new BillOfMaterialsManifest("SKU-OIL-01", "BATCH-9001", items, DateTime.UtcNow);
        var salts = new List<string> { "salt1", "salt2" };

        var root = _zkEngine.ComputeBlindedCommitmentRoot(manifest, salts);

        root.Should().NotBeNullOrWhiteSpace();
        root.Length.Should().Be(64); // SHA-256 hex string
    }

    [Fact]
    public void ComputeBlindedCommitmentRoot_InsufficientSalts_ThrowsArgumentException()
    {
        var items = new List<BomComponentItem>
        {
            new("CMP-1", "Date Paste", 50m, 100m, 2, TradeZone.DomesticAlgeria, "h1", "s1"),
            new("CMP-2", "Plastic Tub", 5m, 50m, 2, TradeZone.GlobalOther, "h2", "s2")
        };
        var manifest = new BillOfMaterialsManifest("SKU-DATES-01", "BATCH-9002", items, DateTime.UtcNow);
        var salts = new List<string> { "only_one_salt" };

        var act = () => _zkEngine.ComputeBlindedCommitmentRoot(manifest, salts);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Must supply a blinding salt for each BOM component*");
    }

    [Fact]
    public void GenerateOriginProof_AboveThreshold_SatisfiesProofAndMarksVerified()
    {
        // 80% Domestic, 20% Global -> Qualifying 80% > 60% threshold
        var items = new List<BomComponentItem>
        {
            new("CMP-ALGERIA-1", "Local Aluminum Extrusion", 150m, 8000m, 10, TradeZone.DomesticAlgeria, "sup1", "sanc1"),
            new("CMP-GLOBAL-2", "Imported Microcontroller", 1m, 2000m, 10, TradeZone.GlobalOther, "sup2", "sanc2")
        };
        var manifest = new BillOfMaterialsManifest("SKU-SOLAR-INVERTER", "BATCH-ALG-2026", items, DateTime.UtcNow);
        var salts = new List<string> { "salt_inverter_1", "salt_inverter_2" };

        var request = new GenerateOriginProofRequest(manifest, TradeZone.AfCFTA, 0.60m);
        var proof = _zkEngine.GenerateOriginProof(request, salts);

        proof.ThresholdSatisfied.Should().BeTrue();
        proof.Status.Should().Be(OriginVerificationStatus.Verified);
        proof.TargetTradeZone.Should().Be(TradeZone.AfCFTA);
        proof.CryptographicSignature.Should().NotBeNullOrWhiteSpace();

        // Verify with engine
        var verifyRequest = new VerifyOriginProofRequest(proof, proof.PublicCommitmentRoot, 0.60m);
        var verifyStatus = _zkEngine.VerifyProof(verifyRequest);

        verifyStatus.Should().Be(OriginVerificationStatus.Verified);
    }

    [Fact]
    public void GenerateOriginProof_BelowThreshold_MarksThresholdNotMet()
    {
        // 20% Domestic, 80% Global -> 20% < 60% threshold
        var items = new List<BomComponentItem>
        {
            new("CMP-1", "Local Packaging Box", 10m, 200m, 10, TradeZone.DomesticAlgeria, "sup1", "sanc1"),
            new("CMP-2", "Imported Lithium Cells", 50m, 8000m, 10, TradeZone.GlobalOther, "sup2", "sanc2")
        };
        var manifest = new BillOfMaterialsManifest("SKU-BATTERY-PACK", "BATCH-IMP-001", items, DateTime.UtcNow);
        var salts = new List<string> { "salt_box", "salt_cells" };

        var request = new GenerateOriginProofRequest(manifest, TradeZone.AfCFTA, 0.60m);
        var proof = _zkEngine.GenerateOriginProof(request, salts);

        proof.ThresholdSatisfied.Should().BeFalse();
        proof.Status.Should().Be(OriginVerificationStatus.ThresholdNotMet);

        var verifyRequest = new VerifyOriginProofRequest(proof, proof.PublicCommitmentRoot, 0.60m);
        var verifyStatus = _zkEngine.VerifyProof(verifyRequest);

        verifyStatus.Should().Be(OriginVerificationStatus.ThresholdNotMet);
    }

    [Fact]
    public void VerifyProof_TamperedCommitmentRoot_ReturnsRejectedTampered()
    {
        var items = new List<BomComponentItem>
        {
            new("CMP-1", "Steel Rods", 500m, 1000m, 10, TradeZone.DomesticAlgeria, "sup1", "sanc1")
        };
        var manifest = new BillOfMaterialsManifest("SKU-STEEL-01", "BATCH-ST-1", items, DateTime.UtcNow);
        var salts = new List<string> { "salt_st" };

        var request = new GenerateOriginProofRequest(manifest, TradeZone.AfCFTA, 0.60m);
        var proof = _zkEngine.GenerateOriginProof(request, salts);

        var tamperedRoot = "0000000000000000000000000000000000000000000000000000000000000000";
        var verifyRequest = new VerifyOriginProofRequest(proof, tamperedRoot, 0.60m);

        var result = _zkEngine.VerifyProof(verifyRequest);
        result.Should().Be(OriginVerificationStatus.RejectedTampered);
    }

    [Fact]
    public void VerifyProof_TamperedSignature_ReturnsInvalidSignature()
    {
        var items = new List<BomComponentItem>
        {
            new("CMP-1", "Wheat Semolina", 500m, 1000m, 10, TradeZone.DomesticAlgeria, "sup1", "sanc1")
        };
        var manifest = new BillOfMaterialsManifest("SKU-COUSCOUS-01", "BATCH-CC-1", items, DateTime.UtcNow);
        var salts = new List<string> { "salt_wheat" };

        var request = new GenerateOriginProofRequest(manifest, TradeZone.AfCFTA, 0.60m);
        var proof = _zkEngine.GenerateOriginProof(request, salts);

        var tamperedProof = proof with { CryptographicSignature = "badsignature12345678badsignature12345678badsignature12345678badsign" };
        var verifyRequest = new VerifyOriginProofRequest(tamperedProof, proof.PublicCommitmentRoot, 0.60m);

        var result = _zkEngine.VerifyProof(verifyRequest);
        result.Should().Be(OriginVerificationStatus.InvalidSignature);
    }

    #endregion

    #region Anti-Sanction Compliance Screening Tests

    [Fact]
    public void ScreenBlindedSuppliers_CompliantSuppliers_ReturnsCompliantWithNoViolations()
    {
        const string auditorSalt = "AuditSalt2026-X";
        var cleanSupplier1 = SanctionComplianceScreeningService.ComputeBlindedIdentifier("DZ-SUPPLIER-CLEAN-99", auditorSalt);
        var cleanSupplier2 = SanctionComplianceScreeningService.ComputeBlindedIdentifier("EU-SUPPLIER-VERIFIED-12", auditorSalt);

        var request = new SanctionScreeningRequest([cleanSupplier1, cleanSupplier2], auditorSalt);
        var result = _sanctionService.ScreenBlindedSuppliers(request);

        result.Status.Should().Be(SanctionScreeningStatus.Compliant);
        result.MatchDetected.Should().BeFalse();
        result.ViolatedSupplierHashes.Should().BeEmpty();
        result.TotalBlindedSuppliersScreened.Should().Be(2);
        result.CryptographicAuditProof.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ScreenBlindedSuppliers_SanctionedSupplierPresent_DetectsViolation()
    {
        const string auditorSalt = "AuditSalt2026-SanctionCheck";
        var cleanSupplier = SanctionComplianceScreeningService.ComputeBlindedIdentifier("DZ-LEGIT-FARM-55", auditorSalt);
        var blacklistedSupplier = SanctionComplianceScreeningService.ComputeBlindedIdentifier("OFAC-SDN-99412", auditorSalt);

        var request = new SanctionScreeningRequest([cleanSupplier, blacklistedSupplier], auditorSalt);
        var result = _sanctionService.ScreenBlindedSuppliers(request);

        result.Status.Should().Be(SanctionScreeningStatus.SanctionViolationDetected);
        result.MatchDetected.Should().BeTrue();
        result.ViolatedSupplierHashes.Should().ContainSingle()
            .Which.Should().Be(blacklistedSupplier);
    }

    [Fact]
    public void RegisterSanctionEntity_DynamicallyEnforcesBlacklist()
    {
        const string auditorSalt = "DynamicSanctionSalt-99";
        const string newlySanctionedId = "ROGUE-TRADER-999";

        // Verify not sanctioned yet
        var blinded = SanctionComplianceScreeningService.ComputeBlindedIdentifier(newlySanctionedId, auditorSalt);
        var initialScreening = _sanctionService.ScreenBlindedSuppliers(new SanctionScreeningRequest([blinded], auditorSalt));
        initialScreening.Status.Should().Be(SanctionScreeningStatus.Compliant);

        // Register sanction
        _sanctionService.RegisterSanctionEntity(newlySanctionedId, "UN-SECURITY-COUNCIL-NEW");

        // Re-screen
        var subsequentScreening = _sanctionService.ScreenBlindedSuppliers(new SanctionScreeningRequest([blinded], auditorSalt));
        subsequentScreening.Status.Should().Be(SanctionScreeningStatus.SanctionViolationDetected);
        subsequentScreening.MatchDetected.Should().BeTrue();
    }

    [Fact]
    public void ScreenBlindedSuppliers_EmptySalt_ThrowsArgumentException()
    {
        var act = () => _sanctionService.ScreenBlindedSuppliers(new SanctionScreeningRequest(["hash"], " "));
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Merkle DAG Provenance Tests

    [Fact]
    public void BuildProvenanceGraph_ValidNodes_ConstructsValidGraphWithRoot()
    {
        var timestamp = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);

        // Node 1: Raw Material
        var meta1 = new Dictionary<string, string> { ["Purity"] = "99.9%", ["MineLocation"] = "Ouenza, Tebessa" };
        var hash1 = VerifiableBOMProvenanceService.ComputeNodeHash(
            "NODE-RAW-01", ProvenanceNodeType.RawMaterialExtraction, "National Mining Corp", "DZ", [], "sha_payload_1", timestamp, meta1);
        var node1 = new ProvenanceDagNode("NODE-RAW-01", ProvenanceNodeType.RawMaterialExtraction, "National Mining Corp", "DZ", [], "sha_payload_1", hash1, timestamp, meta1);

        // Node 2: Smelting
        var meta2 = new Dictionary<string, string> { ["FurnaceTemp"] = "1650C" };
        var hash2 = VerifiableBOMProvenanceService.ComputeNodeHash(
            "NODE-SMELT-02", ProvenanceNodeType.RefiningAndSmelting, "El Hadjar Steelworks", "DZ", ["NODE-RAW-01"], "sha_payload_2", timestamp.AddHours(2), meta2);
        var node2 = new ProvenanceDagNode("NODE-SMELT-02", ProvenanceNodeType.RefiningAndSmelting, "El Hadjar Steelworks", "DZ", ["NODE-RAW-01"], "sha_payload_2", hash2, timestamp.AddHours(2), meta2);

        // Node 3: Quality Check
        var meta3 = new Dictionary<string, string> { ["ISOStandard"] = "ISO-17025", ["Passed"] = "True" };
        var hash3 = VerifiableBOMProvenanceService.ComputeNodeHash(
            "NODE-QC-03", ProvenanceNodeType.QualityInspection, "Algerian Standards Institute (IANOR)", "DZ", ["NODE-SMELT-02"], "sha_payload_3", timestamp.AddHours(4), meta3);
        var node3 = new ProvenanceDagNode("NODE-QC-03", ProvenanceNodeType.QualityInspection, "Algerian Standards Institute (IANOR)", "DZ", ["NODE-SMELT-02"], "sha_payload_3", hash3, timestamp.AddHours(4), meta3);

        var graph = _provenanceService.BuildProvenanceGraph("SKU-STEEL-BEAM-2026", "BATCH-EH-001", [node1, node2, node3]);

        graph.Should().NotBeNull();
        graph.MerkleRootHash.Should().NotBeNullOrWhiteSpace();
        graph.Nodes.Count.Should().Be(3);

        // Generate and verify inclusion proof for Node 1
        var proof1 = _provenanceService.GenerateInclusionProof(graph, "NODE-RAW-01");
        proof1.IsValid.Should().BeTrue();
        proof1.ExpectedRootHash.Should().Be(graph.MerkleRootHash);

        // Generate and verify inclusion proof for Node 3
        var proof3 = _provenanceService.GenerateInclusionProof(graph, "NODE-QC-03");
        proof3.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BuildProvenanceGraph_TamperedNodeHash_ThrowsInvalidOperationException()
    {
        var timestamp = DateTime.UtcNow;
        var fakeHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        var node = new ProvenanceDagNode("NODE-FAKE", ProvenanceNodeType.RawMaterialExtraction, "Fake Corp", "DZ", [], "payload", fakeHash, timestamp, new Dictionary<string, string>());

        var act = () => _provenanceService.BuildProvenanceGraph("SKU-1", "BATCH-1", [node]);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Node hash mismatch*");
    }

    [Fact]
    public void BuildProvenanceGraph_MissingParent_ThrowsInvalidOperationException()
    {
        var timestamp = DateTime.UtcNow;
        var meta = new Dictionary<string, string>();
        var hash = VerifiableBOMProvenanceService.ComputeNodeHash("NODE-1", ProvenanceNodeType.ComponentFabrication, "Corp", "DZ", ["NON-EXISTENT-PARENT"], "p", timestamp, meta);
        var node = new ProvenanceDagNode("NODE-1", ProvenanceNodeType.ComponentFabrication, "Corp", "DZ", ["NON-EXISTENT-PARENT"], "p", hash, timestamp, meta);

        var act = () => _provenanceService.BuildProvenanceGraph("SKU-1", "BATCH-1", [node]);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*references non-existent parent node*");
    }

    [Fact]
    public void VerifyInclusionProof_TamperedLeafHash_FailsVerification()
    {
        var auditPath = new List<string> { "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" };
        var isLeft = new List<bool> { false };
        var expectedRoot = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        var valid = VerifiableBOMProvenanceService.VerifyInclusionProof("0000000000000000000000000000000000000000000000000000000000000000", auditPath, isLeft, expectedRoot);
        valid.Should().BeFalse();
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddZkTraceabilityServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddZkTraceabilityServices();

        services.Should().Contain(d => d.ServiceType == typeof(ZeroKnowledgeOriginProofEngine) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(SanctionComplianceScreeningService) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(VerifiableBOMProvenanceService) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
