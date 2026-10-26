namespace SGrowing.Core.Entities;

public enum CreatorPlatform
{
    Instagram,
    TikTok,
    YouTube,
    Snapchat,
    Facebook,
    BlogOrWebsite
}

public enum CommissionType
{
    Percentage,
    FixedPerDeliveredOrder
}

public enum CreatorPayoutStatus
{
    Draft,
    Approved,
    Transferred,
    Failed
}

public enum EscrowStatus
{
    EscrowHold,          // Order placed via COD; commission held in safety escrow
    ClearedForPayout,    // Courier confirmed parcel Delivered & Cash collected
    VoidedReturn,        // Parcel returned, refused, or canceled -> commission zeroed out safely
    PaidOut              // Disbursed to creator's bank/wallet
}

public class CreatorProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public CreatorPlatform Platform { get; set; } = CreatorPlatform.Instagram;
    public CommissionType CommissionType { get; set; } = CommissionType.Percentage;
    public decimal CommissionRate { get; set; } = 10.0m; // e.g. 10% or 10 TND/SAR per delivered order
    public decimal MinimumPayoutThreshold { get; set; } = 50.0m;
    public string PaymentMethod { get; set; } = "BankTransfer"; // BankTransfer, Flouci, D17, STCPay
    public string PayoutDetails { get; set; } = string.Empty; // IBAN, RIP, or Mobile Wallet ID
    public decimal TotalEarned { get; set; } = 0m;
    public decimal PendingEscrowBalance { get; set; } = 0m;
    public decimal TotalPaidOut { get; set; } = 0m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CreatorProfile Create(
        Guid tenantId,
        string fullName,
        string handle,
        string email,
        string phone,
        CreatorPlatform platform,
        CommissionType commissionType,
        decimal commissionRate,
        decimal minPayoutThreshold = 50.0m,
        string paymentMethod = "BankTransfer",
        string payoutDetails = "")
    {
        return new CreatorProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FullName = fullName.Trim(),
            Handle = handle.Trim().TrimStart('@'),
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phone.Trim(),
            Platform = platform,
            CommissionType = commissionType,
            CommissionRate = commissionRate,
            MinimumPayoutThreshold = minPayoutThreshold,
            PaymentMethod = paymentMethod,
            PayoutDetails = payoutDetails.Trim(),
            TotalEarned = 0m,
            PendingEscrowBalance = 0m,
            TotalPaidOut = 0m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class AffiliateReferralCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatorId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage"; // "Percentage" or "FixedAmount"
    public decimal DiscountValue { get; set; } = 10.0m;
    public int UsageCount { get; set; } = 0;
    public decimal TotalReferredGmv { get; set; } = 0m;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static AffiliateReferralCode Create(
        Guid tenantId,
        Guid creatorId,
        string code,
        string discountType = "Percentage",
        decimal discountValue = 10.0m)
    {
        return new AffiliateReferralCode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatorId = creatorId,
            Code = code.Trim().ToUpperInvariant(),
            DiscountType = discountType,
            DiscountValue = discountValue,
            UsageCount = 0,
            TotalReferredGmv = 0m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class CommissionEscrowLedger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid OrderId { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public string OrderTrackingNumber { get; set; } = string.Empty;
    public decimal OrderAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public EscrowStatus Status { get; set; } = EscrowStatus.EscrowHold;
    public string HoldReason { get; set; } = "Awaiting Courier COD Delivery & Cash Remittance";
    public string? VoidReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClearedAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public DateTime? PaidOutAtUtc { get; set; }

    public static CommissionEscrowLedger Create(
        Guid tenantId,
        Guid creatorId,
        Guid orderId,
        string referralCode,
        string trackingNumber,
        decimal orderAmount,
        decimal commissionAmount)
    {
        return new CommissionEscrowLedger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatorId = creatorId,
            OrderId = orderId,
            ReferralCode = referralCode.Trim().ToUpperInvariant(),
            OrderTrackingNumber = trackingNumber.Trim(),
            OrderAmount = orderAmount,
            CommissionAmount = commissionAmount,
            Status = EscrowStatus.EscrowHold,
            HoldReason = "Awaiting Courier COD Delivery & Cash Remittance",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class CreatorPayoutRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid PayoutBatchId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TND";
    public string PaymentMethod { get; set; } = "BankTransfer";
    public string DestinationAccount { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public CreatorPayoutStatus Status { get; set; } = CreatorPayoutStatus.Draft;
    public int IncludedOrdersCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAtUtc { get; set; }
}

public record CreatorPerformanceSummary(
    Guid CreatorId,
    string FullName,
    string Handle,
    decimal TotalReferredGrossGmv,
    decimal DeliveredNetGmv,
    int TotalReferredOrders,
    int DeliveredOrders,
    int ReturnedOrCanceledOrders,
    decimal DeliverySuccessRate,
    decimal TotalEarnedCommission,
    decimal PendingEscrowCommission,
    decimal VoidedCommission,
    decimal PaidOutCommission,
    decimal AvailableForPayoutBalance
);
