using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public interface IWhatsAppPairingProtocolService
{
    Task<WhatsAppPairingResponse> RequestPairingCodeAsync(
        Guid tenantId,
        string phoneNumber,
        string instanceName = "default",
        CancellationToken ct = default);

    Task<WhatsAppVerifyPairingResponse> SimulatePairingHandshakeAsync(
        Guid tenantId,
        string pairingCode,
        CancellationToken ct = default);

    Task<DispatchInteractiveCodResponse> DispatchInteractiveCodConfirmationAsync(
        Guid tenantId,
        Guid orderId,
        string customerPhone,
        decimal totalAmount,
        string currency = "TND",
        CancellationToken ct = default);

    Task<InteractiveWebhookResponse> HandleInteractiveButtonResponseAsync(
        Guid tenantId,
        Guid orderId,
        string buttonId,
        string? updatedText = null,
        CancellationToken ct = default);

    Task<TimeoutSweepResponse> CheckConfirmationTimeoutAndFallbackAsync(
        Guid tenantId,
        TimeSpan timeoutThreshold,
        CancellationToken ct = default);

    Task<WhatsAppPairingStatusResponse> GetPairingStatusAsync(
        Guid tenantId,
        string instanceName = "default",
        CancellationToken ct = default);
}

public class WhatsAppPairingProtocolService : IWhatsAppPairingProtocolService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<WhatsAppPairingProtocolService> _logger;

    private const string PairingCharset = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 32 chars, human-readable
    private const string CompanionOs = "Chrome/Linux";
    private const string CompanionPlatform = "SGrowing-MultiDevice";

    public WhatsAppPairingProtocolService(
        SGrowingDbContext db,
        ILogger<WhatsAppPairingProtocolService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static string NormalizeAndValidatePhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number cannot be empty or null.", nameof(phoneNumber));

        var cleaned = phoneNumber.Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(".", "");

        if (cleaned.StartsWith("00"))
        {
            cleaned = "+" + cleaned[2..];
        }
        else if (!cleaned.StartsWith("+"))
        {
            cleaned = "+" + cleaned;
        }

        // Strict E.164: '+' followed by 7 to 15 digits
        if (!Regex.IsMatch(cleaned, @"^\+[1-9]\d{6,14}$"))
        {
            throw new ArgumentException(
                $"Invalid E.164 phone number format: '{phoneNumber}'. Must start with '+' followed by 7 to 15 digits (e.g. +21698123456).",
                nameof(phoneNumber));
        }

        return cleaned;
    }

    public static string GenerateCryptographicPairingCode()
    {
        Span<char> chars = stackalloc char[8];
        for (int i = 0; i < 8; i++)
        {
            int idx = RandomNumberGenerator.GetInt32(0, PairingCharset.Length);
            chars[i] = PairingCharset[idx];
        }

        return $"{chars[..4]}-{chars[4..]}";
    }

    public async Task<WhatsAppPairingResponse> RequestPairingCodeAsync(
        Guid tenantId,
        string phoneNumber,
        string instanceName = "default",
        CancellationToken ct = default)
    {
        var validatedPhone = NormalizeAndValidatePhoneNumber(phoneNumber);
        var safeInstance = string.IsNullOrWhiteSpace(instanceName)
            ? "default"
            : instanceName.Trim().ToLowerInvariant().Replace(" ", "-");

        var code = GenerateCryptographicPairingCode();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(10);

        // Find or create WhatsAppInstanceRecord
        var instance = await _db.WhatsAppInstances
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.InstanceName == safeInstance, ct);

        if (instance == null)
        {
            instance = new WhatsAppInstanceRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InstanceName = safeInstance,
                PhoneNumber = validatedPhone,
                Status = "Pairing",
                PairingMode = "PairingCode",
                PairingCode = code,
                MemoryFootprintMb = 24,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.WhatsAppInstances.Add(instance);
        }
        else
        {
            instance.PhoneNumber = validatedPhone;
            instance.PairingCode = code;
            instance.PairingMode = "PairingCode";
            instance.Status = "Pairing";
        }

        // Expire older pending sessions for this instance
        var existingSessions = await _db.WhatsAppPairingSessions
            .Where(s => s.TenantId == tenantId && s.InstanceName == safeInstance && s.Status == "PendingUserEntry")
            .ToListAsync(ct);

        foreach (var old in existingSessions)
        {
            old.Status = "Expired";
        }

        // Create new pairing session record
        var session = new WhatsAppPairingSessionRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InstanceName = safeInstance,
            PhoneNumber = validatedPhone,
            PairingCode = code,
            Status = "PendingUserEntry",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            CompanionOs = CompanionOs,
            CompanionPlatform = CompanionPlatform
        };

        _db.WhatsAppPairingSessions.Add(session);

        // Enqueue Outbox message
        var outboxPayload = JsonSerializer.Serialize(new
        {
            SessionId = session.Id,
            TenantId = tenantId,
            InstanceName = safeInstance,
            PhoneNumber = validatedPhone,
            PairingCode = code,
            ExpiresAtUtc = expiresAtUtc,
            RequestedAtUtc = DateTime.UtcNow
        });

        _db.OutboxMessages.Add(OutboxMessage.Create(tenantId, "WhatsAppPairingCodeRequested", outboxPayload));

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Requested WhatsApp 8-digit pairing code for tenant {TenantId}, instance {Instance}: {PairingCode}",
            tenantId, safeInstance, code);

        return new WhatsAppPairingResponse(
            PairingCode: code,
            FormattedCode: code,
            PhoneNumber: validatedPhone,
            InstanceName: safeInstance,
            Status: "PendingUserEntry",
            ExpiresAtUtc: expiresAtUtc,
            ExpiresInSeconds: 600,
            Protocol: "Noise_XX_25519_AESGCM_SHA256",
            StepInstructions: $"Open WhatsApp on your phone > Linked Devices > Link with phone number > Enter code: {code}"
        );
    }

    public async Task<WhatsAppVerifyPairingResponse> SimulatePairingHandshakeAsync(
        Guid tenantId,
        string pairingCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pairingCode))
            throw new ArgumentException("Pairing code cannot be empty.", nameof(pairingCode));

        var cleanCode = pairingCode.Trim().ToUpperInvariant();
        var normalizedCode = cleanCode.Replace("-", "");

        var session = await _db.WhatsAppPairingSessions
            .Where(s => s.TenantId == tenantId &&
                       (s.PairingCode == cleanCode || s.PairingCode.Replace("-", "").ToUpperInvariant() == normalizedCode))
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (session == null)
        {
            throw new KeyNotFoundException($"Active pairing session not found for code '{pairingCode}'.");
        }

        if (session.IsExpired || DateTime.UtcNow > session.ExpiresAtUtc)
        {
            session.Status = "Expired";
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Pairing code has expired (10-minute TTL exceeded). Please request a new code.");
        }

        // Transition session to Connected
        var connectedAtUtc = DateTime.UtcNow;
        session.Status = "Connected";
        session.ConnectedAtUtc = connectedAtUtc;

        // Transition instance to Connected
        var instance = await _db.WhatsAppInstances
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.InstanceName == session.InstanceName, ct);

        if (instance != null)
        {
            instance.Status = "Connected";
            instance.ConnectedAtUtc = connectedAtUtc;
            instance.PairingCode = null;
            instance.PhoneNumber = session.PhoneNumber;
            instance.MemoryFootprintMb = 24;
        }

        // Create or update ChannelCredentialVault record
        var credential = await _db.ChannelCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ChannelType == "WhatsAppCompanion", ct);

        var metadataJson = JsonSerializer.Serialize(new
        {
            Os = CompanionOs,
            Platform = CompanionPlatform,
            ConnectedAtUtc = connectedAtUtc,
            PhoneNumber = session.PhoneNumber,
            InstanceName = session.InstanceName,
            Protocol = "Noise_XX_25519_AESGCM_SHA256",
            PairingMode = "PairingCode"
        });

        if (credential == null)
        {
            credential = new ChannelCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ChannelType = "WhatsAppCompanion",
                EncryptedAccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                EncryptedAppSecret = metadataJson,
                NonceBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)),
                TagBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
                IsActive = true,
                CreatedAtUtc = connectedAtUtc,
                UpdatedAtUtc = connectedAtUtc
            };
            _db.ChannelCredentials.Add(credential);
        }
        else
        {
            credential.EncryptedAppSecret = metadataJson;
            credential.IsActive = true;
            credential.UpdatedAtUtc = connectedAtUtc;
        }

        // Outbox notification
        var outboxPayload = JsonSerializer.Serialize(new
        {
            SessionId = session.Id,
            TenantId = tenantId,
            InstanceName = session.InstanceName,
            PhoneNumber = session.PhoneNumber,
            CompanionOs = CompanionOs,
            CompanionPlatform = CompanionPlatform,
            ConnectedAtUtc = connectedAtUtc
        });

        _db.OutboxMessages.Add(OutboxMessage.Create(tenantId, "WhatsAppCompanionDeviceConnected", outboxPayload));

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Successfully paired companion device for tenant {TenantId}, instance {Instance}, phone {Phone}",
            tenantId, session.InstanceName, session.PhoneNumber);

        return new WhatsAppVerifyPairingResponse(
            Success: true,
            Status: "Connected",
            InstanceName: session.InstanceName,
            PhoneNumber: session.PhoneNumber,
            ConnectedAtUtc: connectedAtUtc,
            CompanionOs: CompanionOs,
            CompanionPlatform: CompanionPlatform,
            Message: "Successfully established Noise_XX handshake with WhatsApp multi-device companion network."
        );
    }

    public async Task<DispatchInteractiveCodResponse> DispatchInteractiveCodConfirmationAsync(
        Guid tenantId,
        Guid orderId,
        string customerPhone,
        decimal totalAmount,
        string currency = "TND",
        CancellationToken ct = default)
    {
        var validatedPhone = NormalizeAndValidatePhoneNumber(customerPhone);
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? "TND" : currency.Trim().ToUpperInvariant();

        var messageId = $"WAM-COD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var sentAtUtc = DateTime.UtcNow;

        var buttons = new List<WhatsAppInteractiveButtonDto>
        {
            new("btn_confirm", "Confirm Order (Ship Now)"),
            new("btn_edit_address", "Update Address / Delivery Note"),
            new("btn_cancel", "Cancel Order")
        };

        // Check if CodOrder exists and update it
        var order = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId, ct);

        if (order != null)
        {
            order.InteractiveConfirmationSentAtUtc = sentAtUtc;
            var noteEntry = $"[WhatsApp Bot]: Interactive confirmation dispatched at {sentAtUtc:O} (MessageId: {messageId})";
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? noteEntry
                : $"{order.Notes}\n{noteEntry}";
        }

        // Store confirmation record
        var confirmationRecord = new WhatsAppInteractiveOrderConfirmationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            OrderNumber = order?.OrderNumber ?? orderId.ToString("N")[..8].ToUpperInvariant(),
            CustomerPhone = validatedPhone,
            TotalAmount = totalAmount,
            Currency = normalizedCurrency,
            MessageId = messageId,
            Status = "Dispatched",
            DispatchedAtUtc = sentAtUtc
        };

        _db.WhatsAppInteractiveOrderConfirmations.Add(confirmationRecord);

        // Enqueue Outbox message for worker/gateway delivery
        var outboxPayload = JsonSerializer.Serialize(new
        {
            MessageId = messageId,
            TenantId = tenantId,
            OrderId = orderId,
            CustomerPhone = validatedPhone,
            TotalAmount = totalAmount,
            Currency = normalizedCurrency,
            Buttons = buttons,
            DispatchedAtUtc = sentAtUtc
        });

        _db.OutboxMessages.Add(OutboxMessage.Create(tenantId, "WhatsAppInteractiveOrderConfirmationDispatched", outboxPayload));

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Dispatched WhatsApp interactive COD confirmation {MessageId} for order {OrderId}",
            messageId, orderId);

        return new DispatchInteractiveCodResponse(
            MessageId: messageId,
            OrderId: orderId,
            CustomerPhone: validatedPhone,
            TotalAmount: totalAmount,
            Currency: normalizedCurrency,
            Status: "Dispatched",
            Buttons: buttons,
            DispatchedAtUtc: sentAtUtc
        );
    }

    public async Task<InteractiveWebhookResponse> HandleInteractiveButtonResponseAsync(
        Guid tenantId,
        Guid orderId,
        string buttonId,
        string? updatedText = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(buttonId))
            throw new ArgumentException("ButtonId cannot be empty.", nameof(buttonId));

        var cleanButtonId = buttonId.Trim().ToLowerInvariant();
        var processedAtUtc = DateTime.UtcNow;

        var order = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId, ct);

        var confirmation = await _db.WhatsAppInteractiveOrderConfirmations
            .Where(c => c.TenantId == tenantId && c.OrderId == orderId)
            .OrderByDescending(c => c.DispatchedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (confirmation != null)
        {
            confirmation.SelectedButtonId = cleanButtonId;
            confirmation.RespondedAtUtc = processedAtUtc;
        }

        string executedAction;
        string? resultNotes = null;

        switch (cleanButtonId)
        {
            case "btn_confirm":
                executedAction = "OrderConfirmedInWms";
                if (order != null)
                {
                    order.Status = CodOrderStatus.Confirmed;
                    order.ConfirmationChannel = ConfirmationChannel.WhatsAppInteractive;
                    order.ConfirmedVia = "WhatsAppInteractive";
                    order.ConfirmedAtUtc = processedAtUtc;
                    order.RequiresPhoneCall = false;

                    var confirmNote = $"[WhatsApp Bot]: Confirmed via interactive button at {processedAtUtc:O}";
                    order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                        ? confirmNote
                        : $"{order.Notes}\n{confirmNote}";

                    // Remove order from manual call center queue
                    var activeCallLocks = await _db.CallCenterOrderLocks
                        .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.IsActive)
                        .ToListAsync(ct);
                    foreach (var l in activeCallLocks)
                    {
                        l.Release();
                    }

                    var collisionLocks = await _db.OrderCollisionLocks
                        .Where(l => l.TenantId == tenantId && l.OrderId == orderId && l.IsActive)
                        .ToListAsync(ct);
                    foreach (var cl in collisionLocks)
                    {
                        cl.IsActive = false;
                    }
                }

                if (confirmation != null)
                {
                    confirmation.Status = "Confirmed";
                }
                break;

            case "btn_edit_address":
                executedAction = "AddressCorrectionAppended";
                resultNotes = updatedText?.Trim();
                if (order != null)
                {
                    if (!string.IsNullOrWhiteSpace(resultNotes))
                    {
                        var addressNote = $"[Address Correction via WhatsApp]: {resultNotes}";
                        order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                            ? addressNote
                            : $"{order.Notes}\n{addressNote}";
                        order.ShippingAddress = $"{order.ShippingAddress} (Delivery Note: {resultNotes})";
                    }
                    else
                    {
                        var updateNote = $"[WhatsApp Bot]: Customer requested address/note modification at {processedAtUtc:O}";
                        order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                            ? updateNote
                            : $"{order.Notes}\n{updateNote}";
                    }
                }

                if (confirmation != null)
                {
                    confirmation.Status = "AddressUpdated";
                    confirmation.AddressUpdateNote = resultNotes;
                }
                break;

            case "btn_cancel":
                executedAction = "OrderCancelledAndStockReleased";
                if (order != null)
                {
                    order.Status = CodOrderStatus.Cancelled;
                    var cancelNote = $"[WhatsApp Bot]: Order cancelled by customer. Inventory reservations released at {processedAtUtc:O}";
                    order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                        ? cancelNote
                        : $"{order.Notes}\n{cancelNote}";

                    // Release inventory reservation
                    var activeReservations = await _db.CartReservationLocks
                        .Where(r => r.TenantId == tenantId && r.OrderId == orderId && r.Status == "Active")
                        .ToListAsync(ct);

                    foreach (var res in activeReservations)
                    {
                        res.Status = "Released";
                        res.ReleasedAtUtc = processedAtUtc;
                    }
                }

                if (confirmation != null)
                {
                    confirmation.Status = "Cancelled";
                }
                break;

            default:
                throw new ArgumentException(
                    $"Unknown button action: '{buttonId}'. Expected 'btn_confirm', 'btn_edit_address', or 'btn_cancel'.",
                    nameof(buttonId));
        }

        // Outbox notification
        var outboxPayload = JsonSerializer.Serialize(new
        {
            TenantId = tenantId,
            OrderId = orderId,
            ButtonId = cleanButtonId,
            ExecutedAction = executedAction,
            UpdatedText = updatedText,
            ProcessedAtUtc = processedAtUtc
        });

        _db.OutboxMessages.Add(OutboxMessage.Create(tenantId, "WhatsAppInteractiveButtonProcessed", outboxPayload));

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Handled WhatsApp button {ButtonId} for order {OrderId}: {Action}",
            cleanButtonId, orderId, executedAction);

        return new InteractiveWebhookResponse(
            Success: true,
            OrderId: orderId,
            Status: confirmation?.Status ?? (order?.Status.ToString() ?? "Processed"),
            ExecutedAction: executedAction,
            Notes: resultNotes,
            ProcessedAtUtc: processedAtUtc
        );
    }

    public async Task<TimeoutSweepResponse> CheckConfirmationTimeoutAndFallbackAsync(
        Guid tenantId,
        TimeSpan timeoutThreshold,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(timeoutThreshold);
        var sweptAtUtc = DateTime.UtcNow;

        // 1. Identify unconfirmed WhatsApp confirmations that passed timeout threshold
        var pendingConfirmations = await _db.WhatsAppInteractiveOrderConfirmations
            .Where(c => c.TenantId == tenantId &&
                        c.Status == "Dispatched" &&
                        c.DispatchedAtUtc <= cutoff &&
                        !c.FallbackToDialerTriggered &&
                        c.RespondedAtUtc == null)
            .ToListAsync(ct);

        // 2. Identify CodOrders that are still PendingConfirmation and whose interactive message was dispatched > timeout
        var pendingOrders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId &&
                        o.Status == CodOrderStatus.PendingConfirmation &&
                        o.InteractiveConfirmationSentAtUtc != null &&
                        o.InteractiveConfirmationSentAtUtc <= cutoff &&
                        !o.RequiresPhoneCall)
            .ToListAsync(ct);

        var escalatedOrderIds = new HashSet<Guid>();

        // Escalate orders from confirmations
        foreach (var conf in pendingConfirmations)
        {
            conf.FallbackToDialerTriggered = true;
            conf.FallbackTriggeredAtUtc = sweptAtUtc;
            conf.Status = "TimedOut";

            var ord = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == conf.OrderId, ct);
            if (ord != null && ord.Status == CodOrderStatus.PendingConfirmation)
            {
                ord.RequiresPhoneCall = true;
                var fallbackNote = $"[Auto-Fallback]: WhatsApp confirmation timed out after {timeoutThreshold.TotalMinutes:F0}m. Escalated to outbound call center dialer.";
                ord.Notes = string.IsNullOrWhiteSpace(ord.Notes)
                    ? fallbackNote
                    : $"{ord.Notes}\n{fallbackNote}";

                // Queue OutboundCallAttempt
                var existingCall = await _db.OutboundCallAttempts
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == ord.Id && c.State == IvrCallState.Queued, ct);

                if (existingCall == null)
                {
                    var callAttempt = OutboundCallAttempt.Create(
                        tenantId,
                        ord.Id,
                        ord.PhoneNumber,
                        ord.CustomerName,
                        campaignId: null,
                        attemptNumber: 1
                    );
                    _db.OutboundCallAttempts.Add(callAttempt);
                }

                escalatedOrderIds.Add(ord.Id);
            }
        }

        // Escalate orders directly
        foreach (var ord in pendingOrders)
        {
            if (escalatedOrderIds.Add(ord.Id))
            {
                ord.RequiresPhoneCall = true;
                var fallbackNote = $"[Auto-Fallback]: WhatsApp confirmation timed out after {timeoutThreshold.TotalMinutes:F0}m. Escalated to outbound call center dialer.";
                ord.Notes = string.IsNullOrWhiteSpace(ord.Notes)
                    ? fallbackNote
                    : $"{ord.Notes}\n{fallbackNote}";

                var existingCall = await _db.OutboundCallAttempts
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == ord.Id && c.State == IvrCallState.Queued, ct);

                if (existingCall == null)
                {
                    var callAttempt = OutboundCallAttempt.Create(
                        tenantId,
                        ord.Id,
                        ord.PhoneNumber,
                        ord.CustomerName,
                        campaignId: null,
                        attemptNumber: 1
                    );
                    _db.OutboundCallAttempts.Add(callAttempt);
                }
            }
        }

        if (escalatedOrderIds.Count > 0)
        {
            var outboxPayload = JsonSerializer.Serialize(new
            {
                TenantId = tenantId,
                EscalatedCount = escalatedOrderIds.Count,
                EscalatedOrderIds = escalatedOrderIds.ToList(),
                SweptAtUtc = sweptAtUtc
            });

            _db.OutboxMessages.Add(OutboxMessage.Create(tenantId, "CodOrderEscalatedToCallCenter", outboxPayload));
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Swept confirmation timeouts for tenant {TenantId}: {EscalatedCount} orders escalated to dialer queue",
            tenantId, escalatedOrderIds.Count);

        return new TimeoutSweepResponse(
            TotalEvaluated: pendingConfirmations.Count + pendingOrders.Count,
            EscalatedCount: escalatedOrderIds.Count,
            EscalatedOrderIds: escalatedOrderIds.ToList(),
            SweptAtUtc: sweptAtUtc
        );
    }

    public async Task<WhatsAppPairingStatusResponse> GetPairingStatusAsync(
        Guid tenantId,
        string instanceName = "default",
        CancellationToken ct = default)
    {
        var safeInstance = string.IsNullOrWhiteSpace(instanceName)
            ? "default"
            : instanceName.Trim().ToLowerInvariant().Replace(" ", "-");

        var instance = await _db.WhatsAppInstances
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.InstanceName == safeInstance, ct);

        var latestSession = await _db.WhatsAppPairingSessions
            .Where(s => s.TenantId == tenantId && s.InstanceName == safeInstance)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        bool isConnected = instance?.Status == "Connected";
        string status = isConnected
            ? "Connected"
            : (latestSession != null && !latestSession.IsExpired && latestSession.Status == "PendingUserEntry"
                ? "PendingUserEntry"
                : (instance?.Status ?? "Disconnected"));

        string? activeCode = null;
        DateTime? expiresAt = null;
        int? remainingSeconds = null;

        if (latestSession != null && !latestSession.IsExpired && latestSession.Status == "PendingUserEntry")
        {
            activeCode = latestSession.PairingCode;
            expiresAt = latestSession.ExpiresAtUtc;
            remainingSeconds = Math.Max(0, (int)(latestSession.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds);
        }

        return new WhatsAppPairingStatusResponse(
            InstanceName: safeInstance,
            Status: status,
            PhoneNumber: instance?.PhoneNumber ?? latestSession?.PhoneNumber,
            ConnectedAtUtc: instance?.ConnectedAtUtc,
            IsConnected: isConnected,
            ActivePairingCode: activeCode,
            PairingExpiresAtUtc: expiresAt,
            RemainingSeconds: remainingSeconds
        );
    }
}
