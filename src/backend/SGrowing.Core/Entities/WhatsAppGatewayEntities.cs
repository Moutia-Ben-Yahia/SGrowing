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
