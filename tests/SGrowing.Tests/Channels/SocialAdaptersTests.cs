using System.Text.Json;
using FluentAssertions;
using SGrowing.Api.Modules.Channels;
using Xunit;

namespace SGrowing.Tests.Channels;

public class SocialAdaptersTests
{
    #region TelegramAdapter Tests

    [Fact]
    public void TelegramAdapter_ShouldParseTextMessageWithUsername()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new TelegramAdapter();

        var json = """
        {
          "update_id": 99881122,
          "message": {
            "message_id": 45678,
            "from": {
              "id": 123456789,
              "is_bot": false,
              "first_name": "Arthur",
              "last_name": "Morgan",
              "username": "arthur_m"
            },
            "chat": {
              "id": 123456789,
              "type": "private"
            },
            "date": 1729880000,
            "text": "Outlaws for life"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("telegram");
        result.ExternalSenderId.Should().Be("123456789");
        result.SenderName.Should().Be("arthur_m");
        result.ExternalMessageId.Should().Be("45678");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("Outlaws for life");
        result.MediaUrl.Should().BeNull();
    }

    [Fact]
    public void TelegramAdapter_ShouldParseTextMessageWithOnlyFirstName()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new TelegramAdapter();

        var json = """
        {
          "update_id": 102938,
          "message": {
            "message_id": 78901,
            "from": {
              "id": 987654321,
              "is_bot": false,
              "first_name": "Sadie",
              "last_name": "Adler"
            },
            "chat": {
              "id": 987654321,
              "type": "private"
            },
            "text": "Ready when you are"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("telegram");
        result.ExternalSenderId.Should().Be("987654321");
        result.SenderName.Should().Be("Sadie Adler");
        result.ExternalMessageId.Should().Be("78901");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("Ready when you are");
    }

    [Fact]
    public void TelegramAdapter_ShouldParsePhotoMessage()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new TelegramAdapter();

        var json = """
        {
          "update_id": 554433,
          "message": {
            "message_id": 11223,
            "from": {
              "id": 44556677,
              "first_name": "Dutch",
              "username": "van_der_linde"
            },
            "photo": [
              {
                "file_id": "photo_thumb_123",
                "width": 90,
                "height": 90
              },
              {
                "file_id": "photo_highres_789",
                "width": 1280,
                "height": 720
              }
            ],
            "caption": "I have a plan"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("telegram");
        result.ExternalSenderId.Should().Be("44556677");
        result.ContentType.Should().Be("photo");
        result.Content.Should().Be("I have a plan");
        result.MediaUrl.Should().Be("photo_highres_789");
    }

    [Fact]
    public void TelegramAdapter_ShouldParseVoiceNoteMessage()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new TelegramAdapter();

        var json = """
        {
          "update_id": 778899,
          "message": {
            "message_id": 33445,
            "from": {
              "id": 66778899,
              "first_name": "John"
            },
            "voice": {
              "file_id": "voice_note_abc123",
              "duration": 12,
              "mime_type": "audio/ogg"
            },
            "caption": "Listen to this audio memo"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("telegram");
        result.ExternalSenderId.Should().Be("66778899");
        result.ContentType.Should().Be("voice");
        result.Content.Should().Be("Listen to this audio memo");
        result.MediaUrl.Should().Be("voice_note_abc123");
    }

    [Fact]
    public void TelegramAdapter_FormatOutboundMessage_WithoutButtons()
    {
        var adapter = new TelegramAdapter();
        var payload = adapter.FormatOutboundMessage("Hello, this is a broadcast message!");

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        root.GetProperty("text").GetString().Should().Be("Hello, this is a broadcast message!");
        root.TryGetProperty("reply_markup", out _).Should().BeFalse();
    }

    [Fact]
    public void TelegramAdapter_FormatOutboundMessage_WithInlineButtons()
    {
        var adapter = new TelegramAdapter();
        var buttons = new[] { "Confirm Order", "Contact Agent", "Cancel" };
        var payload = adapter.FormatOutboundMessage("Choose an action below:", buttons);

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        root.GetProperty("text").GetString().Should().Be("Choose an action below:");
        root.TryGetProperty("reply_markup", out var markup).Should().BeTrue();
        markup.TryGetProperty("inline_keyboard", out var keyboard).Should().BeTrue();
        keyboard.GetArrayLength().Should().Be(3);

        var firstRow = keyboard[0];
        firstRow[0].GetProperty("text").GetString().Should().Be("Confirm Order");
        firstRow[0].GetProperty("callback_data").GetString().Should().Be("Confirm Order");
    }

    #endregion

    #region DiscordAdapter Tests

    [Fact]
    public void DiscordAdapter_ShouldParseStandardWebhookMessage()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new DiscordAdapter();

        var json = """
        {
          "id": "123456789012345678",
          "channel_id": "987654321098765432",
          "author": {
            "id": "112233445566778899",
            "username": "CloudStrife",
            "discriminator": "0007"
          },
          "content": "Not interested."
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("discord");
        result.ExternalSenderId.Should().Be("112233445566778899");
        result.SenderName.Should().Be("CloudStrife");
        result.ExternalMessageId.Should().Be("123456789012345678");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("Not interested.");
        result.MediaUrl.Should().BeNull();
    }

    [Fact]
    public void DiscordAdapter_ShouldParseMessageWithAttachments()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new DiscordAdapter();

        var json = """
        {
          "id": "223344556677889900",
          "author": {
            "id": "334455667788990011",
            "username": "TifaLockhart"
          },
          "content": "Seventh Heaven daily special menu",
          "attachments": [
            {
              "id": "998877665544332211",
              "filename": "menu.png",
              "content_type": "image/png",
              "url": "https://cdn.discordapp.com/attachments/123/menu.png"
            }
          ]
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("discord");
        result.ExternalSenderId.Should().Be("334455667788990011");
        result.SenderName.Should().Be("TifaLockhart");
        result.ExternalMessageId.Should().Be("223344556677889900");
        result.ContentType.Should().Be("image/png");
        result.Content.Should().Be("Seventh Heaven daily special menu");
        result.MediaUrl.Should().Be("https://cdn.discordapp.com/attachments/123/menu.png");
    }

    [Fact]
    public void DiscordAdapter_ShouldParseMessageWithEmbedsWhenContentEmpty()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new DiscordAdapter();

        var json = """
        {
          "id": "445566778899001122",
          "author": {
            "id": "556677889900112233",
            "username": "BarretWallace"
          },
          "content": "",
          "embeds": [
            {
              "title": "Mission Briefing",
              "description": "Sector 1 Reactor rendezvous at midnight.",
              "image": {
                "url": "https://cdn.discordapp.com/embeds/reactor.jpg"
              }
            }
          ]
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("discord");
        result.ExternalSenderId.Should().Be("556677889900112233");
        result.SenderName.Should().Be("BarretWallace");
        result.ContentType.Should().Be("image");
        result.Content.Should().Be("Sector 1 Reactor rendezvous at midnight.");
        result.MediaUrl.Should().Be("https://cdn.discordapp.com/embeds/reactor.jpg");
    }

    [Fact]
    public void DiscordAdapter_ShouldParseInteractionFormat()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new DiscordAdapter();

        var json = """
        {
          "id": "interaction_998877",
          "type": 2,
          "member": {
            "user": {
              "id": "user_aerith_123",
              "username": "AerithGainsborough"
            }
          },
          "message": {
            "id": "msg_interaction_5544",
            "content": "Would you like to buy a flower?"
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("discord");
        result.ExternalSenderId.Should().Be("user_aerith_123");
        result.SenderName.Should().Be("AerithGainsborough");
        result.ExternalMessageId.Should().Be("msg_interaction_5544");
        result.Content.Should().Be("Would you like to buy a flower?");
    }

    #endregion

    #region MetaGraphAdapter Tests

    [Fact]
    public void MetaGraphAdapter_ShouldParseFacebookMessengerWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new MetaGraphAdapter();

        var json = """
        {
          "object": "page",
          "entry": [
            {
              "id": "PAGE_ID_1001",
              "time": 1729880000000,
              "messaging": [
                {
                  "sender": {
                    "id": "FB_USER_5566",
                    "name": "Geralt of Rivia"
                  },
                  "recipient": {
                    "id": "PAGE_ID_1001"
                  },
                  "timestamp": 1729880000000,
                  "message": {
                    "mid": "m_mid_facebook_12345",
                    "text": "Hmm... Looks like rain."
                  }
                }
              ]
            }
          ]
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("meta_graph");
        result.ExternalSenderId.Should().Be("FB_USER_5566");
        result.SenderName.Should().Be("Geralt of Rivia");
        result.ExternalMessageId.Should().Be("m_mid_facebook_12345");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("Hmm... Looks like rain.");
        result.MediaUrl.Should().BeNull();
    }

    [Fact]
    public void MetaGraphAdapter_ShouldParseInstagramDmWithAttachment()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new MetaGraphAdapter();

        var json = """
        {
          "object": "instagram",
          "entry": [
            {
              "id": "IG_BIZ_ACCOUNT_99",
              "time": 1729881000000,
              "messaging": [
                {
                  "sender": {
                    "id": "IG_USER_7788",
                    "username": "yennefer_v"
                  },
                  "recipient": {
                    "id": "IG_BIZ_ACCOUNT_99"
                  },
                  "message": {
                    "mid": "aWdfbWVzc2FnZV8xMjM=",
                    "text": "Check this portal spell diagram",
                    "attachments": [
                      {
                        "type": "image",
                        "payload": {
                          "url": "https://lookaside.fbsbx.com/ig_messaging_cdn/portal.png"
                        }
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("meta_graph");
        result.ExternalSenderId.Should().Be("IG_USER_7788");
        result.SenderName.Should().Be("yennefer_v");
        result.ExternalMessageId.Should().Be("aWdfbWVzc2FnZV8xMjM=");
        result.ContentType.Should().Be("image");
        result.Content.Should().Be("Check this portal spell diagram");
        result.MediaUrl.Should().Be("https://lookaside.fbsbx.com/ig_messaging_cdn/portal.png");
    }

    [Fact]
    public void MetaGraphAdapter_ShouldParseThreadsWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new MetaGraphAdapter();

        var json = """
        {
          "object": "threads",
          "entry": [
            {
              "id": "THREADS_PAGE_55",
              "messaging": [
                {
                  "sender": {
                    "id": "THREADS_USER_1122"
                  },
                  "recipient": {
                    "id": "THREADS_PAGE_55"
                  },
                  "message": {
                    "mid": "threads_mid_776655",
                    "text": "Loving the new Threads DM integration!"
                  }
                }
              ]
            }
          ]
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("meta_graph");
        result.ExternalSenderId.Should().Be("THREADS_USER_1122");
        result.ExternalMessageId.Should().Be("threads_mid_776655");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("Loving the new Threads DM integration!");
    }

    #endregion

    #region XTwitterAdapter Tests

    [Fact]
    public void XTwitterAdapter_ShouldParseDirectMessageWebhook()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new XTwitterAdapter();

        var json = """
        {
          "for_user_id": "999888777",
          "direct_message_events": [
            {
              "type": "message_create",
              "id": "184920482048204820",
              "created_timestamp": "1729882000000",
              "message_create": {
                "target": {
                  "recipient_id": "999888777"
                },
                "sender_id": "1234567890",
                "message_data": {
                  "text": "Can we schedule a demo call tomorrow?"
                }
              }
            }
          ],
          "users": {
            "1234567890": {
              "id": "1234567890",
              "name": "Commander Shepard",
              "screen_name": "shepard_normandy"
            }
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("x_twitter");
        result.ExternalSenderId.Should().Be("1234567890");
        result.SenderName.Should().Be("Commander Shepard");
        result.ExternalMessageId.Should().Be("184920482048204820");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("Can we schedule a demo call tomorrow?");
        result.MediaUrl.Should().BeNull();
    }

    [Fact]
    public void XTwitterAdapter_ShouldParseDirectMessageWithAttachment()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new XTwitterAdapter();

        var json = """
        {
          "direct_message_events": [
            {
              "type": "message_create",
              "id": "184920482048204821",
              "message_create": {
                "sender_id": "2345678901",
                "sender": {
                  "name": "Liara T'Soni",
                  "screen_name": "shadow_broker"
                },
                "message_data": {
                  "text": "Prothean artifact blueprint",
                  "attachment": {
                    "type": "media",
                    "media": {
                      "id": 987654321,
                      "media_url_https": "https://ton.twitter.com/1.1/ton/data/dm/artifact.png"
                    }
                  }
                }
              }
            }
          ]
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("x_twitter");
        result.ExternalSenderId.Should().Be("2345678901");
        result.SenderName.Should().Be("Liara T'Soni");
        result.ExternalMessageId.Should().Be("184920482048204821");
        result.ContentType.Should().Be("media");
        result.Content.Should().Be("Prothean artifact blueprint");
        result.MediaUrl.Should().Be("https://ton.twitter.com/1.1/ton/data/dm/artifact.png");
    }

    #endregion

    #region SmsAdapter Tests

    [Fact]
    public void SmsAdapter_ShouldParseTwilioJsonPayload()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new SmsAdapter();

        var json = """
        {
          "From": "+15551234567",
          "To": "+15559876543",
          "Body": "YES, confirm my appointment for 3 PM.",
          "MessageSid": "SM99887766554433221100aabbccddeeff"
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("sms");
        result.ExternalSenderId.Should().Be("+15551234567");
        result.ExternalMessageId.Should().Be("SM99887766554433221100aabbccddeeff");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("YES, confirm my appointment for 3 PM.");
        result.MediaUrl.Should().BeNull();
    }

    [Fact]
    public void SmsAdapter_ShouldParseTwilioMmsWithMediaUrl0()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new SmsAdapter();

        var json = """
        {
          "From": "+15552345678",
          "To": "+15558765432",
          "Body": "Here is the receipt image",
          "MessageSid": "MM11223344556677889900aabbccddeeff",
          "MediaUrl0": "https://api.twilio.com/2010-04-01/Accounts/AC123/Messages/MM1122/Media/ME456"
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("sms");
        result.ExternalSenderId.Should().Be("+15552345678");
        result.ExternalMessageId.Should().Be("MM11223344556677889900aabbccddeeff");
        result.ContentType.Should().Be("media");
        result.Content.Should().Be("Here is the receipt image");
        result.MediaUrl.Should().Be("https://api.twilio.com/2010-04-01/Accounts/AC123/Messages/MM1122/Media/ME456");
    }

    [Fact]
    public void SmsAdapter_ShouldParseTelnyxPayload()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new SmsAdapter();

        var json = """
        {
          "data": {
            "event_type": "message.received",
            "id": "telnyx_event_123",
            "payload": {
              "from": {
                "phone_number": "+15553456789"
              },
              "to": [
                {
                  "phone_number": "+15557654321"
                }
              ],
              "text": "Hello from Telnyx SMS webhook!",
              "id": "telnyx_msg_98765",
              "media": [
                {
                  "url": "https://telnyx.com/files/receipt.pdf"
                }
              ]
            }
          }
        }
        """;

        var result = adapter.ParseInboundWebhook(tenantId, json);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("sms");
        result.ExternalSenderId.Should().Be("+15553456789");
        result.ExternalMessageId.Should().Be("telnyx_msg_98765");
        result.ContentType.Should().Be("media");
        result.Content.Should().Be("Hello from Telnyx SMS webhook!");
        result.MediaUrl.Should().Be("https://telnyx.com/files/receipt.pdf");
    }

    [Fact]
    public void SmsAdapter_ShouldParseFormUrlEncodedPayload()
    {
        var tenantId = Guid.NewGuid();
        var adapter = new SmsAdapter();

        var formData = "From=%2B15554567890&To=%2B15556543210&Body=Standard+Twilio+URL+Encoded+SMS&MessageSid=SMurlencoded123456";

        var result = adapter.ParseInboundWebhook(tenantId, formData);

        result.Should().NotBeNull();
        result!.ChannelType.Should().Be("sms");
        result.ExternalSenderId.Should().Be("+15554567890");
        result.ExternalMessageId.Should().Be("SMurlencoded123456");
        result.ContentType.Should().Be("text");
        result.Content.Should().Be("Standard Twilio URL Encoded SMS");
        result.MediaUrl.Should().BeNull();
    }

    #endregion

    #region Edge Cases & Invalid Payloads

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not a json")]
    public void Adapters_ShouldHandleMalformedPayloadsGracefully(string malformedPayload)
    {
        var tenantId = Guid.NewGuid();
        var telegram = new TelegramAdapter();
        var discord = new DiscordAdapter();
        var meta = new MetaGraphAdapter();
        var twitter = new XTwitterAdapter();
        var sms = new SmsAdapter();

        telegram.ParseInboundWebhook(tenantId, malformedPayload).Should().BeNull();
        discord.ParseInboundWebhook(tenantId, malformedPayload).Should().BeNull();
        meta.ParseInboundWebhook(tenantId, malformedPayload).Should().BeNull();
        twitter.ParseInboundWebhook(tenantId, malformedPayload).Should().BeNull();
        sms.ParseInboundWebhook(tenantId, malformedPayload).Should().BeNull();
    }

    #endregion
}
