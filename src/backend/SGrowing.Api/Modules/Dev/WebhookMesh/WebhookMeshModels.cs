namespace SGrowing.Api.Modules.Dev.WebhookMesh;

public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Retrying,
    Failed,
    DeadLettered
}

public record WebhookSubscription(
    string SubscriptionId,
    string TargetUrl,
    List<string> SubscribedEvents,
    string SecretKey,
    bool IsActive,
    int RetryLimit,
    DateTime CreatedAtUtc
);

public record WebhookDeliveryAttempt(
    string DeliveryId,
    string SubscriptionId,
    string EventType,
    string PayloadJson,
    string HmacSignature,
    int AttemptNumber,
    int HttpResponseCode,
    WebhookDeliveryStatus Status,
    double LatencyMs,
    DateTime AttemptedAtUtc
);

public record CreateSubscriptionRequest(
    string TargetUrl,
    List<string> SubscribedEvents,
    int RetryLimit = 5
);

public record TriggerWebhookEventRequest(
    string EventType,
    string PayloadJson
);

public record ReplayDeliveryRequest(
    string DeliveryId,
    string? MutatedPayload = null
);

public record DlqSummaryResult(
    int TotalDeadLettered,
    List<WebhookDeliveryAttempt> DeadLetterMessages
);
