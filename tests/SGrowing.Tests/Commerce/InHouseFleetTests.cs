using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class InHouseFleetTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"InHouseFleetDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task InHouseFleet_RunSheet_DoorstepDelivery_And_CashReconciliation_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new InHouseFleetService(db);
        var tenantId = Guid.NewGuid();
        var hubId = Guid.NewGuid();

        // 1. Register Delivery Driver
        var driver = DeliveryDriver.Create(
            tenantId,
            fullName: "Kais Ben Ali",
            phone: "+21698223344",
            vehicleType: "Peugeot Partner Van",
            licensePlate: "185-TN-4921",
            hubWarehouseId: hubId);
        await service.RegisterDriverAsync(tenantId, driver);
        Assert.Equal(DriverStatus.Available, driver.Status);

        // 2. Create 3 Orders for Greater Tunis
        var order1 = CodOrder.Create(tenantId, "ORD-FLT-01", "Amine Jendoubi", "+21620111222", "Avenue Habib Bourguiba", "Tunis", "Centre Ville", 120m);
        var order2 = CodOrder.Create(tenantId, "ORD-FLT-02", "Salma Khemir", "+21622333444", "Route de La Marsa", "Tunis", "Marsa", 150m);
        var order3 = CodOrder.Create(tenantId, "ORD-FLT-03", "Nabil Dridi", "+21625444555", "Ennasr 2", "Ariana", "Ennasr", 80m);
        db.CodOrders.AddRange(order1, order2, order3);
        await db.SaveChangesAsync();

        // 3. Generate Driver Run-Sheet
        var runSheet = await service.GenerateRunSheetAsync(tenantId, driver.Id, hubId, new List<Guid> { order1.Id, order2.Id, order3.Id });
        Assert.Equal(3, runSheet.TotalParcels);
        Assert.Equal(350m, runSheet.TotalCashToCollect);
        Assert.Equal(DriverStatus.OnRoute, driver.Status);

        var stops = await db.RunSheetStops.Where(s => s.RunSheetId == runSheet.Id).OrderBy(s => s.StopSequence).ToListAsync();
        Assert.Equal(3, stops.Count);

        // 4. Stop 1: Delivered & Paid Cash (120 TND) with signature
        var stop1 = await service.RecordStopDeliveryAsync(
            tenantId,
            stops[0].Id,
            StopDeliveryStatus.DeliveredPaidCash,
            paymentMethod: "CashOnDelivery",
            signatureData: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUg...",
            photoUrl: "https://sgrowing.blob/pod/stop1.jpg",
            lat: 36.8065,
            lon: 10.1815);

        Assert.Equal(StopDeliveryStatus.DeliveredPaidCash, stop1.Status);
        Assert.Equal(CodOrderStatus.Delivered, order1.Status);
        Assert.Equal(120m, driver.CashInHandBalance);

        // 5. Stop 2: Delivered & Paid Online QR (150 TND, no cash added to pocket)
        var stop2 = await service.RecordStopDeliveryAsync(
            tenantId,
            stops[1].Id,
            StopDeliveryStatus.DeliveredPaidOnline,
            paymentMethod: "FlouciQr");

        Assert.Equal(StopDeliveryStatus.DeliveredPaidOnline, stop2.Status);
        Assert.Equal(CodOrderStatus.Delivered, order2.Status);
        Assert.Equal(120m, driver.CashInHandBalance); // Only cash increases pocket balance

        // 6. Stop 3: Doorstep Refused (80 TND refused)
        var stop3 = await service.RecordStopDeliveryAsync(
            tenantId,
            stops[2].Id,
            StopDeliveryStatus.DoorstepRefused,
            failureReason: "Customer changed mind, refused payment");

        Assert.Equal(StopDeliveryStatus.DoorstepRefused, stop3.Status);
        Assert.Equal(CodOrderStatus.Returned, order3.Status);

        // 7. End of Day: Driver turns in 120 TND collected cash
        var reconciliation = await service.ReconcileDriverCashAsync(
            tenantId,
            runSheet.Id,
            actualCashTurnedIn: 120m,
            reconciledBy: "Hub Manager Tunis",
            notes: "All cash balanced perfectly.");

        Assert.Equal(120m, reconciliation.ExpectedCash);
        Assert.Equal(120m, reconciliation.ActualCashTurnedIn);
        Assert.True(reconciliation.IsDiscrepancyZero);
        Assert.Equal(0m, reconciliation.DiscrepancyAmount);
        Assert.Equal(RunSheetStatus.Completed, runSheet.Status);
        Assert.Equal(DriverStatus.Available, driver.Status);
        Assert.Equal(0m, driver.CashInHandBalance);
    }
}
