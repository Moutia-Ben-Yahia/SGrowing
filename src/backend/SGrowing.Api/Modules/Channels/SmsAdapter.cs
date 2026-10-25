using System.Text.Json;
using SGrowing.Core.Channels;

namespace SGrowing.Api.Modules.Channels;

public class SmsAdapter : IChannelAdapter
{
    public string ChannelType => "sms";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            string? from = null;
            string? to = null;
            string? body = null;
            string? messageSid = null;
            string? mediaUrl0 = null;

            var trimmed = rawJson.Trim();
            if (trimmed.StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                var target = root;
                if (root.TryGetProperty("data", out var dataObj) && dataObj.TryGetProperty("payload", out var payloadObj))
                {
                    target = payloadObj;

                    // Telnyx format
                    if (target.TryGetProperty("from", out var telnyxFrom))
                    {
                        from = telnyxFrom.ValueKind == JsonValueKind.Object && telnyxFrom.TryGetProperty("phone_number", out var p)
                            ? p.GetString()
                            : telnyxFrom.GetString();
                    }

                    if (target.TryGetProperty("to", out var telnyxTo))
                    {
                        if (telnyxTo.ValueKind == JsonValueKind.Array && telnyxTo.GetArrayLength() > 0)
                        {
                            var firstTo = telnyxTo[0];
                            to = firstTo.ValueKind == JsonValueKind.Object && firstTo.TryGetProperty("phone_number", out var tp)
                                ? tp.GetString()
                                : firstTo.GetString();
                        }
                        else
                        {
                            to = telnyxTo.GetString();
                        }
                    }

                    if (target.TryGetProperty("text", out var telnyxText))
                        body = telnyxText.GetString();

                    if (target.TryGetProperty("id", out var telnyxId))
                        messageSid = telnyxId.GetString();

                    if (target.TryGetProperty("media", out var telnyxMedia) && telnyxMedia.ValueKind == JsonValueKind.Array && telnyxMedia.GetArrayLength() > 0)
                    {
                        var firstMedia = telnyxMedia[0];
                        if (firstMedia.TryGetProperty("url", out var u))
                            mediaUrl0 = u.GetString();
                    }
                }

                from ??= GetPropertyCaseInsensitive(target, "From", "from");
                to ??= GetPropertyCaseInsensitive(target, "To", "to");
                body ??= GetPropertyCaseInsensitive(target, "Body", "body", "Text", "text");
                messageSid ??= GetPropertyCaseInsensitive(target, "MessageSid", "messageSid", "message_sid", "SmsMessageSid", "SmsSid", "id", "Id");
                mediaUrl0 ??= GetPropertyCaseInsensitive(target, "MediaUrl0", "mediaUrl0", "media_url_0", "MediaUrl", "media_url");
            }
            else if (trimmed.Contains('='))
            {
                var pairs = trimmed.Split('&')
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(
                        p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), 
                        p => Uri.UnescapeDataString(p[1].Replace('+', ' ')), 
                        StringComparer.OrdinalIgnoreCase);

                pairs.TryGetValue("From", out from);
                pairs.TryGetValue("To", out to);
                pairs.TryGetValue("Body", out body);
                if (!pairs.TryGetValue("MessageSid", out messageSid))
                {
                    pairs.TryGetValue("SmsMessageSid", out messageSid);
                }
                pairs.TryGetValue("MediaUrl0", out mediaUrl0);
            }

            if (string.IsNullOrWhiteSpace(from))
                return null;

            messageSid ??= Guid.NewGuid().ToString();
            body ??= "";

            string contentType = !string.IsNullOrWhiteSpace(mediaUrl0) ? "media" : "text";

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: from,
                SenderName: null,
                ExternalMessageId: messageSid,
                ContentType: contentType,
                Content: body,
                MediaUrl: mediaUrl0
            );
        }
        catch
        {
            return null;
        }
    }

    private static string? GetPropertyCaseInsensitive(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
            }
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (propertyNames.Any(n => string.Equals(n, prop.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            }
        }

        return null;
    }
}
