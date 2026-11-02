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
}
