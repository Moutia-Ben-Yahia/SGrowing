using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class DynamicTenantQueryFilterTests
{
    [Fact]
    public async Task DynamicQueryFilter_FiltersCodOrdersAndShipmentRecords_ByCurrentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"SGrowing_DynamicRls_{Guid.NewGuid()}")
            .Options;

        var contextA = new TenantContext();
        contextA.SetTenant(tenantA);

        // 1. Seed data across two tenants
        await using (var seedDb = new SGrowingDbContext(dbOptions, null))
        {
            seedDb.CodOrders.AddRange(
                new CodOrder { Id = Guid.NewGuid(), TenantId = tenantA, OrderNumber = "ORD-A-001", PhoneNumber = "+21699001122", TotalAmount = 120m },
                new CodOrder { Id = Guid.NewGuid(), TenantId = tenantB, OrderNumber = "ORD-B-002", PhoneNumber = "+21688112233", TotalAmount = 250m }
            );

            seedDb.ShipmentRecords.AddRange(
                new ShipmentRecord { Id = Guid.NewGuid(), TenantId = tenantA, TrackingNumber = "TRK-TENANT-A-01", Status = CourierShipmentStatus.InTransit },
                new ShipmentRecord { Id = Guid.NewGuid(), TenantId = tenantB, TrackingNumber = "TRK-TENANT-B-01", Status = CourierShipmentStatus.Delivered }
            );

            await seedDb.SaveChangesAsync();
        }

        // 2. Query as Tenant A
        await using (var queryDbA = new SGrowingDbContext(dbOptions, contextA))
        {
            var orders = await queryDbA.CodOrders.ToListAsync();
            var shipments = await queryDbA.ShipmentRecords.ToListAsync();

            Assert.Single(orders);
            Assert.Equal(tenantA, orders[0].TenantId);
            Assert.Equal("ORD-A-001", orders[0].OrderNumber);
            Assert.Equal("+21699001122", orders[0].PhoneNumber);

            Assert.Single(shipments);
            Assert.Equal(tenantA, shipments[0].TenantId);
            Assert.Equal("TRK-TENANT-A-01", shipments[0].TrackingNumber);
        }

        // 3. Query as Unauthenticated / Admin bypass (null tenant context)
        await using (var adminDb = new SGrowingDbContext(dbOptions, null))
        {
            var orders = await adminDb.CodOrders.ToListAsync();
            var shipments = await adminDb.ShipmentRecords.ToListAsync();

            Assert.Equal(2, orders.Count);
            Assert.Equal(2, shipments.Count);
        }
    }
}
