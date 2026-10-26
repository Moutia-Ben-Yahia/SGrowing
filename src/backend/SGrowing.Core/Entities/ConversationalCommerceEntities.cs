namespace SGrowing.Core.Entities;

public enum InChatCartStatus
{
    Drafting = 1,
    AwaitingCustomerConfirmation = 2,
    Confirmed = 3,
    Abandoned = 4
}

public class InChatCartIntent
{
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Governorate { get; set; }
    public string? DelegationOrAddress { get; set; }
    public string? ProductName { get; set; }
    public string? VariantSku { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal EstimatedShippingFee { get; set; } = 7.0m;
    public decimal TotalAmount => (Quantity * UnitPrice) + EstimatedShippingFee;
    public string Currency { get; set; } = "TND";
    public double ConfidenceScore { get; set; } = 0.95;
    public bool IsCompleteForCheckout => !string.IsNullOrWhiteSpace(PhoneNumber) &&
                                          !string.IsNullOrWhiteSpace(Governorate) &&
                                          !string.IsNullOrWhiteSpace(ProductName) &&
                                          Quantity > 0;
}

public class InChatCartSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? DraftOrderId { get; set; }
    public InChatCartStatus Status { get; set; } = InChatCartStatus.Drafting;
    public string CartItemsJson { get; set; } = "[]";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "TND";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAtUtc { get; set; }
}
