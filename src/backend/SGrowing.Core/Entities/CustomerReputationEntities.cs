namespace SGrowing.Core.Entities;

public enum ReputationTier
{
    Trusted = 1,     // Score >= 80: Auto-confirmed without call
    Standard = 2,    // Score 50-79: WhatsApp / Voice AI confirmation
    HighRisk = 3,    // Score 20-49: Micro-deposit required before dispatch
    Blacklisted = 4  // Score < 20: Serial rejector, COD blocked
}

public enum MicroDepositStatus
{
    Pending = 1,
    Paid = 2,
    Refunded = 3,
    Forfeited = 4
}

public class CustomerReputationScorecard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int ReputationScore { get; set; } = 70; // 0 - 100
    public ReputationTier Tier { get; set; } = ReputationTier.Standard;
    public int TotalOrdersCount { get; set; }
    public int DeliveredOrdersCount { get; set; }
    public int RefusedReturnsCount { get; set; }
    public int CancelledOrdersCount { get; set; }
    public decimal TotalSpentAmount { get; set; }
    public decimal DeliverySuccessRatePercentage { get; set; } = 100.0m;
    public bool RequiresMicroDeposit { get; set; } = false;
    public DateTime LastEvaluatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CustomerReputationScorecard Create(Guid tenantId, string customerPhone, string? email = null)
    {
        return new CustomerReputationScorecard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPhone = customerPhone.Trim(),
            CustomerEmail = email?.Trim().ToLowerInvariant(),
            ReputationScore = 70,
            Tier = ReputationTier.Standard,
            TotalOrdersCount = 0,
            DeliveredOrdersCount = 0,
            RefusedReturnsCount = 0,
            CancelledOrdersCount = 0,
            TotalSpentAmount = 0m,
            DeliverySuccessRatePercentage = 100m,
            RequiresMicroDeposit = false,
            LastEvaluatedAtUtc = DateTime.UtcNow
        };
    }
}

public class MicroDepositPaymentLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public decimal DepositAmount { get; set; } = 10.0m;
    public string Currency { get; set; } = "TND";
    public string PaymentUrl { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = "Flouci"; // Flouci, Konnect, Stripe
    public MicroDepositStatus Status { get; set; } = MicroDepositStatus.Pending;
    public DateTime? PaidAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static MicroDepositPaymentLink Create(
        Guid tenantId,
        Guid orderId,
        string customerPhone,
        decimal amount = 10.0m,
        string currency = "TND",
        string provider = "Flouci")
    {
        var linkId = Guid.NewGuid();
        return new MicroDepositPaymentLink
        {
            Id = linkId,
            TenantId = tenantId,
            OrderId = orderId,
            CustomerPhone = customerPhone.Trim(),
            DepositAmount = amount,
            Currency = currency,
            PaymentProvider = provider,
            PaymentUrl = $"https://pay.sgrowing.com/deposit/{linkId:N}",
            Status = MicroDepositStatus.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public record ReputationEvaluationResult(
    string CustomerPhone,
    int ReputationScore,
    ReputationTier Tier,
    decimal DeliverySuccessRate,
    int RefusedReturnsCount,
    bool RequiresMicroDeposit,
    string RecommendedAction);
