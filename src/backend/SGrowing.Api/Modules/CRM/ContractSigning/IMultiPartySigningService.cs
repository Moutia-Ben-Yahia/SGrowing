namespace SGrowing.Api.Modules.CRM.ContractSigning;

public enum SigningWorkflowType
{
    Sequential, // Signer 1 signs, then Signer 2 is invited, etc.
    Parallel    // All signers receive invitation simultaneously
}

public enum SignerRole
{
    Signer,
    Approver,
    Viewer
}

public enum SignerStatus
{
    Pending,
    Sent,
    Viewed,
    OtpVerified,
    Signed,
    Declined
}

public enum PdfFieldType
{
    Signature,
    Initials,
    DateSigned,
    TextBox,
    Checkbox
}

public record VisualPdfFieldDto(
    Guid FieldId,
    int PageNumber,
    double XCoordinate,
    double YCoordinate,
    double Width,
    double Height,
    PdfFieldType FieldType,
    string SignerEmail,
    bool IsRequired,
    string? Placeholder,
    string? Value
);

public record ContractSignerDto(
    Guid SignerId,
    int OrderIndex,
    string FullName,
    string Email,
    string? PhoneNumber,
    SignerRole Role,
    SignerStatus Status,
    string SigningToken,
    bool RequiresOtp,
    bool IsOtpVerified,
    DateTime? SentAtUtc,
    DateTime? ViewedAtUtc,
    DateTime? SignedAtUtc,
    string? SignatureImageBase64
);

public record MultiPartyContractDto(
    Guid ContractId,
    Guid TenantId,
    string Title,
    string DocumentUrl,
    SigningWorkflowType WorkflowType,
    string OverallStatus, // "Draft", "InProgress", "Completed", "Declined"
    List<ContractSignerDto> Signers,
    List<VisualPdfFieldDto> VisualFields,
    int CurrentSequentialOrderIndex,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? SealedPdfUrl,
    string? ChecksumSha256
);

public record CreateMultiPartyContractRequest(
    string Title,
    string DocumentUrl,
    SigningWorkflowType WorkflowType,
    List<CreateSignerRequest> Signers,
    List<VisualPdfFieldDto> VisualFields
);

public record CreateSignerRequest(
    int OrderIndex,
    string FullName,
    string Email,
    string? PhoneNumber = null,
    SignerRole Role = SignerRole.Signer,
    bool RequiresOtp = true
);

public record OtpVerificationResult(
    bool Success,
    string Message,
    int RemainingAttempts,
    bool IsVerified,
    string? DevTestOtpCode = null
);

public record SubmitSignatureRequest(
    string FullName,
    string SignatureImageBase64,
    List<VisualPdfFieldDto> CompletedFields
);

public interface IMultiPartySigningService
{
    Task<MultiPartyContractDto> CreateMultiPartyContractAsync(Guid tenantId, CreateMultiPartyContractRequest req, CancellationToken ct = default);
    Task<MultiPartyContractDto> InitiateWorkflowAsync(Guid tenantId, Guid contractId, CancellationToken ct = default);
    Task<OtpVerificationResult> SendSignerOtpAsync(string signingToken, string destination = "email", CancellationToken ct = default);
    Task<OtpVerificationResult> VerifySignerOtpAsync(string signingToken, string otpCode, CancellationToken ct = default);
    Task<MultiPartyContractDto> SubmitSignerSignatureAsync(string signingToken, SubmitSignatureRequest req, CancellationToken ct = default);
    Task<MultiPartyContractDto> GetContractStatusAsync(Guid contractId, CancellationToken ct = default);
    Task<MultiPartyContractDto?> GetContractBySigningTokenAsync(string signingToken, CancellationToken ct = default);
}
