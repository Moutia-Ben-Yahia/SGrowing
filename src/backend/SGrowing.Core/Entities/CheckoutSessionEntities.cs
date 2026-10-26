namespace SGrowing.Core.Entities;

public enum CheckoutSessionStatus
{
    Pending = 1,
    OtpSent = 2,
    OtpVerified = 3,
    OrderPlaced = 4,
    Abandoned = 5
}

public class CodCheckoutSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SessionKey { get; set; } = string.Empty;
    public string CartItemsJson { get; set; } = "[]";
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; } = 7m; // Default standard COD delivery
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TotalAmount => Math.Max(0, Subtotal + ShippingFee - DiscountAmount);
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string WilayaCode { get; set; } = string.Empty;
    public string WilayaName { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public bool IsOtpVerified { get; set; } = false;
    public CheckoutSessionStatus Status { get; set; } = CheckoutSessionStatus.Pending;
    public string? PlacedOrderId { get; set; }
    public string? UpsellOfferAcceptedSku { get; set; }
    public decimal UpsellAmount { get; set; } = 0m;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public static CodCheckoutSession Create(
        Guid tenantId,
        string cartItemsJson,
        decimal subtotal,
        decimal shippingFee = 7m)
    {
        return new CodCheckoutSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionKey = $"chk_{Guid.NewGuid():N}",
            CartItemsJson = cartItemsJson,
            Subtotal = subtotal,
            ShippingFee = shippingFee,
            Status = CheckoutSessionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class WilayaZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // e.g. "TN-11" or "16"
    public string NameAr { get; set; } = string.Empty;
    public string NameFr { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "TN"; // TN, DZ, MA, SA
    public decimal StandardShippingFee { get; set; } = 7m;
    public int EstimatedDeliveryDays { get; set; } = 2;
    public string DelegationsJson { get; set; } = "[]"; // List of string
    public bool IsActive { get; set; } = true;

    public static WilayaZone Create(
        string code,
        string nameAr,
        string nameFr,
        string countryCode,
        decimal shippingFee,
        int deliveryDays,
        List<string> delegations)
    {
        return new WilayaZone
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            NameFr = nameFr.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            StandardShippingFee = shippingFee,
            EstimatedDeliveryDays = deliveryDays,
            DelegationsJson = System.Text.Json.JsonSerializer.Serialize(delegations),
            IsActive = true
        };
    }
}
