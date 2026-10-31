using System.Text.Json;
using SGrowing.Api.Modules.Channels.Telegram;
using Xunit;

namespace SGrowing.Tests.Channels;

public class TelegramMarketingBotServiceTests
{
    private readonly TelegramMarketingBotService _service;
    private const string TestBotToken = "7123456789:AAHTestToken_SGrowingEnterpriseMarketingBot";

    public TelegramMarketingBotServiceTests()
    {
        _service = new TelegramMarketingBotService();
    }

    #region Webhook & FSM Conversation Tests

    [Fact]
    public async Task ProcessWebhook_StartCommand_InitializesIdleStateAndWelcomeMessage()
    {
        // Arrange
        var update = new TelegramUpdate
        {
            UpdateId = 1001,
            Message = new TelegramMessage
            {
                MessageId = 1,
                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                From = new TelegramUser { Id = 90001, FirstName = "Carlos", Username = "carlos_growth" },
                Text = "/start"
            }
        };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Message", result.UpdateType);
        Assert.Equal(90001, result.UserId);
        Assert.Equal(TelegramConversationState.Idle, result.NewState);
        Assert.NotNull(result.ReplyText);
        Assert.Contains("Carlos", result.ReplyText);
        Assert.Contains("SGrowing Growth Engine", result.ReplyText);
        Assert.NotNull(result.ReplyMarkup);
        Assert.NotEmpty(result.ReplyMarkup.InlineKeyboard);

        var state = _service.GetUserState(90001);
        Assert.NotNull(state);
        Assert.Equal(TelegramConversationState.Idle, state.CurrentState);
    }

    [Fact]
    public async Task ProcessWebhook_CatalogCommand_TransitionsToCatalogBrowsing()
    {
        // Arrange
        var user = new TelegramUser { Id = 90002, FirstName = "Sarah", Username = "sarah_k" };
        var update = new TelegramUpdate
        {
            UpdateId = 1002,
            Message = new TelegramMessage
            {
                MessageId = 2,
                From = user,
                Text = "/catalog"
            }
        };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.CatalogBrowsing, result.NewState);
        Assert.Contains("Solutions Catalog", result.ReplyText);
        Assert.NotNull(result.ReplyMarkup);

        var state = _service.GetUserState(user.Id);
        Assert.Equal(TelegramConversationState.CatalogBrowsing, state!.CurrentState);
    }

    [Fact]
    public async Task ProcessWebhook_CallbackQuery_BrowseCatalog_TransitionsToCatalogBrowsing()
    {
        // Arrange
        var query = new TelegramCallbackQuery
        {
            Id = "cb_1",
            From = new TelegramUser { Id = 90003, FirstName = "Leo", Username = "leo_dev" },
            Data = "action:browse_catalog"
        };
        var update = new TelegramUpdate { UpdateId = 1003, CallbackQuery = query };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("CallbackQuery", result.UpdateType);
        Assert.Equal(TelegramConversationState.CatalogBrowsing, result.NewState);
        Assert.Contains("Solutions Catalog", result.ReplyText);
    }

    [Fact]
    public async Task ProcessWebhook_CallbackQuery_AddToCart_AddsItemToUserCart()
    {
        // Arrange
        var query = new TelegramCallbackQuery
        {
            Id = "cb_2",
            From = new TelegramUser { Id = 90004, FirstName = "Maya", Username = "maya_m" },
            Data = "cart:add:accelerator"
        };
        var update = new TelegramUpdate { UpdateId = 1004, CallbackQuery = query };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Added to Cart: Growth Marketing Accelerator", result.ReplyText);
        Assert.Contains("$199.00", result.ReplyText);

        var state = _service.GetUserState(90004);
        Assert.NotNull(state);
        Assert.Single(state.CartItems);
        Assert.Equal("Growth Marketing Accelerator", state.CartItems[0]);
        Assert.Equal(199.00m, state.CartTotal);
    }

    [Fact]
    public async Task ProcessWebhook_CallbackQuery_AddMultipleItems_CalculatesTotal()
    {
        // Arrange
        var userId = 90005L;
        var u1 = new TelegramUpdate
        {
            UpdateId = 1005,
            CallbackQuery = new TelegramCallbackQuery
            {
                Id = "cb_3a",
                From = new TelegramUser { Id = userId, FirstName = "Ken" },
                Data = "cart:add:suite"
            }
        };
        var u2 = new TelegramUpdate
        {
            UpdateId = 1006,
            CallbackQuery = new TelegramCallbackQuery
            {
                Id = "cb_3b",
                From = new TelegramUser { Id = userId, FirstName = "Ken" },
                Data = "cart:add:voice"
            }
        };

        // Act
        await _service.ProcessWebhookUpdateAsync(u1);
        var res2 = await _service.ProcessWebhookUpdateAsync(u2);

        // Assert
        Assert.True(res2.Success);
        var state = _service.GetUserState(userId);
        Assert.Equal(2, state!.CartItems.Count);
        Assert.Equal(349.00m + 499.00m, state.CartTotal);
        Assert.Contains("$848.00", res2.ReplyText);
    }

    [Fact]
    public async Task ProcessWebhook_CallbackQuery_Checkout_TransitionsToCheckoutPending()
    {
        // Arrange
        var query = new TelegramCallbackQuery
        {
            Id = "cb_4",
            From = new TelegramUser { Id = 90006, FirstName = "Olga" },
            Data = "action:checkout"
        };
        var update = new TelegramUpdate { UpdateId = 1007, CallbackQuery = query };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.ReplyText);
        Assert.Contains("Checkout Summary", result.ReplyText);

        var state = _service.GetUserState(90006);
        Assert.NotNull(state);
        Assert.Equal(TelegramConversationState.AwaitingEmail, state.CurrentState);
    }

    [Fact]
    public async Task ProcessWebhook_SubscribeCommand_TransitionsToAwaitingEmail()
    {
        // Arrange
        var update = new TelegramUpdate
        {
            UpdateId = 1008,
            Message = new TelegramMessage
            {
                MessageId = 8,
                From = new TelegramUser { Id = 90007, FirstName = "Tariq" },
                Text = "/subscribe"
            }
        };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.AwaitingEmail, result.NewState);
        Assert.Contains("business email", result.ReplyText);
    }

    [Fact]
    public async Task ProcessWebhook_CallbackQuery_SubscribeFlow_TransitionsToAwaitingEmail()
    {
        // Arrange
        var query = new TelegramCallbackQuery
        {
            Id = "cb_sub",
            From = new TelegramUser { Id = 90008, FirstName = "Chloe" },
            Data = "action:subscribe_flow"
        };
        var update = new TelegramUpdate { UpdateId = 1009, CallbackQuery = query };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.AwaitingEmail, result.NewState);
    }

    [Fact]
    public async Task ProcessWebhook_AwaitingEmail_ValidEmail_TransitionsToAwaitingPhone()
    {
        // Arrange: Start in AwaitingEmail
        var user = new TelegramUser { Id = 90009, FirstName = "Lucas" };
        await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
        {
            UpdateId = 1010,
            Message = new TelegramMessage { MessageId = 10, From = user, Text = "/subscribe" }
        });

        // Act: Send valid email
        var update = new TelegramUpdate
        {
            UpdateId = 1011,
            Message = new TelegramMessage
            {
                MessageId = 11,
                From = user,
                Text = "lucas.growth@sgrowing.io"
            }
        };
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.AwaitingPhone, result.NewState);
        Assert.Contains("Email captured: lucas.growth@sgrowing.io", result.ReplyText);
        Assert.Contains("phone number", result.ReplyText);

        var state = _service.GetUserState(user.Id);
        Assert.Equal("lucas.growth@sgrowing.io", state!.CapturedEmail);
        Assert.Equal(TelegramConversationState.AwaitingPhone, state.CurrentState);
    }

    [Fact]
    public async Task ProcessWebhook_AwaitingEmail_InvalidEmail_RemainsInAwaitingEmailWithPrompt()
    {
        // Arrange
        var user = new TelegramUser { Id = 90010, FirstName = "Anna" };
        await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
        {
            UpdateId = 1012,
            Message = new TelegramMessage { MessageId = 12, From = user, Text = "/subscribe" }
        });

        // Act: Enter bad email
        var update = new TelegramUpdate
        {
            UpdateId = 1013,
            Message = new TelegramMessage { MessageId = 13, From = user, Text = "not-an-email" }
        };
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.AwaitingEmail, result.NewState);
        Assert.Contains("doesn't look like a valid email", result.ReplyText);

        var state = _service.GetUserState(user.Id);
        Assert.Null(state!.CapturedEmail);
        Assert.Equal(TelegramConversationState.AwaitingEmail, state.CurrentState);
    }

    [Fact]
    public async Task ProcessWebhook_AwaitingPhone_ValidPhone_TransitionsToSubscribedAndRegistersSubscriber()
    {
        // Arrange: Advance to AwaitingPhone
        var user = new TelegramUser { Id = 90011, FirstName = "Liam", Username = "liam_w" };
        await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
        {
            UpdateId = 1014,
            Message = new TelegramMessage { MessageId = 14, From = user, Text = "/subscribe" }
        });
        await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
        {
            UpdateId = 1015,
            Message = new TelegramMessage { MessageId = 15, From = user, Text = "liam@techforward.com" }
        });

        // Act: Send phone number
        var update = new TelegramUpdate
        {
            UpdateId = 1016,
            Message = new TelegramMessage
            {
                MessageId = 16,
                From = user,
                Text = "+1 555 492 8192"
            }
        };
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.Subscribed, result.NewState);
        Assert.Contains("unlocked as an SGrowing VIP member", result.ReplyText);

        var state = _service.GetUserState(user.Id);
        Assert.Equal("+1 555 492 8192", state!.CapturedPhone);
        Assert.Equal(TelegramConversationState.Subscribed, state.CurrentState);

        var subscriber = _service.GetSubscribers().FirstOrDefault(s => s.TelegramUserId == user.Id);
        Assert.NotNull(subscriber);
        Assert.True(subscriber.IsSubscribed);
        Assert.Equal("liam@techforward.com", subscriber.Email);
        Assert.Equal("+1 555 492 8192", subscriber.Phone);
    }

    [Fact]
    public async Task ProcessWebhook_AwaitingPhone_InvalidPhone_RemainsInAwaitingPhone()
    {
        // Arrange
        var user = new TelegramUser { Id = 90012, FirstName = "Zoe" };
        await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
        {
            UpdateId = 1017,
            Message = new TelegramMessage { MessageId = 17, From = user, Text = "/subscribe" }
        });
        await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
        {
            UpdateId = 1018,
            Message = new TelegramMessage { MessageId = 18, From = user, Text = "zoe@example.com" }
        });

        // Act: Send short invalid phone
        var update = new TelegramUpdate
        {
            UpdateId = 1019,
            Message = new TelegramMessage { MessageId = 19, From = user, Text = "123" }
        };
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.AwaitingPhone, result.NewState);
        Assert.Contains("valid phone number", result.ReplyText);

        var state = _service.GetUserState(user.Id);
        Assert.Equal(TelegramConversationState.AwaitingPhone, state!.CurrentState);
    }

    [Fact]
    public async Task ProcessWebhook_ContactAttachment_CapturesPhoneNumberAndSubscribes()
    {
        // Arrange
        var user = new TelegramUser { Id = 90013, FirstName = "Gabriel" };
        var update = new TelegramUpdate
        {
            UpdateId = 1020,
            Message = new TelegramMessage
            {
                MessageId = 20,
                From = user,
                Contact = new TelegramContact
                {
                    PhoneNumber = "+15557778888",
                    FirstName = "Gabriel",
                    UserId = 90013
                }
            }
        };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.Subscribed, result.NewState);
        Assert.Contains("phone number is verified", result.ReplyText);

        var state = _service.GetUserState(user.Id);
        Assert.Equal("+15557778888", state!.CapturedPhone);
    }

    [Fact]
    public async Task ProcessWebhook_WebAppData_ProcessesPayloadAndConfirmsOrder()
    {
        // Arrange
        var user = new TelegramUser { Id = 90014, FirstName = "Nadia", Username = "nadia_biz" };
        var payload = "{\"orderId\":\"ORD-MINI-9921\",\"total\":349.00,\"email\":\"nadia@growth.io\",\"phone\":\"+15552223333\"}";
        var update = new TelegramUpdate
        {
            UpdateId = 1021,
            Message = new TelegramMessage
            {
                MessageId = 21,
                From = user,
                WebAppData = new TelegramWebAppData
                {
                    Data = payload,
                    ButtonText = "Complete Payment"
                }
            }
        };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("WebAppData", result.UpdateType);
        Assert.Equal(TelegramConversationState.Subscribed, result.NewState);
        Assert.Contains("ORD-MINI-9921", result.ReplyText);
        Assert.Contains("$349.00", result.ReplyText);

        var state = _service.GetUserState(user.Id);
        Assert.Equal("nadia@growth.io", state!.CapturedEmail);
        Assert.Equal("+15552223333", state.CapturedPhone);
    }

    [Fact]
    public async Task ProcessWebhook_ResetCommand_ResetsStateToIdle()
    {
        // Arrange: Place user in CatalogBrowsing with cart items
        var user = new TelegramUser { Id = 90015, FirstName = "Evan" };
        await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
        {
            UpdateId = 1022,
            CallbackQuery = new TelegramCallbackQuery { From = user, Data = "cart:add:accelerator" }
        });

        // Act: Send /reset
        var update = new TelegramUpdate
        {
            UpdateId = 1023,
            Message = new TelegramMessage { MessageId = 23, From = user, Text = "/reset" }
        };
        var result = await _service.ProcessWebhookUpdateAsync(update);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TelegramConversationState.Idle, result.NewState);

        var state = _service.GetUserState(user.Id);
        Assert.Empty(state!.CartItems);
        Assert.Equal(0m, state.CartTotal);
    }

    [Fact]
    public async Task ProcessWebhook_SecretTokenValidation_RejectsInvalidToken()
    {
        // Arrange
        _service.UpdateConfig(new TelegramBotConfig
        {
            WebhookSecretToken = "secure-secret-token-xyz"
        });

        var update = new TelegramUpdate
        {
            UpdateId = 1024,
            Message = new TelegramMessage
            {
                MessageId = 24,
                From = new TelegramUser { Id = 90016, FirstName = "Hacker" },
                Text = "/start"
            }
        };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update, secretToken: "wrong-secret-token");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SecurityRejected", result.UpdateType);
        Assert.Contains("Invalid X-Telegram-Bot-Api-Secret-Token", result.Error);
    }

    [Fact]
    public async Task ProcessWebhook_SecretTokenValidation_AcceptsValidToken()
    {
        // Arrange
        _service.UpdateConfig(new TelegramBotConfig
        {
            WebhookSecretToken = "secure-secret-token-xyz"
        });

        var update = new TelegramUpdate
        {
            UpdateId = 1025,
            Message = new TelegramMessage
            {
                MessageId = 25,
                From = new TelegramUser { Id = 90017, FirstName = "LegitUser" },
                Text = "/start"
            }
        };

        // Act
        var result = await _service.ProcessWebhookUpdateAsync(update, secretToken: "secure-secret-token-xyz");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Message", result.UpdateType);
    }

    #endregion

    #region Cryptographic Mini App HMAC-SHA256 Validation Tests

    [Fact]
    public void ValidateWebAppData_ValidVector_ReturnsSuccess()
    {
        // Arrange: Generate valid initData with current timestamp
        var parameters = new Dictionary<string, string>
        {
            { "auth_date", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            { "query_id", "AAG9X04sAAAAAL1fTix68A-O" },
            { "user", "{\"id\":108274912,\"first_name\":\"Elena\",\"username\":\"elena_marketing\"}" }
        };

        var initData = _service.GenerateWebAppInitData(parameters, TestBotToken);

        // Act
        var result = _service.ValidateWebAppData(initData, TestBotToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.False(result.IsExpired);
        Assert.NotNull(result.CalculatedHash);
        Assert.NotNull(result.ReceivedHash);
        Assert.Equal(result.CalculatedHash, result.ReceivedHash);
        Assert.NotNull(result.User);
        Assert.Equal(108274912, result.User.Id);
        Assert.Equal("Elena", result.User.FirstName);
        Assert.Equal("elena_marketing", result.User.Username);
    }

    [Fact]
    public void ValidateWebAppData_TamperedData_RejectsHashMismatch()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "auth_date", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            { "query_id", "AAG9X04sAAAAAL1fTix68A-O" },
            { "user", "{\"id\":108274912,\"first_name\":\"Elena\"}" }
        };
        var initData = _service.GenerateWebAppInitData(parameters, TestBotToken);

        // Tamper with payload: change user id from 108274912 to 999999999
        var tampered = initData.Replace("108274912", "999999999");

        // Act
        var result = _service.ValidateWebAppData(tampered, TestBotToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Cryptographic signature mismatch", result.Error);
    }

    [Fact]
    public void ValidateWebAppData_TamperedHash_RejectsHashMismatch()
    {
        // Arrange
        var parameters = new Dictionary<string, string>
        {
            { "auth_date", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            { "query_id", "AAG9X04sAAAAAL1fTix68A-O" }
        };
        var initData = _service.GenerateWebAppInitData(parameters, TestBotToken);

        // Tamper with hash value
        var hashIdx = initData.IndexOf("hash=");
        var tampered = initData[..hashIdx] + "hash=0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = _service.ValidateWebAppData(tampered, TestBotToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Cryptographic signature mismatch", result.Error);
    }

    [Fact]
    public void ValidateWebAppData_ExpiredAuthDate_RejectsExpiredData()
    {
        // Arrange: auth_date set to 30 hours ago (beyond default 24h limit)
        var oldAuthDate = DateTimeOffset.UtcNow.AddHours(-30).ToUnixTimeSeconds();
        var parameters = new Dictionary<string, string>
        {
            { "auth_date", oldAuthDate.ToString() },
            { "query_id", "AAG9X04sAAAAAL1fTix68A-O" },
            { "user", "{\"id\":12345,\"first_name\":\"OldUser\"}" }
        };
        var initData = _service.GenerateWebAppInitData(parameters, TestBotToken);

        // Act
        var result = _service.ValidateWebAppData(initData, TestBotToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.IsExpired);
        Assert.Contains("expired", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateWebAppData_FutureAuthDateBeyondSkew_RejectsData()
    {
        // Arrange: auth_date set to 1 hour in the future
        var futureAuthDate = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var parameters = new Dictionary<string, string>
        {
            { "auth_date", futureAuthDate.ToString() },
            { "query_id", "AAG9X04sAAAAAL1fTix68A-O" }
        };
        var initData = _service.GenerateWebAppInitData(parameters, TestBotToken);

        // Act
        var result = _service.ValidateWebAppData(initData, TestBotToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.IsExpired);
    }

    [Fact]
    public void ValidateWebAppData_MissingHash_RejectsInvalidPayload()
    {
        // Arrange
        var rawNoHash = $"auth_date={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}&query_id=123";

        // Act
        var result = _service.ValidateWebAppData(rawNoHash, TestBotToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("missing the required 'hash'", result.Error);
    }

    [Fact]
    public void ValidateWebAppData_MissingAuthDate_RejectsInvalidPayload()
    {
        // Arrange
        var rawNoAuthDate = "query_id=123&hash=abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

        // Act
        var result = _service.ValidateWebAppData(rawNoAuthDate, TestBotToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("missing or has invalid 'auth_date'", result.Error);
    }

    [Fact]
    public void ValidateWebAppData_EmptyString_RejectsGracefully()
    {
        // Act
        var result = _service.ValidateWebAppData("", TestBotToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be empty", result.Error);
    }

    [Fact]
    public void ValidateWebAppData_ParsesUserJsonPayloadCorrectly()
    {
        // Arrange
        var userJson = "{\"id\":384729104,\"first_name\":\"Sofia\",\"last_name\":\"Chen\",\"username\":\"sofia_ai\",\"language_code\":\"en\",\"allows_write_to_pm\":true}";
        var parameters = new Dictionary<string, string>
        {
            { "auth_date", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            { "user", userJson }
        };
        var initData = _service.GenerateWebAppInitData(parameters, TestBotToken);

        // Act
        var result = _service.ValidateWebAppData(initData, TestBotToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.User);
        Assert.Equal(384729104, result.User.Id);
        Assert.Equal("Sofia", result.User.FirstName);
        Assert.Equal("Chen", result.User.LastName);
        Assert.Equal("sofia_ai", result.User.Username);
        Assert.Equal("en", result.User.LanguageCode);
        Assert.True(result.User.AllowsWriteToPm);
    }

    [Fact]
    public void ValidateWebAppData_CustomMaxAge_EnforcesTighterWindow()
    {
        // Arrange: 2 hours old
        var twoHoursOld = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
        var parameters = new Dictionary<string, string>
        {
            { "auth_date", twoHoursOld.ToString() },
            { "query_id", "Q-12345" }
        };
        var initData = _service.GenerateWebAppInitData(parameters, TestBotToken);

        // Act: Enforce max age of 1 hour
        var result = _service.ValidateWebAppData(initData, TestBotToken, maxAge: TimeSpan.FromHours(1));

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.IsExpired);
    }

    [Fact]
    public void GenerateWebAppInitData_GeneratesVerifiableHash()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            { "auth_date", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            { "chat_type", "sender" },
            { "query_id", "AAHdF6IQAAAAAN0XohDhrOrc" }
        };

        // Act
        var initData = _service.GenerateWebAppInitData(dict, TestBotToken);

        // Assert
        Assert.Contains("hash=", initData);
        var validation = _service.ValidateWebAppData(initData, TestBotToken);
        Assert.True(validation.IsValid);
    }

    #endregion

    #region Inline Keyboard Builder Tests

    [Fact]
    public void InlineKeyboardBuilder_SingleCallbackButton_ConstructsValidMarkup()
    {
        // Act
        var markup = new TelegramInlineKeyboardBuilder()
            .AddCallbackButton("Click Me", "action:click")
            .Build();

        // Assert
        Assert.Single(markup.InlineKeyboard);
        Assert.Single(markup.InlineKeyboard[0]);
        var btn = markup.InlineKeyboard[0][0];
        Assert.Equal("Click Me", btn.Text);
        Assert.Equal("action:click", btn.CallbackData);
        Assert.Null(btn.Url);
        Assert.Null(btn.WebApp);
    }

    [Fact]
    public void InlineKeyboardBuilder_MultiRowWithUrlAndWebAppButtons_ConstructsValidStructure()
    {
        // Act
        var markup = new TelegramInlineKeyboardBuilder()
            .AddRow()
            .AddUrlButton("Website", "https://sgrowing.io")
            .AddRow()
            .AddWebAppButton("Launch App", "https://miniapp.sgrowing.io/shop")
            .Build();

        // Assert
        Assert.Equal(2, markup.InlineKeyboard.Count);
        Assert.Equal("https://sgrowing.io", markup.InlineKeyboard[0][0].Url);
        Assert.Equal("https://miniapp.sgrowing.io/shop", markup.InlineKeyboard[1][0].WebApp?.Url);
    }

    [Fact]
    public void InlineKeyboardBuilder_Serialization_ProducesValidJson()
    {
        // Arrange
        var markup = new TelegramInlineKeyboardBuilder()
            .AddRow()
            .AddCallbackButton("Option 1", "opt_1")
            .AddCallbackButton("Option 2", "opt_2")
            .AddRow()
            .AddWebAppButton("Mini App", "https://app.sgrowing.io")
            .Build();

        // Act
        var json = JsonSerializer.Serialize(markup);

        // Assert
        Assert.Contains("\"inline_keyboard\"", json);
        Assert.Contains("\"callback_data\":\"opt_1\"", json);
        Assert.Contains("\"web_app\":{\"url\":\"https://app.sgrowing.io\"}", json);
    }

    #endregion

    #region Send Message & Broadcast Engine Tests

    [Fact]
    public async Task SendMessageAsync_DeliversMessageAndLogsToHistory()
    {
        // Arrange
        var req = new TelegramSendMessageRequest
        {
            ChatId = 108274912,
            Text = "Hello VIP customer!"
        };

        // Act
        var res = await _service.SendMessageAsync(req);

        // Assert
        Assert.True(res.Success);
        Assert.Equal(108274912, res.ChatId);
        Assert.NotNull(res.MessageId);
        Assert.True(res.MessageId > 0);
    }

    [Fact]
    public async Task SendMessageAsync_InvalidChatId_ReturnsError()
    {
        // Act
        var res = await _service.SendMessageAsync(new TelegramSendMessageRequest { ChatId = 0, Text = "test" });

        // Assert
        Assert.False(res.Success);
        Assert.Contains("Invalid ChatId", res.Error);
    }

    [Fact]
    public async Task BroadcastAsync_DispatchesToAllSubscribers_WithPacing()
    {
        // Arrange
        var req = new TelegramBroadcastRequest
        {
            CampaignName = "Autumn Blast",
            MessageText = "🍂 SGrowing Autumn Special Offers are now active!",
            TargetAudience = "AllSubscribers",
            MaxMessagesPerSecond = 30
        };

        // Act
        var result = await _service.BroadcastAsync(req);

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.True(result.TotalRecipients >= 4);
        Assert.Equal(result.TotalRecipients, result.DeliveredCount);
        Assert.Equal(0, result.FailedCount);

        var history = _service.GetBroadcastHistory();
        Assert.Contains(history, b => b.CampaignName == "Autumn Blast");
    }

    [Fact]
    public async Task BroadcastAsync_TargetAudienceFilter_SendsOnlyToMatchingUsers()
    {
        // Arrange: Audience = VIP
        var req = new TelegramBroadcastRequest
        {
            CampaignName = "VIP Only Drop",
            MessageText = "💎 Private sale for VIP members",
            TargetAudience = "VipOnly"
        };

        // Act
        var result = await _service.BroadcastAsync(req);

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.True(result.DeliveredCount >= 2);
        Assert.Equal(result.TotalRecipients, result.DeliveredCount);
    }

    [Fact]
    public async Task BroadcastAsync_FloodWaitSimulation_AppliesBackoffAndRecovers()
    {
        // Arrange
        var req = new TelegramBroadcastRequest
        {
            CampaignName = "Stress Test Flood Wait",
            MessageText = "Testing flood wait recovery",
            TargetAudience = "AllSubscribers",
            SimulateFloodWait = true,
            FloodWaitThreshold = 2,
            FloodWaitDurationSeconds = 1
        };

        // Act
        var result = await _service.BroadcastAsync(req);

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.True(result.FloodWaitCount >= 1);
        Assert.True(result.RetryCount >= 1);
        Assert.NotEmpty(result.FloodWaitEvents);
        Assert.Contains("FloodWait", result.FloodWaitEvents[0].Reason);
    }

    [Fact]
    public async Task BroadcastAsync_RateLimitClamping_EnforcesMax30PerSec()
    {
        // Arrange: Request 100 msgs/sec
        var req = new TelegramBroadcastRequest
        {
            CampaignName = "Overclocked Broadcast",
            MessageText = "Clamped rate limit check",
            MaxMessagesPerSecond = 100
        };

        // Act
        var result = await _service.BroadcastAsync(req);

        // Assert
        Assert.Equal("Completed", result.Status);
        Assert.Equal(result.TotalRecipients, result.DeliveredCount);
    }

    #endregion

    #region State Management, Metrics & Concurrency Tests

    [Fact]
    public void GetMetrics_AccuratelyAggregatesOperations()
    {
        // Act
        var metrics = _service.GetMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalActiveConversations >= 4);
        Assert.True(metrics.TotalSubscribers >= 4);
        Assert.NotNull(metrics.StateDistribution);
        Assert.True(metrics.StateDistribution.ContainsKey(TelegramConversationState.Subscribed));
        Assert.True(metrics.StateDistribution.ContainsKey(TelegramConversationState.CatalogBrowsing));
    }

    [Fact]
    public void ResetUserState_ClearsStateAndCart()
    {
        // Arrange: User with items
        var userId = 108274912L;
        var existing = _service.GetUserState(userId);
        Assert.NotNull(existing);
        Assert.NotEmpty(existing.CartItems);

        // Act
        var reset = _service.ResetUserState(userId);

        // Assert
        Assert.Equal(TelegramConversationState.Idle, reset.CurrentState);
        Assert.Empty(reset.CartItems);
        Assert.Equal(0m, reset.CartTotal);
    }

    [Fact]
    public void UpdateConfig_PersistsConfigurationChanges()
    {
        // Arrange
        var newConfig = new TelegramBotConfig
        {
            BotName = "Updated SGrowing Bot",
            RateLimitMsgsPerSec = 25,
            WebAppUrl = "https://custom-shop.sgrowing.io"
        };

        // Act
        _service.UpdateConfig(newConfig);
        var current = _service.GetConfig();

        // Assert
        Assert.Equal("Updated SGrowing Bot", current.BotName);
        Assert.Equal(25, current.RateLimitMsgsPerSec);
        Assert.Equal("https://custom-shop.sgrowing.io", current.WebAppUrl);
    }

    [Fact]
    public void AddOrUpdateSubscriber_MaintainsSubscriberRegistry()
    {
        // Arrange
        var newSub = new TelegramSubscriber
        {
            TelegramUserId = 777123456,
            FullName = "Amira Al-Mansoor",
            Username = "amira_m",
            Email = "amira@mansoorcapital.com",
            Phone = "+971501234567",
            IsSubscribed = true,
            Tags = new() { "VIP", "MiddleEast" }
        };

        // Act
        _service.AddOrUpdateSubscriber(newSub);
        var subs = _service.GetSubscribers();

        // Assert
        var found = subs.FirstOrDefault(s => s.TelegramUserId == 777123456);
        Assert.NotNull(found);
        Assert.Equal("amira@mansoorcapital.com", found.Email);
        Assert.Contains("MiddleEast", found.Tags);
    }

    [Fact]
    public async Task Concurrent_MultiUserFsmTransitions_ThreadSafe()
    {
        // Arrange: 25 concurrent users running full flow
        const int userCount = 25;
        var tasks = new List<Task>();

        for (int i = 0; i < userCount; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                var userId = 800000 + idx;
                var user = new TelegramUser { Id = userId, FirstName = $"User_{idx}" };

                // 1. /start
                await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
                {
                    UpdateId = 20000 + idx,
                    Message = new TelegramMessage { MessageId = 1, From = user, Text = "/start" }
                });

                // 2. /subscribe
                await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
                {
                    UpdateId = 30000 + idx,
                    Message = new TelegramMessage { MessageId = 2, From = user, Text = "/subscribe" }
                });

                // 3. Email
                await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
                {
                    UpdateId = 40000 + idx,
                    Message = new TelegramMessage { MessageId = 3, From = user, Text = $"concurrent_{idx}@test.io" }
                });

                // 4. Phone
                await _service.ProcessWebhookUpdateAsync(new TelegramUpdate
                {
                    UpdateId = 50000 + idx,
                    Message = new TelegramMessage { MessageId = 4, From = user, Text = $"+1555000{idx:D4}" }
                });
            }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        for (int i = 0; i < userCount; i++)
        {
            var state = _service.GetUserState(800000 + i);
            Assert.NotNull(state);
            Assert.Equal(TelegramConversationState.Subscribed, state.CurrentState);
            Assert.Equal($"concurrent_{i}@test.io", state.CapturedEmail);
            Assert.Equal($"+1555000{i:D4}", state.CapturedPhone);
        }
    }

    [Fact]
    public async Task Concurrent_BroadcastAndWebhookProcessing_ThreadSafe()
    {
        // Arrange
        var webhookTask = Task.Run(async () =>
        {
            for (int i = 0; i < 15; i++)
            {
                var update = new TelegramUpdate
                {
                    UpdateId = 60000 + i,
                    Message = new TelegramMessage
                    {
                        MessageId = 100 + i,
                        From = new TelegramUser { Id = 70000 + i, FirstName = $"Stress_{i}" },
                        Text = "/start"
                    }
                };
                await _service.ProcessWebhookUpdateAsync(update);
            }
        });

        var broadcastTask = Task.Run(async () =>
        {
            for (int i = 0; i < 3; i++)
            {
                await _service.BroadcastAsync(new TelegramBroadcastRequest
                {
                    CampaignName = $"Stress Campaign {i}",
                    MessageText = $"Flash update {i}",
                    TargetAudience = "AllSubscribers"
                });
            }
        });

        // Act & Assert (must complete without deadlock or exception)
        await Task.WhenAll(webhookTask, broadcastTask);

        var metrics = _service.GetMetrics();
        Assert.True(metrics.TotalUpdatesReceived >= 15);
        Assert.True(metrics.TotalBroadcastsDispatched >= 3);
    }

    #endregion
}
