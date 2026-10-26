namespace SGrowing.Core.Entities;

public enum DriverStatus
{
    Available,
    OnRoute,
    OffDuty
}

public enum StopDeliveryStatus
{
    Scheduled,
    ArrivedAtLocation,
    DeliveredPaidCash,
    DeliveredPaidOnline,
    CustomerRescheduled,
    DoorstepRefused
}

public enum RunSheetStatus
{
    Draft,
    Dispatched,
    Completed
}

public class DeliveryDriver
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string VehicleType { get; set; } = "Van"; // Motorcycle, Van, Car
    public string LicensePlate { get; set; } = string.Empty;
    public Guid HubWarehouseId { get; set; }
    public DriverStatus Status { get; set; } = DriverStatus.Available;
    public decimal CashInHandBalance { get; set; } = 0m;
    public int TotalDeliveriesCompleted { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static DeliveryDriver Create(
        Guid tenantId,
        string fullName,
        string phone,
        string vehicleType,
        string licensePlate,
        Guid hubWarehouseId)
    {
        return new DeliveryDriver
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FullName = fullName.Trim(),
            PhoneNumber = phone.Trim(),
            VehicleType = vehicleType.Trim(),
            LicensePlate = licensePlate.Trim(),
            HubWarehouseId = hubWarehouseId,
            Status = DriverStatus.Available,
            CashInHandBalance = 0m,
            TotalDeliveriesCompleted = 0,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class DriverRunSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DriverId { get; set; }
    public Guid HubWarehouseId { get; set; }
    public string SheetNumber { get; set; } = string.Empty;
    public DateTime DateUtc { get; set; } = DateTime.UtcNow.Date;
    public RunSheetStatus Status { get; set; } = RunSheetStatus.Draft;
    public int TotalParcels { get; set; }
    public int DeliveredParcels { get; set; }
    public int RefusedParcels { get; set; }
    public decimal TotalCashToCollect { get; set; }
    public decimal TotalCashActuallyCollected { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class RunSheetStop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RunSheetId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int StopSequence { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;
    public string Delegation { get; set; } = string.Empty;
    public decimal AmountToCollect { get; set; }
    public string PaymentMethodAtDoorstep { get; set; } = "CashOnDelivery"; // CashOnDelivery, FlouciQr, D17
    public StopDeliveryStatus Status { get; set; } = StopDeliveryStatus.Scheduled;
    public string? FailureReason { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
}

public class ProofOfDeliveryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RunSheetStopId { get; set; }
    public string CustomerSignatureData { get; set; } = string.Empty; // Base64 or vector path
    public string DoorstepPhotoUrl { get; set; } = string.Empty;
    public double? GeoLatitude { get; set; }
    public double? GeoLongitude { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DriverCashReconciliation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid RunSheetId { get; set; }
    public Guid DriverId { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ActualCashTurnedIn { get; set; }
    public decimal DiscrepancyAmount => ActualCashTurnedIn - ExpectedCash;
    public bool IsDiscrepancyZero => DiscrepancyAmount == 0m;
    public string Notes { get; set; } = string.Empty;
    public string ReconciledBy { get; set; } = string.Empty;
    public DateTime ReconciledAtUtc { get; set; } = DateTime.UtcNow;
}
