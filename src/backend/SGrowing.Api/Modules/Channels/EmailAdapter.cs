using System.Text.Json;
using SGrowing.Core.Channels;

namespace SGrowing.Api.Modules.Channels;

public class EmailAdapter : IChannelAdapter
{
    public string ChannelType => "email";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            string from = "";
            string fromName = "";
            string textBody = "";
            string messageId = "";

            if (root.TryGetProperty("From", out var fromProp))
                from = fromProp.GetString() ?? "";

            if (root.TryGetProperty("FromName", out var fromNameProp))
                fromName = fromNameProp.GetString() ?? "";

            if (root.TryGetProperty("TextBody", out var textProp))
                textBody = textProp.GetString() ?? "";

            if (root.TryGetProperty("MessageID", out var msgIdProp))
                messageId = msgIdProp.GetString() ?? "";
            else
                messageId = Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(from))
                return null;

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: from,
                SenderName: string.IsNullOrWhiteSpace(fromName) ? null : fromName,
                ExternalMessageId: messageId,
                ContentType: "text",
                Content: textBody
            );
        }
        catch
        {
            return null;
        }
    }
}
