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

    [Fact]
    public async Task DynamicQueryFilter_FiltersContacts_ByCurrentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"SGrowing_ContactsRls_{Guid.NewGuid()}")
            .Options;

        var contextA = new TenantContext();
        contextA.SetTenant(tenantA);

        var contextB = new TenantContext();
        contextB.SetTenant(tenantB);

        await using (var seedDb = new SGrowingDbContext(dbOptions, null))
        {
            seedDb.Contacts.AddRange(
                Contact.Create(tenantA, "Alice", "Smith", "alice@a.com"),
                Contact.Create(tenantA, "Bob", "Smith", "bob@a.com"),
                Contact.Create(tenantB, "Charlie", "Brown", "charlie@b.com")
            );
            await seedDb.SaveChangesAsync();
        }

        // Tenant A sees 2 contacts
        await using (var queryDbA = new SGrowingDbContext(dbOptions, contextA))
        {
            var contactsA = await queryDbA.Contacts.ToListAsync();
            Assert.Equal(2, contactsA.Count);
            Assert.All(contactsA, c => Assert.Equal(tenantA, c.TenantId));
        }

        // Tenant B sees 1 contact
        await using (var queryDbB = new SGrowingDbContext(dbOptions, contextB))
        {
            var contactsB = await queryDbB.Contacts.ToListAsync();
            Assert.Single(contactsB);
            Assert.Equal(tenantB, contactsB[0].TenantId);
            Assert.Equal("Charlie", contactsB[0].FirstName);
        }
    }

    [Fact]
    public async Task DynamicQueryFilter_IgnoreQueryFilters_ReturnsAllEntitiesAcrossAllTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"SGrowing_IgnoreFilters_{Guid.NewGuid()}")
            .Options;

        var contextA = new TenantContext();
        contextA.SetTenant(tenantA);

        await using (var seedDb = new SGrowingDbContext(dbOptions, null))
        {
            seedDb.Contacts.AddRange(
                Contact.Create(tenantA, "Alice", "TenantA", "alice@a.com"),
                Contact.Create(tenantB, "Zoe", "TenantB", "zoe@b.com")
            );
            await seedDb.SaveChangesAsync();
        }

        await using (var queryDbA = new SGrowingDbContext(dbOptions, contextA))
        {
            // Normal query scoped to Tenant A
            var scoped = await queryDbA.Contacts.ToListAsync();
            Assert.Single(scoped);

            // Bypassed query returns both
            var all = await queryDbA.Contacts.IgnoreQueryFilters().ToListAsync();
            Assert.Equal(2, all.Count);
        }
    }

    [Fact]
    public async Task DynamicQueryFilter_TenantContextSwitching_UpdatesSubsequentQueries()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var dbOptions = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"SGrowing_SwitchingRls_{Guid.NewGuid()}")
            .Options;

        var dynamicContext = new TenantContext();
        dynamicContext.SetTenant(tenantA);

        await using (var seedDb = new SGrowingDbContext(dbOptions, null))
        {
            seedDb.Contacts.AddRange(
                Contact.Create(tenantA, "AlphaUser", "Corp", "alpha@corp.com"),
                Contact.Create(tenantB, "BetaUser", "Corp", "beta@corp.com")
            );
            await seedDb.SaveChangesAsync();
        }

        await using var queryDb = new SGrowingDbContext(dbOptions, dynamicContext);

        // First query scoped to Tenant A
        var initial = await queryDb.Contacts.ToListAsync();
        Assert.Single(initial);
        Assert.Equal("AlphaUser", initial[0].FirstName);

        // Switch context to Tenant B
        dynamicContext.SetTenant(tenantB);

        var subsequent = await queryDb.Contacts.ToListAsync();
        Assert.Single(subsequent);
        Assert.Equal("BetaUser", subsequent[0].FirstName);
    }

    [Fact]
    public async Task DynamicQueryFilter_PreventsCrossTenantQueryById()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var contactB = Contact.Create(tenantB, "SecretContact", "B", "secret@b.com");

        var dbOptions = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"SGrowing_CrossTenant_{Guid.NewGuid()}")
            .Options;

        var contextA = new TenantContext();
        contextA.SetTenant(tenantA);

        await using (var seedDb = new SGrowingDbContext(dbOptions, null))
        {
            seedDb.Contacts.Add(contactB);
            await seedDb.SaveChangesAsync();
        }

        // Querying for Contact B's ID from Tenant A's context returns null
        await using (var queryDbA = new SGrowingDbContext(dbOptions, contextA))
        {
            var found = await queryDbA.Contacts.FirstOrDefaultAsync(c => c.Id == contactB.Id);
            Assert.Null(found);
        }
    }
}
