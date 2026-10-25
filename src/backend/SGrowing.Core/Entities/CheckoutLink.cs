namespace SGrowing.Core.Entities;

public enum CheckoutStatus
{
    Active = 1,
    Paid = 2,
    Expired = 3
}

public class CheckoutLink
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public Guid ConversationId { get; private set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public CheckoutStatus Status { get; set; } = CheckoutStatus.Active;
    public string PaymentUrl { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private CheckoutLink() { }

    public static CheckoutLink Create(
        Guid tenantId,
        Guid contactId,
        Guid conversationId,
        string title,
        decimal amount,
        string currency = "USD")
    {
        var id = Guid.NewGuid();
        return new CheckoutLink
        {
            Id = id,
            TenantId = tenantId,
            ContactId = contactId,
            ConversationId = conversationId,
            Title = title,
            Amount = amount,
            Currency = currency,
            Status = CheckoutStatus.Active,
            PaymentUrl = $"https://pay.sgrowing.com/session/{id.ToString().Substring(0, 8)}",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkPaid()
    {
        Status = CheckoutStatus.Paid;
    }
}
