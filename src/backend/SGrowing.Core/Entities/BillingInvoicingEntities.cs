namespace SGrowing.Core.Entities;

public enum InvoiceStatus
{
    Draft = 1,
    Sent = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

public class InvoiceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? DealId { get; set; }
    public Guid? ContactId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal SubTotal { get; set; }
    public decimal TaxPercentage { get; set; } = 19.0m;
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalBaseAmountUsd { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime? PaidAtUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<InvoiceLineItemRecord> LineItems { get; set; } = new();
}

public class InvoiceLineItemRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class PaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "BankTransfer";
    public string? TransactionReference { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Success";
}

public class CurrencyRateRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal RateToBaseUsd { get; set; } = 1.0m;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public record CreateInvoiceLineItemRequest(
    string Description,
    int Quantity,
    decimal UnitPrice
);

public record CreateInvoiceRequest(
    string ClientName,
    string? ClientEmail = null,
    string Currency = "USD",
    decimal TaxPercentage = 19.0m,
    Guid? DealId = null,
    Guid? ContactId = null,
    DateTime? DueDateUtc = null,
    string? Notes = null,
    List<CreateInvoiceLineItemRequest>? LineItems = null
);

public record RecordPaymentRequest(
    decimal Amount,
    string PaymentMethod = "BankTransfer",
    string? TransactionReference = null
);

public record CurrencyConversionResult(
    decimal SourceAmount,
    string FromCurrency,
    decimal TargetAmount,
    string ToCurrency,
    decimal ExchangeRate
);
