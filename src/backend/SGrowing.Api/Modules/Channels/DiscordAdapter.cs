using System.Text.Json;
using SGrowing.Core.Channels;

namespace SGrowing.Api.Modules.Channels;

public class DiscordAdapter : IChannelAdapter
{
    public string ChannelType => "discord";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // Handle Discord webhook message or Discord interaction
            var msg = root;
            if (root.TryGetProperty("message", out var nestedMsg) && nestedMsg.ValueKind == JsonValueKind.Object)
            {
                msg = nestedMsg;
            }

            var messageId = msg.TryGetProperty("id", out var idProp)
                ? idProp.GetString() ?? idProp.ToString()
                : (root.TryGetProperty("id", out var rootIdProp) ? rootIdProp.GetString() ?? rootIdProp.ToString() : Guid.NewGuid().ToString());

            // Resolve author / user
            JsonElement authorElement = default;
            if (msg.TryGetProperty("author", out var authorProp))
            {
                authorElement = authorProp;
            }
            else if (root.TryGetProperty("author", out var rootAuthor))
            {
                authorElement = rootAuthor;
            }
            else if (root.TryGetProperty("user", out var rootUser))
            {
                authorElement = rootUser;
            }
            else if (root.TryGetProperty("member", out var member) && member.TryGetProperty("user", out var memberUser))
            {
                authorElement = memberUser;
            }

            if (authorElement.ValueKind == JsonValueKind.Undefined)
                return null;

            var authorId = authorElement.TryGetProperty("id", out var aId) ? aId.GetString() ?? aId.ToString() : "";
            if (string.IsNullOrWhiteSpace(authorId))
                return null;

            var username = authorElement.TryGetProperty("username", out var u)
                ? u.GetString()
                : (authorElement.TryGetProperty("global_name", out var gn) ? gn.GetString() : null);

            var content = msg.TryGetProperty("content", out var cProp) ? cProp.GetString() ?? "" : "";
            string contentType = "text";
            string? mediaUrl = null;

            // Check attachments
            if (msg.TryGetProperty("attachments", out var attProp) && attProp.ValueKind == JsonValueKind.Array && attProp.GetArrayLength() > 0)
            {
                var att = attProp[0];
                contentType = att.TryGetProperty("content_type", out var ct) ? ct.GetString() ?? "attachment" : "attachment";
                mediaUrl = att.TryGetProperty("url", out var urlProp)
                    ? urlProp.GetString()
                    : (att.TryGetProperty("proxy_url", out var pUrl) ? pUrl.GetString() : null);
            }
            // Check embeds
            else if (msg.TryGetProperty("embeds", out var embProp) && embProp.ValueKind == JsonValueKind.Array && embProp.GetArrayLength() > 0)
            {
                var embed = embProp[0];
                if (string.IsNullOrWhiteSpace(content))
                {
                    var embedDesc = embed.TryGetProperty("description", out var d) ? d.GetString() : null;
                    var embedTitle = embed.TryGetProperty("title", out var t) ? t.GetString() : null;
                    content = embedDesc ?? embedTitle ?? "";
                }

                if (embed.TryGetProperty("image", out var img) && img.TryGetProperty("url", out var imgUrl))
                {
                    mediaUrl = imgUrl.GetString();
                    contentType = "image";
                }
                else if (string.IsNullOrWhiteSpace(mediaUrl))
                {
                    contentType = string.IsNullOrWhiteSpace(content) ? "embed" : "text";
                }
            }

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: authorId,
                SenderName: username,
                ExternalMessageId: messageId,
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
