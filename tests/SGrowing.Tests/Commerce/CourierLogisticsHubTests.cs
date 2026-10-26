using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CourierLogisticsHubTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"LogisticsTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task DispatchOrder_And_GenerateManifest_Should_Create_Awb_And_Bordereau()
    {
        var db = CreateDbContext();
        var hub = new CourierLogisticsHubService(db);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider
        {
            TenantId = tenantId,
            Name = "Express Courrier",
            ApiCode = "EXP",
            SupportedCountries = "TN",
            ApiEndpoint = "https://api.expresscourrier.tn",
            IsActive = true
        };
        db.CourierProviders.Add(courier);

        var order1 = CodOrder.Create(tenantId, "ORD-301", "Youssef Dridi", "+21620111222", "Centre Urbain Nord", "Tunis", "El Menzah", 150m, shippingFee: 8m);
        var order2 = CodOrder.Create(tenantId, "ORD-302", "Salma Trabelsi", "+21622333444", "Avenue de l'Environnement", "Nabeul", "Hammamet", 220m, shippingFee: 8m);
        db.CodOrders.AddRange(order1, order2);
        await db.SaveChangesAsync();

        // Dispatch order 1
        var shipment1 = await hub.DispatchOrderAsync(tenantId, order1.Id, courier.Id);
        Assert.NotNull(shipment1.TrackingNumber);
        Assert.StartsWith("EXP-ORD-301", shipment1.TrackingNumber);
        Assert.StartsWith("*AWB", shipment1.AwbBarcode);
        Assert.Equal(158m, shipment1.CashToCollect); // 150 + 8
        Assert.Equal(CodOrderStatus.Dispatched, order1.Status);

        // Dispatch order 2
        var shipment2 = await hub.DispatchOrderAsync(tenantId, order2.Id, courier.Id);
        Assert.Equal(228m, shipment2.CashToCollect); // 220 + 8

        // Generate Bordereau Manifest
        var manifest = await hub.GenerateBordereauManifestAsync(tenantId, courier.Id, new List<Guid> { order1.Id, order2.Id });
        Assert.Equal(2, manifest.OrderCount);
        Assert.Equal(386m, manifest.TotalCashAmount); // 158 + 228
        Assert.StartsWith("MAN-EXP", manifest.ManifestNumber);
    }

    [Fact]
    public async Task ProcessTrackingWebhook_Should_Handle_Delivery_And_Return_State()
    {
        var db = CreateDbContext();
        var hub = new CourierLogisticsHubService(db);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider { TenantId = tenantId, Name = "Aramex TN", ApiCode = "ARX", IsActive = true };
        db.CourierProviders.Add(courier);

        var order = CodOrder.Create(tenantId, "ORD-401", "Nabil Khemir", "+21699887766", "Avenue Habib Bourguiba", "Bizerte", "Bizerte Nord", 100m, shippingFee: 7m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var shipment = await hub.DispatchOrderAsync(tenantId, order.Id, courier.Id);

        // Webhook: Out for delivery
        var updated1 = await hub.ProcessTrackingWebhookAsync(tenantId, shipment.TrackingNumber, CourierShipmentStatus.OutForDelivery);
        Assert.Equal(CourierShipmentStatus.OutForDelivery, updated1.Status);

        // Webhook: Delivered with cash collected
        var updated2 = await hub.ProcessTrackingWebhookAsync(tenantId, shipment.TrackingNumber, CourierShipmentStatus.Delivered, cashRemitted: 107m);
        Assert.Equal(CourierShipmentStatus.Delivered, updated2.Status);
        Assert.Equal(107m, updated2.CashRemitted);
        Assert.NotNull(updated2.DeliveredAtUtc);
        Assert.Equal(CodOrderStatus.Delivered, order.Status);
    }
}
