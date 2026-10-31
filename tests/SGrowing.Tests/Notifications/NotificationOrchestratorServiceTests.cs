using FluentAssertions;
using SGrowing.Api.Modules.Notifications;
using Xunit;

namespace SGrowing.Tests.Notifications;

public class NotificationOrchestratorServiceTests
{
    private readonly NotificationOrchestratorService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _recipientId = Guid.NewGuid();

    [Fact]
    public async Task DispatchNotificationAsync_ShouldDispatchToInitialChannel_AndCreateFeedItem()
    {
        var req = new DispatchNotificationRequest(
            _tenantId,
            _recipientId,
            "+1234567890",
            "user@sgrowing.io",
            NotificationTopic.TicketSla,
            NotificationPriority.High,
            "SLA Warning: Ticket #482",
            "Ticket requires your response in 10 minutes.",
            "https://app.sgrowing.io/conversations/482"
        );

        var notif = await _service.DispatchNotificationAsync(req);

        notif.Should().NotBeNull();
        notif.NotificationId.Should().NotBeEmpty();
        notif.CurrentChannel.Should().Be(NotificationChannel.InApp);
        notif.Status.Should().Be(DeliveryStatus.Delivered);
        notif.DeliveryAttempts.Should().HaveCount(1);

        // Verify In-App feed
        var feed = await _service.GetInAppFeedAsync(_tenantId, _recipientId);
        feed.Should().ContainSingle(f => f.NotificationId == notif.NotificationId);
        feed[0].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task DispatchNotificationAsync_ShouldDeduplicate_WhenIdempotencyKeyIsRepeated()
    {
        var key = "order-shipped-999";
        var req = new DispatchNotificationRequest(
            _tenantId,
            _recipientId,
            null,
            "buyer@sgrowing.io",
            NotificationTopic.BillingInvoice,
            NotificationPriority.Normal,
            "Invoice #999 Paid",
            "Receipt attached",
            IdempotencyKey: key
        );

        var first = await _service.DispatchNotificationAsync(req);
        var second = await _service.DispatchNotificationAsync(req);

        first.NotificationId.Should().Be(second.NotificationId);
        first.DispatchedAtUtc.Should().Be(second.DispatchedAtUtc);
    }

    [Fact]
    public async Task DispatchNotificationAsync_ShouldMarkOptedOut_WhenTopicIsDisabledInPreferences()
    {
        var prefReq = new SetSubscriberPreferencesRequest(
            _tenantId,
            _recipientId,
            new Dictionary<NotificationTopic, List<NotificationChannel>>
            {
                { NotificationTopic.MarketingNewsletter, new List<NotificationChannel>() } // Opted out of marketing
            }
        );
        await _service.SetSubscriberPreferencesAsync(prefReq);

        var req = new DispatchNotificationRequest(
            _tenantId,
            _recipientId,
            null,
            "user@test.com",
            NotificationTopic.MarketingNewsletter,
            NotificationPriority.Low,
            "Summer Discount 50%",
            "Check our catalog"
        );

        var result = await _service.DispatchNotificationAsync(req);

        result.Status.Should().Be(DeliveryStatus.OptedOut);
        result.DeliveryAttempts.Should().Contain(a => a.Status == DeliveryStatus.OptedOut);
    }

    [Fact]
    public async Task DispatchNotificationAsync_ShouldThrottle_DuringActiveQuietHours_UnlessUrgent()
    {
        var prefReq = new SetSubscriberPreferencesRequest(
            _tenantId,
            _recipientId,
            new Dictionary<NotificationTopic, List<NotificationChannel>>(),
            QuietHoursStartHourUtc: 22,
            QuietHoursEndHourUtc: 7,
            IsQuietHoursEnabled: true
        );
        await _service.SetSubscriberPreferencesAsync(prefReq);

        // Simulate 23:00 UTC (quiet hour)
        var quietTime = new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc);

        var normalReq = new DispatchNotificationRequest(
            _tenantId,
            _recipientId,
            null,
            null,
            NotificationTopic.BillingInvoice,
            NotificationPriority.Normal,
            "Monthly Statement",
            "Statement ready"
        );

        var normalResult = await _service.DispatchNotificationAsync(normalReq, nowUtc: quietTime);
        normalResult.Status.Should().Be(DeliveryStatus.Throttled);

        // Urgent priority should bypass quiet hours
        var urgentReq = new DispatchNotificationRequest(
            _tenantId,
            _recipientId,
            null,
            null,
            NotificationTopic.SecurityAlert,
            NotificationPriority.UrgentCritical,
            "Unauthorized Login Attempt",
            "New IP login detected"
        );

        var urgentResult = await _service.DispatchNotificationAsync(urgentReq, nowUtc: quietTime);
        urgentResult.Status.Should().Be(DeliveryStatus.Delivered);
    }

    [Fact]
    public async Task DispatchNotificationAsync_ShouldThrottle_WhenRateLimitExceeded()
    {
        var prefReq = new SetSubscriberPreferencesRequest(
            _tenantId,
            _recipientId,
            new Dictionary<NotificationTopic, List<NotificationChannel>>(),
            MaxNotificationsPerHour: 2
        );
        await _service.SetSubscriberPreferencesAsync(prefReq);

        var req1 = new DispatchNotificationRequest(_tenantId, _recipientId, null, null, NotificationTopic.SystemMaintenance, NotificationPriority.Normal, "Alert 1", "Body 1");
        var req2 = new DispatchNotificationRequest(_tenantId, _recipientId, null, null, NotificationTopic.SystemMaintenance, NotificationPriority.Normal, "Alert 2", "Body 2");
        var req3 = new DispatchNotificationRequest(_tenantId, _recipientId, null, null, NotificationTopic.SystemMaintenance, NotificationPriority.Normal, "Alert 3", "Body 3");

        var res1 = await _service.DispatchNotificationAsync(req1);
        var res2 = await _service.DispatchNotificationAsync(req2);
        var res3 = await _service.DispatchNotificationAsync(req3);

        res1.Status.Should().Be(DeliveryStatus.Delivered);
        res2.Status.Should().Be(DeliveryStatus.Delivered);
        res3.Status.Should().Be(DeliveryStatus.Throttled);
    }

    [Fact]
    public async Task ProcessFallbackSweepAsync_ShouldCascadeChannels_WhenUnreadPastTimeout()
    {
        var req = new DispatchNotificationRequest(
            _tenantId,
            _recipientId,
            "+1234567890",
            "support@sgrowing.io",
            NotificationTopic.TicketSla,
            NotificationPriority.High,
            "P1 Escalation",
            "Ticket breached SLA"
        );

        var now = DateTime.UtcNow;
        var notif = await _service.DispatchNotificationAsync(req, nowUtc: now);
        notif.CurrentChannel.Should().Be(NotificationChannel.InApp);

        // Advance 5 minutes past 3-minute timeout
        var sweepTime1 = now.AddMinutes(5);
        var fallenBack1 = await _service.ProcessFallbackSweepAsync(_tenantId, nowUtc: sweepTime1);

        fallenBack1.Should().Contain(n => n.NotificationId == notif.NotificationId);
        notif.CurrentChannel.Should().Be(NotificationChannel.WhatsApp);
        notif.Status.Should().Be(DeliveryStatus.FallenBack);

        // Advance another 5 minutes past second timeout
        var sweepTime2 = sweepTime1.AddMinutes(5);
        var fallenBack2 = await _service.ProcessFallbackSweepAsync(_tenantId, nowUtc: sweepTime2);

        fallenBack2.Should().Contain(n => n.NotificationId == notif.NotificationId);
        notif.CurrentChannel.Should().Be(NotificationChannel.Sms);

        // Advance another 5 minutes past third timeout
        var sweepTime3 = sweepTime2.AddMinutes(5);
        var fallenBack3 = await _service.ProcessFallbackSweepAsync(_tenantId, nowUtc: sweepTime3);

        fallenBack3.Should().Contain(n => n.NotificationId == notif.NotificationId);
        notif.CurrentChannel.Should().Be(NotificationChannel.Email);

        // Final timeout: all channels exhausted
        var sweepTime4 = sweepTime3.AddMinutes(5);
        await _service.ProcessFallbackSweepAsync(_tenantId, nowUtc: sweepTime4);

        notif.Status.Should().Be(DeliveryStatus.Failed);
    }

    [Fact]
    public async Task RecordDeliveryReceiptAsync_ShouldMarkRead_AndUpdateFeedItem()
    {
        var req = new DispatchNotificationRequest(_tenantId, _recipientId, null, null, NotificationTopic.BillingInvoice, NotificationPriority.Normal, "Invoice Ready", "Please view");
        var notif = await _service.DispatchNotificationAsync(req);

        var receiptReq = new RecordDeliveryReceiptRequest(_tenantId, notif.NotificationId, NotificationChannel.InApp, IsRead: true);
        var updated = await _service.RecordDeliveryReceiptAsync(receiptReq);

        updated.Status.Should().Be(DeliveryStatus.Read);
        updated.ReadAtUtc.Should().NotBeNull();

        // Feed item should be read
        var unreadFeed = await _service.GetInAppFeedAsync(_tenantId, _recipientId, unreadOnly: true);
        unreadFeed.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMetricsAsync_ShouldAggregateMetricsAccurately()
    {
        var req1 = new DispatchNotificationRequest(_tenantId, _recipientId, null, null, NotificationTopic.SecurityAlert, NotificationPriority.UrgentCritical, "Sec 1", "Body");
        var req2 = new DispatchNotificationRequest(_tenantId, _recipientId, null, null, NotificationTopic.BillingInvoice, NotificationPriority.Normal, "Bill 1", "Body");

        var n1 = await _service.DispatchNotificationAsync(req1);
        await _service.DispatchNotificationAsync(req2);

        await _service.RecordDeliveryReceiptAsync(new RecordDeliveryReceiptRequest(_tenantId, n1.NotificationId, NotificationChannel.InApp, true));

        var metrics = await _service.GetMetricsAsync(_tenantId);

        metrics.TotalDispatched.Should().Be(2);
        metrics.DeliveredCount.Should().Be(2);
        metrics.ReadCount.Should().Be(1);
    }
}
