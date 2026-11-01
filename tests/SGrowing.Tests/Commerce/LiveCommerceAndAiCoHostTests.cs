using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.LiveStreaming;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class LiveCommerceAndAiCoHostTests
{
    private readonly LiveCommerceStreamingService _streamingService = new();
    private readonly LiveVideoAiCoHostService _aiCoHostService = new();

    #region 1. Live Stream Session Lifecycle & Streaming Protocol Tests

    [Fact]
    public async Task CreateSession_ShouldReturnValidSessionWithScheduledStatusAndGeneratedKeys()
    {
        var session = await _streamingService.CreateSessionAsync(
            hostId: "host_sarah_01",
            title: "Summer Flash Fashion Drop",
            description: "Exclusive 50% discount on artisan leather bags and summer wear");

        session.Should().NotBeNull();
        session.SessionId.Should().StartWith("live_");
        session.StreamKey.Should().StartWith("sk_");
        session.HostId.Should().Be("host_sarah_01");
        session.Title.Should().Be("Summer Flash Fashion Drop");
        session.Status.Should().Be(LiveStreamStatus.Scheduled);
        session.StartedAt.Should().BeNull();
        session.EndedAt.Should().BeNull();
        session.CurrentViewers.Should().Be(0);
        session.PeakViewers.Should().Be(0);
        session.TotalSalesAmount.Should().Be(0m);
        session.PinnedProduct.Should().BeNull();
    }

    [Fact]
    public async Task CreateSession_ShouldGenerateCorrectWhipIngestUrl()
    {
        var session = await _streamingService.CreateSessionAsync("host_02", "Ingest Test", "WHIP test");

        session.StreamingProtocol.Should().NotBeNull();
        session.StreamingProtocol.IngestUrl.Should().Be($"https://stream.sgrowing.com/whip/{session.SessionId}");
        session.StreamingProtocol.ProtocolType.Should().Be(StreamingProtocolType.WebRTC_WHIP);
    }

    [Fact]
    public async Task CreateSession_ShouldGenerateCorrectWhepPlaybackUrl()
    {
        var session = await _streamingService.CreateSessionAsync("host_03", "Playback Test", "WHEP test");

        session.StreamingProtocol.PlaybackUrl.Should().Be($"https://stream.sgrowing.com/whep/{session.SessionId}");
    }

    [Fact]
    public async Task CreateSession_ShouldGenerateValidHlsFallbackUrl()
    {
        var session = await _streamingService.CreateSessionAsync("host_04", "HLS Test", "HLS adaptive fallback");

        session.StreamingProtocol.HlsFallbackUrl.Should().Be($"https://stream.sgrowing.com/hls/{session.SessionId}/master.m3u8");
    }

    [Fact]
    public async Task StartSession_ShouldTransitionStatusToLiveAndSetStartedAt()
    {
        var session = await _streamingService.CreateSessionAsync("host_05", "Launch Stream", "Going live");

        var startedSession = await _streamingService.StartSessionAsync(session.SessionId);

        startedSession.Status.Should().Be(LiveStreamStatus.Live);
        startedSession.StartedAt.Should().NotBeNull();
        startedSession.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartSession_WithNonExistentSession_ShouldThrowKeyNotFoundException()
    {
        var act = () => _streamingService.StartSessionAsync("non_existent_id");

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    public async Task StartSession_WhenAlreadyEnded_ShouldThrowInvalidOperationException()
    {
        var session = await _streamingService.CreateSessionAsync("host_06", "Lifecycle Test", "End then start");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.EndSessionAsync(session.SessionId);

        var act = () => _streamingService.StartSessionAsync(session.SessionId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already ended*");
    }

    [Fact]
    public async Task PauseSession_WhenLive_ShouldTransitionStatusToPaused()
    {
        var session = await _streamingService.CreateSessionAsync("host_07", "Pause Test", "Pausing stream");
        await _streamingService.StartSessionAsync(session.SessionId);

        var paused = await _streamingService.PauseSessionAsync(session.SessionId);

        paused.Status.Should().Be(LiveStreamStatus.Paused);
    }

    [Fact]
    public async Task PauseSession_WhenNotLive_ShouldThrowInvalidOperationException()
    {
        var session = await _streamingService.CreateSessionAsync("host_08", "Pause Scheduled", "Not live yet");

        var act = () => _streamingService.PauseSessionAsync(session.SessionId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not Live*");
    }

    [Fact]
    public async Task EndSession_ShouldSetEndedAtAndTransitionStatusToEnded()
    {
        var session = await _streamingService.CreateSessionAsync("host_09", "End Stream Test", "Broadcast ending");
        await _streamingService.StartSessionAsync(session.SessionId);

        var endedSession = await _streamingService.EndSessionAsync(session.SessionId);

        endedSession.Status.Should().Be(LiveStreamStatus.Ended);
        endedSession.EndedAt.Should().NotBeNull();
        endedSession.EndedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task EndSession_ShouldDeactivateActivePinnedProduct()
    {
        var session = await _streamingService.CreateSessionAsync("host_10", "End With Product", "Testing deactivation");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.PinProductAsync(
            session.SessionId, "prod_bag_1", "BAG-LTHR-01", "Leather Tote Bag",
            originalPrice: 12000m, flashPrice: 6900m, stockLimit: 20, duration: TimeSpan.FromMinutes(10));

        var ended = await _streamingService.EndSessionAsync(session.SessionId);

        ended.PinnedProduct.Should().NotBeNull();
        ended.PinnedProduct!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateViewerCount_ShouldTrackPeakViewersAccurately()
    {
        var session = await _streamingService.CreateSessionAsync("host_11", "Viewer Metrics", "Audience spike");
        await _streamingService.StartSessionAsync(session.SessionId);

        await _streamingService.UpdateViewerCountAsync(session.SessionId, 150);
        session.CurrentViewers.Should().Be(150);
        session.PeakViewers.Should().Be(150);

        await _streamingService.UpdateViewerCountAsync(session.SessionId, 420);
        session.CurrentViewers.Should().Be(420);
        session.PeakViewers.Should().Be(420);

        // Viewers drop to 300, peak viewers should remain at 420
        await _streamingService.UpdateViewerCountAsync(session.SessionId, 300);
        session.CurrentViewers.Should().Be(300);
        session.PeakViewers.Should().Be(420);
    }

    #endregion

    #region 2. Flash Product Pinning Tests

    [Fact]
    public async Task PinProduct_WithValidData_ShouldSetActivePinnedProduct()
    {
        var session = await _streamingService.CreateSessionAsync("host_20", "Pin Test", "Valid pin");
        await _streamingService.StartSessionAsync(session.SessionId);

        var pinned = await _streamingService.PinProductAsync(
            session.SessionId,
            productId: "prod_jacket_99",
            sku: "JKT-LTHR-BRN",
            title: "Handcrafted Biker Jacket",
            originalPrice: 25000m,
            flashPrice: 14900m,
            stockLimit: 15,
            duration: TimeSpan.FromMinutes(15));

        pinned.Should().NotBeNull();
        pinned.ProductId.Should().Be("prod_jacket_99");
        pinned.Sku.Should().Be("JKT-LTHR-BRN");
        pinned.OriginalPrice.Should().Be(25000m);
        pinned.FlashSalePrice.Should().Be(14900m);
        pinned.AvailableStock.Should().Be(15);
        pinned.ReservedStock.Should().Be(0);
        pinned.RemainingStock.Should().Be(15);
        pinned.IsActive.Should().BeTrue();
        pinned.IsExpired.Should().BeFalse();
        session.PinnedProduct.Should().Be(pinned);
    }

    [Fact]
    public async Task PinProduct_WhenFlashPriceIsGreaterOrEqualToOriginalPrice_ShouldThrowArgumentException()
    {
        var session = await _streamingService.CreateSessionAsync("host_21", "Invalid Price", "Price error");
        await _streamingService.StartSessionAsync(session.SessionId);

        // Flash price equal to original price
        var actEqual = () => _streamingService.PinProductAsync(
            session.SessionId, "p1", "SKU1", "T-Shirt",
            originalPrice: 5000m, flashPrice: 5000m, stockLimit: 10, duration: TimeSpan.FromMinutes(5));

        await actEqual.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*strictly lower than original*");

        // Flash price higher than original price
        var actHigher = () => _streamingService.PinProductAsync(
            session.SessionId, "p2", "SKU2", "T-Shirt",
            originalPrice: 5000m, flashPrice: 6000m, stockLimit: 10, duration: TimeSpan.FromMinutes(5));

        await actHigher.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*strictly lower than original*");
    }

    [Fact]
    public async Task PinProduct_WhenStockLimitIsZeroOrNegative_ShouldThrowArgumentException()
    {
        var session = await _streamingService.CreateSessionAsync("host_22", "Invalid Stock", "Stock error");
        await _streamingService.StartSessionAsync(session.SessionId);

        var act = () => _streamingService.PinProductAsync(
            session.SessionId, "p3", "SKU3", "Sneakers",
            originalPrice: 8000m, flashPrice: 4000m, stockLimit: 0, duration: TimeSpan.FromMinutes(5));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Stock limit must be greater than zero*");
    }

    [Fact]
    public async Task PinProduct_WhenDurationIsZeroOrNegative_ShouldThrowArgumentException()
    {
        var session = await _streamingService.CreateSessionAsync("host_23", "Invalid Duration", "Duration error");
        await _streamingService.StartSessionAsync(session.SessionId);

        var act = () => _streamingService.PinProductAsync(
            session.SessionId, "p4", "SKU4", "Watch",
            originalPrice: 15000m, flashPrice: 9900m, stockLimit: 5, duration: TimeSpan.Zero);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Flash deal duration must be a positive duration*");
    }

    [Fact]
    public async Task PinProduct_ShouldCalculateCorrectDiscountPercentage()
    {
        var session = await _streamingService.CreateSessionAsync("host_24", "Discount Calc", "Calculating %");
        await _streamingService.StartSessionAsync(session.SessionId);

        // 20000 -> 14000 = 30% discount
        var pinned = await _streamingService.PinProductAsync(
            session.SessionId, "p5", "SKU5", "Wool Coat",
            originalPrice: 20000m, flashPrice: 14000m, stockLimit: 10, duration: TimeSpan.FromMinutes(10));

        pinned.DiscountPercentage.Should().Be(30.00m);
    }

    [Fact]
    public async Task PinProduct_ToEndedSession_ShouldThrowInvalidOperationException()
    {
        var session = await _streamingService.CreateSessionAsync("host_25", "Ended Session Pin", "Test");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.EndSessionAsync(session.SessionId);

        var act = () => _streamingService.PinProductAsync(
            session.SessionId, "p6", "SKU6", "Boots",
            originalPrice: 10000m, flashPrice: 6000m, stockLimit: 10, duration: TimeSpan.FromMinutes(5));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ended live stream session*");
    }

    [Fact]
    public async Task UnpinProduct_WhenProductPinned_ShouldDeactivateAndReturnTrue()
    {
        var session = await _streamingService.CreateSessionAsync("host_26", "Unpin Test", "Deactivation");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.PinProductAsync(
            session.SessionId, "p7", "SKU7", "Scarf",
            originalPrice: 4000m, flashPrice: 2500m, stockLimit: 20, duration: TimeSpan.FromMinutes(10));

        var unpinned = await _streamingService.UnpinProductAsync(session.SessionId);

        unpinned.Should().BeTrue();
        session.PinnedProduct.Should().NotBeNull();
        session.PinnedProduct!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UnpinProduct_WhenNoProductPinned_ShouldReturnFalse()
    {
        var session = await _streamingService.CreateSessionAsync("host_27", "No Pin Unpin", "Nothing pinned");
        await _streamingService.StartSessionAsync(session.SessionId);

        var unpinned = await _streamingService.UnpinProductAsync(session.SessionId);

        unpinned.Should().BeFalse();
    }

    #endregion

    #region 3. Flash Stock Reservation & Inventory Locking Tests

    [Fact]
    public async Task ReserveFlashStock_WithAvailableStock_ShouldSucceedAndIncrementSalesMetrics()
    {
        var session = await _streamingService.CreateSessionAsync("host_30", "Reservation Test", "Flash buy");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.PinProductAsync(
            session.SessionId, "prod_phone_1", "PHONE-01", "Smartphone 5G",
            originalPrice: 50000m, flashPrice: 35000m, stockLimit: 10, duration: TimeSpan.FromMinutes(15));

        var reservation = await _streamingService.ReserveFlashStockAsync(
            session.SessionId,
            productId: "prod_phone_1",
            customerId: "cust_kareem_99",
            quantity: 2);

        reservation.Success.Should().BeTrue();
        reservation.ReservationId.Should().StartWith("res_");
        reservation.Quantity.Should().Be(2);
        reservation.UnitPrice.Should().Be(35000m);
        reservation.TotalAmount.Should().Be(70000m);
        reservation.RemainingStock.Should().Be(8);
        reservation.Message.Should().Contain("locked successfully");

        session.TotalSalesAmount.Should().Be(70000m);
        session.PinnedProduct!.ReservedStock.Should().Be(2);
        session.PinnedProduct.RemainingStock.Should().Be(8);
    }

    [Fact]
    public async Task ReserveFlashStock_WhenRequestedQuantityExceedsAvailableStock_ShouldFailWithRemainingStockReported()
    {
        var session = await _streamingService.CreateSessionAsync("host_31", "Low Stock", "Scarcity test");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.PinProductAsync(
            session.SessionId, "prod_shoes_3", "SHOE-03", "Running Shoes",
            originalPrice: 12000m, flashPrice: 7500m, stockLimit: 3, duration: TimeSpan.FromMinutes(10));

        var reservation = await _streamingService.ReserveFlashStockAsync(
            session.SessionId,
            productId: "prod_shoes_3",
            customerId: "cust_buyer_02",
            quantity: 5);

        reservation.Success.Should().BeFalse();
        reservation.ReservationId.Should().BeNull();
        reservation.RemainingStock.Should().Be(3);
        reservation.Message.Should().Contain("Insufficient flash inventory. Only 3 units remaining.");
        session.TotalSalesAmount.Should().Be(0m);
    }

    [Fact]
    public async Task ReserveFlashStock_WhenFlashSaleHasExpired_ShouldFailAndDeactivateProduct()
    {
        var session = await _streamingService.CreateSessionAsync("host_32", "Expiry Test", "Expired flash");
        await _streamingService.StartSessionAsync(session.SessionId);

        // Pin with duration already expired in the past
        var now = DateTime.UtcNow;
        session.PinnedProduct = new PinnedProductRecord(
            ProductId: "prod_exp_1",
            Sku: "EXP-01",
            Title: "Expired Deal Item",
            OriginalPrice: 1000m,
            FlashSalePrice: 500m,
            AvailableStock: 50,
            ReservedStock: 0,
            PinnedAt: now.AddMinutes(-30),
            ExpirationTime: now.AddMinutes(-5),
            IsActive: true);

        var reservation = await _streamingService.ReserveFlashStockAsync(
            session.SessionId,
            productId: "prod_exp_1",
            customerId: "cust_late_comer",
            quantity: 1);

        reservation.Success.Should().BeFalse();
        reservation.Message.Should().Contain("countdown has expired");
        session.PinnedProduct.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ReserveFlashStock_WhenNoProductPinned_ShouldReturnFailure()
    {
        var session = await _streamingService.CreateSessionAsync("host_33", "No Pin Reserve", "No item");
        await _streamingService.StartSessionAsync(session.SessionId);

        var reservation = await _streamingService.ReserveFlashStockAsync(
            session.SessionId,
            productId: "prod_none",
            customerId: "cust_03",
            quantity: 1);

        reservation.Success.Should().BeFalse();
        reservation.Message.Should().Contain("No active flash product");
    }

    [Fact]
    public async Task ReserveFlashStock_WhenProductIdDoesNotMatchPinnedProduct_ShouldReturnFailure()
    {
        var session = await _streamingService.CreateSessionAsync("host_34", "Mismatch Product", "Mismatch");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.PinProductAsync(
            session.SessionId, "prod_correct", "CORRECT-01", "Correct Product",
            originalPrice: 8000m, flashPrice: 5000m, stockLimit: 10, duration: TimeSpan.FromMinutes(10));

        var reservation = await _streamingService.ReserveFlashStockAsync(
            session.SessionId,
            productId: "prod_wrong_id",
            customerId: "cust_04",
            quantity: 1);

        reservation.Success.Should().BeFalse();
        reservation.Message.Should().Contain("does not match the active pinned flash deal");
    }

    [Fact]
    public async Task ReserveFlashStock_MultipleReservations_ShouldAccuratelyDeductStockAndAccumulateSalesAmount()
    {
        var session = await _streamingService.CreateSessionAsync("host_35", "Multi-Reservation", "Accumulation");
        await _streamingService.StartSessionAsync(session.SessionId);
        await _streamingService.PinProductAsync(
            session.SessionId, "prod_smartwatch", "SMART-WATCH-X", "Smart Watch X Pro",
            originalPrice: 18000m, flashPrice: 10000m, stockLimit: 5, duration: TimeSpan.FromMinutes(15));

        var res1 = await _streamingService.ReserveFlashStockAsync(session.SessionId, "prod_smartwatch", "cust_1", 2);
        var res2 = await _streamingService.ReserveFlashStockAsync(session.SessionId, "prod_smartwatch", "cust_2", 2);
        var res3 = await _streamingService.ReserveFlashStockAsync(session.SessionId, "prod_smartwatch", "cust_3", 1);
        var res4 = await _streamingService.ReserveFlashStockAsync(session.SessionId, "prod_smartwatch", "cust_4", 1);

        res1.Success.Should().BeTrue();
        res2.Success.Should().BeTrue();
        res3.Success.Should().BeTrue();
        res4.Success.Should().BeFalse(); // Stock depleted

        session.PinnedProduct!.ReservedStock.Should().Be(5);
        session.PinnedProduct.RemainingStock.Should().Be(0);
        session.TotalSalesAmount.Should().Be(50000m); // 5 * 10,000 DZD
    }

    #endregion

    #region 4. Real-Time Chat Moderation Tests

    [Fact]
    public async Task ProcessIncomingMessage_WithCleanMessage_ShouldBeApproved()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_mod_01",
            userId: "user_amina",
            username: "AminaDZ",
            text: "Hello everyone! Beautiful products on today's live!");

        result.Message.ModerationStatus.Should().Be(ModerationStatus.Approved);
        result.Message.ModerationReason.Should().BeNull();
        result.AutoReplyText.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithToxicKeywords_ShouldFlagAsToxicAndSuppressAutoReply()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_mod_02",
            userId: "user_troll",
            username: "Troll99",
            text: "This is a total scam and you are all a fraud!");

        result.Message.ModerationStatus.Should().Be(ModerationStatus.FlaggedToxic);
        result.Message.ModerationReason.Should().Contain("Abusive or toxic content detected");
        result.AutoReplyText.Should().BeNull();
        result.GeneratedCue.Should().BeNull();
        result.Message.SentimentScore.Should().BeLessThan(0.0);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithRapidIdenticalDuplicate_ShouldBlockAsSpam()
    {
        var sessionId = "live_mod_03";
        var userId = "spammer_01";
        var text = "Can you show the black leather jacket again please?";

        // First message
        await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, userId, "CoinGuy", text);

        // Immediate duplicate
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, userId, "CoinGuy", text);

        result.Message.ModerationStatus.Should().Be(ModerationStatus.SpamBlocked);
        result.Message.ModerationReason.Should().Contain("Duplicate message flood detected");
        result.Message.DetectedIntent.Should().Be(DetectedIntent.Spam);
        result.AutoReplyText.Should().BeNull();
        result.GeneratedCue.Should().BeNull();
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithCharacterRepetitionFlood_ShouldBlockAsSpam()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_mod_04",
            userId: "user_spammer_chars",
            username: "EchoUser",
            text: "Wooooooooooowwwwwww so cool");

        result.Message.ModerationStatus.Should().Be(ModerationStatus.SpamBlocked);
        result.Message.ModerationReason.Should().Contain("Repetitive character spam");
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithExternalLink_ShouldBlockAsSpam()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_mod_05",
            userId: "user_link_bot",
            username: "PromoBot",
            text: "Check out better discounts at https://external-store.com/promo");

        result.Message.ModerationStatus.Should().Be(ModerationStatus.SpamBlocked);
        result.Message.ModerationReason.Should().Contain("External promotional link spam");
        result.Message.DetectedIntent.Should().Be(DetectedIntent.Spam);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithExcessiveCapslock_ShouldBlockAsSpam()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_mod_06",
            userId: "user_shouter",
            username: "LoudViewer",
            text: "PLEASE GIVE ME FREE GIFTS RIGHT NOW PLEASE");

        result.Message.ModerationStatus.Should().Be(ModerationStatus.SpamBlocked);
        result.Message.ModerationReason.Should().Contain("uppercase shouting spam");
    }

    #endregion

    #region 5. Sentiment Analysis & Intent Detection Tests

    [Fact]
    public async Task ProcessIncomingMessage_WithPositiveWords_ShouldYieldPositiveSentimentScore()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_sent_01",
            userId: "user_fan_01",
            username: "SarahFan",
            text: "I love this jacket, amazing quality and beautiful color!");

        result.Message.SentimentScore.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithNegativeWords_ShouldYieldNegativeSentimentScore()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_sent_02",
            userId: "user_unhappy",
            username: "DisappointedViewer",
            text: "Delivery delay was terrible and the previous order was broken and bad");

        result.Message.SentimentScore.Should().BeLessThan(-0.4);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithProductQuestion_ShouldDetectProductInquiryIntent()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_intent_01",
            userId: "user_buyer_11",
            username: "KarimDZ",
            text: "Is this genuine leather and what are the exact dimensions?");

        result.Message.DetectedIntent.Should().Be(DetectedIntent.ProductInquiry);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithPriceOrDiscountQuestion_ShouldDetectPriceInquiryIntent()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_intent_02",
            userId: "user_buyer_12",
            username: "LinaFashion",
            text: "How much is the flash discount price and what is the shipping fee?");

        result.Message.DetectedIntent.Should().Be(DetectedIntent.PriceInquiry);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithCompliment_ShouldDetectComplimentIntent()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_intent_03",
            userId: "user_fan_03",
            username: "YasmineArt",
            text: "Looks great, awesome host! You look great tonight!");

        result.Message.DetectedIntent.Should().Be(DetectedIntent.Compliment);
    }

    [Fact]
    public async Task ProcessIncomingMessage_WithGeneralGreeting_ShouldDetectGeneralIntent()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_intent_04",
            userId: "user_viewer_04",
            username: "TarekAlger",
            text: "Hello everyone, watching live from Oran!");

        result.Message.DetectedIntent.Should().Be(DetectedIntent.General);
    }

    #endregion

    #region 6. AI Co-Host Auto-Replies & Host Earpiece Cue Generation Tests

    [Fact]
    public async Task ProcessIncomingMessage_ForSizingInquiry_ShouldGenerateHelpfulSizingAutoReply()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_cohost_01",
            userId: "user_size_01",
            username: "Fatima_Z",
            text: "What size should I pick? Does it fit true to size?");

        result.AutoReplyText.Should().Contain("free returns within 30 days and sizes run true to size");
    }

    [Fact]
    public async Task ProcessIncomingMessage_ForShippingInquiry_ShouldGenerateShippingAutoReply()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_cohost_02",
            userId: "user_ship_02",
            username: "Walid_Oran",
            text: "How much is shipping to Setif?");

        result.AutoReplyText.Should().Contain("Standard shipping takes 24-48 hours with cash-on-delivery");
    }

    [Fact]
    public async Task ProcessIncomingMessage_ForPriceInquiry_ShouldGenerateFlashPromotionAutoReply()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_cohost_03",
            userId: "user_price_03",
            username: "Nadia_Cost",
            text: "How much is this flash sale price?");

        result.AutoReplyText.Should().Contain("flash sale price is exclusively available during this live stream");
    }

    [Fact]
    public async Task ProcessIncomingMessage_ForProductMaterialInquiry_ShouldGenerateHighUrgencyHostEarpieceCue()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_cue_01",
            userId: "user_inquiry_01",
            username: "Sarah",
            text: "Is this genuine leather?");

        result.GeneratedCue.Should().NotBeNull();
        result.GeneratedCue!.Urgency.Should().Be(CueUrgency.High);
        result.GeneratedCue.HostPromptText.Should().Contain("[Earpiece Cue] Sarah asks: Is this genuine leather?");
        result.GeneratedCue.RecommendedAction.Should().Contain("texture close-up to the camera");
    }

    [Fact]
    public async Task ProcessIncomingMessage_ForPriceInquiry_ShouldGenerateUrgentHostEarpieceCue()
    {
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId: "live_cue_02",
            userId: "user_buyer_22",
            username: "Rachid_Blida",
            text: "What is the final discount price for this deal?");

        result.GeneratedCue.Should().NotBeNull();
        result.GeneratedCue!.Urgency.Should().Be(CueUrgency.Urgent);
        result.GeneratedCue.RecommendedAction.Should().Contain("remind viewers about limited flash inventory");
    }

    [Fact]
    public async Task GetPendingHostCues_ShouldReturnCuesSortedByUrgencyDescendingThenTimestamp()
    {
        var sessionId = "live_cue_sort_session";

        // Generate Low urgency cue (compliment)
        await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, "u1", "Adel", "Awesome host, love this stream!");

        // Generate High urgency cue (material)
        await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, "u2", "Samia", "Is this real genuine leather?");

        // Generate Urgent cue (price)
        await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, "u3", "Mehdi", "What is the flash price cost?");

        var cues = await _aiCoHostService.GetPendingHostCuesAsync(sessionId);

        cues.Should().HaveCount(3);
        cues[0].Urgency.Should().Be(CueUrgency.Urgent);
        cues[1].Urgency.Should().Be(CueUrgency.High);
        cues[2].Urgency.Should().Be(CueUrgency.Low);
    }

    [Fact]
    public async Task AcknowledgeCue_ShouldMarkCueAsAcknowledgedAndRemoveFromPendingFeed()
    {
        var sessionId = "live_cue_ack_session";
        var result = await _aiCoHostService.ProcessIncomingMessageAsync(
            sessionId, "user_ack", "Sonia", "Can you show the sizing on camera?");

        result.GeneratedCue.Should().NotBeNull();
        var cueId = result.GeneratedCue!.CueId;

        var pendingBefore = await _aiCoHostService.GetPendingHostCuesAsync(sessionId);
        pendingBefore.Should().ContainSingle(c => c.CueId == cueId);

        var ackSuccess = await _aiCoHostService.AcknowledgeCueAsync(sessionId, cueId);
        ackSuccess.Should().BeTrue();

        var pendingAfter = await _aiCoHostService.GetPendingHostCuesAsync(sessionId);
        pendingAfter.Should().NotContain(c => c.CueId == cueId);
    }

    [Fact]
    public async Task GetChatHistory_ShouldReturnMessagesInChronologicalOrder()
    {
        var sessionId = "live_history_session";

        await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, "u1", "User1", "Message one");
        await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, "u2", "User2", "Message two");
        await _aiCoHostService.ProcessIncomingMessageAsync(sessionId, "u3", "User3", "Message three");

        var history = await _aiCoHostService.GetChatHistoryAsync(sessionId, limit: 10);

        history.Should().HaveCount(3);
        history[0].Text.Should().Be("Message one");
        history[1].Text.Should().Be("Message two");
        history[2].Text.Should().Be("Message three");
    }

    #endregion

    #region 7. Dependency Injection & Health Tests

    [Fact]
    public void AddLiveCommerceServices_RegistersAllRequiredDependenciesWithoutBuildingProvider()
    {
        // Must NOT call services.BuildServiceProvider() according to rules!
        var services = new ServiceCollection();
        services.AddLiveCommerceServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(ILiveCommerceStreamingService) &&
            d.ImplementationType == typeof(LiveCommerceStreamingService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ILiveVideoAiCoHostService) &&
            d.ImplementationType == typeof(LiveVideoAiCoHostService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void StreamingProtocolInfo_Record_ShouldRetainValuesCorrectly()
    {
        var info = new StreamingProtocolInfo(
            IngestUrl: "https://stream.sgrowing.com/whip/test-1",
            PlaybackUrl: "https://stream.sgrowing.com/whep/test-1",
            HlsFallbackUrl: "https://stream.sgrowing.com/hls/test-1/master.m3u8",
            ProtocolType: StreamingProtocolType.WebRTC_WHIP);

        info.IngestUrl.Should().Be("https://stream.sgrowing.com/whip/test-1");
        info.PlaybackUrl.Should().Be("https://stream.sgrowing.com/whep/test-1");
        info.HlsFallbackUrl.Should().Be("https://stream.sgrowing.com/hls/test-1/master.m3u8");
        info.ProtocolType.Should().Be(StreamingProtocolType.WebRTC_WHIP);
    }

    [Fact]
    public void LiveCommerceHealthResponse_Record_ShouldHoldPayload()
    {
        var now = DateTime.UtcNow;
        var health = new LiveCommerceHealthResponse(
            Status: "Healthy",
            Service: "Interactive Live Commerce & Real-Time Video AI Co-Host Engine",
            ActiveSessionsCount: 4,
            TimestampUtc: now);

        health.Status.Should().Be("Healthy");
        health.Service.Should().Be("Interactive Live Commerce & Real-Time Video AI Co-Host Engine");
        health.ActiveSessionsCount.Should().Be(4);
        health.TimestampUtc.Should().Be(now);
    }

    #endregion
}
