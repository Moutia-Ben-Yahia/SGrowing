using System.Text.Json;
using SGrowing.Core.Channels;

namespace SGrowing.Api.Modules.Channels;

public class MetaGraphAdapter : IChannelAdapter
{
    public string ChannelType => "meta_graph";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("entry", out var entryArray) || entryArray.ValueKind != JsonValueKind.Array || entryArray.GetArrayLength() == 0)
                return null;

            var entry = entryArray[0];
            if (!entry.TryGetProperty("messaging", out var messagingArray) || messagingArray.ValueKind != JsonValueKind.Array || messagingArray.GetArrayLength() == 0)
                return null;

            var messaging = messagingArray[0];

            // sender.id
            if (!messaging.TryGetProperty("sender", out var sender) || !sender.TryGetProperty("id", out var senderIdProp))
                return null;

            var senderId = senderIdProp.GetString() ?? senderIdProp.ToString();
            if (string.IsNullOrWhiteSpace(senderId))
                return null;

            string? senderName = sender.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : (sender.TryGetProperty("username", out var uProp) ? uProp.GetString() : null);

            // recipient.id (parsed for webhook validation / tracking)
            string? recipientId = null;
            if (messaging.TryGetProperty("recipient", out var recipient) && recipient.TryGetProperty("id", out var recIdProp))
            {
                recipientId = recIdProp.GetString() ?? recIdProp.ToString();
            }

            // message
            if (!messaging.TryGetProperty("message", out var msg))
                return null;

            var messageId = msg.TryGetProperty("mid", out var midProp)
                ? midProp.GetString()
                : (msg.TryGetProperty("id", out var idProp) ? idProp.GetString() : Guid.NewGuid().ToString());

            var content = msg.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
            string contentType = "text";
            string? mediaUrl = null;

            if (msg.TryGetProperty("attachments", out var attArray) && attArray.ValueKind == JsonValueKind.Array && attArray.GetArrayLength() > 0)
            {
                var firstAtt = attArray[0];
                if (firstAtt.TryGetProperty("type", out var typeProp))
                {
                    contentType = typeProp.GetString() ?? "attachment";
                }
                else
                {
                    contentType = "attachment";
                }

                if (firstAtt.TryGetProperty("payload", out var payload) && payload.TryGetProperty("url", out var urlProp))
                {
                    mediaUrl = urlProp.GetString();
                }
            }

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: senderId,
                SenderName: senderName,
                ExternalMessageId: messageId ?? Guid.NewGuid().ToString(),
                ContentType: contentType,
                Content: content,
                MediaUrl: mediaUrl
            );
        }
        catch
        {
            return null;
        }
    }
}
