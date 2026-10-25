using System.Text.Json;
using MarketingStudio.Core.Channels;

namespace MarketingStudio.Api.Modules.Channels;

public class WhatsAppCloudAdapter : IChannelAdapter
{
    public string ChannelType => "whatsapp_cloud";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("entry", out var entryArray) || entryArray.GetArrayLength() == 0)
                return null;

            var entry = entryArray[0];
            if (!entry.TryGetProperty("changes", out var changes) || changes.GetArrayLength() == 0)
                return null;

            var value = changes[0].GetProperty("value");
            if (!value.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0)
                return null;

            var msg = messages[0];
            var from = msg.GetProperty("from").GetString() ?? "";
            var id = msg.GetProperty("id").GetString() ?? "";
            var type = msg.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "text";

            string content = "";
            string? mediaUrl = null;

            if (type == "text" && msg.TryGetProperty("text", out var textObj))
            {
                content = textObj.GetProperty("body").GetString() ?? "";
            }
            else if (type == "image" && msg.TryGetProperty("image", out var imgObj))
            {
                content = imgObj.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";
                mediaUrl = imgObj.TryGetProperty("id", out var imgId) ? $"wa-media://{imgId.GetString()}" : null;
            }

            string? senderName = null;
            if (value.TryGetProperty("contacts", out var contacts) && contacts.GetArrayLength() > 0)
            {
                var contact = contacts[0];
                if (contact.TryGetProperty("profile", out var profile) && profile.TryGetProperty("name", out var nameProp))
                {
                    senderName = nameProp.GetString();
                }
            }

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: from,
                SenderName: senderName,
                ExternalMessageId: id,
                ContentType: type ?? "text",
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
