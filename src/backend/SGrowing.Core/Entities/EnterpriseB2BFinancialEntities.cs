namespace SGrowing.Core.Entities;

public class B2BCreditAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; } = 10000m;
    public decimal OutstandingBalance { get; set; } = 0m;
    public decimal AvailableCredit => Math.Max(0, CreditLimit - OutstandingBalance);
    public int PaymentTermsDays { get; set; } = 30; // 15, 30, 60
    public string CreditScoreRating { get; set; } = "AA"; // AAA, AA, A, BBB, Risk
    public bool IsFrozen { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static B2BCreditAccount Create(
        Guid tenantId,
        Guid companyId,
        string companyName,
        decimal creditLimit = 10000m,
        int termsDays = 30,
        string score = "AA")
    {
        return new B2BCreditAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CompanyName = companyName.Trim(),
            CreditLimit = creditLimit,
            OutstandingBalance = 0m,
            PaymentTermsDays = termsDays,
            CreditScoreRating = score.Trim(),
            IsFrozen = false,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class FactoringReceivableRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CourierProviderId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public decimal InTransitCashFloat { get; set; }
    public decimal VerifiedDeliveredCash { get; set; }
    public decimal AdvanceDiscountRatePercentage { get; set; } = 2.5m; // 2.5% factoring fee
    public decimal ImmediateCashAvailable => Math.Round(VerifiedDeliveredCash * (1 - (AdvanceDiscountRatePercentage / 100m)), 2);
    public string Status { get; set; } = "Draft"; // Draft, Requested, Funded, Settled
    public DateTime? FundedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static FactoringReceivableRecord Create(
        Guid tenantId,
        string courierId,
        string courierName,
        decimal inTransitFloat,
        decimal deliveredFloat,
        decimal discountRate = 2.5m)
    {
        return new FactoringReceivableRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = courierId.Trim(),
            CourierName = courierName.Trim(),
            InTransitCashFloat = inTransitFloat,
            VerifiedDeliveredCash = deliveredFloat,
            AdvanceDiscountRatePercentage = discountRate,
            Status = "Draft",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class TenantApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "sg_live_"; // sg_live_ or sg_test_
    public string KeyMasked { get; set; } = string.Empty; // e.g. sg_live_****abcd
    public string HashedSecret { get; set; } = string.Empty;
    public string AllowedScopesJson { get; set; } = "[\"orders:read\"]";
    public int RateLimitPerMinute { get; set; } = 120;
    public bool IsRevoked { get; set; } = false;
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static (TenantApiKey Entity, string PlainKey) Create(
        Guid tenantId,
        string name,
        List<string> scopes,
        bool isLive = true,
        int rateLimit = 120)
    {
        var prefix = isLive ? "sg_live_" : "sg_test_";
        var rawToken = Guid.NewGuid().ToString("N");
        var plainKey = $"{prefix}{rawToken}";
        var masked = $"{prefix}****{rawToken[^4..]}";

        var hash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainKey)));

        var entity = new TenantApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            KeyPrefix = prefix,
            KeyMasked = masked,
            HashedSecret = hash,
            AllowedScopesJson = System.Text.Json.JsonSerializer.Serialize(scopes),
            RateLimitPerMinute = rateLimit,
            IsRevoked = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        return (entity, plainKey);
    }
}

public class WebhookSubscriptionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string SubscribedEventsJson { get; set; } = "[\"order.delivered\",\"order.refused\"]";
    public bool IsActive { get; set; } = true;
    public int FailureCount { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static WebhookSubscriptionRecord Create(
        Guid tenantId,
        string targetUrl,
        List<string> events)
    {
        return new WebhookSubscriptionRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetUrl = targetUrl.Trim(),
            SecretKey = $"whsec_{Guid.NewGuid():N}",
            SubscribedEventsJson = System.Text.Json.JsonSerializer.Serialize(events),
            IsActive = true,
            FailureCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class WebhookDeliveryAttemptLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SubscriptionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public int ResponseStatusCode { get; set; } = 200;
    public bool IsSuccess { get; set; } = true;
    public long ExecutionDurationMs { get; set; } = 45;
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
    public int AttemptNumber { get; set; } = 1;
    public int MaxRetries { get; set; } = 5;
    public DateTime? NextAttemptAtUtc { get; set; }
    public bool IsDeadLetter { get; set; } = false;
    public string? DeadLetterReason { get; set; }
    public string? SignatureHeader { get; set; }
    public long TimestampHeader { get; set; }

    public static WebhookDeliveryAttemptLog Create(
        Guid tenantId,
        Guid subscriptionId,
        string eventType,
        string payloadJson,
        int statusCode,
        bool isSuccess,
        long durationMs,
        int attemptNumber = 1,
        int maxRetries = 5,
        string? signature = null,
        long timestamp = 0)
    {
        return new WebhookDeliveryAttemptLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            EventType = eventType.Trim(),
            PayloadJson = payloadJson,
            ResponseStatusCode = statusCode,
            IsSuccess = isSuccess,
            ExecutionDurationMs = durationMs,
            AttemptedAtUtc = DateTime.UtcNow,
            AttemptNumber = attemptNumber,
            MaxRetries = maxRetries,
            SignatureHeader = signature,
            TimestampHeader = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}
