using System.Text.Json;
using SGrowing.Core.Channels;

namespace SGrowing.Api.Modules.Channels;

public class XTwitterAdapter : IChannelAdapter
{
    public string ChannelType => "x_twitter";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("direct_message_events", out var eventsArray) || eventsArray.ValueKind != JsonValueKind.Array || eventsArray.GetArrayLength() == 0)
                return null;

            var dmEvent = eventsArray[0];
            var eventId = dmEvent.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? idProp.ToString() : Guid.NewGuid().ToString();

            if (!dmEvent.TryGetProperty("message_create", out var messageCreate))
                return null;

            if (!messageCreate.TryGetProperty("sender_id", out var senderIdProp))
                return null;

            var senderId = senderIdProp.GetString() ?? senderIdProp.ToString();
            if (string.IsNullOrWhiteSpace(senderId))
                return null;

            string? senderName = null;
            if (root.TryGetProperty("users", out var users) && users.TryGetProperty(senderId, out var userObj))
            {
                senderName = userObj.TryGetProperty("name", out var n)
                    ? n.GetString()
                    : (userObj.TryGetProperty("screen_name", out var sn) ? sn.GetString() : null);
            }
            else if (messageCreate.TryGetProperty("sender", out var senderObj))
            {
                senderName = senderObj.TryGetProperty("name", out var n)
                    ? n.GetString()
                    : (senderObj.TryGetProperty("screen_name", out var sn) ? sn.GetString() : null);
            }

            if (!messageCreate.TryGetProperty("message_data", out var messageData))
                return null;

            var text = messageData.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
            string contentType = "text";
            string? mediaUrl = null;

            if (messageData.TryGetProperty("attachment", out var attObj))
            {
                contentType = attObj.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "attachment" : "attachment";
                if (attObj.TryGetProperty("media", out var mediaObj))
                {
                    mediaUrl = mediaObj.TryGetProperty("media_url_https", out var muProp)
                        ? muProp.GetString()
                        : (mediaObj.TryGetProperty("media_url", out var muProp2) ? muProp2.GetString() : null);
                }
            }

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: senderId,
                SenderName: senderName,
                ExternalMessageId: eventId,
                ContentType: contentType,
                Content: text,
                MediaUrl: mediaUrl
            );
        }
        catch
        {
            return null;
        }
    }
}
