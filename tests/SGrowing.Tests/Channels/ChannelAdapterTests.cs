using FluentAssertions;
using SGrowing.Api.Modules.Channels;
using Xunit;

namespace SGrowing.Tests.Channels;

public class ChannelAdapterTests
{
    [Fact]
    public void WhatsAppCloudAdapter_ShouldParseValidMetaWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new WhatsAppCloudAdapter();

        var metaJson = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "WHATSAPP_ACCOUNT_ID",
            "changes": [{
              "value": {
                "messaging_product": "whatsapp",
                "contacts": [{ "profile": { "name": "Elena Fisher" }, "wa_id": "15559876543" }],
                "messages": [{
                  "from": "15559876543",
                  "id": "wamid.HBgLMTU1NTk4NzY1NDM=",
                  "timestamp": "1729880000",
                  "text": { "body": "Can you provide pricing for enterprise?" },
                  "type": "text"
                }]
              },
              "field": "messages"
            }]
          }]
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, metaJson);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("whatsapp_cloud");
        result.ExternalSenderId.Should().Be("15559876543");
        result.SenderName.Should().Be("Elena Fisher");
        result.ExternalMessageId.Should().Be("wamid.HBgLMTU1NTk4NzY1NDM=");
        result.Content.Should().Be("Can you provide pricing for enterprise?");
    }

    [Fact]
    public void EvolutionApiAdapter_ShouldParseValidBaileysWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new EvolutionApiAdapter();

        var evoJson = """
        {
          "event": "messages.upsert",
          "instance": "support_instance",
          "data": {
            "key": {
              "remoteJid": "447123456789@s.whatsapp.net",
              "fromMe": false,
              "id": "3EB0C123456789"
            },
            "pushName": "Nathan Drake",
            "message": {
              "conversation": "Looking forward to our call tomorrow."
            },
            "messageType": "conversation"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, evoJson);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("evolution_api");
        result.ExternalSenderId.Should().Be("447123456789");
        result.SenderName.Should().Be("Nathan Drake");
        result.ExternalMessageId.Should().Be("3EB0C123456789");
        result.Content.Should().Be("Looking forward to our call tomorrow.");
    }

    [Fact]
    public void EmailAdapter_ShouldParseInboundEmailWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new EmailAdapter();

        var emailJson = """
        {
          "From": "victor.sullivan@fortune.org",
          "FromName": "Victor Sullivan",
          "Subject": "Contract review follow-up",
          "TextBody": "We have reviewed section 4 and agree with the terms.",
          "MessageID": "msg_abc123xyz"
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, emailJson);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("email");
        result.ExternalSenderId.Should().Be("victor.sullivan@fortune.org");
        result.SenderName.Should().Be("Victor Sullivan");
        result.ExternalMessageId.Should().Be("msg_abc123xyz");
        result.Content.Should().Be("We have reviewed section 4 and agree with the terms.");
    }
}
