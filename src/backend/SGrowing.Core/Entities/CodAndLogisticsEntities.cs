namespace SGrowing.Core.Entities;

public enum CodOrderStatus
{
    PendingConfirmation = 1,
    Confirmed = 2,
    Postponed = 3,
    Cancelled = 4,
    FakeDetected = 5,
    Dispatched = 6,
    Delivered = 7,
    Returned = 8
}

public enum FraudRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Fraudulent = 4
}

public enum ConfirmationChannel
{
    PhoneCall = 1,
    WhatsAppInteractive = 2,
    SmsInteractive = 3,
    AutoVerified = 4
}

public enum CallOutcome
{
    Confirmed = 1,
    NoAnswer = 2,
    Busy = 3,
    CancelledByCustomer = 4,
    AddressIncomplete = 5,
    CallbackRequested = 6
}

public enum CourierShipmentStatus
{
    Created = 1,
    PickedUp = 2,
    InTransit = 3,
    OutForDelivery = 4,
    Delivered = 5,
    FailedAttempt = 6,
    ReturnedToSender = 7
}

public class CodOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public string GovernorateOrWilaya { get; set; } = string.Empty;
    public string DelegationOrCommune { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "TN"; // TN, DZ, EG, MA
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public string CodCurrency { get; set; } = "TND";
    public CodOrderStatus Status { get; set; } = CodOrderStatus.PendingConfirmation;
    public FraudRiskLevel RiskLevel { get; set; } = FraudRiskLevel.Low;
    public int RiskScore { get; set; } = 0; // 0 to 100
    public string FraudFlagsJson { get; set; } = "[]";
    public ConfirmationChannel? ConfirmationChannel { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public string? Notes { get; set; }
    public bool RequiresPhoneCall { get; set; } = false;
    public string? ConfirmedVia { get; set; }
    public DateTime? InteractiveConfirmationSentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CodOrder Create(
        Guid tenantId,
        string orderNumber,
        string customerName,
        string phoneNumber,
        string shippingAddress,
        string governorateOrWilaya,
        string delegationOrCommune,
        decimal totalAmount,
        string countryCode = "TN",
        string codCurrency = "TND",
        decimal shippingFee = 7.0m,
        string? alternatePhone = null,
        string itemsJson = "[]",
        string? notes = null)
    {
        return new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = orderNumber.Trim(),
            CustomerName = customerName.Trim(),
            PhoneNumber = phoneNumber.Trim(),
            AlternatePhone = alternatePhone?.Trim(),
            ShippingAddress = shippingAddress.Trim(),
            GovernorateOrWilaya = governorateOrWilaya.Trim(),
            DelegationOrCommune = delegationOrCommune.Trim(),
            CountryCode = countryCode.ToUpperInvariant(),
            CodCurrency = codCurrency.ToUpperInvariant(),
            TotalAmount = totalAmount,
            ShippingFee = shippingFee,
            Status = CodOrderStatus.PendingConfirmation,
            RiskLevel = FraudRiskLevel.Low,
            RiskScore = 0,
            FraudFlagsJson = "[]",
            ItemsJson = itemsJson,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class OrderCollisionLock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public DateTime LockedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc || !IsActive;
}

public class CallOutcomeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public CallOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime? ScheduledCallbackUtc { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CourierProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty; // Aramex, Bosta, Yalidine, Mylerz, Express Courrier, Livri
    public string ApiCode { get; set; } = string.Empty;
    public string SupportedCountries { get; set; } = "TN";
    public string ApiEndpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ShipmentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CourierProviderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string AwbBarcode { get; set; } = string.Empty;
    public string LabelPdfUrl { get; set; } = string.Empty;
    public CourierShipmentStatus Status { get; set; } = CourierShipmentStatus.Created;
    public decimal CashToCollect { get; set; }
    public decimal CashRemitted { get; set; }
    public DateTime DispatchedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAtUtc { get; set; }
}

public class ShippingManifest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CourierProviderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string ManifestNumber { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalCashAmount { get; set; }
    public decimal ExpectedFreightCost { get; set; }
    public string Status { get; set; } = "PendingPickup"; // PendingPickup, Dispatched, Settled, Generated, HandedOverToCourier
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? HandedOverAtUtc { get; set; }
    public string OrderIdsJson { get; set; } = "[]";
}

public class BlacklistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Type { get; set; } = "Phone"; // Phone, IpAddress, Address
    public string Value { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
