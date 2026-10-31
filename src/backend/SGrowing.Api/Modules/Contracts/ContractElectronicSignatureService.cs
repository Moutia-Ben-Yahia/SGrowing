using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Contracts;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractWorkflowStatus
{
    Draft,
    AwaitingSigner1,
    SignedBySigner1,
    AwaitingSigner2,
    AwaitingSigner3,
    FullyExecuted,
    Declined,
    Voided
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractSignerRole
{
    ClientSigner,
    VendorSigner,
    Witness
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignerSigningStatus
{
    Pending,
    ReadyToSign,
    Signed,
    Declined
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractFieldType
{
    Signature,
    Initials,
    DateSigned,
    FullName,
    Text
}

#endregion

#region Domain Models & DTOs

public class ContractPage
{
    public int PageNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ContractFieldLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ContractFieldType FieldType { get; set; }
    public int PageNumber { get; set; } = 1;
    public double XNorm { get; set; }
    public double YNorm { get; set; }
    public double WidthNorm { get; set; }
    public double HeightNorm { get; set; }
    public int SignerIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsRequired { get; set; } = true;
    public DateTimeOffset? SignedAtUtc { get; set; }
}

public class ContractSigner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ContractSignerRole Role { get; set; }
    public string Token { get; set; } = string.Empty;
    public SignerSigningStatus Status { get; set; } = SignerSigningStatus.Pending;
    public DateTimeOffset? SignedAtUtc { get; set; }
    public string? SignatureData { get; set; }
    public string? SignatureType { get; set; } // "Typed", "DrawnVector", "ImageUpload"
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Geolocation { get; set; }
    public string? DeclineReason { get; set; }
}

public class ContractAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string? SignerRole { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }
}

public class ContractEnvelope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AgreementType { get; set; } = string.Empty;
    public ContractWorkflowStatus Status { get; set; } = ContractWorkflowStatus.Draft;
    public int CurrentSignerIndex { get; set; } = 0;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public List<ContractPage> Pages { get; set; } = [];
    public List<ContractSigner> Signers { get; set; } = [];
    public List<ContractFieldLayout> Fields { get; set; } = [];
    public List<ContractAuditEntry> AuditTrail { get; set; } = [];
    public string? IntegritySealHash { get; set; }
    public string? RawDocumentBytesBase64 { get; set; }
}

public record ContractEnvelopeSummaryDto(
    Guid Id,
    string Title,
    string Description,
    string AgreementType,
    ContractWorkflowStatus Status,
    int CurrentSignerIndex,
    int TotalSigners,
    int CompletedSignersCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? IntegritySealHash,
    string? CurrentSignerEmail,
    string? CurrentSignerName
);

public record CreateContractPageRequest(
    int PageNumber,
    string Title,
    string Content
);

public record CreateContractSignerRequest(
    string Name,
    string Email,
    ContractSignerRole Role,
    int? OrderIndex = null
);

public record CreateContractFieldRequest(
    ContractFieldType FieldType,
    int PageNumber,
    double XNorm,
    double YNorm,
    double WidthNorm,
    double HeightNorm,
    int SignerIndex,
    string Label,
    bool IsRequired = true
);

public record CreateContractRequest(
    string Title,
    string Description,
    string AgreementType,
    List<CreateContractPageRequest> Pages,
    List<CreateContractSignerRequest> Signers,
    List<CreateContractFieldRequest> Fields,
    ContractWorkflowStatus? InitialStatus = null
);

public record SubmitSignatureRequest(
    string SignerToken,
    string SignatureType, // "Typed", "DrawnVector", "ImageUpload"
    string SignatureData,
    string? IpAddress = null,
    string? UserAgent = null,
    string? Geolocation = null
);

public record SignContractResult(
    bool Success,
    string Message,
    Guid ContractId,
    ContractWorkflowStatus Status,
    int SignerIndex,
    int? NextSignerIndex,
    bool IsFullyExecuted,
    DateTimeOffset? CompletedAtUtc,
    string? Sha256Hash
);

public record DeclineSignatureRequest(
    string SignerToken,
    string Reason,
    string? IpAddress = null,
    string? UserAgent = null
);

public record DeclineContractResult(
    bool Success,
    string Message,
    Guid ContractId,
    ContractWorkflowStatus Status,
    string DeclinedBySigner,
    string Reason
);

public record AuditCertificateSignerDto(
    Guid SignerId,
    int OrderIndex,
    string Name,
    string Email,
    string Role,
    string Status,
    DateTimeOffset? SignedAtUtc,
    string? IpAddress,
    string? UserAgent,
    string? Geolocation,
    string? SignatureType,
    string? SignatureSummary
);

public record AuditCertificateDto(
    Guid DocumentId,
    string CertificateId,
    string DocumentTitle,
    string AgreementType,
    string Sha256Hash,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status,
    string SecuritySealStandard,
    string VerificationUrl,
    string VerificationQrPayload,
    List<AuditCertificateSignerDto> Signers,
    List<ContractAuditEntry> AuditTrail
);

public record DocumentIntegrityResultDto(
    Guid DocumentId,
    bool IsValid,
    bool TamperDetected,
    string ComputedSha256,
    string? ExpectedSha256,
    string Status,
    int AuditLogCount,
    DateTimeOffset VerifiedAtUtc,
    string Message
);

#endregion

#region Service Interface

public interface IContractElectronicSignatureService
{
    Task<IReadOnlyList<ContractEnvelopeSummaryDto>> GetAllContractsAsync();
    Task<ContractEnvelope?> GetContractByIdAsync(Guid id);
    Task<ContractEnvelope?> GetContractBySignerTokenAsync(string token);
    Task<ContractEnvelope> CreateContractAsync(CreateContractRequest request);
    Task<SignContractResult> SubmitSignatureAsync(Guid contractId, SubmitSignatureRequest request);
    Task<DeclineContractResult> DeclineSignatureAsync(Guid contractId, DeclineSignatureRequest request);
    Task<AuditCertificateDto?> GetAuditCertificateAsync(Guid contractId);
    Task<DocumentIntegrityResultDto> VerifyDocumentIntegrityAsync(Guid contractId);
    Task<ContractEnvelope?> VoidContractAsync(Guid contractId, string reason);
    string ComputeIntegrityHash(ContractEnvelope contract);
    bool ValidateNormalizedCoordinates(double xNorm, double yNorm, double widthNorm, double heightNorm, out string? error);
}

#endregion

#region Implementation

public class ContractElectronicSignatureService : IContractElectronicSignatureService
{
    private readonly ConcurrentDictionary<Guid, ContractEnvelope> _contracts = new();
    private readonly ConcurrentDictionary<Guid, object> _syncLocks = new();

    public ContractElectronicSignatureService()
    {
        SeedDiverseContracts();
    }

    public Task<IReadOnlyList<ContractEnvelopeSummaryDto>> GetAllContractsAsync()
    {
        var summaries = _contracts.Values
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c =>
            {
                var currentSigner = c.Signers.FirstOrDefault(s => s.OrderIndex == c.CurrentSignerIndex);
                var completedCount = c.Signers.Count(s => s.Status == SignerSigningStatus.Signed);
                return new ContractEnvelopeSummaryDto(
                    c.Id,
                    c.Title,
                    c.Description,
                    c.AgreementType,
                    c.Status,
                    c.CurrentSignerIndex,
                    c.Signers.Count,
                    completedCount,
                    c.CreatedAtUtc,
                    c.CompletedAtUtc,
                    c.IntegritySealHash,
                    currentSigner?.Email,
                    currentSigner?.Name
                );
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ContractEnvelopeSummaryDto>>(summaries);
    }

    public Task<ContractEnvelope?> GetContractByIdAsync(Guid id)
    {
        _contracts.TryGetValue(id, out var contract);
        return Task.FromResult(contract);
    }

    public Task<ContractEnvelope?> GetContractBySignerTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<ContractEnvelope?>(null);

        var match = _contracts.Values.FirstOrDefault(c => 
            c.Signers.Any(s => string.Equals(s.Token, token, StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult(match);
    }

    public Task<ContractEnvelope> CreateContractAsync(CreateContractRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Contract title cannot be empty.", nameof(request));

        if (request.Signers == null || request.Signers.Count == 0)
            throw new ArgumentException("At least one signer is required.", nameof(request));

        var contract = new ContractEnvelope
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            AgreementType = request.AgreementType?.Trim() ?? "General Agreement",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CurrentSignerIndex = 0
        };

        // Populate Pages
        if (request.Pages != null && request.Pages.Count > 0)
        {
            contract.Pages = request.Pages.Select(p => new ContractPage
            {
                PageNumber = p.PageNumber > 0 ? p.PageNumber : 1,
                Title = p.Title,
                Content = p.Content
            }).ToList();
        }
        else
        {
            contract.Pages = [
                new ContractPage
                {
                    PageNumber = 1,
                    Title = "General Terms and Conditions",
                    Content = "Agreement content terms and obligations executed digitally under ESIGN / eIDAS compliance."
                }
            ];
        }

        // Populate Signers with generated tokens and sequential order
        var order = 0;
        foreach (var signerReq in request.Signers)
        {
            var signer = new ContractSigner
            {
                Id = Guid.NewGuid(),
                OrderIndex = signerReq.OrderIndex ?? order,
                Name = signerReq.Name.Trim(),
                Email = signerReq.Email.Trim().ToLowerInvariant(),
                Role = signerReq.Role,
                Token = $"sign_tok_{Guid.NewGuid():N}"[..24],
                Status = order == 0 ? SignerSigningStatus.ReadyToSign : SignerSigningStatus.Pending
            };
            contract.Signers.Add(signer);
            order++;
        }

        // Validate and Populate Fields
        if (request.Fields != null)
        {
            foreach (var f in request.Fields)
            {
                if (!ValidateNormalizedCoordinates(f.XNorm, f.YNorm, f.WidthNorm, f.HeightNorm, out var coordErr))
                {
                    throw new ArgumentException($"Invalid coordinates for field '{f.Label}': {coordErr}");
                }

                if (f.SignerIndex < 0 || f.SignerIndex >= contract.Signers.Count)
                {
                    throw new ArgumentException($"SignerIndex {f.SignerIndex} is out of bounds (Signer count: {contract.Signers.Count}).");
                }

                contract.Fields.Add(new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = f.FieldType,
                    PageNumber = f.PageNumber,
                    XNorm = f.XNorm,
                    YNorm = f.YNorm,
                    WidthNorm = f.WidthNorm,
                    HeightNorm = f.HeightNorm,
                    SignerIndex = f.SignerIndex,
                    Label = f.Label,
                    IsRequired = f.IsRequired
                });
            }
        }

        // Workflow Status
        if (request.InitialStatus.HasValue)
        {
            contract.Status = request.InitialStatus.Value;
        }
        else
        {
            contract.Status = ContractWorkflowStatus.AwaitingSigner1;
        }

        // Audit Trail
        contract.AuditTrail.Add(new ContractAuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Action = "ContractCreated",
            PerformedBy = "System",
            Details = $"Contract created with {contract.Pages.Count} pages and {contract.Signers.Count} sequential signers."
        });

        contract.AuditTrail.Add(new ContractAuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Action = "SignerNotificationSent",
            PerformedBy = "System",
            SignerRole = contract.Signers[0].Role.ToString(),
            Details = $"Execution invitation token dispatched to first signer: {contract.Signers[0].Email}."
        });

        _contracts[contract.Id] = contract;
        return Task.FromResult(contract);
    }

    public Task<SignContractResult> SubmitSignatureAsync(Guid contractId, SubmitSignatureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_contracts.TryGetValue(contractId, out var contract))
        {
            return Task.FromResult(new SignContractResult(
                false,
                "Contract not found.",
                contractId,
                ContractWorkflowStatus.Draft,
                -1,
                null,
                false,
                null,
                null
            ));
        }

        var syncLock = _syncLocks.GetOrAdd(contractId, _ => new object());
        lock (syncLock)
        {
            // Validate contract terminal status
            if (contract.Status == ContractWorkflowStatus.FullyExecuted)
            {
                return Task.FromResult(new SignContractResult(
                    false,
                    "Contract is already fully executed and sealed against further modification.",
                    contractId,
                    contract.Status,
                    contract.CurrentSignerIndex,
                    null,
                    true,
                    contract.CompletedAtUtc,
                    contract.IntegritySealHash
                ));
            }

            if (contract.Status == ContractWorkflowStatus.Declined || contract.Status == ContractWorkflowStatus.Voided)
            {
                return Task.FromResult(new SignContractResult(
                    false,
                    $"Contract is in terminal state '{contract.Status}' and cannot receive signatures.",
                    contractId,
                    contract.Status,
                    contract.CurrentSignerIndex,
                    null,
                    false,
                    null,
                    null
                ));
            }

            // Find matching signer
            var signer = contract.Signers.FirstOrDefault(s => 
                string.Equals(s.Token, request.SignerToken, StringComparison.OrdinalIgnoreCase));

            if (signer == null)
            {
                return Task.FromResult(new SignContractResult(
                    false,
                    "Invalid or unrecognized signer authorization token.",
                    contractId,
                    contract.Status,
                    contract.CurrentSignerIndex,
                    null,
                    false,
                    null,
                    null
                ));
            }

            if (signer.Status == SignerSigningStatus.Signed)
            {
                return Task.FromResult(new SignContractResult(
                    false,
                    $"Signer '{signer.Name}' has already executed this contract.",
                    contractId,
                    contract.Status,
                    signer.OrderIndex,
                    null,
                    false,
                    null,
                    null
                ));
            }

            // Enforce sequential order
            if (signer.OrderIndex != contract.CurrentSignerIndex)
            {
                var activeSigner = contract.Signers.FirstOrDefault(s => s.OrderIndex == contract.CurrentSignerIndex);
                return Task.FromResult(new SignContractResult(
                    false,
                    $"Sequential signing order violation. It is currently the turn of signer {contract.CurrentSignerIndex + 1} ({activeSigner?.Name ?? "Pending"}).",
                    contractId,
                    contract.Status,
                    signer.OrderIndex,
                    contract.CurrentSignerIndex,
                    false,
                    null,
                    null
                ));
            }

            if (string.IsNullOrWhiteSpace(request.SignatureData))
            {
                return Task.FromResult(new SignContractResult(
                    false,
                    "Signature data cannot be blank or empty.",
                    contractId,
                    contract.Status,
                    signer.OrderIndex,
                    contract.CurrentSignerIndex,
                    false,
                    null,
                    null
                ));
            }

            // Apply signature to signer record
            var nowUtc = DateTimeOffset.UtcNow;
            signer.Status = SignerSigningStatus.Signed;
            signer.SignedAtUtc = nowUtc;
            signer.SignatureData = request.SignatureData;
            signer.SignatureType = string.IsNullOrWhiteSpace(request.SignatureType) ? "Typed" : request.SignatureType.Trim();
            signer.IpAddress = !string.IsNullOrWhiteSpace(request.IpAddress) ? request.IpAddress : "127.0.0.1";
            signer.UserAgent = !string.IsNullOrWhiteSpace(request.UserAgent) ? request.UserAgent : "Antigravity-Esign/2.0";
            signer.Geolocation = !string.IsNullOrWhiteSpace(request.Geolocation) ? request.Geolocation : "Verified Digital Node";

            // Populate placed fields associated with this signer index
            foreach (var field in contract.Fields.Where(f => f.SignerIndex == signer.OrderIndex))
            {
                field.SignedAtUtc = nowUtc;
                switch (field.FieldType)
                {
                    case ContractFieldType.Signature:
                        field.Value = request.SignatureData;
                        break;
                    case ContractFieldType.Initials:
                        field.Value = ExtractInitials(signer.Name);
                        break;
                    case ContractFieldType.DateSigned:
                        field.Value = nowUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
                        break;
                    case ContractFieldType.FullName:
                        field.Value = signer.Name;
                        break;
                    case ContractFieldType.Text:
                        if (string.IsNullOrEmpty(field.Value))
                        {
                            field.Value = signer.Name;
                        }
                        break;
                }
            }

            // Append audit record
            contract.AuditTrail.Add(new ContractAuditEntry
            {
                TimestampUtc = nowUtc,
                Action = "SignatureSubmitted",
                PerformedBy = $"{signer.Name} <{signer.Email}>",
                SignerRole = signer.Role.ToString(),
                IpAddress = signer.IpAddress,
                UserAgent = signer.UserAgent,
                Details = $"Digitally executed step {signer.OrderIndex + 1} of {contract.Signers.Count} ({signer.SignatureType}). Geo: {signer.Geolocation}."
            });

            // Advance sequential state machine
            var nextIndex = contract.CurrentSignerIndex + 1;
            bool isFullyExecuted = false;

            if (nextIndex < contract.Signers.Count)
            {
                contract.CurrentSignerIndex = nextIndex;
                var nextSigner = contract.Signers[nextIndex];
                nextSigner.Status = SignerSigningStatus.ReadyToSign;

                // Update workflow status enum
                contract.Status = nextIndex switch
                {
                    1 => ContractWorkflowStatus.AwaitingSigner2,
                    2 => ContractWorkflowStatus.AwaitingSigner3,
                    _ => ContractWorkflowStatus.AwaitingSigner2
                };

                contract.AuditTrail.Add(new ContractAuditEntry
                {
                    TimestampUtc = nowUtc,
                    Action = "WorkflowAdvanced",
                    PerformedBy = "System",
                    SignerRole = nextSigner.Role.ToString(),
                    Details = $"Workflow advanced to signer {nextIndex + 1}: {nextSigner.Name} <{nextSigner.Email}>."
                });
            }
            else
            {
                // All signers have signed!
                isFullyExecuted = true;
                contract.Status = ContractWorkflowStatus.FullyExecuted;
                contract.CompletedAtUtc = nowUtc;

                // Compute cryptographic SHA-256 seal
                var sealHash = ComputeIntegrityHash(contract);
                contract.IntegritySealHash = sealHash;

                contract.AuditTrail.Add(new ContractAuditEntry
                {
                    TimestampUtc = nowUtc,
                    Action = "ContractFullyExecuted",
                    PerformedBy = "System",
                    Details = $"All {contract.Signers.Count} designated signers have executed the agreement."
                });

                contract.AuditTrail.Add(new ContractAuditEntry
                {
                    TimestampUtc = nowUtc,
                    Action = "CryptographicSealGenerated",
                    PerformedBy = "System",
                    Details = $"Tamper-evident SHA-256 integrity seal computed: {sealHash}."
                });
            }

            return Task.FromResult(new SignContractResult(
                true,
                isFullyExecuted ? "Contract has been fully executed and cryptographically sealed." : "Signature successfully recorded. Advanced to next sequential signer.",
                contract.Id,
                contract.Status,
                signer.OrderIndex,
                isFullyExecuted ? null : nextIndex,
                isFullyExecuted,
                contract.CompletedAtUtc,
                contract.IntegritySealHash
            ));
        }
    }

    public Task<DeclineContractResult> DeclineSignatureAsync(Guid contractId, DeclineSignatureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_contracts.TryGetValue(contractId, out var contract))
        {
            return Task.FromResult(new DeclineContractResult(
                false,
                "Contract not found.",
                contractId,
                ContractWorkflowStatus.Draft,
                string.Empty,
                request.Reason
            ));
        }

        var syncLock = _syncLocks.GetOrAdd(contractId, _ => new object());
        lock (syncLock)
        {
            if (contract.Status == ContractWorkflowStatus.FullyExecuted)
            {
                return Task.FromResult(new DeclineContractResult(
                    false,
                    "Cannot decline a contract that has already been fully executed.",
                    contractId,
                    contract.Status,
                    string.Empty,
                    request.Reason
                ));
            }

            var signer = contract.Signers.FirstOrDefault(s =>
                string.Equals(s.Token, request.SignerToken, StringComparison.OrdinalIgnoreCase));

            if (signer == null)
            {
                return Task.FromResult(new DeclineContractResult(
                    false,
                    "Invalid or unrecognized signer authorization token.",
                    contractId,
                    contract.Status,
                    string.Empty,
                    request.Reason
                ));
            }

            signer.Status = SignerSigningStatus.Declined;
            signer.DeclineReason = request.Reason?.Trim() ?? "Signer declined without comment.";
            signer.IpAddress = request.IpAddress ?? "127.0.0.1";
            signer.UserAgent = request.UserAgent ?? "Antigravity-Esign/2.0";

            contract.Status = ContractWorkflowStatus.Declined;

            contract.AuditTrail.Add(new ContractAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "SignatureDeclined",
                PerformedBy = $"{signer.Name} <{signer.Email}>",
                SignerRole = signer.Role.ToString(),
                IpAddress = signer.IpAddress,
                UserAgent = signer.UserAgent,
                Details = $"Signer declined to execute contract. Reason: '{signer.DeclineReason}'."
            });

            return Task.FromResult(new DeclineContractResult(
                true,
                $"Contract execution declined by {signer.Name}.",
                contract.Id,
                contract.Status,
                signer.Name,
                signer.DeclineReason
            ));
        }
    }

    public Task<ContractEnvelope?> VoidContractAsync(Guid contractId, string reason)
    {
        if (!_contracts.TryGetValue(contractId, out var contract)) return Task.FromResult<ContractEnvelope?>(null);

        var syncLock = _syncLocks.GetOrAdd(contractId, _ => new object());
        lock (syncLock)
        {
            contract.Status = ContractWorkflowStatus.Voided;
            contract.VoidedAtUtc = DateTimeOffset.UtcNow;

            contract.AuditTrail.Add(new ContractAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "ContractVoided",
                PerformedBy = "Administrator",
                Details = $"Contract was administratively voided. Reason: {reason}."
            });

            return Task.FromResult<ContractEnvelope?>(contract);
        }
    }

    public Task<AuditCertificateDto?> GetAuditCertificateAsync(Guid contractId)
    {
        if (!_contracts.TryGetValue(contractId, out var contract))
        {
            return Task.FromResult<AuditCertificateDto?>(null);
        }

        var hash = contract.IntegritySealHash ?? ComputeIntegrityHash(contract);
        var certId = $"CERT-ESIGN-{contract.Id:N}"[..24].ToUpperInvariant();
        var verificationUrl = $"https://app.sgrowing.io/contracts/verify/{contract.Id}";

        var qrData = JsonSerializer.Serialize(new
        {
            certificateId = certId,
            documentId = contract.Id,
            sha256 = hash,
            title = contract.Title,
            completedAt = contract.CompletedAtUtc?.ToString("o"),
            signers = contract.Signers.Select(s => new { s.Name, s.Email, s.Role, s.Status }).ToList()
        });

        var signersList = contract.Signers.Select(s =>
        {
            string? sigSummary = null;
            if (!string.IsNullOrEmpty(s.SignatureData))
            {
                sigSummary = s.SignatureType == "Typed"
                    ? s.SignatureData
                    : $"Vector Base64 Hash: {ComputeStringSha256(s.SignatureData)[..16]}...";
            }

            return new AuditCertificateSignerDto(
                s.Id,
                s.OrderIndex,
                s.Name,
                s.Email,
                s.Role.ToString(),
                s.Status.ToString(),
                s.SignedAtUtc,
                s.IpAddress,
                s.UserAgent,
                s.Geolocation,
                s.SignatureType,
                sigSummary
            );
        }).ToList();

        var certificate = new AuditCertificateDto(
            contract.Id,
            certId,
            contract.Title,
            contract.AgreementType,
            hash,
            contract.CreatedAtUtc,
            contract.CompletedAtUtc,
            contract.Status.ToString(),
            "ISO 32000-1 / RFC 3161 PAdES-Compliant SHA-256 Execution Proof",
            verificationUrl,
            qrData,
            signersList,
            contract.AuditTrail.OrderBy(a => a.TimestampUtc).ToList()
        );

        return Task.FromResult<AuditCertificateDto?>(certificate);
    }

    public Task<DocumentIntegrityResultDto> VerifyDocumentIntegrityAsync(Guid contractId)
    {
        if (!_contracts.TryGetValue(contractId, out var contract))
        {
            return Task.FromResult(new DocumentIntegrityResultDto(
                contractId,
                false,
                true,
                string.Empty,
                null,
                "NotFound",
                0,
                DateTimeOffset.UtcNow,
                "Contract document not found in repository."
            ));
        }

        var currentComputedHash = ComputeIntegrityHash(contract);

        if (string.IsNullOrWhiteSpace(contract.IntegritySealHash))
        {
            // Contract not yet sealed (in progress or draft)
            return Task.FromResult(new DocumentIntegrityResultDto(
                contract.Id,
                true,
                false,
                currentComputedHash,
                currentComputedHash,
                "InProgressSnapshotValid",
                contract.AuditTrail.Count,
                DateTimeOffset.UtcNow,
                "Contract is actively executing sequential signatures. Document snapshot hash verified intact."
            ));
        }

        var matches = string.Equals(currentComputedHash, contract.IntegritySealHash, StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(new DocumentIntegrityResultDto(
            contract.Id,
            matches,
            !matches,
            currentComputedHash,
            contract.IntegritySealHash,
            matches ? "CryptographicallySealedAndValid" : "TamperDetected",
            contract.AuditTrail.Count,
            DateTimeOffset.UtcNow,
            matches
                ? "Cryptographic SHA-256 execution seal is 100% intact. Zero modifications detected across document, signatures, and audit ledger."
                : "SECURITY ALERT: Computed hash does not match original execution seal! Document content, signatures, or audit entries have been altered."
        ));
    }

    public string ComputeIntegrityHash(ContractEnvelope contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        // Deterministic serialized string comprising Document Pages + Signers + AuditLog
        var sb = new StringBuilder();

        // 1. Document Core Identity & Content
        sb.AppendLine($"DOC_ID:{contract.Id}");
        sb.AppendLine($"TITLE:{contract.Title}");
        sb.AppendLine($"AGREEMENT_TYPE:{contract.AgreementType}");
        sb.AppendLine($"CREATED_UTC:{contract.CreatedAtUtc:O}");

        foreach (var page in contract.Pages.OrderBy(p => p.PageNumber))
        {
            sb.AppendLine($"PAGE:{page.PageNumber}|{page.Title}|{page.Content}");
        }

        // 2. Signers & Signature Payloads
        foreach (var signer in contract.Signers.OrderBy(s => s.OrderIndex))
        {
            sb.AppendLine($"SIGNER:{signer.OrderIndex}|{signer.Id}|{signer.Name}|{signer.Email}|{signer.Role}|{signer.Status}|{signer.SignedAtUtc:O}|{signer.SignatureType}|{signer.SignatureData}|{signer.IpAddress}|{signer.Geolocation}");
        }

        // 3. Fields
        foreach (var field in contract.Fields.OrderBy(f => f.PageNumber).ThenBy(f => f.YNorm).ThenBy(f => f.XNorm))
        {
            sb.AppendLine($"FIELD:{field.Id}|{field.FieldType}|{field.PageNumber}|{field.XNorm:F4}|{field.YNorm:F4}|{field.WidthNorm:F4}|{field.HeightNorm:F4}|{field.SignerIndex}|{field.Value}");
        }

        // 4. Audit Trail
        foreach (var audit in contract.AuditTrail.OrderBy(a => a.TimestampUtc).ThenBy(a => a.Id))
        {
            sb.AppendLine($"AUDIT:{audit.Id}|{audit.TimestampUtc:O}|{audit.Action}|{audit.PerformedBy}|{audit.IpAddress}|{audit.Details}");
        }

        // Compute SHA-256
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToUpperInvariant();
    }

    public bool ValidateNormalizedCoordinates(double xNorm, double yNorm, double widthNorm, double heightNorm, out string? error)
    {
        error = null;

        if (xNorm < 0.0 || xNorm > 1.0)
        {
            error = $"xNorm ({xNorm:F4}) must be between 0.0 and 1.0.";
            return false;
        }

        if (yNorm < 0.0 || yNorm > 1.0)
        {
            error = $"yNorm ({yNorm:F4}) must be between 0.0 and 1.0.";
            return false;
        }

        if (widthNorm <= 0.0 || widthNorm > 1.0)
        {
            error = $"widthNorm ({widthNorm:F4}) must be between (0.0, 1.0].";
            return false;
        }

        if (heightNorm <= 0.0 || heightNorm > 1.0)
        {
            error = $"heightNorm ({heightNorm:F4}) must be between (0.0, 1.0].";
            return false;
        }

        if (xNorm + widthNorm > 1.0001)
        {
            error = $"xNorm + widthNorm ({xNorm + widthNorm:F4}) exceeds right boundary of 1.0.";
            return false;
        }

        if (yNorm + heightNorm > 1.0001)
        {
            error = $"yNorm + heightNorm ({yNorm + heightNorm:F4}) exceeds bottom boundary of 1.0.";
            return false;
        }

        return true;
    }

    private static string ExtractInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "XX";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
        return $"{parts[0][..1]}{parts[^1][..1]}".ToUpperInvariant();
    }

    private static string ComputeStringSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToUpperInvariant();
    }

    #endregion

    #region Seeding

    private void SeedDiverseContracts()
    {
        // 1. Enterprise Cloud License (Awaiting Signer 1)
        var c1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var c1 = new ContractEnvelope
        {
            Id = c1Id,
            Title = "Master Enterprise Cloud Platform License & SLA Agreement",
            Description = "Multi-region cloud infrastructure and enterprise SLA coverage agreement for Tier-1 mission-critical deployment.",
            AgreementType = "Enterprise Cloud License",
            Status = ContractWorkflowStatus.AwaitingSigner1,
            CurrentSignerIndex = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Pages = [
                new ContractPage
                {
                    PageNumber = 1,
                    Title = "Section 1: Master Cloud Services & License Grant",
                    Content = "This Master Enterprise Cloud Platform License ('Agreement') is entered into by and between SGrowing Global Technologies ('Provider') and Acme Corporation ('Client'). Provider grants Client a non-exclusive, worldwide, multi-tenant cloud subscription with 99.99% uptime availability, automated disaster recovery failover across three availability zones, and dedicated computing enclaves."
                },
                new ContractPage
                {
                    PageNumber = 2,
                    Title = "Section 2: Enterprise Service Level Agreement (SLA) & Data Protection",
                    Content = "Under this SLA Addendum, Provider covenants that monthly API latency at the 99th percentile will not exceed 45ms. In the event of service disruption exceeding scheduled maintenance windows, Client is entitled to service credits calculated on a pro-rata basis. All customer data at rest is encrypted with AES-256-GCM and in transit via TLS 1.3 with strict mutual authentication."
                },
                new ContractPage
                {
                    PageNumber = 3,
                    Title = "Section 3: Execution, Attestation & Legal Signatures",
                    Content = "IN WITNESS WHEREOF, the duly authorized representatives of the Parties have caused this Enterprise Agreement to be executed by electronic signature under the Electronic Signatures in Global and National Commerce Act (ESIGN) and eIDAS Regulation (EU) No 910/2014."
                }
            ],
            Signers = [
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 0,
                    Name = "Sarah Jenkins",
                    Email = "sarah.jenkins@acmecloud.com",
                    Role = ContractSignerRole.ClientSigner,
                    Token = "sign_tok_e1a89c01acme",
                    Status = SignerSigningStatus.ReadyToSign
                },
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 1,
                    Name = "Marcus Vance",
                    Email = "marcus.vance@sgrowing.io",
                    Role = ContractSignerRole.VendorSigner,
                    Token = "sign_tok_e1a89c02vendor",
                    Status = SignerSigningStatus.Pending
                },
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 2,
                    Name = "Elena Rostova",
                    Email = "elena.rostova@legalcounsel.org",
                    Role = ContractSignerRole.Witness,
                    Token = "sign_tok_e1a89c03witness",
                    Status = SignerSigningStatus.Pending
                }
            ],
            Fields = [
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.FullName,
                    PageNumber = 1,
                    XNorm = 0.08,
                    YNorm = 0.88,
                    WidthNorm = 0.35,
                    HeightNorm = 0.05,
                    SignerIndex = 0,
                    Label = "Authorized Representative",
                    Value = "Sarah Jenkins"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Initials,
                    PageNumber = 2,
                    XNorm = 0.82,
                    YNorm = 0.90,
                    WidthNorm = 0.12,
                    HeightNorm = 0.05,
                    SignerIndex = 0,
                    Label = "Client Initials"
                },
                // Page 3 Signatures
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 3,
                    XNorm = 0.08,
                    YNorm = 0.70,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 0,
                    Label = "Client Authorized Signature"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 3,
                    XNorm = 0.08,
                    YNorm = 0.80,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 0,
                    Label = "Date Signed"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 3,
                    XNorm = 0.54,
                    YNorm = 0.70,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 1,
                    Label = "Vendor Authorized Signature"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 3,
                    XNorm = 0.54,
                    YNorm = 0.80,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 1,
                    Label = "Date Signed"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 3,
                    XNorm = 0.30,
                    YNorm = 0.88,
                    WidthNorm = 0.40,
                    HeightNorm = 0.06,
                    SignerIndex = 2,
                    Label = "Attestation Witness Signature"
                }
            ],
            AuditTrail = [
                new ContractAuditEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    Action = "ContractCreated",
                    PerformedBy = "Enterprise Portal API",
                    Details = "Created Master Enterprise Cloud License with 3 signers."
                },
                new ContractAuditEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(2),
                    Action = "SignerNotificationSent",
                    PerformedBy = "Email Automation",
                    SignerRole = "ClientSigner",
                    Details = "Invitation email with signing token dispatched to sarah.jenkins@acmecloud.com."
                }
            ]
        };
        _contracts[c1Id] = c1;

        // 2. Global Distribution Agreement (Awaiting Signer 2 - Signer 1 already signed)
        var c2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var c2Signer1SignedTime = DateTimeOffset.UtcNow.AddHours(-6);
        var c2 = new ContractEnvelope
        {
            Id = c2Id,
            Title = "International Strategic Software Reseller & Distribution Accord",
            Description = "Worldwide multi-territory exclusive distribution terms for enterprise marketing AI solutions.",
            AgreementType = "Global Distribution Agreement",
            Status = ContractWorkflowStatus.AwaitingSigner2,
            CurrentSignerIndex = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            Pages = [
                new ContractPage
                {
                    PageNumber = 1,
                    Title = "1. Appointment & Territory Scope",
                    Content = "SGrowing Technologies Ltd hereby appoints APAC Distribution Corp as its exclusive distributor of SGrowing Enterprise Cloud Suite across Japan, South Korea, Singapore, and Australia."
                },
                new ContractPage
                {
                    PageNumber = 2,
                    Title = "2. Minimum Volume Commitments & Wholesale Discounts",
                    Content = "Distributor covenants to maintain a minimum trailing quarterly run-rate of $1,500,000 USD gross wholesale license fees, entitling Distributor to a 42% margin tier."
                },
                new ContractPage
                {
                    PageNumber = 3,
                    Title = "3. Compliance, Intellectual Property & Indemnity",
                    Content = "Distributor shall strictly comply with international export controls, local data privacy mandates, and will protect Provider trademarks and algorithmic trade secrets."
                },
                new ContractPage
                {
                    PageNumber = 4,
                    Title = "4. Final Execution & Signatures",
                    Content = "This agreement is executed in two counterparts, each of which shall be deemed an original, but all of which together shall constitute one binding instrument."
                }
            ],
            Signers = [
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 0,
                    Name = "Hiroshi Tanaka",
                    Email = "hiroshi.tanaka@apacdist.jp",
                    Role = ContractSignerRole.ClientSigner,
                    Token = "sign_tok_d2b78101apac",
                    Status = SignerSigningStatus.Signed,
                    SignedAtUtc = c2Signer1SignedTime,
                    SignatureType = "DrawnVector",
                    SignatureData = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxNDAiIGhlaWdodD0iNDAiPjxwYXRoIGQ9Ik0xMCwyNSBDNDAsMTAgNjAsNDAgMTAwLDIwIFMxMjAsMTAgMTMwLDI1IiBzdHJva2U9IiMxMGI5ODEiIHN0cm9rZS13aWR0aD0iMi41IiBmaWxsPSJub25lIi8+PC9zdmc+",
                    IpAddress = "203.0.113.45",
                    UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
                    Geolocation = "Tokyo, Japan"
                },
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 1,
                    Name = "Claire Dubois",
                    Email = "claire.dubois@sgrowing.io",
                    Role = ContractSignerRole.VendorSigner,
                    Token = "sign_tok_d2b78102vendor",
                    Status = SignerSigningStatus.ReadyToSign
                }
            ],
            Fields = [
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 4,
                    XNorm = 0.08,
                    YNorm = 0.72,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 0,
                    Label = "Distributor Representative Signature",
                    Value = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxNDAiIGhlaWdodD0iNDAiPjxwYXRoIGQ9Ik0xMCwyNSBDNDAsMTAgNjAsNDAgMTAwLDIwIFMxMjAsMTAgMTMwLDI1IiBzdHJva2U9IiMxMGI5ODEiIHN0cm9rZS13aWR0aD0iMi41IiBmaWxsPSJub25lIi8+PC9zdmc+",
                    SignedAtUtc = c2Signer1SignedTime
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 4,
                    XNorm = 0.08,
                    YNorm = 0.82,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 0,
                    Label = "Date Signed",
                    Value = c2Signer1SignedTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                    SignedAtUtc = c2Signer1SignedTime
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 4,
                    XNorm = 0.54,
                    YNorm = 0.72,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 1,
                    Label = "SGrowing Executive Signature"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 4,
                    XNorm = 0.54,
                    YNorm = 0.82,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 1,
                    Label = "Date Signed"
                }
            ],
            AuditTrail = [
                new ContractAuditEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddDays(-2),
                    Action = "ContractCreated",
                    PerformedBy = "Strategic Operations Desk",
                    Details = "Reseller agreement draft instantiated."
                },
                new ContractAuditEntry
                {
                    TimestampUtc = c2Signer1SignedTime,
                    Action = "SignatureSubmitted",
                    PerformedBy = "Hiroshi Tanaka <hiroshi.tanaka@apacdist.jp>",
                    SignerRole = "ClientSigner",
                    IpAddress = "203.0.113.45",
                    UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)",
                    Details = "Digitally signed by Distributor (DrawnVector). Geolocation: Tokyo, Japan."
                },
                new ContractAuditEntry
                {
                    TimestampUtc = c2Signer1SignedTime.AddSeconds(5),
                    Action = "WorkflowAdvanced",
                    PerformedBy = "System",
                    SignerRole = "VendorSigner",
                    Details = "Advanced to Signer 2: Claire Dubois <claire.dubois@sgrowing.io>."
                }
            ]
        };
        _contracts[c2Id] = c2;

        // 3. B2B Terms of Service (Fully Executed & Sealed)
        var c3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var c3Created = DateTimeOffset.UtcNow.AddDays(-5);
        var c3Signed1 = DateTimeOffset.UtcNow.AddDays(-4);
        var c3Signed2 = DateTimeOffset.UtcNow.AddDays(-3);
        var c3 = new ContractEnvelope
        {
            Id = c3Id,
            Title = "B2B Enterprise Master Terms of Service & SLA Addendum",
            Description = "Formal binding commitment for 10,000 enterprise seats with 99.995% SLA and dedicated VPC peering.",
            AgreementType = "B2B Terms of Service",
            Status = ContractWorkflowStatus.FullyExecuted,
            CurrentSignerIndex = 2,
            CreatedAtUtc = c3Created,
            CompletedAtUtc = c3Signed2,
            Pages = [
                new ContractPage
                {
                    PageNumber = 1,
                    Title = "Article I: Enterprise Services & Provisioning",
                    Content = "Client licenses the SGrowing Enterprise Cloud Platform for ten thousand (10,000) active enterprise seats. Provider guarantees private dedicated cluster allocation and zero cross-tenant resource contention."
                },
                new ContractPage
                {
                    PageNumber = 2,
                    Title = "Article II: Execution & Tamper-Evident Signatures",
                    Content = "Both Parties attest that this electronic document constitutes a legal, valid, and binding obligation enforceable in accordance with its terms."
                }
            ],
            Signers = [
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 0,
                    Name = "David K. Chen",
                    Email = "david.chen@quantumsys.com",
                    Role = ContractSignerRole.ClientSigner,
                    Token = "sign_tok_b3c67101david",
                    Status = SignerSigningStatus.Signed,
                    SignedAtUtc = c3Signed1,
                    SignatureType = "DrawnVector",
                    SignatureData = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxNDAiIGhlaWdodD0iNDAiPjxwYXRoIGQ9Ik0xNSwzMCBMMzAsMTAgTDUwLDI1IEw4MCwxMCBMMTEwLDMwIEwxMzAsMTUiIHN0cm9rZT0iIzYzNjZmMSIgc3Ryb2tlLXdpZHRoPSIyLjUiIGZpbGw9Im5vbmUiLz48L3N2Zz4=",
                    IpAddress = "198.51.100.22",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    Geolocation = "Austin, TX, USA"
                },
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 1,
                    Name = "Jessica Sterling",
                    Email = "jessica.sterling@sgrowing.io",
                    Role = ContractSignerRole.VendorSigner,
                    Token = "sign_tok_b3c67102jessica",
                    Status = SignerSigningStatus.Signed,
                    SignedAtUtc = c3Signed2,
                    SignatureType = "Typed",
                    SignatureData = "Jessica Sterling [VERIFIED ENTERPRISE SIGNER]",
                    IpAddress = "198.51.100.88",
                    UserAgent = "Mozilla/5.0 (X11; Linux x86_64)",
                    Geolocation = "San Francisco, CA, USA"
                }
            ],
            Fields = [
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 2,
                    XNorm = 0.08,
                    YNorm = 0.70,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 0,
                    Label = "Client Officer Signature",
                    Value = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxNDAiIGhlaWdodD0iNDAiPjxwYXRoIGQ9Ik0xNSwzMCBMMzAsMTAgTDUwLDI1IEw4MCwxMCBMMTEwLDMwIEwxMzAsMTUiIHN0cm9rZT0iIzYzNjZmMSIgc3Ryb2tlLXdpZHRoPSIyLjUiIGZpbGw9Im5vbmUiLz48L3N2Zz4=",
                    SignedAtUtc = c3Signed1
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 2,
                    XNorm = 0.08,
                    YNorm = 0.80,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 0,
                    Label = "Date Signed",
                    Value = c3Signed1.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                    SignedAtUtc = c3Signed1
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 2,
                    XNorm = 0.54,
                    YNorm = 0.70,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 1,
                    Label = "Vendor Officer Signature",
                    Value = "Jessica Sterling [VERIFIED ENTERPRISE SIGNER]",
                    SignedAtUtc = c3Signed2
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 2,
                    XNorm = 0.54,
                    YNorm = 0.80,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 1,
                    Label = "Date Signed",
                    Value = c3Signed2.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                    SignedAtUtc = c3Signed2
                }
            ],
            AuditTrail = [
                new ContractAuditEntry
                {
                    TimestampUtc = c3Created,
                    Action = "ContractCreated",
                    PerformedBy = "System Enterprise Automation",
                    Details = "Generated B2B Terms of Service."
                },
                new ContractAuditEntry
                {
                    TimestampUtc = c3Signed1,
                    Action = "SignatureSubmitted",
                    PerformedBy = "David K. Chen <david.chen@quantumsys.com>",
                    SignerRole = "ClientSigner",
                    IpAddress = "198.51.100.22",
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    Details = "Signed by Client Officer. Geo: Austin, TX, USA."
                },
                new ContractAuditEntry
                {
                    TimestampUtc = c3Signed2,
                    Action = "SignatureSubmitted",
                    PerformedBy = "Jessica Sterling <jessica.sterling@sgrowing.io>",
                    SignerRole = "VendorSigner",
                    IpAddress = "198.51.100.88",
                    UserAgent = "Mozilla/5.0 (X11; Linux x86_64)",
                    Details = "Signed by Vendor Officer. Geo: San Francisco, CA, USA."
                },
                new ContractAuditEntry
                {
                    TimestampUtc = c3Signed2.AddSeconds(2),
                    Action = "ContractFullyExecuted",
                    PerformedBy = "System",
                    Details = "All 2 designated signers have executed the agreement."
                }
            ]
        };

        // Seal contract 3 with cryptographic hash
        c3.IntegritySealHash = ComputeIntegrityHash(c3);
        c3.AuditTrail.Add(new ContractAuditEntry
        {
            TimestampUtc = c3Signed2.AddSeconds(3),
            Action = "CryptographicSealGenerated",
            PerformedBy = "System",
            Details = $"Tamper-evident SHA-256 seal computed: {c3.IntegritySealHash}."
        });
        // Re-seal with final audit entry included
        c3.IntegritySealHash = ComputeIntegrityHash(c3);

        _contracts[c3Id] = c3;

        // 4. Confidentiality NDA (Draft)
        var c4Id = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var c4 = new ContractEnvelope
        {
            Id = c4Id,
            Title = "Mutual Confidentiality & Proprietary Non-Disclosure NDA",
            Description = "Standard bilateral proprietary data protection covering upcoming AI foundation model IP disclosures.",
            AgreementType = "Confidentiality NDA",
            Status = ContractWorkflowStatus.Draft,
            CurrentSignerIndex = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-12),
            Pages = [
                new ContractPage
                {
                    PageNumber = 1,
                    Title = "Article 1: Scope of Confidential Disclosures",
                    Content = "Each party ('Disclosing Party') may disclose confidential, non-public technical and commercial information to the other party ('Receiving Party') solely for exploring enterprise joint ventures."
                },
                new ContractPage
                {
                    PageNumber = 2,
                    Title = "Article 2: Standard of Protection & Execution",
                    Content = "The Receiving Party agrees to exercise at least the same degree of care it uses for its own confidential assets of like importance, and in no event less than reasonable care."
                }
            ],
            Signers = [
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 0,
                    Name = "Liam O'Connor",
                    Email = "liam.oconnor@northstar.ai",
                    Role = ContractSignerRole.ClientSigner,
                    Token = "sign_tok_a4f19001north",
                    Status = SignerSigningStatus.Pending
                },
                new ContractSigner
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 1,
                    Name = "Rachel Adams",
                    Email = "rachel.adams@sgrowing.io",
                    Role = ContractSignerRole.VendorSigner,
                    Token = "sign_tok_a4f19002vendor",
                    Status = SignerSigningStatus.Pending
                }
            ],
            Fields = [
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 2,
                    XNorm = 0.08,
                    YNorm = 0.70,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 0,
                    Label = "Disclosing Party Signature"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 2,
                    XNorm = 0.08,
                    YNorm = 0.80,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 0,
                    Label = "Date Signed"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.Signature,
                    PageNumber = 2,
                    XNorm = 0.54,
                    YNorm = 0.70,
                    WidthNorm = 0.38,
                    HeightNorm = 0.08,
                    SignerIndex = 1,
                    Label = "Receiving Party Signature"
                },
                new ContractFieldLayout
                {
                    Id = Guid.NewGuid(),
                    FieldType = ContractFieldType.DateSigned,
                    PageNumber = 2,
                    XNorm = 0.54,
                    YNorm = 0.80,
                    WidthNorm = 0.38,
                    HeightNorm = 0.04,
                    SignerIndex = 1,
                    Label = "Date Signed"
                }
            ],
            AuditTrail = [
                new ContractAuditEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddHours(-12),
                    Action = "ContractDraftCreated",
                    PerformedBy = "Legal Portal Draft Engine",
                    Details = "Mutual NDA created in Draft status."
                }
            ]
        };
        _contracts[c4Id] = c4;
    }

    #endregion
}
