namespace SGrowing.Core.Entities;

public enum StorefrontPlatform
{
    Shopify = 1,
    WooCommerce = 2,
    Medusa = 3,
    BigCommerce = 4
}

public enum AbandonedCartStatus
{
    Detected = 1,
    RecoveryTriggered = 2,
    Recovered = 3,
    Expired = 4
}

public class StorefrontConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public StorefrontPlatform Platform { get; set; } = StorefrontPlatform.Shopify;
    public string StoreDomain { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static StorefrontConnection Create(
        Guid tenantId,
        StorefrontPlatform platform,
        string storeDomain,
        string webhookSecret,
        string accessToken)
    {
        return new StorefrontConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Platform = platform,
            StoreDomain = storeDomain.Trim().ToLowerInvariant(),
            WebhookSecret = webhookSecret,
            AccessToken = accessToken,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class AbandonedCartRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid StorefrontConnectionId { get; set; }
    public string ExternalCartId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public decimal CartTotal { get; set; }
    public string RecoveryUrl { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public AbandonedCartStatus Status { get; set; } = AbandonedCartStatus.Detected;
    public int RecoveryAttempts { get; set; } = 0;
    public DateTime DetectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RecoveredAtUtc { get; set; }

    public static AbandonedCartRecord Create(
        Guid tenantId,
        Guid storefrontConnectionId,
        string externalCartId,
        string customerEmail,
        decimal cartTotal,
        string recoveryUrl,
        string? customerPhone = null,
        string itemsJson = "[]")
    {
        return new AbandonedCartRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StorefrontConnectionId = storefrontConnectionId,
            ExternalCartId = externalCartId,
            CustomerEmail = customerEmail.Trim().ToLowerInvariant(),
            CustomerPhone = customerPhone?.Trim(),
            CartTotal = cartTotal,
            RecoveryUrl = recoveryUrl,
            ItemsJson = itemsJson,
            Status = AbandonedCartStatus.Detected,
            DetectedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkRecoveryTriggered()
    {
        Status = AbandonedCartStatus.RecoveryTriggered;
        RecoveryAttempts++;
    }

    public void MarkRecovered()
    {
        Status = AbandonedCartStatus.Recovered;
        RecoveredAtUtc = DateTime.UtcNow;
    }
}
