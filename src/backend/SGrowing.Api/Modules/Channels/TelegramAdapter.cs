using System.Text.Json;
using SGrowing.Core.Channels;

namespace SGrowing.Api.Modules.Channels;

public class TelegramAdapter : IChannelAdapter
{
    public string ChannelType => "telegram";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            string? updateId = null;
            if (root.TryGetProperty("update_id", out var updateIdProp))
            {
                updateId = updateIdProp.ToString();
            }

            JsonElement msg = default;
            if (root.TryGetProperty("message", out var m))
            {
                msg = m;
            }
            else if (root.TryGetProperty("edited_message", out var em))
            {
                msg = em;
            }
            else if (root.TryGetProperty("channel_post", out var cp))
            {
                msg = cp;
            }
            else
            {
                return null;
            }

            if (!msg.TryGetProperty("from", out var fromProp) && !msg.TryGetProperty("chat", out fromProp))
            {
                return null;
            }

            var fromId = fromProp.TryGetProperty("id", out var idProp) ? idProp.ToString() : "";
            if (string.IsNullOrWhiteSpace(fromId))
                return null;

            string? username = fromProp.TryGetProperty("username", out var u) ? u.GetString() : null;
            string? firstName = fromProp.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
            string? lastName = fromProp.TryGetProperty("last_name", out var ln) ? ln.GetString() : null;

            string? senderName = !string.IsNullOrWhiteSpace(username)
                ? username
                : (!string.IsNullOrWhiteSpace(lastName) ? $"{firstName} {lastName}".Trim() : firstName);

            var externalMessageId = msg.TryGetProperty("message_id", out var midProp)
                ? midProp.ToString()
                : (updateId ?? Guid.NewGuid().ToString());

            string contentType = "text";
            string content = "";
            string? mediaUrl = null;

            if (msg.TryGetProperty("text", out var textProp))
            {
                contentType = "text";
                content = textProp.GetString() ?? "";
            }
            else if (msg.TryGetProperty("photo", out var photoProp) && photoProp.ValueKind == JsonValueKind.Array && photoProp.GetArrayLength() > 0)
            {
                contentType = "photo";
                content = msg.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";
                var lastPhoto = photoProp[photoProp.GetArrayLength() - 1];
                if (lastPhoto.TryGetProperty("file_id", out var fid))
                {
                    mediaUrl = fid.GetString();
                }
                else if (lastPhoto.TryGetProperty("url", out var urlProp))
                {
                    mediaUrl = urlProp.GetString();
                }
            }
            else if (msg.TryGetProperty("voice", out var voiceProp))
            {
                contentType = "voice";
                content = msg.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";
                if (voiceProp.TryGetProperty("file_id", out var vfid))
                {
                    mediaUrl = vfid.GetString();
                }
                else if (voiceProp.TryGetProperty("url", out var vurl))
                {
                    mediaUrl = vurl.GetString();
                }
            }
            else if (msg.TryGetProperty("caption", out var generalCaption))
            {
                content = generalCaption.GetString() ?? "";
            }

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: fromId,
                SenderName: senderName,
                ExternalMessageId: externalMessageId,
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

    public string FormatOutboundMessage(string text, string[]? inlineButtons = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["text"] = text
        };

        if (inlineButtons != null && inlineButtons.Length > 0)
        {
            payload["reply_markup"] = new
            {
                inline_keyboard = inlineButtons.Select(b => new[]
                {
                    new { text = b, callback_data = b }
                }).ToArray()
            };
        }

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }
}
