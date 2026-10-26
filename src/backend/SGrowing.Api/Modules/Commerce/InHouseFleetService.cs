using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IInHouseFleetService
{
    Task<DeliveryDriver> RegisterDriverAsync(Guid tenantId, DeliveryDriver driver);
    Task<DriverRunSheet> GenerateRunSheetAsync(Guid tenantId, Guid driverId, Guid hubWarehouseId, List<Guid> orderIds);
    Task<RunSheetStop> RecordStopDeliveryAsync(
        Guid tenantId,
        Guid stopId,
        StopDeliveryStatus status,
        string paymentMethod = "CashOnDelivery",
        string? failureReason = null,
        string signatureData = "",
        string photoUrl = "",
        double? lat = null,
        double? lon = null);
    Task<DriverCashReconciliation> ReconcileDriverCashAsync(
        Guid tenantId,
        Guid runSheetId,
        decimal actualCashTurnedIn,
        string reconciledBy,
        string notes = "");
    Task<DriverRunSheet?> GetDriverRunSheetAsync(Guid tenantId, Guid runSheetId);
    Task<List<DeliveryDriver>> ListDriversAsync(Guid tenantId);
}

public class InHouseFleetService : IInHouseFleetService
{
    private readonly SGrowingDbContext _db;

    public InHouseFleetService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<DeliveryDriver> RegisterDriverAsync(Guid tenantId, DeliveryDriver driver)
    {
        driver.TenantId = tenantId;
        _db.DeliveryDrivers.Add(driver);
        await _db.SaveChangesAsync();
        return driver;
    }

    public async Task<DriverRunSheet> GenerateRunSheetAsync(
        Guid tenantId,
        Guid driverId,
        Guid hubWarehouseId,
        List<Guid> orderIds)
    {
        var driver = await _db.DeliveryDrivers
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == driverId);

        if (driver == null)
            throw new InvalidOperationException($"Driver {driverId} not found.");

        var orders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId && orderIds.Contains(o.Id))
            .ToListAsync();

        var runSheetId = Guid.NewGuid();
        var sheetNumber = $"RUN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}";
        decimal totalCash = orders.Sum(o => o.TotalAmount);

        var runSheet = new DriverRunSheet
        {
            Id = runSheetId,
            TenantId = tenantId,
            DriverId = driverId,
            HubWarehouseId = hubWarehouseId,
            SheetNumber = sheetNumber,
            DateUtc = DateTime.UtcNow.Date,
            Status = RunSheetStatus.Dispatched,
            TotalParcels = orders.Count,
            TotalCashToCollect = totalCash,
            TotalCashActuallyCollected = 0m,
            StartedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        driver.Status = DriverStatus.OnRoute;

        int sequence = 1;
        foreach (var order in orders)
        {
            var stop = new RunSheetStop
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RunSheetId = runSheetId,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                StopSequence = sequence++,
                CustomerName = order.CustomerName,
                CustomerPhone = order.PhoneNumber,
                ShippingAddress = order.ShippingAddress,
                Governorate = order.GovernorateOrWilaya,
                Delegation = order.DelegationOrCommune,
                AmountToCollect = order.TotalAmount,
                PaymentMethodAtDoorstep = "CashOnDelivery",
                Status = StopDeliveryStatus.Scheduled
            };

            order.Status = CodOrderStatus.Dispatched;
            _db.RunSheetStops.Add(stop);
        }

        _db.DriverRunSheets.Add(runSheet);
        await _db.SaveChangesAsync();

        return runSheet;
    }

    public async Task<RunSheetStop> RecordStopDeliveryAsync(
        Guid tenantId,
        Guid stopId,
        StopDeliveryStatus status,
        string paymentMethod = "CashOnDelivery",
        string? failureReason = null,
        string signatureData = "",
        string photoUrl = "",
        double? lat = null,
        double? lon = null)
    {
        var stop = await _db.RunSheetStops
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == stopId);

        if (stop == null)
            throw new InvalidOperationException($"Stop {stopId} not found.");

        var runSheet = await _db.DriverRunSheets
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == stop.RunSheetId);

        var order = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == stop.OrderId);

        var driver = runSheet != null
            ? await _db.DeliveryDrivers.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == runSheet.DriverId)
            : null;

        stop.Status = status;
        stop.PaymentMethodAtDoorstep = paymentMethod;
        stop.FailureReason = failureReason;
        stop.DeliveredAtUtc = DateTime.UtcNow;

        if (status is StopDeliveryStatus.DeliveredPaidCash or StopDeliveryStatus.DeliveredPaidOnline)
        {
            if (order != null) order.Status = CodOrderStatus.Delivered;
            if (runSheet != null)
            {
                runSheet.DeliveredParcels++;
                if (paymentMethod == "CashOnDelivery")
                {
                    runSheet.TotalCashActuallyCollected += stop.AmountToCollect;
                    if (driver != null) driver.CashInHandBalance += stop.AmountToCollect;
                }
            }
            if (driver != null) driver.TotalDeliveriesCompleted++;

            // Create Proof of Delivery
            var pod = new ProofOfDeliveryRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RunSheetStopId = stop.Id,
                CustomerSignatureData = signatureData,
                DoorstepPhotoUrl = photoUrl,
                GeoLatitude = lat,
                GeoLongitude = lon,
                CapturedAtUtc = DateTime.UtcNow
            };
            _db.ProofOfDeliveryRecords.Add(pod);
        }
        else if (status is StopDeliveryStatus.DoorstepRefused)
        {
            if (order != null) order.Status = CodOrderStatus.Returned;
            if (runSheet != null) runSheet.RefusedParcels++;
        }

        await _db.SaveChangesAsync();
        return stop;
    }

    public async Task<DriverCashReconciliation> ReconcileDriverCashAsync(
        Guid tenantId,
        Guid runSheetId,
        decimal actualCashTurnedIn,
        string reconciledBy,
        string notes = "")
    {
        var runSheet = await _db.DriverRunSheets
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runSheetId);

        if (runSheet == null)
            throw new InvalidOperationException($"RunSheet {runSheetId} not found.");

        var driver = await _db.DeliveryDrivers
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == runSheet.DriverId);

        decimal expected = runSheet.TotalCashActuallyCollected;

        var reconciliation = new DriverCashReconciliation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RunSheetId = runSheetId,
            DriverId = runSheet.DriverId,
            ExpectedCash = expected,
            ActualCashTurnedIn = actualCashTurnedIn,
            Notes = notes.Trim(),
            ReconciledBy = reconciledBy.Trim(),
            ReconciledAtUtc = DateTime.UtcNow
        };

        runSheet.Status = RunSheetStatus.Completed;
        runSheet.CompletedAtUtc = DateTime.UtcNow;

        if (driver != null)
        {
            driver.Status = DriverStatus.Available;
            driver.CashInHandBalance = Math.Max(0m, driver.CashInHandBalance - actualCashTurnedIn);
        }

        _db.DriverCashReconciliations.Add(reconciliation);
        await _db.SaveChangesAsync();

        return reconciliation;
    }

    public async Task<DriverRunSheet?> GetDriverRunSheetAsync(Guid tenantId, Guid runSheetId)
    {
        return await _db.DriverRunSheets
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == runSheetId);
    }

    public async Task<List<DeliveryDriver>> ListDriversAsync(Guid tenantId)
    {
        return await _db.DeliveryDrivers
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .OrderBy(d => d.FullName)
            .ToListAsync();
    }
}
