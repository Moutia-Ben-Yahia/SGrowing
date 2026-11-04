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

    [Fact]
    public void TelegramAdapter_ShouldParseValidTelegramUpdateWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new TelegramAdapter();

        var telegramJson = """
        {
          "update_id": 9928172,
          "message": {
            "message_id": 1024,
            "from": {
              "id": 12345678,
              "username": "sarah_growth",
              "first_name": "Sarah",
              "last_name": "Mansour"
            },
            "text": "Hello SGrowing team, please send onboarding guide"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, telegramJson);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("telegram");
        result.ExternalSenderId.Should().Be("12345678");
        result.SenderName.Should().Be("sarah_growth");
        result.ExternalMessageId.Should().Be("1024");
        result.Content.Should().Be("Hello SGrowing team, please send onboarding guide");
    }

    [Fact]
    public void TelegramAdapter_ShouldReturnNull_WhenPayloadHasNoMessageOrSender()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new TelegramAdapter();

        var emptyUpdateJson = "{\"update_id\":123}";
        var result = adapter.ParseInboundWebhook(tenantId, emptyUpdateJson);

        result.Should().BeNull();
    }

    [Fact]
    public void DiscordAdapter_ShouldParseValidDiscordMessageWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new DiscordAdapter();

        var discordJson = """
        {
          "id": "119922883377",
          "content": "Can we integrate Discord webhooks?",
          "author": {
            "id": "8877665544",
            "username": "gamer_merchant"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, discordJson);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("discord");
        result.ExternalSenderId.Should().Be("8877665544");
        result.SenderName.Should().Be("gamer_merchant");
        result.ExternalMessageId.Should().Be("119922883377");
        result.Content.Should().Be("Can we integrate Discord webhooks?");
    }

    [Fact]
    public void DiscordAdapter_ShouldReturnNull_WhenAuthorIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new DiscordAdapter();

        var noAuthorJson = "{\"id\":\"119922883377\",\"content\":\"No author here\"}";
        var result = adapter.ParseInboundWebhook(tenantId, noAuthorJson);

        result.Should().BeNull();
    }
}
