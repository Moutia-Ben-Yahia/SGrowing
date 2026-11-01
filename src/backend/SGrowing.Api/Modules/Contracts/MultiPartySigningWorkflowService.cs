using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Contracts;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignerWorkflowMode
{
    Sequential,
    Parallel
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SignerLifecycleStatus
{
    Invited,
    Viewed,
    PartiallySigned,
    FullySigned,
    Declined,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel
{
    WhatsApp,
    Email,
    Sms
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationTriggerType
{
    Invitation,
    Reminder,
    CompletionNotice,
    DeclinedNotice
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MultiPartyContractStatus
{
    Draft,
    InProgress,
    PartiallySigned,
    Completed,
    Declined,
    Expired,
    Voided
}

#endregion

#region Records & DTOs

public record SignerNotificationRecord(
    Guid NotificationId,
    Guid SignerId,
    string Recipient,
    NotificationChannel Channel,
    NotificationTriggerType TriggerType,
    DateTimeOffset SentAtUtc,
    string Status,
    string MessageBody,
    string ActionUrl
);

public class MultiPartySigner
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public ContractSignerRole Role { get; set; } = ContractSignerRole.ClientSigner;
    public SignerLifecycleStatus LifecycleStatus { get; set; } = SignerLifecycleStatus.Invited;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset? TokenExpiresAtUtc { get; set; }
    public DateTimeOffset? InvitedAtUtc { get; set; }
    public DateTimeOffset? ViewedAtUtc { get; set; }
    public DateTimeOffset? PartiallySignedAtUtc { get; set; }
    public DateTimeOffset? FullySignedAtUtc { get; set; }
    public DateTimeOffset? DeclinedAtUtc { get; set; }
    public string? DeclineReason { get; set; }
    public string? SignatureType { get; set; } // "DrawnVector", "Typed", "ImageUpload"
    public string? SignatureData { get; set; } // Normalized SVG path or base64 data
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Geolocation { get; set; }
    public List<SignerNotificationRecord> NotificationHistory { get; set; } = [];
}

public class MultiPartyWorkflowEnvelope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AgreementType { get; set; } = string.Empty;
    public SignerWorkflowMode WorkflowMode { get; set; } = SignerWorkflowMode.Sequential;
    public MultiPartyContractStatus Status { get; set; } = MultiPartyContractStatus.InProgress;
    public int CurrentSignerIndex { get; set; } = 0;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public List<ContractPage> Pages { get; set; } = [];
    public List<MultiPartySigner> Signers { get; set; } = [];
    public List<ContractAuditEntry> AuditTrail { get; set; } = [];
    public string? SealedCertificateHash { get; set; }
}

public record SigningTokenPayload(
    Guid TenantId,
    Guid ContractId,
    Guid SignerId,
    int SignerIndex,
    DateTimeOffset ExpiresAtUtc,
    string Nonce
);

public record SigningLinkDto(
    string Token,
    string Url,
    DateTimeOffset ExpiresAtUtc,
    Guid SignerId,
    string SignerEmail
);

public record CreateMultiPartySignerRequest(
    string Name,
    string Email,
    string? PhoneNumber,
    ContractSignerRole Role,
    int? OrderIndex = null
);

public record CreateMultiPartyWorkflowRequest(
    Guid TenantId,
    string Title,
    string Description,
    string AgreementType,
    SignerWorkflowMode WorkflowMode,
    List<ContractPage> Pages,
    List<CreateMultiPartySignerRequest> Signers,
    TimeSpan? ExpirationWindow = null
);

public record SubmitMultiPartySignatureRequest(
    string SignerToken,
    string SignatureType, // "DrawnVector", "Typed", "ImageUpload"
    string SignatureData,
    bool IsPartialSignature = false,
    string? IpAddress = null,
    string? UserAgent = null,
    string? Geolocation = null
);

public record SubmitMultiPartySignatureResult(
    bool Success,
    string Message,
    Guid ContractId,
    MultiPartyContractStatus Status,
    Guid SignerId,
    SignerLifecycleStatus SignerStatus,
    int? NextSignerIndex,
    bool IsFullyExecuted,
    DateTimeOffset? CompletedAtUtc
);

public record DeclineMultiPartySignatureRequest(
    string SignerToken,
    string Reason,
    string? IpAddress = null,
    string? UserAgent = null
);

public record DeclineMultiPartyResult(
    bool Success,
    string Message,
    Guid ContractId,
    MultiPartyContractStatus Status,
    string DeclinedBySigner,
    string Reason
);

#endregion

#region Service Interface

public interface IMultiPartySigningWorkflowService
{
    Task<MultiPartyWorkflowEnvelope> CreateWorkflowAsync(CreateMultiPartyWorkflowRequest request);
    Task<MultiPartyWorkflowEnvelope?> GetWorkflowAsync(Guid tenantId, Guid contractId);
    Task<MultiPartySigner?> GetSignerByTokenAsync(string token);
    SigningLinkDto GenerateSigningLink(Guid tenantId, Guid contractId, Guid signerId, int signerIndex, string signerEmail, TimeSpan? expiration = null);
    bool ValidateSigningToken(string token, out SigningTokenPayload? payload, out string? errorMessage);
    Task<MultiPartySigner?> RecordSignerViewedAsync(Guid tenantId, Guid contractId, string token, string? ipAddress = null, string? userAgent = null, string? geolocation = null);
    Task<SubmitMultiPartySignatureResult> SubmitSignatureAsync(Guid tenantId, Guid contractId, SubmitMultiPartySignatureRequest request);
    Task<DeclineMultiPartyResult> DeclineSignatureAsync(Guid tenantId, Guid contractId, DeclineMultiPartySignatureRequest request);
    Task<List<SignerNotificationRecord>> TriggerRecipientNotificationsAsync(Guid tenantId, Guid contractId, Guid signerId, List<NotificationChannel> channels);
    List<MultiPartyWorkflowEnvelope> GetAllWorkflows(Guid tenantId);
}

#endregion

#region Service Implementation

public class MultiPartySigningWorkflowService : IMultiPartySigningWorkflowService
{
    private static readonly byte[] HmacSecretKey = Encoding.UTF8.GetBytes("SGrowing_Enterprise_Esign_Hmac_SigningKey_2026_SecureVault!");
    private readonly ConcurrentDictionary<Guid, MultiPartyWorkflowEnvelope> _contracts = new();

    public MultiPartySigningWorkflowService()
    {
        SeedInitialWorkflow();
    }

    public Task<MultiPartyWorkflowEnvelope> CreateWorkflowAsync(CreateMultiPartyWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Signers == null || request.Signers.Count == 0)
        {
            throw new ArgumentException("A multi-party signing workflow requires at least one signer.", nameof(request));
        }

        var contractId = Guid.NewGuid();
        var expirationWindow = request.ExpirationWindow ?? TimeSpan.FromHours(72);
        var workflowExpiresAt = DateTimeOffset.UtcNow.Add(expirationWindow);

        var envelope = new MultiPartyWorkflowEnvelope
        {
            Id = contractId,
            TenantId = request.TenantId,
            Title = request.Title,
            Description = request.Description,
            AgreementType = request.AgreementType,
            WorkflowMode = request.WorkflowMode,
            Status = MultiPartyContractStatus.InProgress,
            CurrentSignerIndex = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = workflowExpiresAt,
            Pages = request.Pages?.Count > 0 ? request.Pages : [
                new() { PageNumber = 1, Title = "General Terms & Recitals", Content = "Enterprise software agreement." }
            ],
            Signers = [],
            AuditTrail = []
        };

        envelope.AuditTrail.Add(new ContractAuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Action = "WorkflowCreated",
            PerformedBy = "System",
            Details = $"Multi-party signing workflow created in {request.WorkflowMode} mode with {request.Signers.Count} signers. Expiration window: {expirationWindow.TotalHours}h."
        });

        // Initialize signers
        for (int i = 0; i < request.Signers.Count; i++)
        {
            var reqSigner = request.Signers[i];
            var signerId = Guid.NewGuid();
            var orderIndex = reqSigner.OrderIndex ?? i;

            var link = GenerateSigningLink(
                request.TenantId,
                contractId,
                signerId,
                orderIndex,
                reqSigner.Email,
                expirationWindow
            );

            // In Sequential mode, only the first signer is active/Invited. Later signers are Pending until their turn.
            // In Parallel mode, all signers are immediately Invited.
            var isInitiallyInvited = request.WorkflowMode == SignerWorkflowMode.Parallel || i == 0;

            var signer = new MultiPartySigner
            {
                Id = signerId,
                OrderIndex = orderIndex,
                Name = reqSigner.Name,
                Email = reqSigner.Email,
                PhoneNumber = reqSigner.PhoneNumber,
                Role = reqSigner.Role,
                LifecycleStatus = isInitiallyInvited ? SignerLifecycleStatus.Invited : SignerLifecycleStatus.Invited, // initial lifecycle state
                Token = link.Token,
                TokenExpiresAtUtc = link.ExpiresAtUtc,
                InvitedAtUtc = isInitiallyInvited ? DateTimeOffset.UtcNow : null,
                NotificationHistory = []
            };

            envelope.Signers.Add(signer);

            if (isInitiallyInvited)
            {
                // Dispatch automated multi-channel invitations (WhatsApp, Email, SMS)
                SendNotifications(envelope, signer, NotificationTriggerType.Invitation, link.Url, [
                    NotificationChannel.Email,
                    NotificationChannel.WhatsApp,
                    NotificationChannel.Sms
                ]);
            }
        }

        _contracts[contractId] = envelope;
        return Task.FromResult(envelope);
    }

    public Task<MultiPartyWorkflowEnvelope?> GetWorkflowAsync(Guid tenantId, Guid contractId)
    {
        if (_contracts.TryGetValue(contractId, out var envelope) && envelope.TenantId == tenantId)
        {
            CheckAndApplyExpiration(envelope);
            return Task.FromResult<MultiPartyWorkflowEnvelope?>(envelope);
        }
        return Task.FromResult<MultiPartyWorkflowEnvelope?>(null);
    }

    public List<MultiPartyWorkflowEnvelope> GetAllWorkflows(Guid tenantId)
    {
        return _contracts.Values.Where(c => c.TenantId == tenantId).ToList();
    }

    public Task<MultiPartySigner?> GetSignerByTokenAsync(string token)
    {
        if (!ValidateSigningToken(token, out var payload, out _))
        {
            return Task.FromResult<MultiPartySigner?>(null);
        }

        if (_contracts.TryGetValue(payload!.ContractId, out var envelope))
        {
            var signer = envelope.Signers.FirstOrDefault(s => s.Id == payload.SignerId);
            return Task.FromResult(signer);
        }

        return Task.FromResult<MultiPartySigner?>(null);
    }

    public SigningLinkDto GenerateSigningLink(
        Guid tenantId,
        Guid contractId,
        Guid signerId,
        int signerIndex,
        string signerEmail,
        TimeSpan? expiration = null)
    {
        var window = expiration ?? TimeSpan.FromHours(72);
        var expiresAt = DateTimeOffset.UtcNow.Add(window);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

        var payload = new SigningTokenPayload(tenantId, contractId, signerId, signerIndex, expiresAt, nonce);
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadBase64 = Convert.ToBase64String(payloadBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        using var hmac = new HMACSHA256(HmacSecretKey);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var signatureBase64 = Convert.ToBase64String(hashBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var token = $"{payloadBase64}.{signatureBase64}";
        var url = $"https://app.sgrowing.io/contracts/sign/{contractId}?token={token}";

        return new SigningLinkDto(token, url, expiresAt, signerId, signerEmail);
    }

    public bool ValidateSigningToken(string token, out SigningTokenPayload? payload, out string? errorMessage)
    {
        payload = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            errorMessage = "Signer token is missing or empty.";
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            errorMessage = "Malformed signing token format.";
            return false;
        }

        try
        {
            var payloadBase64 = parts[0].Replace('-', '+').Replace('_', '/');
            switch (payloadBase64.Length % 4)
            {
                case 2: payloadBase64 += "=="; break;
                case 3: payloadBase64 += "="; break;
            }

            var signatureBase64 = parts[1].Replace('-', '+').Replace('_', '/');
            switch (signatureBase64.Length % 4)
            {
                case 2: signatureBase64 += "=="; break;
                case 3: signatureBase64 += "="; break;
            }

            var payloadBytes = Convert.FromBase64String(payloadBase64);
            var expectedSignatureBytes = Convert.FromBase64String(signatureBase64);

            using var hmac = new HMACSHA256(HmacSecretKey);
            var computedHash = hmac.ComputeHash(payloadBytes);

            if (!CryptographicOperations.FixedTimeEquals(computedHash, expectedSignatureBytes))
            {
                errorMessage = "Invalid cryptographic HMAC signature on signer token.";
                return false;
            }

            var parsed = JsonSerializer.Deserialize<SigningTokenPayload>(payloadBytes);
            if (parsed == null)
            {
                errorMessage = "Failed to deserialize token payload.";
                return false;
            }

            if (DateTimeOffset.UtcNow > parsed.ExpiresAtUtc)
            {
                errorMessage = $"Signing token expired at {parsed.ExpiresAtUtc:O}.";
                // Mark signer as expired if contract found
                if (_contracts.TryGetValue(parsed.ContractId, out var envelope))
                {
                    var signer = envelope.Signers.FirstOrDefault(s => s.Id == parsed.SignerId);
                    if (signer != null && signer.LifecycleStatus != SignerLifecycleStatus.FullySigned)
                    {
                        signer.LifecycleStatus = SignerLifecycleStatus.Expired;
                    }
                }
                return false;
            }

            payload = parsed;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Token cryptographic verification failure: {ex.Message}";
            return false;
        }
    }

    public Task<MultiPartySigner?> RecordSignerViewedAsync(
        Guid tenantId,
        Guid contractId,
        string token,
        string? ipAddress = null,
        string? userAgent = null,
        string? geolocation = null)
    {
        if (!ValidateSigningToken(token, out var payload, out _) || payload!.ContractId != contractId)
        {
            return Task.FromResult<MultiPartySigner?>(null);
        }

        if (!_contracts.TryGetValue(contractId, out var envelope) || envelope.TenantId != tenantId)
        {
            return Task.FromResult<MultiPartySigner?>(null);
        }

        var signer = envelope.Signers.FirstOrDefault(s => s.Id == payload.SignerId);
        if (signer == null)
        {
            return Task.FromResult<MultiPartySigner?>(null);
        }

        if (signer.LifecycleStatus == SignerLifecycleStatus.Invited)
        {
            signer.LifecycleStatus = SignerLifecycleStatus.Viewed;
            signer.ViewedAtUtc = DateTimeOffset.UtcNow;
            signer.IpAddress = ipAddress ?? signer.IpAddress;
            signer.UserAgent = userAgent ?? signer.UserAgent;
            signer.Geolocation = geolocation ?? signer.Geolocation;

            envelope.AuditTrail.Add(new ContractAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "DocumentViewed",
                PerformedBy = signer.Name,
                SignerRole = signer.Role.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Details = $"Signer {signer.Name} ({signer.Email}) viewed contract agreement in viewer."
            });
        }

        return Task.FromResult<MultiPartySigner?>(signer);
    }

    public Task<SubmitMultiPartySignatureResult> SubmitSignatureAsync(
        Guid tenantId,
        Guid contractId,
        SubmitMultiPartySignatureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ValidateSigningToken(request.SignerToken, out var payload, out var tokenError))
        {
            return Task.FromResult(new SubmitMultiPartySignatureResult(
                false,
                tokenError ?? "Invalid signing token.",
                contractId,
                MultiPartyContractStatus.InProgress,
                Guid.Empty,
                SignerLifecycleStatus.Declined,
                null,
                false,
                null
            ));
        }

        if (!_contracts.TryGetValue(contractId, out var envelope) || envelope.TenantId != tenantId)
        {
            return Task.FromResult(new SubmitMultiPartySignatureResult(
                false,
                $"Contract envelope {contractId} not found for tenant {tenantId}.",
                contractId,
                MultiPartyContractStatus.Draft,
                payload!.SignerId,
                SignerLifecycleStatus.Declined,
                null,
                false,
                null
            ));
        }

        lock (envelope)
        {
            CheckAndApplyExpiration(envelope);
            if (envelope.Status == MultiPartyContractStatus.Expired)
            {
                return Task.FromResult(new SubmitMultiPartySignatureResult(
                    false,
                    "This multi-party signing workflow has expired.",
                    contractId,
                    MultiPartyContractStatus.Expired,
                    payload!.SignerId,
                    SignerLifecycleStatus.Expired,
                    null,
                    false,
                    null
                ));
            }

            if (envelope.Status == MultiPartyContractStatus.Completed)
            {
                return Task.FromResult(new SubmitMultiPartySignatureResult(
                    false,
                    "Contract has already been fully executed.",
                    contractId,
                    envelope.Status,
                    payload!.SignerId,
                    SignerLifecycleStatus.FullySigned,
                    null,
                    true,
                    envelope.CompletedAtUtc
                ));
            }

            var signer = envelope.Signers.FirstOrDefault(s => s.Id == payload!.SignerId);
            if (signer == null)
            {
                return Task.FromResult(new SubmitMultiPartySignatureResult(
                    false,
                    "Signer not recognized in workflow envelope.",
                    contractId,
                    envelope.Status,
                    payload!.SignerId,
                    SignerLifecycleStatus.Declined,
                    null,
                    false,
                    null
                ));
            }

            if (signer.LifecycleStatus == SignerLifecycleStatus.FullySigned)
            {
                return Task.FromResult(new SubmitMultiPartySignatureResult(
                    false,
                    $"Signer {signer.Name} has already submitted their final signature.",
                    contractId,
                    envelope.Status,
                    signer.Id,
                    signer.LifecycleStatus,
                    envelope.CurrentSignerIndex,
                    envelope.Status == MultiPartyContractStatus.Completed,
                    envelope.CompletedAtUtc
                ));
            }

            // Sequential order enforcement
            if (envelope.WorkflowMode == SignerWorkflowMode.Sequential)
            {
                if (signer.OrderIndex != envelope.CurrentSignerIndex)
                {
                    return Task.FromResult(new SubmitMultiPartySignatureResult(
                        false,
                        $"Out-of-turn signature rejected. Sequential workflow is currently awaiting Signer #{envelope.CurrentSignerIndex + 1}.",
                        contractId,
                        envelope.Status,
                        signer.Id,
                        signer.LifecycleStatus,
                        envelope.CurrentSignerIndex,
                        false,
                        null
                    ));
                }
            }

            // Apply partial vs full signature
            signer.IpAddress = request.IpAddress ?? signer.IpAddress ?? "127.0.0.1";
            signer.UserAgent = request.UserAgent ?? signer.UserAgent ?? "EnterpriseViewer/1.0";
            signer.Geolocation = request.Geolocation ?? signer.Geolocation ?? "Local / Corporate Gateway";
            signer.SignatureType = request.SignatureType;
            signer.SignatureData = request.SignatureData;

            if (request.IsPartialSignature)
            {
                signer.LifecycleStatus = SignerLifecycleStatus.PartiallySigned;
                signer.PartiallySignedAtUtc = DateTimeOffset.UtcNow;
                envelope.Status = MultiPartyContractStatus.PartiallySigned;

                envelope.AuditTrail.Add(new ContractAuditEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Action = "PartialSignatureSaved",
                    PerformedBy = signer.Name,
                    SignerRole = signer.Role.ToString(),
                    IpAddress = signer.IpAddress,
                    UserAgent = signer.UserAgent,
                    Details = $"Signer {signer.Name} saved initials/partial signature ({request.SignatureType})."
                });

                return Task.FromResult(new SubmitMultiPartySignatureResult(
                    true,
                    "Partial signature recorded successfully.",
                    contractId,
                    envelope.Status,
                    signer.Id,
                    signer.LifecycleStatus,
                    envelope.CurrentSignerIndex,
                    false,
                    null
                ));
            }

            // Full signature completion for this signer
            signer.LifecycleStatus = SignerLifecycleStatus.FullySigned;
            signer.FullySignedAtUtc = DateTimeOffset.UtcNow;

            envelope.AuditTrail.Add(new ContractAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "SignerCompleted",
                PerformedBy = signer.Name,
                SignerRole = signer.Role.ToString(),
                IpAddress = signer.IpAddress,
                UserAgent = signer.UserAgent,
                Details = $"Signer {signer.Name} completed full digital execution ({request.SignatureType})."
            });

            // Advance sequential workflow or check parallel completion
            int? nextSignerIndex = null;
            bool isAllFullySigned = envelope.Signers.All(s => s.LifecycleStatus == SignerLifecycleStatus.FullySigned);

            if (envelope.WorkflowMode == SignerWorkflowMode.Sequential)
            {
                envelope.CurrentSignerIndex++;
                if (envelope.CurrentSignerIndex < envelope.Signers.Count)
                {
                    nextSignerIndex = envelope.CurrentSignerIndex;
                    var nextSigner = envelope.Signers.FirstOrDefault(s => s.OrderIndex == envelope.CurrentSignerIndex);
                    if (nextSigner != null)
                    {
                        nextSigner.LifecycleStatus = SignerLifecycleStatus.Invited;
                        nextSigner.InvitedAtUtc = DateTimeOffset.UtcNow;

                        // Generate fresh link and trigger automated notifications to the next signer
                        var nextLink = GenerateSigningLink(
                            tenantId,
                            contractId,
                            nextSigner.Id,
                            nextSigner.OrderIndex,
                            nextSigner.Email,
                            envelope.ExpiresAtUtc.HasValue ? envelope.ExpiresAtUtc.Value - DateTimeOffset.UtcNow : TimeSpan.FromHours(72)
                        );
                        nextSigner.Token = nextLink.Token;
                        nextSigner.TokenExpiresAtUtc = nextLink.ExpiresAtUtc;

                        SendNotifications(envelope, nextSigner, NotificationTriggerType.Invitation, nextLink.Url, [
                            NotificationChannel.Email,
                            NotificationChannel.WhatsApp,
                            NotificationChannel.Sms
                        ]);

                        envelope.AuditTrail.Add(new ContractAuditEntry
                        {
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Action = "SequentialAdvance",
                            PerformedBy = "System",
                            Details = $"Advanced workflow to Signer #{nextSigner.OrderIndex + 1} ({nextSigner.Name}). Notifications triggered."
                        });
                    }
                }
            }

            if (isAllFullySigned)
            {
                envelope.Status = MultiPartyContractStatus.Completed;
                envelope.CompletedAtUtc = DateTimeOffset.UtcNow;

                envelope.AuditTrail.Add(new ContractAuditEntry
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Action = "WorkflowFullyExecuted",
                    PerformedBy = "System",
                    Details = "All parties have completed legally-binding digital execution. Contract ready for cryptographic sealing."
                });

                // Notify all signers of contract completion
                foreach (var s in envelope.Signers)
                {
                    SendNotifications(envelope, s, NotificationTriggerType.CompletionNotice, $"https://app.sgrowing.io/contracts/view/{contractId}", [
                        NotificationChannel.Email,
                        NotificationChannel.WhatsApp
                    ]);
                }
            }
            else
            {
                envelope.Status = MultiPartyContractStatus.PartiallySigned;
            }

            return Task.FromResult(new SubmitMultiPartySignatureResult(
                true,
                isAllFullySigned ? "All signers completed. Contract fully executed!" : "Signature recorded. Workflow progressed.",
                contractId,
                envelope.Status,
                signer.Id,
                signer.LifecycleStatus,
                nextSignerIndex,
                isAllFullySigned,
                envelope.CompletedAtUtc
            ));
        }
    }

    public Task<DeclineMultiPartyResult> DeclineSignatureAsync(
        Guid tenantId,
        Guid contractId,
        DeclineMultiPartySignatureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ValidateSigningToken(request.SignerToken, out var payload, out var tokenError))
        {
            return Task.FromResult(new DeclineMultiPartyResult(
                false,
                tokenError ?? "Invalid signing token.",
                contractId,
                MultiPartyContractStatus.InProgress,
                "Unknown",
                request.Reason
            ));
        }

        if (!_contracts.TryGetValue(contractId, out var envelope) || envelope.TenantId != tenantId)
        {
            return Task.FromResult(new DeclineMultiPartyResult(
                false,
                "Contract envelope not found.",
                contractId,
                MultiPartyContractStatus.Draft,
                "Unknown",
                request.Reason
            ));
        }

        lock (envelope)
        {
            var signer = envelope.Signers.FirstOrDefault(s => s.Id == payload!.SignerId);
            if (signer == null)
            {
                return Task.FromResult(new DeclineMultiPartyResult(
                    false,
                    "Signer not found in envelope.",
                    contractId,
                    envelope.Status,
                    "Unknown",
                    request.Reason
                ));
            }

            signer.LifecycleStatus = SignerLifecycleStatus.Declined;
            signer.DeclinedAtUtc = DateTimeOffset.UtcNow;
            signer.DeclineReason = request.Reason;
            signer.IpAddress = request.IpAddress ?? signer.IpAddress;
            signer.UserAgent = request.UserAgent ?? signer.UserAgent;

            envelope.Status = MultiPartyContractStatus.Declined;

            envelope.AuditTrail.Add(new ContractAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "SignatureDeclined",
                PerformedBy = signer.Name,
                SignerRole = signer.Role.ToString(),
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Details = $"Contract declined by {signer.Name} ({signer.Email}). Reason: {request.Reason}"
            });

            // Notify other signers about declined agreement
            foreach (var s in envelope.Signers.Where(s => s.Id != signer.Id))
            {
                SendNotifications(envelope, s, NotificationTriggerType.DeclinedNotice, $"https://app.sgrowing.io/contracts/view/{contractId}", [
                    NotificationChannel.Email
                ]);
            }

            return Task.FromResult(new DeclineMultiPartyResult(
                true,
                "Contract signing declined successfully.",
                contractId,
                envelope.Status,
                signer.Name,
                request.Reason
            ));
        }
    }

    public Task<List<SignerNotificationRecord>> TriggerRecipientNotificationsAsync(
        Guid tenantId,
        Guid contractId,
        Guid signerId,
        List<NotificationChannel> channels)
    {
        if (!_contracts.TryGetValue(contractId, out var envelope) || envelope.TenantId != tenantId)
        {
            return Task.FromResult<List<SignerNotificationRecord>>([]);
        }

        var signer = envelope.Signers.FirstOrDefault(s => s.Id == signerId);
        if (signer == null)
        {
            return Task.FromResult<List<SignerNotificationRecord>>([]);
        }

        var actionUrl = $"https://app.sgrowing.io/contracts/sign/{contractId}?token={signer.Token}";
        var records = SendNotifications(envelope, signer, NotificationTriggerType.Reminder, actionUrl, channels);
        return Task.FromResult(records);
    }

    private static List<SignerNotificationRecord> SendNotifications(
        MultiPartyWorkflowEnvelope envelope,
        MultiPartySigner signer,
        NotificationTriggerType triggerType,
        string actionUrl,
        List<NotificationChannel> channels)
    {
        var records = new List<SignerNotificationRecord>();

        foreach (var channel in channels)
        {
            var recipient = channel switch
            {
                NotificationChannel.Email => signer.Email,
                NotificationChannel.WhatsApp => signer.PhoneNumber ?? signer.Email,
                NotificationChannel.Sms => signer.PhoneNumber ?? "+10000000000",
                _ => signer.Email
            };

            var body = triggerType switch
            {
                NotificationTriggerType.Invitation =>
                    $"[SGrowing Enterprise E-Sign] You are invited to review and digitally sign: \"{envelope.Title}\". Click to sign securely: {actionUrl}",
                NotificationTriggerType.Reminder =>
                    $"[SGrowing E-Sign Reminder] Your pending digital signature is required for \"{envelope.Title}\". Review link: {actionUrl}",
                NotificationTriggerType.CompletionNotice =>
                    $"[SGrowing E-Sign Executed] \"{envelope.Title}\" has been fully executed by all parties. Access your tamper-evident copy here: {actionUrl}",
                NotificationTriggerType.DeclinedNotice =>
                    $"[SGrowing E-Sign Update] Signing was declined for \"{envelope.Title}\". Status updated.",
                _ => $"Contract notification for {envelope.Title}: {actionUrl}"
            };

            var record = new SignerNotificationRecord(
                Guid.NewGuid(),
                signer.Id,
                recipient,
                channel,
                triggerType,
                DateTimeOffset.UtcNow,
                "Delivered",
                body,
                actionUrl
            );

            signer.NotificationHistory.Add(record);
            records.Add(record);
        }

        return records;
    }

    private static void CheckAndApplyExpiration(MultiPartyWorkflowEnvelope envelope)
    {
        if (envelope.ExpiresAtUtc.HasValue && DateTimeOffset.UtcNow > envelope.ExpiresAtUtc.Value)
        {
            if (envelope.Status is MultiPartyContractStatus.InProgress or MultiPartyContractStatus.PartiallySigned)
            {
                envelope.Status = MultiPartyContractStatus.Expired;
                foreach (var signer in envelope.Signers)
                {
                    if (signer.LifecycleStatus != SignerLifecycleStatus.FullySigned)
                    {
                        signer.LifecycleStatus = SignerLifecycleStatus.Expired;
                    }
                }
            }
        }
    }

    private void SeedInitialWorkflow()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var contractId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var envelope = new MultiPartyWorkflowEnvelope
        {
            Id = contractId,
            TenantId = tenantId,
            Title = "Enterprise Master Services & SLA Agreement 2026",
            Description = "Multi-party cloud platform deployment and data confidentiality agreement.",
            AgreementType = "Enterprise MSA",
            WorkflowMode = SignerWorkflowMode.Sequential,
            Status = MultiPartyContractStatus.InProgress,
            CurrentSignerIndex = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(71),
            Pages = [
                new() { PageNumber = 1, Title = "Parties & Engagement Scope", Content = "Customer agrees to enterprise cloud SLA terms." },
                new() { PageNumber = 2, Title = "Commercial Terms & Pricing Table", Content = "Monthly baseline $12,500 + consumption tiers." },
                new() { PageNumber = 3, Title = "Signatures & Execution Execution Block", Content = "Duly authorized execution signatures." }
            ],
            Signers = [],
            AuditTrail = []
        };

        var signer1Id = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var signer2Id = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

        var link1 = GenerateSigningLink(tenantId, contractId, signer1Id, 0, "cfo@acmecorp.com");
        var link2 = GenerateSigningLink(tenantId, contractId, signer2Id, 1, "legal@sgrowing.io");

        envelope.Signers.Add(new MultiPartySigner
        {
            Id = signer1Id,
            OrderIndex = 0,
            Name = "Sarah Jenkins",
            Email = "cfo@acmecorp.com",
            PhoneNumber = "+14155552671",
            Role = ContractSignerRole.ClientSigner,
            LifecycleStatus = SignerLifecycleStatus.Invited,
            Token = link1.Token,
            TokenExpiresAtUtc = link1.ExpiresAtUtc,
            InvitedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        });

        envelope.Signers.Add(new MultiPartySigner
        {
            Id = signer2Id,
            OrderIndex = 1,
            Name = "Alexander Vance",
            Email = "legal@sgrowing.io",
            PhoneNumber = "+14155558832",
            Role = ContractSignerRole.VendorSigner,
            LifecycleStatus = SignerLifecycleStatus.Invited,
            Token = link2.Token,
            TokenExpiresAtUtc = link2.ExpiresAtUtc
        });

        _contracts[contractId] = envelope;
    }
}

#endregion
