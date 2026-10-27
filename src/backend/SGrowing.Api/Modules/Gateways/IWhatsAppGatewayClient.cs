using System.Text.Json;

namespace SGrowing.Api.Modules.Gateways;

public record WhatsAppSessionStateDto(string InstanceName, string Status, string? QrCodeBase64, DateTime UpdatedAtUtc);
public record SendWhatsAppMessageResultDto(string MessageId, string Status, string RecipientPhone, DateTime TimestampUtc);
public record InboundWhatsAppEventDto(string EventType, string SenderPhone, string? Text, string? MediaUrl, string? SelectedOptionId);

public interface IWhatsAppGatewayClient
{
    Task<WhatsAppSessionStateDto> GetSessionStateAsync(string instanceName, CancellationToken ct = default);
    Task<SendWhatsAppMessageResultDto> SendTextMessageAsync(string instanceName, string recipientPhone, string message, CancellationToken ct = default);
    Task<SendWhatsAppMessageResultDto> SendInteractiveButtonsAsync(string instanceName, string recipientPhone, string bodyText, List<string> buttons, CancellationToken ct = default);
    Task<InboundWhatsAppEventDto> ParseWebhookPayloadAsync(JsonElement payload);
}

public class EvolutionApiGatewayClient : IWhatsAppGatewayClient
{
    public Task<WhatsAppSessionStateDto> GetSessionStateAsync(string instanceName, CancellationToken ct = default)
    {
        // Evolution API simulation / client adapter
        var simulatedQr = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        return Task.FromResult(new WhatsAppSessionStateDto(
            instanceName,
            "connected",
            simulatedQr,
            DateTime.UtcNow
        ));
    }

    public Task<SendWhatsAppMessageResultDto> SendTextMessageAsync(string instanceName, string recipientPhone, string message, CancellationToken ct = default)
    {
        var msgId = $"EVO-{Guid.NewGuid():N}";
        return Task.FromResult(new SendWhatsAppMessageResultDto(
            msgId,
            "SENT",
            recipientPhone,
            DateTime.UtcNow
        ));
    }

    public Task<SendWhatsAppMessageResultDto> SendInteractiveButtonsAsync(string instanceName, string recipientPhone, string bodyText, List<string> buttons, CancellationToken ct = default)
    {
        var msgId = $"EVO-BTN-{Guid.NewGuid():N}";
        return Task.FromResult(new SendWhatsAppMessageResultDto(
            msgId,
            "SENT",
            recipientPhone,
            DateTime.UtcNow
        ));
    }

    public Task<InboundWhatsAppEventDto> ParseWebhookPayloadAsync(JsonElement payload)
    {
        string eventType = "messages.upsert";
        string sender = "";
        string? text = null;
        string? mediaUrl = null;
        string? selectedOptionId = null;

        if (payload.TryGetProperty("event", out var evProp))
        {
            eventType = evProp.GetString() ?? eventType;
        }

        if (payload.TryGetProperty("data", out var dataProp))
        {
            if (dataProp.TryGetProperty("key", out var keyProp) && keyProp.TryGetProperty("remoteJid", out var jidProp))
            {
                sender = jidProp.GetString()?.Replace("@s.whatsapp.net", "") ?? "";
            }

            if (dataProp.TryGetProperty("message", out var msgProp))
            {
                if (msgProp.TryGetProperty("conversation", out var convProp))
                {
                    text = convProp.GetString();
                }
                else if (msgProp.TryGetProperty("extendedTextMessage", out var extProp) && extProp.TryGetProperty("text", out var extText))
                {
                    text = extText.GetString();
                }
                else if (msgProp.TryGetProperty("buttonsResponseMessage", out var btnProp) && btnProp.TryGetProperty("selectedButtonId", out var btnId))
                {
                    selectedOptionId = btnId.GetString();
                    text = selectedOptionId;
                }
            }
        }

        return Task.FromResult(new InboundWhatsAppEventDto(eventType, sender, text, mediaUrl, selectedOptionId));
    }
}
