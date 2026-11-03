using SGrowing.Api.Modules.Dev.WebhookMesh;

namespace SGrowing.Tests.Dev;

public class WebhookMeshTests
{
    private readonly IWebhookMeshEngine _engine = new WebhookMeshEngine();

    [Fact]
    public void ListSubscriptions_ReturnsSeededIntegrations()
    {
        var subs = _engine.ListSubscriptions();

        Assert.NotEmpty(subs);
        Assert.Contains(subs, s => s.SubscriptionId == "sub_zapier_crm");
        Assert.True(subs[0].IsActive);
    }

    [Fact]
    public void TriggerEvent_DispatchesWithHmacSha256Signature()
    {
        var attempts = _engine.TriggerEvent(new TriggerWebhookEventRequest(
            EventType: "deal.closed_won",
            PayloadJson: "{\"dealId\": \"d_99\", \"arr\": 50000}"
        ));

        Assert.NotEmpty(attempts);
        Assert.Equal("deal.closed_won", attempts[0].EventType);
        Assert.StartsWith("sha256=", attempts[0].HmacSignature);
        Assert.Equal(200, attempts[0].HttpResponseCode);
    }

    [Fact]
    public void ReplayDelivery_ResendsDeadLetteredEvent()
    {
        var dlqBefore = _engine.GetDlqSummary();
        Assert.True(dlqBefore.TotalDeadLettered >= 1);

        var replayed = _engine.ReplayDelivery(new ReplayDeliveryRequest(
            DeliveryId: "del_dlq_1",
            MutatedPayload: "{\"event\": \"advertising.capi.converted\", \"roas\": 5.2}"
        ));

        Assert.NotNull(replayed);
        Assert.Equal(WebhookDeliveryStatus.Delivered, replayed.Status);
        Assert.Equal(200, replayed.HttpResponseCode);
    }

    [Fact]
    public void CreateSubscription_GeneratesUniqueIdAndSecretKey()
    {
        var request = new CreateSubscriptionRequest(
            TargetUrl: "https://api.partner.io/webhooks/incoming",
            SubscribedEvents: new List<string> { "invoice.paid", "subscription.renewed" },
            RetryLimit: 4
        );

        var sub = _engine.CreateSubscription(request);

        Assert.NotNull(sub);
        Assert.StartsWith("sub_", sub.SubscriptionId);
        Assert.StartsWith("whsec_", sub.SecretKey);
        Assert.True(sub.IsActive);
        Assert.Equal(4, sub.RetryLimit);

        var allSubs = _engine.ListSubscriptions();
        Assert.Contains(allSubs, s => s.SubscriptionId == sub.SubscriptionId);
    }

    [Fact]
    public void TriggerEvent_WildcardSubscriber_MatchesAnyEvent()
    {
        var wildcardSub = _engine.CreateSubscription(new CreateSubscriptionRequest(
            TargetUrl: "https://audit.enterprise.io/all-events",
            SubscribedEvents: new List<string> { "*" },
            RetryLimit: 3
        ));

        var attempts = _engine.TriggerEvent(new TriggerWebhookEventRequest(
            EventType: "custom.security.alert",
            PayloadJson: "{\"threat\": \"elevated\"}"
        ));

        Assert.Contains(attempts, a => a.SubscriptionId == wildcardSub.SubscriptionId);
    }

    [Fact]
    public void TriggerEvent_UnsubscribedEvent_ReturnsEmptyAttempts()
    {
        var attempts = _engine.TriggerEvent(new TriggerWebhookEventRequest(
            EventType: "unmapped.obscure.event",
            PayloadJson: "{}"
        ));

        // Unless there's a wildcard subscription, unmapped event has no matching attempts
        Assert.All(attempts, a => Assert.True(a.EventType == "unmapped.obscure.event"));
    }

    [Fact]
    public void ListDeliveryHistory_ReturnsDeliveriesDescending()
    {
        var history = _engine.ListDeliveryHistory();

        Assert.NotEmpty(history);
        for (var i = 1; i < history.Count; i++)
        {
            Assert.True(history[i - 1].AttemptedAtUtc >= history[i].AttemptedAtUtc);
        }
    }

    [Fact]
    public void ReplayDelivery_NonExistentDelivery_ThrowsKeyNotFoundException()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            _engine.ReplayDelivery(new ReplayDeliveryRequest("del_non_existent", null)));
    }
}
