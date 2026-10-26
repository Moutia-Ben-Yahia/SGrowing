using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public record DriverCashSummaryDto(
    Guid DriverId,
    string DriverName,
    decimal TotalCashInHand,
    int CompletedStopsCount,
    int PendingStopsCount,
    DateTime LastUpdatedUtc
);

public interface ICourierCompanionService
{
    Task<DriverMobileSession> StartDriverSessionAsync(Guid tenantId, Guid driverId, string driverName, string deviceModel = "Android PWA");
    Task<DriverRunSheet?> GetActiveRunSheetAsync(Guid tenantId, Guid driverId);
    Task<List<RunSheetStop>> GetRunSheetStopsAsync(Guid tenantId, Guid runSheetId);
    Task<DoorstepProofOfDelivery> SubmitDoorstepProofOfDeliveryAsync(
        Guid tenantId,
        Guid stopId,
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
        string paymentMethod = "CashOnDelivery");
    Task<DriverCashSummaryDto> GetDriverCashSummaryAsync(Guid tenantId, Guid driverId);
    Task<bool> PingDriverGpsHeartbeatAsync(Guid tenantId, Guid driverId, double lat, double lng, int battery);
}

public class CourierCompanionService : ICourierCompanionService
{
    private readonly SGrowingDbContext _db;

    public CourierCompanionService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<DriverMobileSession> StartDriverSessionAsync(Guid tenantId, Guid driverId, string driverName, string deviceModel = "Android PWA")
    {
        var session = DriverMobileSession.Create(tenantId, driverId, driverName, deviceModel);
        _db.DriverMobileSessions.Add(session);

        // Update driver status to OnRoute
        var driver = await _db.DeliveryDrivers.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == driverId);
        if (driver != null)
        {
            driver.Status = DriverStatus.OnRoute;
        }

        SGrowingTelemetry.ActiveCourierDriversCounter.Add(1);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<DriverRunSheet?> GetActiveRunSheetAsync(Guid tenantId, Guid driverId)
    {
        return await _db.DriverRunSheets
            .Where(r => r.TenantId == tenantId && r.DriverId == driverId && r.Status != RunSheetStatus.Completed)
            .OrderByDescending(r => r.DateUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<List<RunSheetStop>> GetRunSheetStopsAsync(Guid tenantId, Guid runSheetId)
    {
        return await _db.RunSheetStops
            .Where(s => s.TenantId == tenantId && s.RunSheetId == runSheetId)
            .OrderBy(s => s.StopSequence)
            .ToListAsync();
    }

    public async Task<DoorstepProofOfDelivery> SubmitDoorstepProofOfDeliveryAsync(
        Guid tenantId,
        Guid stopId,
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
        var pod = DoorstepProofOfDelivery.Create(
            tenantId,
            stopId,
            orderId,
            orderNumber,
            driverId,
            signatureBase64,
            photoUrl,
            cashCollected,
            recipientName,
            recipientRelation,
            lat,
            lng,
            paymentMethod
        );

        _db.DoorstepProofOfDeliveries.Add(pod);

        // Update stop status
        var stop = await _db.RunSheetStops.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == stopId);
        if (stop != null)
        {
            stop.Status = paymentMethod == "CashOnDelivery" 
                ? StopDeliveryStatus.DeliveredPaidCash 
                : StopDeliveryStatus.DeliveredPaidOnline;
            stop.ActualPaymentCollected = cashCollected;
            stop.ProofOfDeliveryId = pod.Id;
            stop.CompletedAtUtc = DateTime.UtcNow;
        }

        // Update driver cash in hand
        var driver = await _db.DeliveryDrivers.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == driverId);
        if (driver != null)
        {
            driver.CashInHandBalance += cashCollected;
            driver.TotalDeliveriesCompleted += 1;
        }

        // Update run sheet totals
        if (stop != null)
        {
            var sheet = await _db.DriverRunSheets.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == stop.RunSheetId);
            if (sheet != null)
            {
                sheet.DeliveredParcels += 1;
                sheet.TotalCashActuallyCollected += cashCollected;
            }
        }

        SGrowingTelemetry.DeliveriesCompletedCounter.Add(1);
        await _db.SaveChangesAsync();
        return pod;
    }

    public async Task<DriverCashSummaryDto> GetDriverCashSummaryAsync(Guid tenantId, Guid driverId)
    {
        var driver = await _db.DeliveryDrivers.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == driverId);
        var activeSheet = await GetActiveRunSheetAsync(tenantId, driverId);

        int completed = 0;
        int pending = 0;
        if (activeSheet != null)
        {
            completed = activeSheet.DeliveredParcels;
            pending = Math.Max(0, activeSheet.TotalParcels - activeSheet.DeliveredParcels - activeSheet.RefusedParcels);
        }

        return new DriverCashSummaryDto(
            driverId,
            driver?.FullName ?? "Courier Driver",
            driver?.CashInHandBalance ?? 0m,
            completed,
            pending,
            DateTime.UtcNow
        );
    }

    public async Task<bool> PingDriverGpsHeartbeatAsync(Guid tenantId, Guid driverId, double lat, double lng, int battery)
    {
        var session = await _db.DriverMobileSessions
            .Where(s => s.TenantId == tenantId && s.DriverId == driverId && s.IsOnline)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (session != null)
        {
            session.LastGpsLatitude = lat;
            session.LastGpsLongitude = lng;
            session.BatteryLevelPercent = battery;
            session.LastSyncAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        return false;
    }
}
