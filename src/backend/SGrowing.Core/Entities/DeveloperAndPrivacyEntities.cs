namespace SGrowing.Core.Entities;

public class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public string SecretToken { get; set; } = string.Empty;
    public string SubscribedEvents { get; set; } = "*"; // Comma-separated event types or "*"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static WebhookSubscription Create(
        Guid tenantId,
        string targetUrl,
        string secretToken,
        string subscribedEvents = "*")
    {
        return new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetUrl = targetUrl.Trim(),
            SecretToken = secretToken,
            SubscribedEvents = subscribedEvents.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public bool IsSubscribedTo(string eventType)
    {
        if (!IsActive) return false;
        if (SubscribedEvents == "*") return true;

        var events = SubscribedEvents.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return events.Contains(eventType, StringComparer.OrdinalIgnoreCase);
    }
}

public class WebhookDeliveryLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int? ResponseStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime DeliveredAtUtc { get; set; } = DateTime.UtcNow;
}

public class PrivacyErasureRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string Status { get; set; } = "Pending"; // "Pending", "Completed"
    public int ErasedRecordsCount { get; set; } = 0;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public static PrivacyErasureRequest Create(Guid tenantId, string contactEmail, string? contactPhone = null)
    {
        return new PrivacyErasureRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactEmail = contactEmail.Trim().ToLowerInvariant(),
            ContactPhone = contactPhone?.Trim(),
            Status = "Pending",
            RequestedAtUtc = DateTime.UtcNow
        };
    }
}
