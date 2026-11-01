using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Api.Modules.Gateways.Android;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppClusterAndAntiBanTests
{
    private readonly Guid _testTenant = Guid.NewGuid();

    #region 1. WhatsApp Baileys Session Cluster & State Machine Tests

    [Fact]
    public async Task RegisterInstance_InitializesInDisconnectedState()
    {
        var service = new WhatsAppSessionClusterService(NullLogger<WhatsAppSessionClusterService>.Instance);
        var req = new RegisterClusterInstanceRequest("SalesDesk", "+21698111222", "SalesDesk");

        var session = await service.RegisterInstanceAsync(_testTenant, req);

        session.Should().NotBeNull();
        session.InstanceName.Should().Be("SalesDesk");
        session.PhoneNumber.Should().Be("+21698111222");
        session.Role.Should().Be("SalesDesk");
        session.State.Should().Be(BaileysSessionState.Disconnected);
        session.IsHealthy.Should().BeFalse();
        session.History.Should().ContainSingle(h => h.Reason.Contains("registered"));
    }

    [Fact]
    public async Task TransitionState_DisconnectedToPairingQrRequired_GeneratesQrAndPairingCode()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("SupportDesk", "+21698222333", "SupportDesk"));

        var updated = await service.TransitionStateAsync(_testTenant, "SupportDesk", BaileysSessionState.PairingQrRequired, "Requesting initial QR");

        updated.State.Should().Be(BaileysSessionState.PairingQrRequired);
        updated.QrCodePayload.Should().NotBeNullOrWhiteSpace();
        updated.PairingCode.Should().NotBeNullOrWhiteSpace();
        updated.PairingCode.Should().MatchRegex(@"^\d{4}-\d{4}$");
        updated.History.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransitionState_PairingQrRequiredToConnected_SetsConnectedAtAndResetsAttempts()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("VipConcierge", "+21698333444", "VipConcierge"));
        await service.TransitionStateAsync(_testTenant, "VipConcierge", BaileysSessionState.PairingQrRequired);

        var connected = await service.TransitionStateAsync(_testTenant, "VipConcierge", BaileysSessionState.Connected, "Phone linked successfully");

        connected.State.Should().Be(BaileysSessionState.Connected);
        connected.ConnectedAtUtc.Should().NotBeNull();
        connected.ReconnectAttempts.Should().Be(0);
        connected.QrCodePayload.Should().BeNull();
        connected.PairingCode.Should().BeNull();
        connected.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionState_ConnectedToReconnecting_IncrementsAttempts()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("SalesDesk", "+21698111222"));
        await service.TransitionStateAsync(_testTenant, "SalesDesk", BaileysSessionState.Connected);

        var reconnecting = await service.TransitionStateAsync(_testTenant, "SalesDesk", BaileysSessionState.Reconnecting, "Network glitch");

        reconnecting.State.Should().Be(BaileysSessionState.Reconnecting);
        reconnecting.ReconnectAttempts.Should().Be(1);
        reconnecting.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task TransitionState_ConnectedToBanned_ClearsActiveSessionAndSetsBanned()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("SpamRiskLine", "+21698555666"));
        await service.TransitionStateAsync(_testTenant, "SpamRiskLine", BaileysSessionState.Connected);

        var banned = await service.TransitionStateAsync(_testTenant, "SpamRiskLine", BaileysSessionState.Banned, "Spam threshold exceeded");

        banned.State.Should().Be(BaileysSessionState.Banned);
        banned.ConnectedAtUtc.Should().BeNull();
        banned.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task TransitionState_BannedInstance_ThrowsInvalidOperationExceptionWithoutAdminOverride()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("BannedLine", "+21698555777"));
        await service.TransitionStateAsync(_testTenant, "BannedLine", BaileysSessionState.Banned);

        var act = async () => await service.TransitionStateAsync(_testTenant, "BannedLine", BaileysSessionState.Connected, "Normal reconnect");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*administrative override*");
    }

    [Fact]
    public async Task TransitionState_BannedInstance_AllowsTransitionWithAdminOverride()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("BannedLine", "+21698555777"));
        await service.TransitionStateAsync(_testTenant, "BannedLine", BaileysSessionState.Banned);

        var restored = await service.TransitionStateAsync(_testTenant, "BannedLine", BaileysSessionState.Disconnected, "admin_override: unbanned by compliance");

        restored.State.Should().Be(BaileysSessionState.Disconnected);
    }

    [Fact]
    public async Task SelectLoadBalancedInstance_RoundRobin_CyclesThroughActiveInstances()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("Line1", "+21698000001", "General"));
        await service.RegisterInstanceAsync(_testTenant, new("Line2", "+21698000002", "General"));
        await service.RegisterInstanceAsync(_testTenant, new("Line3", "+21698000003", "General"));

        await service.TransitionStateAsync(_testTenant, "Line1", BaileysSessionState.Connected);
        await service.TransitionStateAsync(_testTenant, "Line2", BaileysSessionState.Connected);
        await service.TransitionStateAsync(_testTenant, "Line3", BaileysSessionState.Connected);

        var pickedNames = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            var picked = await service.SelectLoadBalancedInstanceAsync(_testTenant, null, ClusterLoadBalanceStrategy.RoundRobin);
            picked.Should().NotBeNull();
            pickedNames.Add(picked!.InstanceName);
        }

        // Each instance should appear at least once across 6 round-robin rounds
        pickedNames.Should().Contain("Line1");
        pickedNames.Should().Contain("Line2");
        pickedNames.Should().Contain("Line3");
    }

    [Fact]
    public async Task SelectLoadBalancedInstance_RoleFiltering_ReturnsOnlyRequestedRole()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("Sales1", "+21698000011", "SalesDesk"));
        await service.RegisterInstanceAsync(_testTenant, new("Support1", "+21698000012", "SupportDesk"));

        await service.TransitionStateAsync(_testTenant, "Sales1", BaileysSessionState.Connected);
        await service.TransitionStateAsync(_testTenant, "Support1", BaileysSessionState.Connected);

        var salesPicked = await service.SelectLoadBalancedInstanceAsync(_testTenant, "SalesDesk");
        var supportPicked = await service.SelectLoadBalancedInstanceAsync(_testTenant, "SupportDesk");

        salesPicked.Should().NotBeNull();
        salesPicked!.InstanceName.Should().Be("Sales1");

        supportPicked.Should().NotBeNull();
        supportPicked!.InstanceName.Should().Be("Support1");
    }

    [Fact]
    public async Task SelectLoadBalancedInstance_ReturnsNullWhenNoInstanceConnected()
    {
        var service = new WhatsAppSessionClusterService();
        await service.RegisterInstanceAsync(_testTenant, new("Line1", "+21698000001"));

        var picked = await service.SelectLoadBalancedInstanceAsync(_testTenant);

        picked.Should().BeNull();
    }

    [Fact]
    public async Task SelectLoadBalancedInstance_LeastLoaded_SelectsInstanceWithSmallestLoad()
    {
        var service = new WhatsAppSessionClusterService();
        var i1 = await service.RegisterInstanceAsync(_testTenant, new("Line1", "+21698000001"));
        var i2 = await service.RegisterInstanceAsync(_testTenant, new("Line2", "+21698000002"));

        await service.TransitionStateAsync(_testTenant, "Line1", BaileysSessionState.Connected);
        await service.TransitionStateAsync(_testTenant, "Line2", BaileysSessionState.Connected);

        i1.InFlightMessagesCount = 10;
        i1.TotalMessagesSent = 150;

        i2.InFlightMessagesCount = 1;
        i2.TotalMessagesSent = 20;

        var picked = await service.SelectLoadBalancedInstanceAsync(_testTenant, null, ClusterLoadBalanceStrategy.LeastLoaded);

        picked.Should().NotBeNull();
        picked!.InstanceName.Should().Be("Line2");
    }

    #endregion

    #region 2. Encrypted Session State & Health Watchdog Tests

    [Fact]
    public void EncryptAndDecrypt_RoundTrip_SuccessfullyRecoversSessionCredentials()
    {
        var service = new WhatsAppSessionClusterService();
        string sampleBaileysCreds = JsonSerializer.Serialize(new
        {
            noiseKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            signedIdentityKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            accountSettings = new { unarchiveChats = true }
        });

        string cipherText = service.EncryptSessionPayload(sampleBaileysCreds);

        cipherText.Should().NotBeNullOrWhiteSpace();
        cipherText.Should().NotBe(sampleBaileysCreds);

        string decrypted = service.DecryptSessionPayload(cipherText);

        decrypted.Should().Be(sampleBaileysCreds);
    }

    [Fact]
    public void Decrypt_TamperedPayload_ThrowsCryptographicException()
    {
        var service = new WhatsAppSessionClusterService();
        string plaintext = "sensitive-signal-protocol-keys-2026";
        string cipherText = service.EncryptSessionPayload(plaintext);

        // Tamper with base64 payload bytes
        byte[] bytes = Convert.FromBase64String(cipherText);
        bytes[^1] ^= 0xFF; // Flip last byte
        string tampered = Convert.ToBase64String(bytes);

        var act = () => service.DecryptSessionPayload(tampered);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public async Task RunHealthCheckWatchdog_MarksStaleHeartbeatAsReconnecting()
    {
        var service = new WhatsAppSessionClusterService();
        var inst = await service.RegisterInstanceAsync(_testTenant, new("WatchdogLine", "+21698999888"));
        await service.TransitionStateAsync(_testTenant, "WatchdogLine", BaileysSessionState.Connected);

        // Manually simulate stale heartbeat older than 90 seconds
        inst.LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-3);

        var report = await service.RunHealthCheckWatchdogAsync(_testTenant, TimeSpan.FromSeconds(90));

        report.TotalInstances.Should().Be(1);
        report.ReconnectingCount.Should().Be(1);
        report.ConnectedCount.Should().Be(0);

        var updated = await service.GetInstanceAsync(_testTenant, "WatchdogLine");
        updated!.State.Should().Be(BaileysSessionState.Reconnecting);
        updated.ReconnectAttempts.Should().Be(1);
    }

    [Fact]
    public async Task RunHealthCheckWatchdog_ExceedingMaxRetries_TransitionsToDisconnected()
    {
        var service = new WhatsAppSessionClusterService();
        var inst = await service.RegisterInstanceAsync(_testTenant, new("DeadSocket", "+21698999777"));
        await service.TransitionStateAsync(_testTenant, "DeadSocket", BaileysSessionState.Connected);

        inst.LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-10);
        inst.ReconnectAttempts = 4; // next iteration should reach 5 and disconnect

        var report = await service.RunHealthCheckWatchdogAsync(_testTenant, TimeSpan.FromSeconds(60));

        report.DisconnectedCount.Should().Be(1);
        var updated = await service.GetInstanceAsync(_testTenant, "DeadSocket");
        updated!.State.Should().Be(BaileysSessionState.Disconnected);
    }

    #endregion

    #region 3. Anti-Ban Poisson Jitter & Typing Simulation Tests

    [Fact]
    public void CalculatePoissonJitterDelay_ProducesValuesStrictlyWithinRange()
    {
        var service = new AntiBanMessagePacingService();

        for (int i = 0; i < 200; i++)
        {
            double delay = service.CalculatePoissonJitterDelay(lambda: 3.2, minRange: 1.8, maxRange: 5.4);
            delay.Should().BeInRange(1.8, 5.4, "delay must strictly adhere to specified minimum and maximum bounds");
        }
    }

    [Fact]
    public void CalculatePoissonJitterDelay_TargetMeanApproximatesExpectedDistribution()
    {
        var service = new AntiBanMessagePacingService();
        const int samples = 1000;
        double sum = 0;

        for (int i = 0; i < samples; i++)
        {
            sum += service.CalculatePoissonJitterDelay(lambda: 3.2, minRange: 1.8, maxRange: 5.4);
        }

        double mean = sum / samples;
        // Mean of truncated exponential on [1.8, 5.4] with lambda 3.2 should be comfortably around 3.0 - 3.4
        mean.Should().BeInRange(2.8, 3.6);
    }

    [Fact]
    public void CalculateTypingDurationMs_CalculatesAccurateDurationWithBounds()
    {
        var service = new AntiBanMessagePacingService();

        // Short message (5 chars)
        int shortDuration = service.CalculateTypingDurationMs(5, msPerChar: 25, minMs: 400, maxMs: 12000);
        shortDuration.Should().BeInRange(400, 700);

        // Long message (200 chars: 200 * 25 = 5000ms + base)
        int longDuration = service.CalculateTypingDurationMs(200, msPerChar: 25, minMs: 400, maxMs: 12000);
        longDuration.Should().BeInRange(4500, 6500);

        // Giant message clamped to max
        int giantDuration = service.CalculateTypingDurationMs(1000, msPerChar: 25, minMs: 400, maxMs: 12000);
        giantDuration.Should().Be(12000);
    }

    [Fact]
    public void SimulatePresenceSequence_ProducesComposingStateWithValidIntervals()
    {
        var service = new AntiBanMessagePacingService();
        string text = "Bonjour, votre commande #SG-9812 est en cours d'acheminement.";

        var presence = service.SimulatePresenceSequence(text);

        presence.Should().NotBeNull();
        presence.PresenceState.Should().Be("composing");
        presence.ReadReceiptDelayMs.Should().BeInRange(500, 2200);
        presence.TypingDurationMs.Should().BeGreaterThan(500);
        presence.PoissonPacingDelaySeconds.Should().BeInRange(1.8, 5.4);
        presence.TotalPreDispatchDelaySeconds.Should().BeGreaterThan(2.0);
    }

    #endregion

    #region 4. Progressive Daily Warmup Quota Tests

    [Fact]
    public void WarmupSchedule_VerifiesQuotasForDays1Through7()
    {
        var service = new AntiBanMessagePacingService();

        service.GetDailyQuotaForDay(1).Should().Be(50);
        service.GetDailyQuotaForDay(2).Should().Be(120);
        service.GetDailyQuotaForDay(3).Should().Be(200);
        service.GetDailyQuotaForDay(4).Should().Be(350);
        service.GetDailyQuotaForDay(5).Should().Be(500);
        service.GetDailyQuotaForDay(6).Should().Be(750);
        service.GetDailyQuotaForDay(7).Should().Be(1000);
    }

    [Fact]
    public void CheckWarmupThrottle_PermitsMessageWhenWithinDailyQuota()
    {
        var service = new AntiBanMessagePacingService();
        string phone = "+21698765432";

        var decision = service.CheckWarmupThrottle(phone, dayOfWarmup: 1, messageCount: 1);

        decision.IsAllowed.Should().BeTrue();
        decision.CurrentDay.Should().Be(1);
        decision.DailyLimit.Should().Be(50);
        decision.TodaySentCount.Should().Be(0);
        decision.RemainingQuota.Should().Be(50);
        decision.ThrottleReason.Should().BeNull();
    }

    [Fact]
    public void CheckWarmupThrottle_DeniesMessageWhenExceedingDailyLimit()
    {
        var service = new AntiBanMessagePacingService();
        string phone = "+21698765433";

        // Day 1 limit is 50. Simulate sending 50 messages.
        for (int i = 0; i < 50; i++)
        {
            service.RecordMessageSent(phone, dayOfWarmup: 1, 1).Should().BeTrue();
        }

        // 51st message should be throttled
        var decision = service.CheckWarmupThrottle(phone, dayOfWarmup: 1, 1);

        decision.IsAllowed.Should().BeFalse();
        decision.TodaySentCount.Should().Be(50);
        decision.RemainingQuota.Should().Be(0);
        decision.Status.Should().Be("DailyLimitExceeded");
        decision.ThrottleReason.Should().Contain("Day 1: 50/50 sent");
    }

    [Fact]
    public void RecordMessageSent_DeductsRemainingQuotaUntilExhausted()
    {
        var service = new AntiBanMessagePacingService();
        string phone = "+21698765434";

        // Day 2 limit is 120. Send batch of 100.
        bool recorded = service.RecordMessageSent(phone, dayOfWarmup: 2, count: 100);
        recorded.Should().BeTrue();

        var check = service.CheckWarmupThrottle(phone, dayOfWarmup: 2, 1);
        check.IsAllowed.Should().BeTrue();
        check.RemainingQuota.Should().Be(20);

        // Attempting to send 25 more exceeds 120
        bool overLimit = service.RecordMessageSent(phone, dayOfWarmup: 2, count: 25);
        overLimit.Should().BeFalse();
    }

    #endregion

    #region 5. WhatsApp Flows & Catalog Schema Generation & Validation

    [Fact]
    public void GenerateAddressCollectionFlowSchema_ProducesValidMetaFlowV3Json()
    {
        var service = new WhatsAppFlowsAndCatalogService();

        string json = service.GenerateAddressCollectionFlowSchema();

        json.Should().NotBeNullOrWhiteSpace();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("version").GetString().Should().Be("3.0");
        var screens = root.GetProperty("screens");
        screens.GetArrayLength().Should().Be(1);

        var firstScreen = screens[0];
        firstScreen.GetProperty("id").GetString().Should().Be("ADDRESS_COLLECTION_SCREEN");
        var children = firstScreen.GetProperty("layout").GetProperty("children");

        // Verify required address fields exist in layout
        var fieldNames = children.EnumerateArray()
            .Where(e => e.TryGetProperty("name", out _))
            .Select(e => e.GetProperty("name").GetString())
            .ToList();

        fieldNames.Should().Contain("full_name");
        fieldNames.Should().Contain("phone_number");
        fieldNames.Should().Contain("street_address");
        fieldNames.Should().Contain("governorate");
        fieldNames.Should().Contain("city");
    }

    [Fact]
    public void GenerateAppointmentBookingFlowSchema_ProducesValidServicesAndSlots()
    {
        var service = new WhatsAppFlowsAndCatalogService();

        string json = service.GenerateAppointmentBookingFlowSchema();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("screens")[0].GetProperty("id").GetString().Should().Be("APPOINTMENT_BOOKING_SCREEN");

        var children = root.GetProperty("screens")[0].GetProperty("layout").GetProperty("children");
        var datePicker = children.EnumerateArray().FirstOrDefault(c => c.GetProperty("type").GetString() == "DatePicker");
        datePicker.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        datePicker.GetProperty("name").GetString().Should().Be("appointment_date");
    }

    [Fact]
    public void GenerateProductVariantSelectionFlowSchema_ProducesValidVariantsAndSizes()
    {
        var service = new WhatsAppFlowsAndCatalogService();

        string json = service.GenerateProductVariantSelectionFlowSchema();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("screens")[0].GetProperty("id").GetString().Should().Be("PRODUCT_VARIANT_SCREEN");

        var children = root.GetProperty("screens")[0].GetProperty("layout").GetProperty("children");
        var sizeRadio = children.EnumerateArray().FirstOrDefault(c => c.TryGetProperty("name", out var n) && n.GetString() == "selected_size");
        sizeRadio.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void ValidateFlowResponsePayload_Address_SucceedsForValidData()
    {
        var service = new WhatsAppFlowsAndCatalogService();
        var validPayload = JsonSerializer.Serialize(new
        {
            full_name = "Mohamed Ben Ali",
            phone_number = "+21698123456",
            street_address = "Avenue Habib Bourguiba, Appt 4B",
            governorate = "tunis",
            city = "Tunis Centre"
        });

        var result = service.ValidateFlowResponsePayload("address", validPayload);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.ExtractedData["full_name"].Should().Be("Mohamed Ben Ali");
        result.ExtractedData["governorate"].Should().Be("tunis");
    }

    [Fact]
    public void ValidateFlowResponsePayload_Address_FailsWhenMissingRequiredFields()
    {
        var service = new WhatsAppFlowsAndCatalogService();
        var incomplete = JsonSerializer.Serialize(new
        {
            full_name = "Kamel",
            // Missing phone_number, street_address, governorate, city
        });

        var result = service.ValidateFlowResponsePayload("address", incomplete);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("phone_number"));
        result.Errors.Should().Contain(e => e.Contains("street_address"));
        result.Errors.Should().Contain(e => e.Contains("governorate"));
        result.Errors.Should().Contain(e => e.Contains("city"));
    }

    [Fact]
    public void ValidateFlowResponsePayload_Appointment_FailsForPastDates()
    {
        var service = new WhatsAppFlowsAndCatalogService();
        var payload = JsonSerializer.Serialize(new
        {
            customer_name = "Sonia",
            customer_phone = "+21698222111",
            service_type = "vip_demo",
            appointment_date = "2020-01-01", // In the past!
            time_slot = "morning_1"
        });

        var result = service.ValidateFlowResponsePayload("appointment", payload);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("past"));
    }

    [Fact]
    public void ValidateFlowResponsePayload_Variant_ValidatesQuantityBounds()
    {
        var service = new WhatsAppFlowsAndCatalogService();
        var payload = JsonSerializer.Serialize(new
        {
            product_id = "SKU-SHIRT-01",
            selected_size = "L",
            selected_color = "black",
            quantity = 0 // Invalid quantity (< 1)
        });

        var result = service.ValidateFlowResponsePayload("variant", payload);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("quantity must be between 1 and 100"));
    }

    #endregion

    #region 6. Interactive Lists, Buttons & Catalog Rendering

    [Fact]
    public void RenderInteractiveListMenu_ProducesCompliantJsonPayload()
    {
        var service = new WhatsAppFlowsAndCatalogService();
        var req = new InteractiveListMenuRequest(
            RecipientPhone: "+21698123456",
            Title: "Order Support",
            Body: "How can our concierge desk assist you today?",
            ButtonText: "Choose Action",
            Sections:
            [
                new("Order Inquiries", [
                    new("track_order", "Track Delivery Status", "Real-time courier geolocation"),
                    new("request_return", "Request Return / Exchange", "Immediate replacement within 48h")
                ]),
                new("Account & VIP", [
                    new("speak_agent", "Speak to Live Agent", "Connect to human concierge")
                ])
            ],
            Header: "SGrowing Concierge",
            Footer: "Reply 'STOP' to unsubscribe"
        );

        string json = service.RenderInteractiveListMenu(req);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        root.GetProperty("type").GetString().Should().Be("interactive");

        var interactive = root.GetProperty("interactive");
        interactive.GetProperty("type").GetString().Should().Be("list");
        interactive.GetProperty("action").GetProperty("button").GetString().Should().Be("Choose Action");
        interactive.GetProperty("action").GetProperty("sections").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void RenderDynamicQuickReplies_CapsAtThreeButtons()
    {
        var service = new WhatsAppFlowsAndCatalogService();
        var req = new QuickReplyButtonsRequest(
            RecipientPhone: "+21698123456",
            Body: "Confirm delivery appointment for tomorrow at 10:00?",
            Buttons:
            [
                new("btn_yes", "Yes, Confirm"),
                new("btn_reschedule", "Reschedule"),
                new("btn_cancel", "Cancel Order"),
                new("btn_extra", "Extra Button Beyond WhatsApp Limit")
            ]
        );

        string json = service.RenderDynamicQuickReplies(req);

        using var doc = JsonDocument.Parse(json);
        var buttons = doc.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("buttons");
        buttons.GetArrayLength().Should().Be(3); // Meta WhatsApp limit is 3 quick reply buttons
    }

    [Fact]
    public void RenderNativeCatalogCart_CalculatesTotalAmountAndSections()
    {
        var service = new WhatsAppFlowsAndCatalogService();
        var req = new CatalogCartRenderRequest(
            RecipientPhone: "+21698123456",
            CatalogId: "sgrowing_catalog_tn",
            HeaderText: "Best-Selling Leather Goods",
            BodyText: "Order directly in WhatsApp with cash on delivery.",
            Sections:
            [
                new("Wallets & Belts", [
                    new("SKU-W-01", "Full-grain Italian Wallet", 85.0m, "TND", Quantity: 2),
                    new("SKU-B-02", "Reversible Classic Belt", 60.0m, "TND", Quantity: 1)
                ])
            ]
        );

        string json = service.RenderNativeCatalogCart(req);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("cart_metadata").GetProperty("estimated_total").GetDecimal().Should().Be(230.0m); // (85*2) + (60*1) = 230
        root.GetProperty("cart_metadata").GetProperty("currency").GetString().Should().Be("TND");
        root.GetProperty("cart_metadata").GetProperty("item_count").GetInt32().Should().Be(2);
    }

    #endregion

    #region 7. Cascading Omnichannel Failover Ladder Tests

    [Fact]
    public async Task SendPriorityTransactionalMessage_WhenWhatsAppConnected_DeliversViaWhatsApp()
    {
        var clusterService = new WhatsAppSessionClusterService();
        await clusterService.RegisterInstanceAsync(_testTenant, new("PrimaryLine", "+21698111000", "SupportDesk"));
        await clusterService.TransitionStateAsync(_testTenant, "PrimaryLine", BaileysSessionState.Connected);

        var smsMock = new Mock<IAndroidSmsGatewayService>();
        var emailMock = new Mock<ISmtpEmailSender>();

        var failover = new OmnichannelFailoverService(clusterService, smsMock.Object, emailMock.Object);

        var request = new PriorityMessageRequest(
            _testTenant,
            RecipientPhone: "+21698123456",
            RecipientEmail: "customer@example.com",
            Subject: "Your OTP Code",
            Body: "Your SGrowing security verification code is 492018.",
            MessageType: PriorityMessageType.Otp
        );

        var result = await failover.SendPriorityTransactionalMessageAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulChannel.Should().Be(FailoverChannel.WhatsApp);
        result.Hops.Should().ContainSingle();
        result.Hops[0].Channel.Should().Be(FailoverChannel.WhatsApp);
        result.Hops[0].Succeeded.Should().BeTrue();

        // SMS and Email should not be contacted when WhatsApp succeeds
        smsMock.Verify(s => s.DispatchSmsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
        emailMock.Verify(e => e.SendTransactionalEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendPriorityTransactionalMessage_WhenWhatsAppTimesOut_CascadesToAndroidSms()
    {
        var clusterService = new WhatsAppSessionClusterService();
        await clusterService.RegisterInstanceAsync(_testTenant, new("PrimaryLine", "+21698111000", "SupportDesk"));
        await clusterService.TransitionStateAsync(_testTenant, "PrimaryLine", BaileysSessionState.Connected);

        var smsMock = new Mock<IAndroidSmsGatewayService>();
        smsMock.Setup(s => s.DispatchSmsAsync(It.IsAny<Guid>(), "+21698123456", It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(new AndroidSmsDispatchResultDto(
                IsSuccess: true,
                DispatchId: Guid.NewGuid(),
                DeviceId: Guid.NewGuid(),
                DeviceName: "Samsung Galaxy S22 Gateway",
                SimSlot: 1,
                RecipientPhone: "+21698123456",
                Status: "Dispatched",
                Message: "SMS dispatched successfully via hardware SIM 1"));

        var emailMock = new Mock<ISmtpEmailSender>();

        var failover = new OmnichannelFailoverService(clusterService, smsMock.Object, emailMock.Object);

        // Inject simulated WhatsApp failure / ack timeout
        failover.WhatsAppDeliveryProbe = (_, _, _) => Task.FromResult(false);

        var request = new PriorityMessageRequest(
            _testTenant,
            RecipientPhone: "+21698123456",
            RecipientEmail: "customer@example.com",
            Subject: "Urgent Order Notification",
            Body: "Delivery attempted but customer unreachable.",
            MessageType: PriorityMessageType.OrderConfirmation,
            WhatsAppTimeout: TimeSpan.FromSeconds(60)
        );

        var result = await failover.SendPriorityTransactionalMessageAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulChannel.Should().Be(FailoverChannel.AndroidSms);
        result.Hops.Should().HaveCount(2);

        // Hop 1: WhatsApp failed/timed out
        result.Hops[0].Channel.Should().Be(FailoverChannel.WhatsApp);
        result.Hops[0].Succeeded.Should().BeFalse();

        // Hop 2: SMS succeeded
        result.Hops[1].Channel.Should().Be(FailoverChannel.AndroidSms);
        result.Hops[1].Succeeded.Should().BeTrue();

        smsMock.Verify(s => s.DispatchSmsAsync(It.IsAny<Guid>(), "+21698123456", It.IsAny<string>(), It.IsAny<int?>()), Times.Once);
        emailMock.Verify(e => e.SendTransactionalEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendPriorityTransactionalMessage_WhenWhatsAppAndSmsFail_CascadesToEmail()
    {
        var clusterService = new WhatsAppSessionClusterService();
        // No connected WhatsApp instances registered

        var smsMock = new Mock<IAndroidSmsGatewayService>();
        smsMock.Setup(s => s.DispatchSmsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(new AndroidSmsDispatchResultDto(
                IsSuccess: false,
                DispatchId: null,
                DeviceId: null,
                DeviceName: null,
                SimSlot: 1,
                RecipientPhone: "+21698123456",
                Status: "Failed",
                Message: "All Android handsets are offline or battery dead"));

        var emailMock = new Mock<ISmtpEmailSender>();
        emailMock.Setup(e => e.SendTransactionalEmailAsync(
                It.IsAny<Guid>(),
                "customer@example.com",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailDispatchResult(
                IsSuccess: true,
                MessageId: "smtp_msg_1009",
                Status: "Sent"));

        var failover = new OmnichannelFailoverService(clusterService, smsMock.Object, emailMock.Object);

        var request = new PriorityMessageRequest(
            _testTenant,
            RecipientPhone: "+21698123456",
            RecipientEmail: "customer@example.com",
            Subject: "Critical Invoice Alert",
            Body: "Invoice #INV-2026-09 is overdue. Please settle balance.",
            MessageType: PriorityMessageType.InvoiceAlert
        );

        var result = await failover.SendPriorityTransactionalMessageAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.SuccessfulChannel.Should().Be(FailoverChannel.TransactionalEmail);
        result.Hops.Should().HaveCount(3);

        result.Hops[0].Channel.Should().Be(FailoverChannel.WhatsApp);
        result.Hops[0].Succeeded.Should().BeFalse();

        result.Hops[1].Channel.Should().Be(FailoverChannel.AndroidSms);
        result.Hops[1].Succeeded.Should().BeFalse();

        result.Hops[2].Channel.Should().Be(FailoverChannel.TransactionalEmail);
        result.Hops[2].Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task SendPriorityTransactionalMessage_WhenAllFail_ReturnsFailureResultWithHopTelemetry()
    {
        var clusterService = new WhatsAppSessionClusterService();
        // No connected WhatsApp instances

        var smsMock = new Mock<IAndroidSmsGatewayService>();
        smsMock.Setup(s => s.DispatchSmsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(new AndroidSmsDispatchResultDto(
                IsSuccess: false, null, null, null, 1, "+21698123456", "Failed", "No SIM available"));

        var emailMock = new Mock<ISmtpEmailSender>();
        emailMock.Setup(e => e.SendTransactionalEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailDispatchResult(false, null, "Failed", "SMTP server refused connection"));

        var failover = new OmnichannelFailoverService(clusterService, smsMock.Object, emailMock.Object);

        var request = new PriorityMessageRequest(
            _testTenant,
            RecipientPhone: "+21698123456",
            RecipientEmail: "customer@example.com",
            Subject: "Security Alert",
            Body: "Suspicious login attempt detected.",
            MessageType: PriorityMessageType.SecurityAlert
        );

        var result = await failover.SendPriorityTransactionalMessageAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.SuccessfulChannel.Should().BeNull();
        result.Hops.Should().HaveCount(3);
        result.Hops.All(h => !h.Succeeded).Should().BeTrue();
        result.ErrorMessage.Should().Contain("All omnichannel failover channels failed");
    }

    #endregion
}
