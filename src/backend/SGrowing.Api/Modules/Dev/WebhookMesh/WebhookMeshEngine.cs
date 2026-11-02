using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Dev.WebhookMesh;

public interface IWebhookMeshEngine
{
    List<WebhookSubscription> ListSubscriptions();
    WebhookSubscription CreateSubscription(CreateSubscriptionRequest request);
    List<WebhookDeliveryAttempt> TriggerEvent(TriggerWebhookEventRequest request);
    List<WebhookDeliveryAttempt> ListDeliveryHistory();
    WebhookDeliveryAttempt ReplayDelivery(ReplayDeliveryRequest request);
    DlqSummaryResult GetDlqSummary();
}

public class WebhookMeshEngine : IWebhookMeshEngine
{
    private readonly ConcurrentDictionary<string, WebhookSubscription> _subscriptions = new();
    private readonly ConcurrentBag<WebhookDeliveryAttempt> _deliveries = new();

    public WebhookMeshEngine()
    {
        SeedDefaultSubscriptions();
        SeedSampleDeliveries();
    }

    private void SeedDefaultSubscriptions()
    {
        var sub1 = new WebhookSubscription(
            SubscriptionId: "sub_zapier_crm",
            TargetUrl: "https://hooks.zapier.com/hooks/catch/enterprise_crm/882194/",
            SubscribedEvents: new List<string> { "crm.contact.created", "deal.closed_won" },
            SecretKey: "whsec_super_secret_enterprise_signing_key_1122",
            IsActive: true,
            RetryLimit: 5,
            CreatedAtUtc: DateTime.UtcNow.AddDays(-7)
        );

        var sub2 = new WebhookSubscription(
            SubscriptionId: "sub_n8n_capi",
            TargetUrl: "https://automation.internal.enterprise/webhook/ad-capi-sync",
            SubscribedEvents: new List<string> { "advertising.capi.converted" },
            SecretKey: "whsec_n8n_secure_hmac_key_9988",
            IsActive: true,
            RetryLimit: 3,
            CreatedAtUtc: DateTime.UtcNow.AddDays(-3)
        );

        _subscriptions[sub1.SubscriptionId] = sub1;
        _subscriptions[sub2.SubscriptionId] = sub2;
    }

    private void SeedSampleDeliveries()
    {
        var hmac = ComputeHmacSignature("{\"event\": \"deal.closed_won\", \"amount\": 125000}", "whsec_super_secret_enterprise_signing_key_1122");

        _deliveries.Add(new WebhookDeliveryAttempt(
            DeliveryId: "del_succ_1",
            SubscriptionId: "sub_zapier_crm",
            EventType: "deal.closed_won",
            PayloadJson: "{\"event\": \"deal.closed_won\", \"amount\": 125000}",
            HmacSignature: hmac,
            AttemptNumber: 1,
            HttpResponseCode: 200,
            Status: WebhookDeliveryStatus.Delivered,
            LatencyMs: 124.5,
            AttemptedAtUtc: DateTime.UtcNow.AddHours(-1)
        ));

        _deliveries.Add(new WebhookDeliveryAttempt(
            DeliveryId: "del_dlq_1",
            SubscriptionId: "sub_n8n_capi",
            EventType: "advertising.capi.converted",
            PayloadJson: "{\"event\": \"advertising.capi.converted\", \"roas\": 4.8}",
            HmacSignature: ComputeHmacSignature("{\"event\": \"advertising.capi.converted\", \"roas\": 4.8}", "whsec_n8n_secure_hmac_key_9988"),
            AttemptNumber: 5,
            HttpResponseCode: 504,
            Status: WebhookDeliveryStatus.DeadLettered,
            LatencyMs: 15000.0,
            AttemptedAtUtc: DateTime.UtcNow.AddHours(-2)
        ));
    }

    public List<WebhookSubscription> ListSubscriptions() =>
        _subscriptions.Values.OrderByDescending(s => s.CreatedAtUtc).ToList();

    public WebhookSubscription CreateSubscription(CreateSubscriptionRequest request)
    {
        var id = "sub_" + Guid.NewGuid().ToString("N")[..8];
        var secret = "whsec_" + Guid.NewGuid().ToString("N");

        var sub = new WebhookSubscription(
            SubscriptionId: id,
            TargetUrl: request.TargetUrl,
            SubscribedEvents: request.SubscribedEvents,
            SecretKey: secret,
            IsActive: true,
            RetryLimit: request.RetryLimit,
            CreatedAtUtc: DateTime.UtcNow
        );

        _subscriptions[id] = sub;
        return sub;
    }

    public List<WebhookDeliveryAttempt> TriggerEvent(TriggerWebhookEventRequest request)
    {
        var matchingSubs = _subscriptions.Values
            .Where(s => s.IsActive && (s.SubscribedEvents.Contains("*") || s.SubscribedEvents.Contains(request.EventType)))
            .ToList();

        var attempts = new List<WebhookDeliveryAttempt>();

        foreach (var sub in matchingSubs)
        {
            var hmac = ComputeHmacSignature(request.PayloadJson, sub.SecretKey);
            var attempt = new WebhookDeliveryAttempt(
                DeliveryId: "del_" + Guid.NewGuid().ToString("N")[..8],
                SubscriptionId: sub.SubscriptionId,
                EventType: request.EventType,
                PayloadJson: request.PayloadJson,
                HmacSignature: hmac,
                AttemptNumber: 1,
                HttpResponseCode: 200,
                Status: WebhookDeliveryStatus.Delivered,
                LatencyMs: 82.0,
                AttemptedAtUtc: DateTime.UtcNow
            );

            _deliveries.Add(attempt);
            attempts.Add(attempt);
        }

        return attempts;
    }

    public List<WebhookDeliveryAttempt> ListDeliveryHistory() =>
        _deliveries.OrderByDescending(d => d.AttemptedAtUtc).ToList();

    public WebhookDeliveryAttempt ReplayDelivery(ReplayDeliveryRequest request)
    {
        var target = _deliveries.FirstOrDefault(d => d.DeliveryId == request.DeliveryId);
        if (target == null)
        {
            throw new KeyNotFoundException($"Webhook delivery '{request.DeliveryId}' not found.");
        }

        var payload = request.MutatedPayload ?? target.PayloadJson;
        var sub = _subscriptions.TryGetValue(target.SubscriptionId, out var s) ? s : null;
        var hmac = ComputeHmacSignature(payload, sub?.SecretKey ?? "fallback_key");

        var replayed = new WebhookDeliveryAttempt(
            DeliveryId: "del_replay_" + Guid.NewGuid().ToString("N")[..8],
            SubscriptionId: target.SubscriptionId,
            EventType: target.EventType,
            PayloadJson: payload,
            HmacSignature: hmac,
            AttemptNumber: target.AttemptNumber + 1,
            HttpResponseCode: 200,
            Status: WebhookDeliveryStatus.Delivered,
            LatencyMs: 64.0,
            AttemptedAtUtc: DateTime.UtcNow
        );

        _deliveries.Add(replayed);
        return replayed;
    }

    public DlqSummaryResult GetDlqSummary()
    {
        var dlq = _deliveries.Where(d => d.Status == WebhookDeliveryStatus.DeadLettered).ToList();
        return new DlqSummaryResult(
            TotalDeadLettered: dlq.Count,
            DeadLetterMessages: dlq
        );
    }

    private static string ComputeHmacSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
