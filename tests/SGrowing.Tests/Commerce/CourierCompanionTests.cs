using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CourierCompanionTests
{
    private readonly SGrowingDbContext _db;
    private readonly CourierCompanionService _service;

    public CourierCompanionTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new CourierCompanionService(_db);
    }

    [Fact]
    public async Task StartDriverSession_ShouldRegisterActivePwaSessionAndTrackHeartbeat()
    {
        var tenantId = Guid.NewGuid();
        var driver = DeliveryDriver.Create(tenantId, "Walid Courier", "+21698765432", "Motorcycle", "215 TN 4821", Guid.NewGuid());
        _db.DeliveryDrivers.Add(driver);
        await _db.SaveChangesAsync();

        var session = await _service.StartDriverSessionAsync(tenantId, driver.Id, driver.FullName, "Xiaomi Redmi Note PWA");

        Assert.NotNull(session);
        Assert.Equal(driver.Id, session.DriverId);
        Assert.True(session.IsOnline);
        Assert.StartsWith("drv_sess_", session.SessionToken);

        // Ping heartbeat
        var pingSuccess = await _service.PingDriverGpsHeartbeatAsync(tenantId, driver.Id, 36.8065, 10.1815, 85);
        Assert.True(pingSuccess);

        var updated = await _db.DriverMobileSessions.FindAsync(session.Id);
        Assert.NotNull(updated);
        Assert.Equal(85, updated.BatteryLevelPercent);
        Assert.Equal(36.8065, updated.LastGpsLatitude);
    }

    [Fact]
    public async Task SubmitDoorstepProofOfDelivery_ShouldRecordSignatureAndCashInHand()
    {
        var tenantId = Guid.NewGuid();
        var driver = DeliveryDriver.Create(tenantId, "Firas Express", "+21622334455", "Van", "198 TN 9032", Guid.NewGuid());
        _db.DeliveryDrivers.Add(driver);

        var runSheet = new DriverRunSheet
        {
            TenantId = tenantId,
            DriverId = driver.Id,
            HubWarehouseId = Guid.NewGuid(),
            SheetNumber = "RS-20261026-01",
            DateUtc = DateTime.UtcNow,
            TotalParcels = 1,
            TotalCashToCollect = 125.50m
        };
        _db.DriverRunSheets.Add(runSheet);

        var stop = new RunSheetStop
        {
            TenantId = tenantId,
            RunSheetId = runSheet.Id,
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-88192",
            StopSequence = 1,
            CustomerName = "Amira Ben Salem",
            CustomerPhone = "+21699887766",
            ShippingAddress = "15 Rue Habib Bourguiba, Marsa",
            AmountToCollect = 125.50m
        };
        _db.RunSheetStops.Add(stop);
        runSheet.TotalParcels = 1;
        runSheet.TotalCashToCollect = 125.50m;
        await _db.SaveChangesAsync();

        var pod = await _service.SubmitDoorstepProofOfDeliveryAsync(
            tenantId,
            stop.Id,
            stop.OrderId,
            stop.OrderNumber,
            driver.Id,
            signatureBase64: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...",
            photoUrl: "https://storage.sgrowing.net/pod/photo-stop-1.jpg",
            cashCollected: 125.50m,
            recipientName: "Amira Ben Salem",
            recipientRelation: "Self",
            lat: 36.8782,
            lng: 10.3247,
            paymentMethod: "CashOnDelivery"
        );

        Assert.NotNull(pod);
        Assert.Equal(125.50m, pod.CashCollected);
        Assert.Equal("Amira Ben Salem", pod.RecipientName);

        // Verify driver cash drawer
        var updatedDriver = await _db.DeliveryDrivers.FindAsync(driver.Id);
        Assert.NotNull(updatedDriver);
        Assert.Equal(125.50m, updatedDriver.CashInHandBalance);
        Assert.Equal(1, updatedDriver.TotalDeliveriesCompleted);

        // Verify summary
        var summary = await _service.GetDriverCashSummaryAsync(tenantId, driver.Id);
        Assert.Equal(125.50m, summary.TotalCashInHand);
        Assert.Equal(1, summary.CompletedStopsCount);
        Assert.Equal(0, summary.PendingStopsCount);
    }
}
