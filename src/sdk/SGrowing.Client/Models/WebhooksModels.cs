using System.Text.Json.Serialization;

namespace SGrowing.Client.Models;

/// <summary>
/// Record representing an active outbound webhook subscription.
/// </summary>
public record WebhookSubscriptionRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("targetUrl")] string TargetUrl,
    [property: JsonPropertyName("secretKey")] string SecretKey,
    [property: JsonPropertyName("subscribedEventsJson")] string SubscribedEventsJson,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("failureCount")] int FailureCount,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc
);

/// <summary>
/// Execution attempt log for an outbound webhook delivery.
/// </summary>
public record WebhookDeliveryAttemptLog(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("subscriptionId")] Guid SubscriptionId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("httpStatusCode")] int HttpStatusCode,
    [property: JsonPropertyName("isSuccess")] bool IsSuccess,
    [property: JsonPropertyName("durationMs")] long DurationMs,
    [property: JsonPropertyName("failureReason")] string? FailureReason,
    [property: JsonPropertyName("attemptsCount")] int AttemptsCount,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc
);

/// <summary>
/// Request to trigger a synthetic test webhook dispatch.
/// </summary>
public record TestDispatchWebhookRequest(
    [property: JsonPropertyName("tenantId")] Guid TenantId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("payload")] object Payload
);

/// <summary>
/// Response received from triggering a test webhook dispatch.
/// </summary>
public record TestDispatchWebhookResponse(
    [property: JsonPropertyName("dispatchedCount")] int DispatchedCount,
    [property: JsonPropertyName("logs")] List<WebhookDeliveryAttemptLog> Logs
);
