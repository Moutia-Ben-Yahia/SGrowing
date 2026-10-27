using SGrowing.Api.Modules.CRM.ContractSigning;
using Xunit;

namespace SGrowing.Tests.CRM;

public class MultiPartySigningTests
{
    private readonly MultiPartySigningService _signingService = new();

    [Fact]
    public async Task MultiPartySigning_SequentialWorkflow_EnforcesOrderAndOtpVerification()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var visualFields = new List<VisualPdfFieldDto>
        {
            new(Guid.NewGuid(), 1, 100, 200, 150, 40, PdfFieldType.Signature, "ceo@firstcorp.com", true, "Sign Here", null),
            new(Guid.NewGuid(), 1, 300, 200, 150, 40, PdfFieldType.Signature, "cfo@secondcorp.com", true, "Countersign Here", null)
        };

        var req = new CreateMultiPartyContractRequest(
            Title: "Enterprise Software License Master Agreement",
            DocumentUrl: "https://storage.sgrowing.io/contracts/msa-2026.pdf",
            WorkflowType: SigningWorkflowType.Sequential,
            Signers: [
                new(OrderIndex: 1, FullName: "CEO FirstCorp", Email: "ceo@firstcorp.com", RequiresOtp: true),
                new(OrderIndex: 2, FullName: "CFO SecondCorp", Email: "cfo@secondcorp.com", RequiresOtp: false)
            ],
            VisualFields: visualFields
        );

        // Act 1: Create draft contract
        var contract = await _signingService.CreateMultiPartyContractAsync(tenantId, req);
        Assert.Equal(2, contract.Signers.Count);
        Assert.Equal("Draft", contract.OverallStatus);

        // Act 2: Start workflow
        var started = await _signingService.InitiateWorkflowAsync(tenantId, contract.ContractId);
        Assert.Equal("InProgress", started.OverallStatus);
        Assert.Equal(SignerStatus.Sent, started.Signers[0].Status);
        Assert.Equal(SignerStatus.Pending, started.Signers[1].Status); // Signer 2 remains Pending in sequential flow

        // Act 3: Signer 1 OTP lifecycle
        var signer1Token = started.Signers[0].SigningToken;
        var otpSend = await _signingService.SendSignerOtpAsync(signer1Token, "email");
        Assert.True(otpSend.Success);

        // Verify wrong code fails
        var otpWrong = await _signingService.VerifySignerOtpAsync(signer1Token, "000000");
        Assert.False(otpWrong.Success);
        Assert.Equal(2, otpWrong.RemainingAttempts);

        // Verify with correct code from OTP dispatch
        Assert.NotNull(otpSend.DevTestOtpCode);
        var otpValid = await _signingService.VerifySignerOtpAsync(signer1Token, otpSend.DevTestOtpCode);
        Assert.True(otpValid.Success);
        Assert.True(otpValid.IsVerified);

        // Act 4: Submit signature for Signer 1
        var afterSigner1 = await _signingService.SubmitSignerSignatureAsync(
            signer1Token,
            new SubmitSignatureRequest(
                FullName: "CEO FirstCorp",
                SignatureImageBase64: "base64_sig_ceo",
                CompletedFields: [visualFields[0] with { Value = "Signed By CEO" }]
            )
        );

        Assert.Equal(SignerStatus.Signed, afterSigner1.Signers[0].Status);
        // Signer 2 should now automatically transition from Pending to Sent in sequential orchestration
        Assert.Equal(SignerStatus.Sent, afterSigner1.Signers[1].Status);
        Assert.Equal(2, afterSigner1.CurrentSequentialOrderIndex);
        Assert.Equal("InProgress", afterSigner1.OverallStatus);

        // Act 5: Submit signature for Signer 2
        var signer2Token = afterSigner1.Signers[1].SigningToken;
        var afterSigner2 = await _signingService.SubmitSignerSignatureAsync(
            signer2Token,
            new SubmitSignatureRequest(
                FullName: "CFO SecondCorp",
                SignatureImageBase64: "base64_sig_cfo",
                CompletedFields: [visualFields[1] with { Value = "Countersigned By CFO" }]
            )
        );

        Assert.Equal(SignerStatus.Signed, afterSigner2.Signers[1].Status);
        Assert.Equal("Completed", afterSigner2.OverallStatus);
        Assert.NotNull(afterSigner2.ChecksumSha256);
        Assert.NotNull(afterSigner2.SealedPdfUrl);
        Assert.Equal("Countersigned By CFO", afterSigner2.VisualFields[1].Value);
    }

    [Fact]
    public async Task MultiPartySigning_ParallelWorkflow_AllowsConcurrentSigning()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var req = new CreateMultiPartyContractRequest(
            Title: "Mutual Non-Disclosure Agreement",
            DocumentUrl: "https://storage.sgrowing.io/contracts/mnda.pdf",
            WorkflowType: SigningWorkflowType.Parallel,
            Signers: [
                new(OrderIndex: 1, FullName: "Partner A", Email: "partner.a@corp.com", RequiresOtp: false),
                new(OrderIndex: 1, FullName: "Partner B", Email: "partner.b@corp.com", RequiresOtp: false)
            ],
            VisualFields: []
        );

        var contract = await _signingService.CreateMultiPartyContractAsync(tenantId, req);
        var started = await _signingService.InitiateWorkflowAsync(tenantId, contract.ContractId);

        // Assert: Both signers should be in Sent status simultaneously
        Assert.Equal(SignerStatus.Sent, started.Signers[0].Status);
        Assert.Equal(SignerStatus.Sent, started.Signers[1].Status);

        // Signer 1 signs
        var afterSigner1 = await _signingService.SubmitSignerSignatureAsync(
            started.Signers[0].SigningToken,
            new SubmitSignatureRequest("Partner A", "base64_sig_1", [])
        );
        Assert.Equal(SignerStatus.Signed, afterSigner1.Signers[0].Status);
        Assert.Equal(SignerStatus.Sent, afterSigner1.Signers[1].Status);
        Assert.Equal("InProgress", afterSigner1.OverallStatus);

        // Signer 2 signs
        var afterSigner2 = await _signingService.SubmitSignerSignatureAsync(
            started.Signers[1].SigningToken,
            new SubmitSignatureRequest("Partner B", "base64_sig_2", [])
        );
        Assert.Equal(SignerStatus.Signed, afterSigner2.Signers[1].Status);
        Assert.Equal("Completed", afterSigner2.OverallStatus);
        Assert.NotNull(afterSigner2.ChecksumSha256);
        Assert.NotNull(afterSigner2.SealedPdfUrl);
    }
}
