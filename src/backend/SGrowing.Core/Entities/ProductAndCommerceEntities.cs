namespace SGrowing.Core.Entities;

public enum ProductType
{
    Physical = 1,
    Digital = 2,
    Service = 3,
    Subscription = 4
}

public enum QuoteStatus
{
    Draft = 1,
    Sent = 2,
    Accepted = 3,
    ConvertedToOrder = 4,
    Expired = 5
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProductType ProductType { get; set; } = ProductType.Subscription;
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static Product Create(
        Guid tenantId,
        string name,
        string slug,
        string description,
        ProductType productType,
        decimal basePrice,
        string currency = "USD")
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description.Trim(),
            ProductType = productType,
            BasePrice = basePrice,
            Currency = currency.ToUpperInvariant(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class ProductVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int InventoryStock { get; set; } = 100;
    public string AttributesJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;

    public static ProductVariant Create(
        Guid tenantId,
        Guid productId,
        string sku,
        string name,
        decimal price,
        int inventoryStock = 100,
        string attributesJson = "{}")
    {
        return new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            SKU = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Price = price,
            InventoryStock = inventoryStock,
            AttributesJson = attributesJson,
            IsActive = true
        };
    }
}

public class QuoteLineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuoteProposalId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class QuoteProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? DealId { get; set; }
    public Guid ContactId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(14);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<QuoteLineItem> LineItems { get; set; } = new();

    public static QuoteProposal Create(
        Guid tenantId,
        Guid contactId,
        string quoteNumber,
        string currency = "USD",
        Guid? companyId = null,
        Guid? dealId = null,
        decimal discountPercent = 0,
        decimal taxPercent = 0,
        string? notes = null)
    {
        return new QuoteProposal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            CompanyId = companyId,
            DealId = dealId,
            QuoteNumber = quoteNumber,
            Status = QuoteStatus.Draft,
            DiscountPercent = discountPercent,
            TaxPercent = taxPercent,
            Currency = currency.ToUpperInvariant(),
            Notes = notes,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
            CreatedAtUtc = DateTime.UtcNow,
            LineItems = new List<QuoteLineItem>()
        };
    }

    public void AddLineItem(Guid productId, string description, int quantity, decimal unitPrice, Guid? variantId = null)
    {
        var item = new QuoteLineItem
        {
            Id = Guid.NewGuid(),
            QuoteProposalId = Id,
            ProductId = productId,
            ProductVariantId = variantId,
            Description = description,
            Quantity = Math.Max(1, quantity),
            UnitPrice = unitPrice
        };
        LineItems.Add(item);
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        Subtotal = LineItems.Sum(li => li.Quantity * li.UnitPrice);
        var discountAmount = Subtotal * (DiscountPercent / 100m);
        var taxable = Subtotal - discountAmount;
        var taxAmount = taxable * (TaxPercent / 100m);
        TotalAmount = Math.Round(taxable + taxAmount, 2);
    }

    public void MarkSent() => Status = QuoteStatus.Sent;
    public void MarkAccepted() => Status = QuoteStatus.Accepted;
    public void MarkConvertedToOrder() => Status = QuoteStatus.ConvertedToOrder;
}
