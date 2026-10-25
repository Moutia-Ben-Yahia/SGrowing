using System.Text.Json;
using MarketingStudio.Core.Channels;

namespace MarketingStudio.Api.Modules.Channels;

public class EvolutionApiAdapter : IChannelAdapter
{
    public string ChannelType => "evolution_api";

    public InboundChannelMessage? ParseInboundWebhook(Guid tenantId, string rawJson, IDictionary<string, string>? headers = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var data))
                return null;

            if (!data.TryGetProperty("key", out var key))
                return null;

            // Ignore messages sent by ourselves
            if (key.TryGetProperty("fromMe", out var fromMe) && fromMe.GetBoolean())
                return null;

            var remoteJid = key.GetProperty("remoteJid").GetString() ?? "";
            var phone = remoteJid.Split('@')[0];
            var id = key.GetProperty("id").GetString() ?? "";

            string senderName = data.TryGetProperty("pushName", out var push) ? push.GetString() ?? "" : "";
            string content = "";

            if (data.TryGetProperty("message", out var messageObj))
            {
                if (messageObj.TryGetProperty("conversation", out var convText))
                {
                    content = convText.GetString() ?? "";
                }
                else if (messageObj.TryGetProperty("extendedTextMessage", out var extText))
                {
                    content = extText.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                }
            }

            return new InboundChannelMessage(
                TenantId: tenantId,
                ChannelType: ChannelType,
                ExternalSenderId: phone,
                SenderName: string.IsNullOrWhiteSpace(senderName) ? null : senderName,
                ExternalMessageId: id,
                ContentType: "text",
                Content: content
            );
        }
        catch
        {
            return null;
        }
    }
}
