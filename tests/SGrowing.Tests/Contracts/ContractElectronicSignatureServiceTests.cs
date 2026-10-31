using SGrowing.Api.Modules.Contracts;
using Xunit;

namespace SGrowing.Tests.Contracts;

public class ContractElectronicSignatureServiceTests
{
    private readonly ContractElectronicSignatureService _service;

    public ContractElectronicSignatureServiceTests()
    {
        _service = new ContractElectronicSignatureService();
    }

    [Fact]
    public async Task SeedData_ContainsFourDiverseContracts()
    {
        var contracts = await _service.GetAllContractsAsync();

        Assert.NotNull(contracts);
        Assert.True(contracts.Count >= 4);

        Assert.Contains(contracts, c => c.AgreementType == "Enterprise Cloud License");
        Assert.Contains(contracts, c => c.AgreementType == "Global Distribution Agreement");
        Assert.Contains(contracts, c => c.AgreementType == "B2B Terms of Service");
        Assert.Contains(contracts, c => c.AgreementType == "Confidentiality NDA");
    }

    [Fact]
    public async Task SeedData_EnterpriseCloudLicense_HasExpectedSignersAndFields()
    {
        var contractId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var contract = await _service.GetContractByIdAsync(contractId);

        Assert.NotNull(contract);
        Assert.Equal(ContractWorkflowStatus.AwaitingSigner1, contract.Status);
        Assert.Equal(3, contract.Pages.Count);
        Assert.Equal(3, contract.Signers.Count);
        Assert.Equal(ContractSignerRole.ClientSigner, contract.Signers[0].Role);
        Assert.Equal(ContractSignerRole.VendorSigner, contract.Signers[1].Role);
        Assert.Equal(ContractSignerRole.Witness, contract.Signers[2].Role);

        Assert.Equal(SignerSigningStatus.ReadyToSign, contract.Signers[0].Status);
        Assert.Equal(SignerSigningStatus.Pending, contract.Signers[1].Status);
        Assert.Equal(SignerSigningStatus.Pending, contract.Signers[2].Status);

        Assert.True(contract.Fields.Count >= 5);
        Assert.Contains(contract.Fields, f => f.FieldType == ContractFieldType.Signature);
        Assert.Contains(contract.Fields, f => f.FieldType == ContractFieldType.Initials);
        Assert.Contains(contract.Fields, f => f.FieldType == ContractFieldType.DateSigned);
    }

    [Fact]
    public async Task SeedData_B2BTermsOfService_IsFullyExecutedWithSha256Seal()
    {
        var contractId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var contract = await _service.GetContractByIdAsync(contractId);

        Assert.NotNull(contract);
        Assert.Equal(ContractWorkflowStatus.FullyExecuted, contract.Status);
        Assert.NotNull(contract.CompletedAtUtc);
        Assert.NotNull(contract.IntegritySealHash);
        Assert.Equal(64, contract.IntegritySealHash.Length);

        Assert.All(contract.Signers, s => Assert.Equal(SignerSigningStatus.Signed, s.Status));
    }

    [Theory]
    [InlineData(0.1, 0.2, 0.3, 0.4)]
    [InlineData(0.0, 0.0, 1.0, 1.0)]
    [InlineData(0.5, 0.5, 0.5, 0.5)]
    [InlineData(0.8, 0.8, 0.2, 0.2)]
    public void ValidateNormalizedCoordinates_ValidBounds_ReturnsTrue(double x, double y, double w, double h)
    {
        var valid = _service.ValidateNormalizedCoordinates(x, y, w, h, out var error);
        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateNormalizedCoordinates_NegativeX_ReturnsFalse()
    {
        var valid = _service.ValidateNormalizedCoordinates(-0.05, 0.1, 0.3, 0.2, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("xNorm", error);
    }

    [Fact]
    public void ValidateNormalizedCoordinates_NegativeY_ReturnsFalse()
    {
        var valid = _service.ValidateNormalizedCoordinates(0.1, -0.01, 0.3, 0.2, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("yNorm", error);
    }

    [Fact]
    public void ValidateNormalizedCoordinates_WidthExceedsBounds_ReturnsFalse()
    {
        var valid = _service.ValidateNormalizedCoordinates(0.8, 0.1, 0.3, 0.2, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("exceeds right boundary", error);
    }

    [Fact]
    public void ValidateNormalizedCoordinates_HeightExceedsBounds_ReturnsFalse()
    {
        var valid = _service.ValidateNormalizedCoordinates(0.1, 0.8, 0.3, 0.3, out var error);
        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("exceeds bottom boundary", error);
    }

    [Fact]
    public void ValidateNormalizedCoordinates_ZeroOrNegativeDimensions_ReturnsFalse()
    {
        var validZeroW = _service.ValidateNormalizedCoordinates(0.1, 0.1, 0.0, 0.2, out var err1);
        Assert.False(validZeroW);

        var validZeroH = _service.ValidateNormalizedCoordinates(0.1, 0.1, 0.2, 0.0, out var err2);
        Assert.False(validZeroH);
    }

    [Fact]
    public async Task CreateContract_ValidRequest_InitializesWithSequentialSigners()
    {
        var request = new CreateContractRequest(
            Title: "Custom Master Service Agreement",
            Description: "Testing dynamic contract creation",
            AgreementType: "Master Services Agreement",
            Pages: [
                new CreateContractPageRequest(1, "Page 1: Scope", "Testing scope"),
                new CreateContractPageRequest(2, "Page 2: Signatures", "Testing signatures")
            ],
            Signers: [
                new CreateContractSignerRequest("Alice Anderson", "alice@example.com", ContractSignerRole.ClientSigner),
                new CreateContractSignerRequest("Bob Brown", "bob@example.com", ContractSignerRole.VendorSigner)
            ],
            Fields: [
                new CreateContractFieldRequest(ContractFieldType.Signature, 2, 0.1, 0.7, 0.35, 0.08, 0, "Alice Signature"),
                new CreateContractFieldRequest(ContractFieldType.Signature, 2, 0.55, 0.7, 0.35, 0.08, 1, "Bob Signature")
            ]
        );

        var contract = await _service.CreateContractAsync(request);

        Assert.NotNull(contract);
        Assert.Equal("Custom Master Service Agreement", contract.Title);
        Assert.Equal(ContractWorkflowStatus.AwaitingSigner1, contract.Status);
        Assert.Equal(2, contract.Signers.Count);
        Assert.StartsWith("sign_tok_", contract.Signers[0].Token);
        Assert.StartsWith("sign_tok_", contract.Signers[1].Token);
        Assert.Equal(SignerSigningStatus.ReadyToSign, contract.Signers[0].Status);
        Assert.Equal(SignerSigningStatus.Pending, contract.Signers[1].Status);
        Assert.Equal(2, contract.Fields.Count);
        Assert.NotEmpty(contract.AuditTrail);
    }

    [Fact]
    public async Task CreateContract_MissingTitle_ThrowsArgumentException()
    {
        var request = new CreateContractRequest(
            Title: "   ",
            Description: "Empty title test",
            AgreementType: "Test",
            Pages: [new CreateContractPageRequest(1, "P1", "C1")],
            Signers: [new CreateContractSignerRequest("Alice", "a@a.com", ContractSignerRole.ClientSigner)],
            Fields: []
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateContractAsync(request));
    }

    [Fact]
    public async Task CreateContract_NoSigners_ThrowsArgumentException()
    {
        var request = new CreateContractRequest(
            Title: "No Signers Contract",
            Description: "Test",
            AgreementType: "Test",
            Pages: [new CreateContractPageRequest(1, "P1", "C1")],
            Signers: [],
            Fields: []
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateContractAsync(request));
    }

    [Fact]
    public async Task CreateContract_InvalidFieldCoordinates_ThrowsArgumentException()
    {
        var request = new CreateContractRequest(
            Title: "Invalid Field Contract",
            Description: "Test",
            AgreementType: "Test",
            Pages: [new CreateContractPageRequest(1, "P1", "C1")],
            Signers: [new CreateContractSignerRequest("Alice", "a@a.com", ContractSignerRole.ClientSigner)],
            Fields: [new CreateContractFieldRequest(ContractFieldType.Signature, 1, 0.9, 0.9, 0.4, 0.4, 0, "Out of bounds")]
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateContractAsync(request));
    }

    [Fact]
    public async Task CreateContract_FieldSignerIndexOutOfRange_ThrowsArgumentException()
    {
        var request = new CreateContractRequest(
            Title: "Invalid Signer Index Contract",
            Description: "Test",
            AgreementType: "Test",
            Pages: [new CreateContractPageRequest(1, "P1", "C1")],
            Signers: [new CreateContractSignerRequest("Alice", "a@a.com", ContractSignerRole.ClientSigner)],
            Fields: [new CreateContractFieldRequest(ContractFieldType.Signature, 1, 0.1, 0.1, 0.2, 0.1, 5, "Bad Signer Index")]
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateContractAsync(request));
    }

    [Fact]
    public async Task GetContractBySignerToken_ValidToken_ReturnsContractEnvelope()
    {
        var contract = await _service.GetContractBySignerTokenAsync("sign_tok_e1a89c01acme");

        Assert.NotNull(contract);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), contract.Id);
    }

    [Fact]
    public async Task GetContractBySignerToken_InvalidToken_ReturnsNull()
    {
        var contract = await _service.GetContractBySignerTokenAsync("non_existent_token_9999");
        Assert.Null(contract);
    }

    [Fact]
    public async Task SubmitSignature_NonExistentContract_ReturnsFailure()
    {
        var result = await _service.SubmitSignatureAsync(Guid.NewGuid(), new SubmitSignatureRequest(
            SignerToken: "any_token",
            SignatureType: "Typed",
            SignatureData: "Jane Doe"
        ));

        Assert.False(result.Success);
        Assert.Equal("Contract not found.", result.Message);
    }

    [Fact]
    public async Task SubmitSignature_InvalidToken_ReturnsFailure()
    {
        var contractId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var result = await _service.SubmitSignatureAsync(contractId, new SubmitSignatureRequest(
            SignerToken: "bogus_token_123",
            SignatureType: "Typed",
            SignatureData: "Jane Doe"
        ));

        Assert.False(result.Success);
        Assert.Contains("Invalid or unrecognized signer authorization token", result.Message);
    }

    [Fact]
    public async Task SubmitSignature_OutSequenceSigner_FailsToSign()
    {
        var contractId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // Current index is 0
        // Attempting to sign using token of signer 1 (Marcus Vance)
        var result = await _service.SubmitSignatureAsync(contractId, new SubmitSignatureRequest(
            SignerToken: "sign_tok_e1a89c02vendor",
            SignatureType: "Typed",
            SignatureData: "Marcus Vance"
        ));

        Assert.False(result.Success);
        Assert.Contains("Sequential signing order violation", result.Message);
    }

    [Fact]
    public async Task SubmitSignature_ActiveSigner_AdvancesWorkflowToNextSigner()
    {
        // Contract 1: Sarah Jenkins (token: sign_tok_e1a89c01acme) is active
        var contractId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var result = await _service.SubmitSignatureAsync(contractId, new SubmitSignatureRequest(
            SignerToken: "sign_tok_e1a89c01acme",
            SignatureType: "Typed",
            SignatureData: "Sarah Jenkins [DIGITALLY SIGNED]",
            IpAddress: "192.0.2.1",
            UserAgent: "Mozilla/5.0 TestBrowser/1.0",
            Geolocation: "Boston, MA, USA"
        ));

        Assert.True(result.Success);
        Assert.False(result.IsFullyExecuted);
        Assert.Equal(1, result.NextSignerIndex);
        Assert.Equal(ContractWorkflowStatus.AwaitingSigner2, result.Status);

        var contract = await _service.GetContractByIdAsync(contractId);
        Assert.NotNull(contract);
        Assert.Equal(1, contract.CurrentSignerIndex);
        Assert.Equal(SignerSigningStatus.Signed, contract.Signers[0].Status);
        Assert.Equal(SignerSigningStatus.ReadyToSign, contract.Signers[1].Status);
    }

    [Fact]
    public async Task SubmitSignature_CapturesMetadata_IpUserAgentGeoAndUtc()
    {
        // Use a newly created contract to isolate metadata check
        var request = new CreateContractRequest(
            Title: "Metadata Tracking Agreement",
            Description: "Audit tracking test",
            AgreementType: "Audit Test",
            Pages: [new CreateContractPageRequest(1, "Page 1", "Content")],
            Signers: [new CreateContractSignerRequest("John Doe", "john@example.com", ContractSignerRole.ClientSigner)],
            Fields: [new CreateContractFieldRequest(ContractFieldType.Signature, 1, 0.1, 0.7, 0.3, 0.1, 0, "Sign")]
        );

        var created = await _service.CreateContractAsync(request);
        var signer = created.Signers[0];

        var signResult = await _service.SubmitSignatureAsync(created.Id, new SubmitSignatureRequest(
            SignerToken: signer.Token,
            SignatureType: "DrawnVector",
            SignatureData: "data:image/svg+xml;base64,PHN2Zz48cGF0aC8+PC9zdmc+",
            IpAddress: "198.51.100.42",
            UserAgent: "Antigravity-Agent/2026.1",
            Geolocation: "Zurich, Switzerland"
        ));

        Assert.True(signResult.Success);
        Assert.True(signResult.IsFullyExecuted);

        var contract = await _service.GetContractByIdAsync(created.Id);
        Assert.NotNull(contract);
        var completedSigner = contract.Signers[0];

        Assert.Equal("198.51.100.42", completedSigner.IpAddress);
        Assert.Equal("Antigravity-Agent/2026.1", completedSigner.UserAgent);
        Assert.Equal("Zurich, Switzerland", completedSigner.Geolocation);
        Assert.NotNull(completedSigner.SignedAtUtc);

        var auditEntry = contract.AuditTrail.FirstOrDefault(a => a.Action == "SignatureSubmitted");
        Assert.NotNull(auditEntry);
        Assert.Equal("198.51.100.42", auditEntry.IpAddress);
        Assert.Contains("Zurich, Switzerland", auditEntry.Details);
    }

    [Fact]
    public async Task SubmitSignature_PopulatesVisualFields_ForActiveSigner()
    {
        var request = new CreateContractRequest(
            Title: "Field Population Test",
            Description: "Field test",
            AgreementType: "Test",
            Pages: [new CreateContractPageRequest(1, "Page 1", "Content")],
            Signers: [new CreateContractSignerRequest("Emily Watson", "emily@example.com", ContractSignerRole.ClientSigner)],
            Fields: [
                new CreateContractFieldRequest(ContractFieldType.Signature, 1, 0.1, 0.6, 0.3, 0.1, 0, "Signature"),
                new CreateContractFieldRequest(ContractFieldType.Initials, 1, 0.5, 0.6, 0.1, 0.05, 0, "Initials"),
                new CreateContractFieldRequest(ContractFieldType.DateSigned, 1, 0.1, 0.75, 0.3, 0.05, 0, "Date"),
                new CreateContractFieldRequest(ContractFieldType.FullName, 1, 0.1, 0.85, 0.3, 0.05, 0, "Full Name")
            ]
        );

        var created = await _service.CreateContractAsync(request);
        var signer = created.Signers[0];

        await _service.SubmitSignatureAsync(created.Id, new SubmitSignatureRequest(
            SignerToken: signer.Token,
            SignatureType: "Typed",
            SignatureData: "Emily Watson"
        ));

        var contract = await _service.GetContractByIdAsync(created.Id);
        Assert.NotNull(contract);

        var sigField = contract.Fields.First(f => f.FieldType == ContractFieldType.Signature);
        var initField = contract.Fields.First(f => f.FieldType == ContractFieldType.Initials);
        var dateField = contract.Fields.First(f => f.FieldType == ContractFieldType.DateSigned);
        var nameField = contract.Fields.First(f => f.FieldType == ContractFieldType.FullName);

        Assert.Equal("Emily Watson", sigField.Value);
        Assert.Equal("EW", initField.Value);
        Assert.NotNull(dateField.Value);
        Assert.Contains("UTC", dateField.Value);
        Assert.Equal("Emily Watson", nameField.Value);
    }

    [Fact]
    public async Task SubmitSignature_FinalSigner_TransitionsToFullyExecutedAndComputesSeal()
    {
        // Contract 2 is awaiting Signer 2 (Claire Dubois, token: sign_tok_d2b78102vendor)
        var contractId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var result = await _service.SubmitSignatureAsync(contractId, new SubmitSignatureRequest(
            SignerToken: "sign_tok_d2b78102vendor",
            SignatureType: "Typed",
            SignatureData: "Claire Dubois [VP Strategic Alliances]"
        ));

        Assert.True(result.Success);
        Assert.True(result.IsFullyExecuted);
        Assert.Equal(ContractWorkflowStatus.FullyExecuted, result.Status);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.NotNull(result.Sha256Hash);
        Assert.Equal(64, result.Sha256Hash.Length);

        var contract = await _service.GetContractByIdAsync(contractId);
        Assert.NotNull(contract);
        Assert.Equal(ContractWorkflowStatus.FullyExecuted, contract.Status);
        Assert.Equal(result.Sha256Hash, contract.IntegritySealHash);
    }

    [Fact]
    public async Task SubmitSignature_AlreadySignedSigner_ReturnsFailure()
    {
        // In Contract 3, all signers have already signed
        var contractId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var result = await _service.SubmitSignatureAsync(contractId, new SubmitSignatureRequest(
            SignerToken: "sign_tok_b3c67101david",
            SignatureType: "Typed",
            SignatureData: "David Chen Again"
        ));

        Assert.False(result.Success);
        Assert.Contains("fully executed", result.Message);
    }

    [Fact]
    public async Task DeclineSignature_ActiveSigner_TransitionsContractToDeclinedWithReason()
    {
        var request = new CreateContractRequest(
            Title: "Decline Test Agreement",
            Description: "Decline test",
            AgreementType: "Test",
            Pages: [new CreateContractPageRequest(1, "Page 1", "Content")],
            Signers: [new CreateContractSignerRequest("Tom Davis", "tom@example.com", ContractSignerRole.ClientSigner)],
            Fields: []
        );

        var created = await _service.CreateContractAsync(request);
        var signer = created.Signers[0];

        var declineResult = await _service.DeclineSignatureAsync(created.Id, new DeclineSignatureRequest(
            SignerToken: signer.Token,
            Reason: "Terms in section 3 are unacceptable to our risk team."
        ));

        Assert.True(declineResult.Success);
        Assert.Equal(ContractWorkflowStatus.Declined, declineResult.Status);
        Assert.Equal("Tom Davis", declineResult.DeclinedBySigner);

        var contract = await _service.GetContractByIdAsync(created.Id);
        Assert.NotNull(contract);
        Assert.Equal(ContractWorkflowStatus.Declined, contract.Status);
        Assert.Equal(SignerSigningStatus.Declined, contract.Signers[0].Status);
        Assert.Contains("Terms in section 3", contract.Signers[0].DeclineReason);
    }

    [Fact]
    public async Task DeclineSignature_InvalidToken_ReturnsFailure()
    {
        var contractId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var result = await _service.DeclineSignatureAsync(contractId, new DeclineSignatureRequest(
            SignerToken: "unrecognized_token",
            Reason: "No reason"
        ));

        Assert.False(result.Success);
        Assert.Contains("Invalid or unrecognized signer authorization token", result.Message);
    }

    [Fact]
    public async Task DeclineSignature_OnFullyExecutedContract_ReturnsFailure()
    {
        var contractId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var result = await _service.DeclineSignatureAsync(contractId, new DeclineSignatureRequest(
            SignerToken: "sign_tok_b3c67101david",
            Reason: "Too late"
        ));

        Assert.False(result.Success);
        Assert.Contains("Cannot decline a contract that has already been fully executed", result.Message);
    }

    [Fact]
    public async Task VoidContract_SetsStatusToVoidedWithTimestampAndAudit()
    {
        var contractId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var voided = await _service.VoidContractAsync(contractId, "Superseded by Master Services Accord v2.0");

        Assert.NotNull(voided);
        Assert.Equal(ContractWorkflowStatus.Voided, voided.Status);
        Assert.NotNull(voided.VoidedAtUtc);

        var audit = voided.AuditTrail.Last();
        Assert.Equal("ContractVoided", audit.Action);
        Assert.Contains("Superseded by Master Services Accord v2.0", audit.Details);
    }

    [Fact]
    public async Task VerifyDocumentIntegrity_SealedContractUntampered_ReturnsValidAndNoTamper()
    {
        var contractId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var verifyResult = await _service.VerifyDocumentIntegrityAsync(contractId);

        Assert.NotNull(verifyResult);
        Assert.True(verifyResult.IsValid);
        Assert.False(verifyResult.TamperDetected);
        Assert.Equal("CryptographicallySealedAndValid", verifyResult.Status);
        Assert.Equal(verifyResult.ComputedSha256, verifyResult.ExpectedSha256);
        Assert.NotEmpty(verifyResult.ComputedSha256);
    }

    [Fact]
    public async Task VerifyDocumentIntegrity_TamperedContent_DetectsTamperAndReportsFailure()
    {
        var contractId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var contract = await _service.GetContractByIdAsync(contractId);
        Assert.NotNull(contract);

        // Tamper with document page content
        var originalContent = contract.Pages[0].Content;
        try
        {
            contract.Pages[0].Content = "MALICIOUSLY MODIFIED: Payout is now 100,000,000 USD to offshore account.";

            var verifyResult = await _service.VerifyDocumentIntegrityAsync(contractId);

            Assert.NotNull(verifyResult);
            Assert.False(verifyResult.IsValid);
            Assert.True(verifyResult.TamperDetected);
            Assert.Equal("TamperDetected", verifyResult.Status);
            Assert.NotEqual(verifyResult.ComputedSha256, verifyResult.ExpectedSha256);
            Assert.Contains("SECURITY ALERT", verifyResult.Message);
        }
        finally
        {
            // Restore original content
            contract.Pages[0].Content = originalContent;
        }
    }

    [Fact]
    public async Task GetAuditCertificate_GeneratesCompleteCertificateWithQrPayload()
    {
        var contractId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var cert = await _service.GetAuditCertificateAsync(contractId);

        Assert.NotNull(cert);
        Assert.Equal(contractId, cert.DocumentId);
        Assert.StartsWith("CERT-ESIGN-", cert.CertificateId);
        Assert.Equal("B2B Enterprise Master Terms of Service & SLA Addendum", cert.DocumentTitle);
        Assert.Equal(64, cert.Sha256Hash.Length);
        Assert.NotNull(cert.CompletedAtUtc);
        Assert.Equal("FullyExecuted", cert.Status);
        Assert.Contains("ISO 32000-1", cert.SecuritySealStandard);
        Assert.NotEmpty(cert.VerificationUrl);
        Assert.NotEmpty(cert.VerificationQrPayload);
        Assert.Contains(contractId.ToString(), cert.VerificationQrPayload);
        Assert.Equal(2, cert.Signers.Count);
        Assert.NotEmpty(cert.AuditTrail);
    }

    [Fact]
    public async Task ConcurrentSigning_ThreadSafety_GuaranteesIntegrity()
    {
        // Create contract with 2 sequential signers
        var request = new CreateContractRequest(
            Title: "Concurrent Stress Agreement",
            Description: "Concurrency test",
            AgreementType: "Stress Test",
            Pages: [new CreateContractPageRequest(1, "Page 1", "Content")],
            Signers: [
                new CreateContractSignerRequest("Signer 0", "signer0@enterprise.com", ContractSignerRole.ClientSigner, 0),
                new CreateContractSignerRequest("Signer 1", "signer1@enterprise.com", ContractSignerRole.VendorSigner, 1)
            ],
            Fields: []
        );

        var created = await _service.CreateContractAsync(request);
        var activeSigner = created.Signers[0];

        // 10 concurrent threads simultaneously attempt to submit signature for the exact same active signer
        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            return await _service.SubmitSignatureAsync(created.Id, new SubmitSignatureRequest(
                SignerToken: activeSigner.Token,
                SignatureType: "Typed",
                SignatureData: $"Signer 0 (Attempt {i})"
            ));
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Exactly one concurrent call must succeed, 9 must fail safely with zero state corruption
        var successCount = results.Count(r => r.Success);
        Assert.Equal(1, successCount);

        var failCount = results.Count(r => !r.Success);
        Assert.Equal(9, failCount);

        var contract = await _service.GetContractByIdAsync(created.Id);
        Assert.NotNull(contract);
        Assert.Equal(1, contract.CurrentSignerIndex);
        Assert.Equal(SignerSigningStatus.Signed, contract.Signers[0].Status);
        Assert.Equal(SignerSigningStatus.ReadyToSign, contract.Signers[1].Status);
    }
}
