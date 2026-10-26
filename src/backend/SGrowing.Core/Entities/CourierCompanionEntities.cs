namespace SGrowing.Core.Entities;

public class DriverMobileSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = "Android PWA";
    public double LastGpsLatitude { get; set; }
    public double LastGpsLongitude { get; set; }
    public int BatteryLevelPercent { get; set; } = 100;
    public bool IsOnline { get; set; } = true;
    public DateTime LastSyncAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DriverMobileSession Create(
        Guid tenantId,
        Guid driverId,
        string driverName,
        string deviceModel = "Android PWA")
    {
        return new DriverMobileSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DriverId = driverId,
            DriverName = driverName.Trim(),
            SessionToken = $"drv_sess_{Guid.NewGuid():N}",
            DeviceModel = deviceModel.Trim(),
            IsOnline = true,
            LastSyncAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class DoorstepProofOfDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RunSheetStopId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid DriverId { get; set; }
    public string CustomerSignatureBase64 { get; set; } = string.Empty;
    public string ParcelPhotoUrl { get; set; } = string.Empty;
    public decimal CashCollected { get; set; }
    public string PaymentMethod { get; set; } = "CashOnDelivery"; // CashOnDelivery, MobileWallet, Flouci, BaridiMob
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientRelation { get; set; } = "Self"; // Self, FamilyMember, Concierge, Colleague
    public double GpsLatitude { get; set; }
    public double GpsLongitude { get; set; }
    public DateTime DeliveredAtUtc { get; set; } = DateTime.UtcNow;

    public static DoorstepProofOfDelivery Create(
        Guid tenantId,
        Guid runSheetStopId,
        Guid orderId,
        string orderNumber,
        Guid driverId,
        string signatureBase64,
        string photoUrl,
        decimal cashCollected,
        string recipientName,
        string recipientRelation = "Self",
        double lat = 36.8065,
        double lng = 10.1815,
        string paymentMethod = "CashOnDelivery")
    {
        return new DoorstepProofOfDelivery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RunSheetStopId = runSheetStopId,
            OrderId = orderId,
            OrderNumber = orderNumber.Trim(),
            DriverId = driverId,
            CustomerSignatureBase64 = signatureBase64,
            ParcelPhotoUrl = photoUrl,
            CashCollected = cashCollected,
            RecipientName = recipientName.Trim(),
            RecipientRelation = recipientRelation.Trim(),
            GpsLatitude = lat,
            GpsLongitude = lng,
            PaymentMethod = paymentMethod,
            DeliveredAtUtc = DateTime.UtcNow
        };
    }
}
