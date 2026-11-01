using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.ZeroKnowledge;
using Xunit;

namespace SGrowing.Tests.Security;

/// <summary>
/// Unit tests for Phase 27: Decentralized Zero-Knowledge (ZK) Proofs for
/// Private B2B Invoicing & Proof-of-Solvency on .NET 10 / C# 14.
/// </summary>
public class ZeroKnowledgeSolvencyTests
{
    private readonly ZkSolvencyEngine _engine = new();
    private readonly ZkPrivateInvoiceService _invoiceService;
    private readonly MerkleSolvencyLedgerService _merkleLedger = new();

    public ZeroKnowledgeSolvencyTests()
    {
        _invoiceService = new ZkPrivateInvoiceService(_engine);
        _merkleLedger.Clear();
    }

    // ==========================================
    // 1. Pedersen Commitment Tests
    // ==========================================

    [Fact]
    public void Commit_ValidValue_GeneratesNonEmptyCommitmentAndBlindingFactor()
    {
        // Act
        var commitment = _engine.Commit(150_000.50m);

        // Assert
        commitment.Should().NotBeNull();
        commitment.CommitmentHex.Should().NotBeNullOrWhiteSpace();
        commitment.BlindingFactorHex.Should().NotBeNullOrWhiteSpace();
        commitment.GeneratorG.Should().Be("2");
        commitment.GeneratorH.Should().NotBeNullOrWhiteSpace();
        commitment.CommittedValueHidden.Should().BeTrue();
    }

    [Fact]
    public void Commit_WithExplicitBlindingFactor_ProducesDeterministicCommitment()
    {
        // Arrange
        const decimal value = 75_000m;
        const string blindingFactor = "ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890";

        // Act
        var c1 = _engine.Commit(value, blindingFactor);
        var c2 = _engine.Commit(value, blindingFactor);

        // Assert
        c1.CommitmentHex.Should().Be(c2.CommitmentHex);
        c1.BlindingFactorHex.Should().Be(blindingFactor);
    }

    [Fact]
    public void VerifyCommitment_ValidOpening_ReturnsTrue()
    {
        // Arrange
        const decimal value = 42_500.25m;
        var commitment = _engine.Commit(value);

        // Act
        var isValid = _engine.VerifyCommitment(
            commitment.CommitmentHex,
            value,
            commitment.BlindingFactorHex);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyCommitment_WrongValue_ReturnsFalse()
    {
        // Arrange
        var commitment = _engine.Commit(10_000m);

        // Act
        var isValid = _engine.VerifyCommitment(
            commitment.CommitmentHex,
            10_001m, // Incorrect value
            commitment.BlindingFactorHex);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyCommitment_WrongBlindingFactor_ReturnsFalse()
    {
        // Arrange
        var commitment = _engine.Commit(25_000m);
        const string wrongBlinding = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";

        // Act
        var isValid = _engine.VerifyCommitment(
            commitment.CommitmentHex,
            25_000m,
            wrongBlinding);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyCommitment_MalformedOrEmptyInput_ReturnsFalse()
    {
        _engine.VerifyCommitment(string.Empty, 100m, "123").Should().BeFalse();
        _engine.VerifyCommitment("123", 100m, string.Empty).Should().BeFalse();
        _engine.VerifyCommitment("NOT_HEX", 100m, "123").Should().BeFalse();
    }

    // ==========================================
    // 2. Homomorphic Commitment Addition & Subtraction Tests
    // ==========================================

    [Fact]
    public void AddCommitments_HomomorphicSum_OpensToSumOfValuesAndBlindingFactors()
    {
        // Arrange: C(v1) * C(v2) = C(v1 + v2)
        const decimal v1 = 120_000m;
        const decimal v2 = 80_000m;
        const decimal expectedSum = 200_000m;

        var c1 = _engine.Commit(v1);
        var c2 = _engine.Commit(v2);

        // Act
        var cSum = _engine.AddCommitments(c1, c2);

        // Assert
        var isValidOpening = _engine.VerifyCommitment(
            cSum.CommitmentHex,
            expectedSum,
            cSum.BlindingFactorHex);

        isValidOpening.Should().BeTrue("homomorphic multiplication of commitments must open to the sum of secret values");
    }

    [Fact]
    public void AddCommitments_MultipleChainedCommitments_PreservesHomomorphicAdditiveProperty()
    {
        // Arrange
        var c1 = _engine.Commit(10_000m);
        var c2 = _engine.Commit(20_000m);
        var c3 = _engine.Commit(30_000m);

        // Act
        var sum12 = _engine.AddCommitments(c1, c2);
        var totalSum = _engine.AddCommitments(sum12, c3);

        // Assert
        var isValid = _engine.VerifyCommitment(
            totalSum.CommitmentHex,
            60_000m,
            totalSum.BlindingFactorHex);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void CombineCommitmentsHex_MultipliesPointsModuloP()
    {
        // Arrange
        var c1 = _engine.Commit(500m);
        var c2 = _engine.Commit(700m);

        // Act
        var combinedHex = _engine.CombineCommitmentsHex(c1.CommitmentHex, c2.CommitmentHex);
        var expectedSumCommitment = _engine.AddCommitments(c1, c2);

        // Assert
        combinedHex.Should().Be(expectedSumCommitment.CommitmentHex);
    }

    [Fact]
    public void SubtractCommitmentsHex_HomomorphicSubtraction_MatchesDifferenceCommitment()
    {
        // Arrange: C(v1) * C(v2)^-1 = C(v1 - v2)
        const decimal v1 = 50_000m;
        const decimal v2 = 20_000m;
        const decimal expectedDiff = 30_000m;

        var c1 = _engine.Commit(v1);
        var c2 = _engine.Commit(v2);

        // Act
        var diffHex = _engine.SubtractCommitmentsHex(c1.CommitmentHex, c2.CommitmentHex);

        // Reconstruct expected difference commitment directly
        var r1 = BigInteger.Parse("00" + c1.BlindingFactorHex, System.Globalization.NumberStyles.HexNumber);
        var r2 = BigInteger.Parse("00" + c2.BlindingFactorHex, System.Globalization.NumberStyles.HexNumber);
        var (g, h, p, q) = _engine.GetGroupParameters();
        var diffR = ((r1 - r2) % q + q) % q;

        var diffCommitment = _engine.Commit(expectedDiff, diffR.ToString("X"));

        // Assert
        diffHex.Should().Be(diffCommitment.CommitmentHex);
    }

    // ==========================================
    // 3. Bulletproof-Style Range Proof Tests
    // ==========================================

    [Fact]
    public void GenerateRangeProof_ValidInRangeValue_GeneratesVerifiableProof()
    {
        // Arrange
        const decimal value = 750_000m;
        const decimal lowerBound = 0m;
        const decimal upperBound = 1_000_000m;
        var commitment = _engine.Commit(value);

        // Act
        var proof = _engine.GenerateRangeProof(
            value,
            commitment.BlindingFactorHex,
            lowerBound,
            upperBound);

        // Assert
        proof.Should().NotBeNull();
        proof.ProofId.Should().StartWith("proof_");
        proof.CommitmentHex.Should().Be(commitment.CommitmentHex);
        proof.LowerBound.Should().Be(lowerBound);
        proof.UpperBound.Should().Be(upperBound);
        proof.ProofBytesBase64.Should().NotBeNullOrWhiteSpace();
        proof.VerificationHash.Should().NotBeNullOrWhiteSpace();

        var isVerified = _engine.VerifyRangeProof(proof);
        isVerified.Should().BeTrue("valid in-range balance proof must verify strictly over public parameters");
    }

    [Fact]
    public void GenerateRangeProof_ValueBelowLowerBound_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const decimal value = 50m;
        const decimal lowerBound = 100m;
        const decimal upperBound = 1_000m;
        var commitment = _engine.Commit(value);

        // Act & Assert
        var act = () => _engine.GenerateRangeProof(
            value,
            commitment.BlindingFactorHex,
            lowerBound,
            upperBound);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*outside range*");
    }

    [Fact]
    public void GenerateRangeProof_ValueAboveUpperBound_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const decimal value = 2_500_000m;
        const decimal lowerBound = 0m;
        const decimal upperBound = 1_000_000m;
        var commitment = _engine.Commit(value);

        // Act & Assert
        var act = () => _engine.GenerateRangeProof(
            value,
            commitment.BlindingFactorHex,
            lowerBound,
            upperBound);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*outside range*");
    }

    [Fact]
    public void GenerateRangeProof_InvertedBounds_ThrowsArgumentException()
    {
        // Arrange
        var commitment = _engine.Commit(500m);

        // Act & Assert
        var act = () => _engine.GenerateRangeProof(
            500m,
            commitment.BlindingFactorHex,
            1_000m, // lowerBound > upperBound
            100m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be less than lower bound*");
    }

    [Fact]
    public void VerifyRangeProof_TamperedProofBytes_ReturnsFalse()
    {
        // Arrange
        var commitment = _engine.Commit(50_000m);
        var proof = _engine.GenerateRangeProof(50_000m, commitment.BlindingFactorHex, 0m, 100_000m);

        // Tamper with base64 payload
        var tamperedBytes = Convert.FromBase64String(proof.ProofBytesBase64);
        tamperedBytes[10] ^= 0xFF;
        var tamperedProof = proof with { ProofBytesBase64 = Convert.ToBase64String(tamperedBytes) };

        // Act
        var isVerified = _engine.VerifyRangeProof(tamperedProof);

        // Assert
        isVerified.Should().BeFalse("tampered proof payload must fail verification");
    }

    [Fact]
    public void VerifyRangeProof_TamperedCommitmentHex_ReturnsFalse()
    {
        // Arrange
        var commitment = _engine.Commit(50_000m);
        var proof = _engine.GenerateRangeProof(50_000m, commitment.BlindingFactorHex, 0m, 100_000m);

        var tamperedProof = proof with { CommitmentHex = "1234567890ABCDEF" };

        // Act
        var isVerified = _engine.VerifyRangeProof(tamperedProof);

        // Assert
        isVerified.Should().BeFalse("tampered commitment must fail range verification");
    }

    [Fact]
    public void VerifyRangeProof_TamperedLowerBound_ReturnsFalse()
    {
        // Arrange
        var commitment = _engine.Commit(50_000m);
        var proof = _engine.GenerateRangeProof(50_000m, commitment.BlindingFactorHex, 0m, 100_000m);

        var tamperedProof = proof with { LowerBound = 10_000m };

        // Act
        var isVerified = _engine.VerifyRangeProof(tamperedProof);

        // Assert
        isVerified.Should().BeFalse();
    }

    [Fact]
    public void VerifyRangeProof_TamperedUpperBound_ReturnsFalse()
    {
        // Arrange
        var commitment = _engine.Commit(50_000m);
        var proof = _engine.GenerateRangeProof(50_000m, commitment.BlindingFactorHex, 0m, 100_000m);

        var tamperedProof = proof with { UpperBound = 200_000m };

        // Act
        var isVerified = _engine.VerifyRangeProof(tamperedProof);

        // Assert
        isVerified.Should().BeFalse();
    }

    [Fact]
    public void VerifyRangeProof_TamperedVerificationHash_ReturnsFalse()
    {
        // Arrange
        var commitment = _engine.Commit(50_000m);
        var proof = _engine.GenerateRangeProof(50_000m, commitment.BlindingFactorHex, 0m, 100_000m);

        var tamperedProof = proof with { VerificationHash = "DEADBEEF12345678" };

        // Act
        var isVerified = _engine.VerifyRangeProof(tamperedProof);

        // Assert
        isVerified.Should().BeFalse();
    }

    [Fact]
    public void VerifyRangeProof_NullOrEmptyProof_ReturnsFalse()
    {
        _engine.VerifyRangeProof(null!).Should().BeFalse();
    }

    // ==========================================
    // 4. Zero-Knowledge Solvency Proof Tests
    // ==========================================

    [Fact]
    public void GenerateSolvencyProof_SolventTenant_GeneratesCertifiedStatement()
    {
        // Arrange: Assets (5,000,000) > Liabilities (3,000,000)
        const string tenantId = "tenant_alpha";
        const decimal totalAssets = 5_000_000m;
        const decimal totalLiabilities = 3_000_000m;

        // Act
        var statement = _engine.GenerateSolvencyProof(
            tenantId,
            totalAssets,
            totalLiabilities,
            requiredReserveRatio: 1.0);

        // Assert
        statement.Should().NotBeNull();
        statement.TenantId.Should().Be(tenantId);
        statement.TotalAssetsCommitment.Should().NotBeNullOrWhiteSpace();
        statement.TotalLiabilitiesCommitment.Should().NotBeNullOrWhiteSpace();
        statement.ProofOfSolvencyHex.Should().NotBeNullOrWhiteSpace();
        statement.IsVerified.Should().BeTrue();

        var isVerified = _engine.VerifySolvencyStatement(statement);
        isVerified.Should().BeTrue("verified solvency proof must pass verification purely over commitments");
    }

    [Fact]
    public void GenerateSolvencyProof_ExactlyBalancedTenant_GeneratesCertifiedStatement()
    {
        // Arrange: Assets == Liabilities (Net Solvency = 0)
        const string tenantId = "tenant_breakeven";
        const decimal totalAssets = 2_500_000m;
        const decimal totalLiabilities = 2_500_000m;

        // Act
        var statement = _engine.GenerateSolvencyProof(
            tenantId,
            totalAssets,
            totalLiabilities,
            requiredReserveRatio: 1.0);

        // Assert
        statement.IsVerified.Should().BeTrue();
        _engine.VerifySolvencyStatement(statement).Should().BeTrue();
    }

    [Fact]
    public void GenerateSolvencyProof_InsolventTenant_ThrowsInvalidOperationException()
    {
        // Arrange: Assets (1,000,000) < Liabilities (2,000,000)
        const string tenantId = "tenant_insolvent";
        const decimal totalAssets = 1_000_000m;
        const decimal totalLiabilities = 2_000_000m;

        // Act & Assert
        var act = () => _engine.GenerateSolvencyProof(
            tenantId,
            totalAssets,
            totalLiabilities,
            requiredReserveRatio: 1.0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*insolvent*");
    }

    [Fact]
    public void GenerateSolvencyProof_CustomReserveRatio_EnforcesCollateralRequirement()
    {
        // Arrange: Assets = 1,100,000, Liabilities = 1,000,000
        // At 100% reserve: 1,100,000 >= 1,000,000 -> SOLVENT
        // At 125% reserve: 1,100,000 < 1,250,000 -> INSOLVENT
        const string tenantId = "tenant_margin";
        const decimal assets = 1_100_000m;
        const decimal liabilities = 1_000_000m;

        // Act 1: 100% reserve succeeds
        var solventStatement = _engine.GenerateSolvencyProof(
            tenantId,
            assets,
            liabilities,
            requiredReserveRatio: 1.0);

        solventStatement.IsVerified.Should().BeTrue();
        _engine.VerifySolvencyStatement(solventStatement).Should().BeTrue();

        // Act 2: 125% reserve fails
        var actInsolvent = () => _engine.GenerateSolvencyProof(
            tenantId,
            assets,
            liabilities,
            requiredReserveRatio: 1.25);

        actInsolvent.Should().Throw<InvalidOperationException>()
            .WithMessage("*insolvent*");
    }

    [Fact]
    public void VerifySolvencyStatement_TamperedAssetsCommitment_FailsVerification()
    {
        // Arrange
        var statement = _engine.GenerateSolvencyProof("tenant_audit", 1_000_000m, 500_000m);
        var tampered = statement with { TotalAssetsCommitment = "FEDCBA0987654321" };

        // Act
        var isValid = _engine.VerifySolvencyStatement(tampered);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySolvencyStatement_TamperedLiabilitiesCommitment_FailsVerification()
    {
        // Arrange
        var statement = _engine.GenerateSolvencyProof("tenant_audit", 1_000_000m, 500_000m);
        var tampered = statement with { TotalLiabilitiesCommitment = "1234567890ABCDEF" };

        // Act
        var isValid = _engine.VerifySolvencyStatement(tampered);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySolvencyStatement_TamperedProofOfSolvencyHex_FailsVerification()
    {
        // Arrange
        var statement = _engine.GenerateSolvencyProof("tenant_audit", 1_000_000m, 500_000m);
        var tampered = statement with { ProofOfSolvencyHex = "DEADBEEFCAFE" };

        // Act
        var isValid = _engine.VerifySolvencyStatement(tampered);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySolvencyStatement_TamperedReserveRatio_FailsVerification()
    {
        // Arrange
        var statement = _engine.GenerateSolvencyProof("tenant_audit", 1_000_000m, 500_000m, requiredReserveRatio: 1.0);
        var tampered = statement with { RequiredReserveRatio = 1.5 };

        // Act
        var isValid = _engine.VerifySolvencyStatement(tampered);

        // Assert
        isValid.Should().BeFalse();
    }

    // ==========================================
    // 5. Private B2B Invoice Tests
    // ==========================================

    [Fact]
    public void CreatePrivateInvoice_ValidParameters_GeneratesConcealedRecord()
    {
        // Arrange
        var request = new CreatePrivateInvoiceRequest
        {
            MerchantId = "merchant_nike_corp",
            SupplierId = "supplier_logistics_gmbh",
            Amount = 145_000.75m,
            DueDateUtc = DateTime.UtcNow.AddDays(30),
            PurchaseOrderRef = "PO-2026-9988",
            POAmount = 145_000.75m
        };

        // Act
        var invoice = _invoiceService.CreatePrivateInvoice(request);

        // Assert
        invoice.Should().NotBeNull();
        invoice.InvoiceHash.Should().NotBeNullOrWhiteSpace();
        invoice.MerchantCommitment.Should().NotBeNullOrWhiteSpace();
        invoice.MerchantCommitment.Should().NotContain("nike_corp", "merchant ID must be blinded");
        invoice.SupplierCommitment.Should().NotBeNullOrWhiteSpace();
        invoice.SupplierCommitment.Should().NotContain("logistics_gmbh", "supplier ID must be blinded");
        invoice.AmountCommitment.Should().NotBeNullOrWhiteSpace();
        invoice.DueDateCommitment.Should().NotBeNullOrWhiteSpace();
        invoice.IsSettled.Should().BeFalse();
    }

    [Fact]
    public void VerifyPrivateInvoice_MatchingPurchaseOrder_SucceedsWithoutDisclosingPrice()
    {
        // Arrange
        var request = new CreatePrivateInvoiceRequest
        {
            MerchantId = "merchant_1",
            SupplierId = "supplier_1",
            Amount = 88_000m,
            DueDateUtc = DateTime.UtcNow.AddDays(15),
            PurchaseOrderRef = "PO-1001",
            POAmount = 88_000m
        };
        var invoice = _invoiceService.CreatePrivateInvoice(request);

        var verifyRequest = new VerifyPrivateInvoiceRequest
        {
            InvoiceHash = invoice.InvoiceHash,
            PurchaseOrderCommitmentHex = invoice.PurchaseOrderCommitment!
        };

        // Act
        var response = _invoiceService.VerifyPrivateInvoice(verifyRequest);

        // Assert
        response.IsValid.Should().BeTrue();
        response.MatchesPurchaseOrder.Should().BeTrue();
        response.InvoiceHash.Should().Be(invoice.InvoiceHash);
    }

    [Fact]
    public void VerifyPrivateInvoice_MismatchedPurchaseOrderAmount_FailsVerification()
    {
        // Arrange: Invoice Amount (50,000) != PO Amount (45,000)
        var request = new CreatePrivateInvoiceRequest
        {
            MerchantId = "merchant_2",
            SupplierId = "supplier_2",
            Amount = 50_000m,
            DueDateUtc = DateTime.UtcNow.AddDays(10),
            PurchaseOrderRef = "PO-1002",
            POAmount = 45_000m
        };
        var invoice = _invoiceService.CreatePrivateInvoice(request);

        // Generate different PO commitment
        var differentPoCommitment = _engine.Commit(45_000m);

        var verifyRequest = new VerifyPrivateInvoiceRequest
        {
            InvoiceHash = invoice.InvoiceHash,
            PurchaseOrderCommitmentHex = differentPoCommitment.CommitmentHex
        };

        // Act
        var response = _invoiceService.VerifyPrivateInvoice(verifyRequest);

        // Assert
        response.IsValid.Should().BeFalse();
        response.MatchesPurchaseOrder.Should().BeFalse();
    }

    [Fact]
    public void VerifyPrivateInvoice_UnknownInvoiceHash_ReturnsInvalid()
    {
        // Arrange
        var verifyRequest = new VerifyPrivateInvoiceRequest
        {
            InvoiceHash = "NON_EXISTENT_INVOICE_HASH",
            PurchaseOrderCommitmentHex = "1234"
        };

        // Act
        var response = _invoiceService.VerifyPrivateInvoice(verifyRequest);

        // Assert
        response.IsValid.Should().BeFalse();
        response.Reason.Should().Contain("not found");
    }

    [Fact]
    public void SettleInvoice_ValidSettlementProof_MarksInvoiceSettledAndHidesBankRib()
    {
        // Arrange
        var invoice = _invoiceService.CreatePrivateInvoice(new CreatePrivateInvoiceRequest
        {
            MerchantId = "merchant_3",
            SupplierId = "supplier_3",
            Amount = 120_000m,
            DueDateUtc = DateTime.UtcNow.AddDays(20),
            PurchaseOrderRef = "PO-1003",
            POAmount = 120_000m
        });

        const string paymentProofHex = "A1B2C3D4E5F60718293A4B5C6D7E8F90";
        var settleRequest = new SettlePrivateInvoiceRequest
        {
            InvoiceHash = invoice.InvoiceHash,
            SettledAmount = 120_000m,
            PaymentProofHex = paymentProofHex,
            MaskedBankRib = "FR76 **** **** **** 4321"
        };

        // Act
        var settled = _invoiceService.SettleInvoice(settleRequest);
        var retrieved = _invoiceService.GetInvoiceByHash(invoice.InvoiceHash);

        // Assert
        settled.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.IsSettled.Should().BeTrue();
        retrieved.PaymentProofHex.Should().Be(paymentProofHex);
    }

    [Fact]
    public void SettleInvoice_MismatchedSettledAmount_RejectsSettlement()
    {
        // Arrange
        var invoice = _invoiceService.CreatePrivateInvoice(new CreatePrivateInvoiceRequest
        {
            MerchantId = "merchant_4",
            SupplierId = "supplier_4",
            Amount = 100_000m,
            DueDateUtc = DateTime.UtcNow.AddDays(5),
            PurchaseOrderRef = "PO-1004",
            POAmount = 100_000m
        });

        var settleRequest = new SettlePrivateInvoiceRequest
        {
            InvoiceHash = invoice.InvoiceHash,
            SettledAmount = 90_000m, // Incorrect settled amount
            PaymentProofHex = "A1B2C3D4E5F60718293A4B5C6D7E8F90"
        };

        // Act
        var settled = _invoiceService.SettleInvoice(settleRequest);

        // Assert
        settled.Should().BeFalse();
        var retrieved = _invoiceService.GetInvoiceByHash(invoice.InvoiceHash);
        retrieved!.IsSettled.Should().BeFalse();
    }

    [Fact]
    public void SettleInvoice_EmptyPaymentProof_RejectsSettlement()
    {
        // Arrange
        var invoice = _invoiceService.CreatePrivateInvoice(new CreatePrivateInvoiceRequest
        {
            MerchantId = "merchant_5",
            SupplierId = "supplier_5",
            Amount = 10_000m,
            DueDateUtc = DateTime.UtcNow.AddDays(5),
            PurchaseOrderRef = "PO-1005",
            POAmount = 10_000m
        });

        var settleRequest = new SettlePrivateInvoiceRequest
        {
            InvoiceHash = invoice.InvoiceHash,
            SettledAmount = 10_000m,
            PaymentProofHex = "SHORT" // Invalid proof
        };

        // Act
        var settled = _invoiceService.SettleInvoice(settleRequest);

        // Assert
        settled.Should().BeFalse();
    }

    [Fact]
    public void GetInvoiceByHash_UnknownHash_ReturnsNull()
    {
        _invoiceService.GetInvoiceByHash("unknown_hash").Should().BeNull();
    }

    // ==========================================
    // 6. Merkle Solvency Ledger & Inclusion Proof Tests
    // ==========================================

    [Fact]
    public void MerkleLedger_EmptyTree_ReturnsValidEmptyRoot()
    {
        var root = _merkleLedger.GetSolvencyRoot();
        root.Should().NotBeNullOrWhiteSpace();
        root.Length.Should().Be(64, "SHA-256 hex root hash must be 64 characters");
    }

    [Fact]
    public void MerkleLedger_SingleTenant_TreeRootMatchesLeafHash()
    {
        // Arrange
        var statement = _engine.GenerateSolvencyProof("single_tenant", 1_000_000m, 600_000m);

        // Act
        _merkleLedger.RegisterSolvencyStatement(statement);
        var proof = _merkleLedger.GenerateInclusionProof("single_tenant");

        // Assert
        proof.Should().NotBeNull();
        proof.RootHash.Should().Be(proof.LeafHash);
        proof.MerklePath.Should().BeEmpty();
        _merkleLedger.VerifyInclusionProof(proof).Should().BeTrue();
    }

    [Fact]
    public void MerkleLedger_MultipleTenants_GeneratesValidInclusionProofForEveryTenant()
    {
        // Arrange: Register 4 tenants
        var tenants = new[] { "tenant_01", "tenant_02", "tenant_03", "tenant_04" };
        foreach (var t in tenants)
        {
            var stmt = _engine.GenerateSolvencyProof(t, 2_000_000m, 1_000_000m);
            _merkleLedger.RegisterSolvencyStatement(stmt);
        }

        var globalRoot = _merkleLedger.GetSolvencyRoot();

        // Act & Assert for each tenant
        foreach (var t in tenants)
        {
            var proof = _merkleLedger.GenerateInclusionProof(t);
            proof.RootHash.Should().Be(globalRoot);
            proof.MerklePath.Should().NotBeEmpty();

            var isValid = _merkleLedger.VerifyInclusionProof(proof);
            isValid.Should().BeTrue($"tenant {t} must independently verify inclusion against global solvency root");
        }
    }

    [Fact]
    public void MerkleLedger_OddNumberOfTenants_GeneratesVerifiableTree()
    {
        // Arrange: 3 tenants (odd count, requires duplication of last branch)
        var tenants = new[] { "tenant_a", "tenant_b", "tenant_c" };
        foreach (var t in tenants)
        {
            var stmt = _engine.GenerateSolvencyProof(t, 500_000m, 200_000m);
            _merkleLedger.RegisterSolvencyStatement(stmt);
        }

        // Act & Assert
        foreach (var t in tenants)
        {
            var proof = _merkleLedger.GenerateInclusionProof(t);
            _merkleLedger.VerifyInclusionProof(proof).Should().BeTrue();
        }
    }

    [Fact]
    public void MerkleLedger_TamperedLeafHash_FailsVerification()
    {
        // Arrange
        var stmt1 = _engine.GenerateSolvencyProof("t1", 1_000_000m, 500_000m);
        var stmt2 = _engine.GenerateSolvencyProof("t2", 2_000_000m, 800_000m);
        _merkleLedger.RegisterSolvencyStatement(stmt1);
        _merkleLedger.RegisterSolvencyStatement(stmt2);

        var proof = _merkleLedger.GenerateInclusionProof("t1");
        var tamperedProof = proof with { LeafHash = "DEADBEEF00000000DEADBEEF00000000DEADBEEF00000000DEADBEEF00000000" };

        // Act
        var isValid = _merkleLedger.VerifyInclusionProof(tamperedProof);

        // Assert
        isValid.Should().BeFalse("tampered leaf hash must fail Merkle inclusion verification");
    }

    [Fact]
    public void MerkleLedger_TamperedRootHash_FailsVerification()
    {
        // Arrange
        var stmt1 = _engine.GenerateSolvencyProof("t1", 1_000_000m, 500_000m);
        var stmt2 = _engine.GenerateSolvencyProof("t2", 2_000_000m, 800_000m);
        _merkleLedger.RegisterSolvencyStatement(stmt1);
        _merkleLedger.RegisterSolvencyStatement(stmt2);

        var proof = _merkleLedger.GenerateInclusionProof("t1");
        var tamperedProof = proof with { RootHash = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF" };

        // Act
        var isValid = _merkleLedger.VerifyInclusionProof(tamperedProof);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void MerkleLedger_TamperedSiblingInPath_FailsVerification()
    {
        // Arrange
        var stmt1 = _engine.GenerateSolvencyProof("t1", 1_000_000m, 500_000m);
        var stmt2 = _engine.GenerateSolvencyProof("t2", 2_000_000m, 800_000m);
        _merkleLedger.RegisterSolvencyStatement(stmt1);
        _merkleLedger.RegisterSolvencyStatement(stmt2);

        var proof = _merkleLedger.GenerateInclusionProof("t1");
        var tamperedPath = proof.MerklePath.Select(p => p[..2] + "0000000000000000").ToList();
        var tamperedProof = proof with { MerklePath = tamperedPath };

        // Act
        var isValid = _merkleLedger.VerifyInclusionProof(tamperedProof);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void MerkleLedger_TamperedAuditIndex_FailsVerification()
    {
        // Arrange
        var stmt1 = _engine.GenerateSolvencyProof("t1", 1_000_000m, 500_000m);
        var stmt2 = _engine.GenerateSolvencyProof("t2", 2_000_000m, 800_000m);
        _merkleLedger.RegisterSolvencyStatement(stmt1);
        _merkleLedger.RegisterSolvencyStatement(stmt2);

        var proof = _merkleLedger.GenerateInclusionProof("t1");
        var tamperedProof = proof with { AuditIndex = 99 };

        // Act
        var isValid = _merkleLedger.VerifyInclusionProof(tamperedProof);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void MerkleLedger_GenerateInclusionProof_UnknownTenant_ThrowsKeyNotFoundException()
    {
        var act = () => _merkleLedger.GenerateInclusionProof("non_existent_tenant");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void MerkleLedger_RegisterNewTenant_DeterministicallyUpdatesRoot()
    {
        // Arrange
        var stmt1 = _engine.GenerateSolvencyProof("tenant_a", 1_000_000m, 500_000m);
        _merkleLedger.RegisterSolvencyStatement(stmt1);
        var root1 = _merkleLedger.GetSolvencyRoot();

        // Act
        var stmt2 = _engine.GenerateSolvencyProof("tenant_b", 2_000_000m, 800_000m);
        _merkleLedger.RegisterSolvencyStatement(stmt2);
        var root2 = _merkleLedger.GetSolvencyRoot();

        // Assert
        root2.Should().NotBe(root1, "adding a new verified statement must deterministically update Merkle root");
    }

    // ==========================================
    // 7. Engine Health & System Metrics Tests
    // ==========================================

    [Fact]
    public void GetHealth_ReturnsOperationalStatusAndSecurityBits()
    {
        // Act
        var health = _engine.GetHealth();

        // Assert
        health.Should().NotBeNull();
        health.Status.Should().Be("HEALTHY");
        health.PrimeGroup.Should().Be("RFC-3526-MODP-2048");
        health.SecurityBits.Should().Be(112);
        health.TotalCommitmentsCreated.Should().BeGreaterThanOrEqualTo(0);
        health.TotalProofsGenerated.Should().BeGreaterThanOrEqualTo(0);
    }

    // ==========================================
    // 8. Thread-Safety & Concurrency Tests
    // ==========================================

    [Fact]
    public void ConcurrentOperations_CommitmentsAndLedgerUpdates_ExecuteSafely()
    {
        // Arrange
        const int threadCount = 8;
        const int opsPerThread = 15;
        _merkleLedger.Clear();

        // Act
        Parallel.For(0, threadCount, t =>
        {
            for (int i = 0; i < opsPerThread; i++)
            {
                var val = 10_000m + (t * 100) + i;
                var commitment = _engine.Commit(val);
                _engine.VerifyCommitment(commitment.CommitmentHex, val, commitment.BlindingFactorHex).Should().BeTrue();

                var tenantId = $"concurrent_tenant_{t}_{i}";
                var stmt = _engine.GenerateSolvencyProof(tenantId, val * 2, val);
                _merkleLedger.RegisterSolvencyStatement(stmt);
            }
        });

        // Assert
        var summary = _merkleLedger.GetLedgerSummary();
        summary.TotalTenants.Should().Be(threadCount * opsPerThread);
        summary.RootHash.Should().NotBeNullOrWhiteSpace();
    }

    // ==========================================
    // 9. Dependency Injection Registration Tests
    // ==========================================

    [Fact]
    public void AddZeroKnowledgeSolvencyServices_RegistersServicesInServiceCollection_WithoutBuildingProvider()
    {
        // Arrange: Strict Rule: Do not call services.BuildServiceProvider() in tests.
        // Direct ServiceDescriptor assertion on ServiceCollection is required.
        var services = new ServiceCollection();

        // Act
        services.AddZeroKnowledgeSolvencyServices();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(IZkSolvencyEngine) &&
                                        sd.ImplementationType == typeof(ZkSolvencyEngine) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd => sd.ServiceType == typeof(IZkPrivateInvoiceService) &&
                                        sd.ImplementationType == typeof(ZkPrivateInvoiceService) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(sd => sd.ServiceType == typeof(IMerkleSolvencyLedgerService) &&
                                        sd.ImplementationType == typeof(MerkleSolvencyLedgerService) &&
                                        sd.Lifetime == ServiceLifetime.Singleton);
    }
}
