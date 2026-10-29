namespace SGrowing.Core.Entities;

public class WhatsAppInstanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string InstanceName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = "Disconnected"; // Disconnected, Pairing, Connected, Reconnecting
    public string PairingMode { get; set; } = "PairingCode"; // PairingCode, QrCode
    public string? PairingCode { get; set; }
    public string? QrData { get; set; }
    public int MemoryFootprintMb { get; set; } = 22;
    public long MessagesSent { get; set; }
    public long MessagesReceived { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CreateWhatsAppInstanceRequest(
    string InstanceName,
    string? PhoneNumber = null,
    string PairingMode = "PairingCode");

public record RequestPairingCodeRequest(
    string PhoneNumber);

public record PairingCodeResult(
    string InstanceName,
    string PhoneNumber,
    string PairingCode,
    int ExpiresInSeconds,
    string Protocol = "Noise_XX_25519_AESGCM_SHA256");

public record QrCodeResult(
    string InstanceName,
    string QrData,
    int ExpiresInSeconds);

public record DispatchWhatsAppMessageRequest(
    string RecipientPhoneNumber,
    string MessageType = "text", // text, image, audio, document
    string Content = "",
    string? MediaUrl = null);

public record WhatsAppMessageDispatchResult(
    string MessageId,
    string Recipient,
    string Status, // Dispatched, Delivered, Read
    string BrokerFanout, // "NATS + RabbitMQ + WebSocket"
    int LatencyMs,
    DateTime DispatchedAtUtc);

public record WhatsAppButton(
    string ButtonId,
    string ButtonText);

public record SendInteractiveButtonsRequest(
    string RecipientPhoneNumber,
    string Title,
    string Body,
    string? Footer = null,
    List<WhatsAppButton>? Buttons = null);

public record WhatsAppListRow(
    string RowId,
    string Title,
    string? Description = null);

public record WhatsAppListSection(
    string SectionTitle,
    List<WhatsAppListRow> Rows);

public record SendInteractiveListRequest(
    string RecipientPhoneNumber,
    string Title,
    string Body,
    string ButtonText,
    string? Footer = null,
    List<WhatsAppListSection>? Sections = null);

public record WhatsAppInteractiveCallbackPayload(
    string ButtonId,
    string? SelectedRowId = null,
    string? ButtonText = null,
    string? ContextMessageId = null);

public class WhatsAppPairingSessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string InstanceName { get; set; } = "default";
    public string PhoneNumber { get; set; } = string.Empty;
    public string PairingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingUserEntry"; // PendingUserEntry, Connected, Expired, Failed
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(10);
    public DateTime? ConnectedAtUtc { get; set; }
    public string CompanionOs { get; set; } = "Chrome/Linux";
    public string CompanionPlatform { get; set; } = "SGrowing-MultiDevice";

    public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc && Status == "PendingUserEntry";
}

public class WhatsAppInteractiveOrderConfirmationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "TND";
    public string MessageId { get; set; } = string.Empty;
    public string Status { get; set; } = "Dispatched"; // Dispatched, Confirmed, AddressUpdated, Cancelled, TimedOut
    public string? SelectedButtonId { get; set; }
    public string? AddressUpdateNote { get; set; }
    public DateTime DispatchedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
    public bool FallbackToDialerTriggered { get; set; } = false;
    public DateTime? FallbackTriggeredAtUtc { get; set; }
}

public record WhatsAppPairingRequest(
    string PhoneNumber,
    string InstanceName = "default");

public record WhatsAppPairingResponse(
    string PairingCode,
    string FormattedCode,
    string PhoneNumber,
    string InstanceName,
    string Status,
    DateTime ExpiresAtUtc,
    int ExpiresInSeconds,
    string Protocol,
    string StepInstructions);

public record WhatsAppVerifyPairingRequest(
    string PairingCode,
    string InstanceName = "default");

public record WhatsAppVerifyPairingResponse(
    bool Success,
    string Status,
    string InstanceName,
    string PhoneNumber,
    DateTime ConnectedAtUtc,
    string CompanionOs,
    string CompanionPlatform,
    string Message);

public record WhatsAppPairingStatusResponse(
    string InstanceName,
    string Status,
    string? PhoneNumber,
    DateTime? ConnectedAtUtc,
    bool IsConnected,
    string? ActivePairingCode,
    DateTime? PairingExpiresAtUtc,
    int? RemainingSeconds);

public record DispatchInteractiveCodRequest(
    Guid OrderId,
    string CustomerPhone,
    decimal TotalAmount,
    string Currency = "TND");

public record WhatsAppInteractiveButtonDto(
    string ButtonId,
    string ButtonText);

public record DispatchInteractiveCodResponse(
    string MessageId,
    Guid OrderId,
    string CustomerPhone,
    decimal TotalAmount,
    string Currency,
    string Status,
    List<WhatsAppInteractiveButtonDto> Buttons,
    DateTime DispatchedAtUtc);

public record InteractiveWebhookRequest(
    Guid OrderId,
    string ButtonId,
    string? UpdatedText = null);

public record InteractiveWebhookResponse(
    bool Success,
    Guid OrderId,
    string Status,
    string ExecutedAction,
    string? Notes,
    DateTime ProcessedAtUtc);

public record TimeoutSweepRequest(
    double? TimeoutMinutes = 30);

public record TimeoutSweepResponse(
    int TotalEvaluated,
    int EscalatedCount,
    List<Guid> EscalatedOrderIds,
    DateTime SweptAtUtc);
