using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Architecture;

public class MultiTenantIsolationArchitectureTests
{
    private static SGrowingDbContext CreateDbContext(string dbName, ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public void TenantDataEntities_ImplementITenantScoped_OrHaveTenantIdProperty()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var entityTypes = db.Model.GetEntityTypes()
            .Where(e => e.BaseType == null && !e.IsOwned())
            .ToList();

        // System-level or root reference entities exempt from having a TenantId parent scope
        var exemptTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(Tenant),
            nameof(SystemRole),
            nameof(RolePermissionClaim),
            nameof(OutboxMessage),
            nameof(OutboxMessageEntity),
            nameof(AgentPersonaRecord),
            nameof(CurrencyRateRecord),
            nameof(WilayaZone),
            nameof(DevRateLimitBucketEntity)
        };

        var tenantDataEntities = entityTypes
            .Where(e => e.ClrType != null && e.ClrType.IsClass && !e.ClrType.IsAbstract && !exemptTypeNames.Contains(e.ClrType.Name))
            .ToList();

        tenantDataEntities.Should().NotBeEmpty("there must be tenant data entities configured in SGrowingDbContext");

        var nonCompliant = new List<string>();
        foreach (var entityType in tenantDataEntities)
        {
            var clrType = entityType.ClrType;
            var implementsITenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
            var hasTenantIdProp = clrType.GetProperty("TenantId") != null ||
                                  clrType.GetProperties().Any(p => p.Name.EndsWith("TenantId", StringComparison.OrdinalIgnoreCase)) ||
                                  entityType.FindProperty("TenantId") != null;
            var hasParentWithTenantScope = entityType.GetForeignKeys()
                .Any(fk => fk.PrincipalEntityType.FindProperty("TenantId") != null || typeof(ITenantScoped).IsAssignableFrom(fk.PrincipalEntityType.ClrType)) ||
                clrType.GetProperty("CadenceId") != null ||
                clrType.GetProperty("SubscriptionId") != null ||
                clrType.GetProperty("MissionId") != null;

            if (!implementsITenantScoped && !hasTenantIdProp && !hasParentWithTenantScope)
            {
                nonCompliant.Add(clrType.Name);
            }

            if (hasTenantIdProp)
            {
                var tenantIdProp = clrType.GetProperty("TenantId") ??
                                   clrType.GetProperties().FirstOrDefault(p => p.Name.EndsWith("TenantId", StringComparison.OrdinalIgnoreCase));
                tenantIdProp.Should().NotBeNull();
                var isValidType = tenantIdProp!.PropertyType == typeof(Guid) ||
                                  tenantIdProp.PropertyType == typeof(string) ||
                                  tenantIdProp.PropertyType == typeof(Guid?);
                isValidType.Should().BeTrue(
                    $"TenantId property on entity '{clrType.FullName}' must be Guid or string");
            }
        }

        nonCompliant.Should().BeEmpty(
            $"All tenant data entities must implement ITenantScoped, declare a TenantId property, or belong to a tenant-scoped aggregate root. Non-compliant: {string.Join(", ", nonCompliant)}");
    }

    [Fact]
    public void TenantScopedEntity_ImplementsITenantScoped()
    {
        typeof(ITenantScoped).IsAssignableFrom(typeof(TenantScopedEntity)).Should().BeTrue(
            "TenantScopedEntity base entity model must implement ITenantScoped interface");
    }

    [Fact]
    public async Task MultiTenantDbContext_EnforcesTenantBoundaryScoping_OnQueries()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var contextA = new TenantContext();
        contextA.SetTenant(tenantA);

        var contextB = new TenantContext();
        contextB.SetTenant(tenantB);

        // 1. Seed data for both tenants under unauthenticated/admin context
        await using (var seedDb = CreateDbContext(dbName, null))
        {
            seedDb.Contacts.AddRange(
                Contact.Create(tenantA, "Alice", "TenantA", "alice@tenant-a.com"),
                Contact.Create(tenantB, "Bob", "TenantB", "bob@tenant-b.com")
            );

            seedDb.CodOrders.AddRange(
                new CodOrder { Id = Guid.NewGuid(), TenantId = tenantA, OrderNumber = "ORD-A-1", PhoneNumber = "+21699111111", TotalAmount = 100m },
                new CodOrder { Id = Guid.NewGuid(), TenantId = tenantB, OrderNumber = "ORD-B-1", PhoneNumber = "+21699222222", TotalAmount = 200m }
            );

            await seedDb.SaveChangesAsync();
        }

        // 2. Query as Tenant A
        await using (var queryDbA = CreateDbContext(dbName, contextA))
        {
            var contactsA = await queryDbA.Contacts.ToListAsync();
            contactsA.Should().ContainSingle();
            contactsA[0].FirstName.Should().Be("Alice");
            contactsA[0].TenantId.Should().Be(tenantA);

            var ordersA = await queryDbA.CodOrders.ToListAsync();
            ordersA.Should().ContainSingle();
            ordersA[0].OrderNumber.Should().Be("ORD-A-1");
            ordersA[0].TenantId.Should().Be(tenantA);
        }

        // 3. Query as Tenant B
        await using (var queryDbB = CreateDbContext(dbName, contextB))
        {
            var contactsB = await queryDbB.Contacts.ToListAsync();
            contactsB.Should().ContainSingle();
            contactsB[0].FirstName.Should().Be("Bob");
            contactsB[0].TenantId.Should().Be(tenantB);

            var ordersB = await queryDbB.CodOrders.ToListAsync();
            ordersB.Should().ContainSingle();
            ordersB[0].OrderNumber.Should().Be("ORD-B-1");
            ordersB[0].TenantId.Should().Be(tenantB);
        }

        // 4. Admin query sees all
        await using (var adminDb = CreateDbContext(dbName, null))
        {
            var allContacts = await adminDb.Contacts.ToListAsync();
            allContacts.Should().HaveCount(2);

            var allOrders = await adminDb.CodOrders.ToListAsync();
            allOrders.Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task CrossTenantIsolation_QueryingMismatchedEntity_ReturnsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var bContactId = Guid.NewGuid();
        var bOrderId = Guid.NewGuid();

        // Seed data for Tenant B
        await using (var seedDb = CreateDbContext(dbName, null))
        {
            var bContact = Contact.Create(tenantB, "Alien", "Contact", "alien@tenant-b.com");
            bContactId = bContact.Id;
            seedDb.Contacts.Add(bContact);

            seedDb.CodOrders.Add(new CodOrder
            {
                Id = bOrderId,
                TenantId = tenantB,
                OrderNumber = "ORD-B-CONFIDENTIAL",
                PhoneNumber = "+21699887766",
                TotalAmount = 500m
            });

            await seedDb.SaveChangesAsync();
        }

        var contextA = new TenantContext();
        contextA.SetTenant(tenantA);

        // Attempt to query Tenant B's entities as Tenant A
        await using var queryDbA = CreateDbContext(dbName, contextA);

        var queriedContact = await queryDbA.Contacts.FirstOrDefaultAsync(c => c.Id == bContactId);
        queriedContact.Should().BeNull("Global tenant query filter must ensure cross-tenant queries return not found (null)");

        var queriedOrder = await queryDbA.CodOrders.FirstOrDefaultAsync(o => o.Id == bOrderId);
        queriedOrder.Should().BeNull("Tenant B order must not be visible to Tenant A");
    }

    [Fact]
    public async Task CrossTenantIsolation_MutatingMismatchedTenantEntity_ThrowsUnauthorizedAccessException()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var contextA = new TenantContext();
        contextA.SetTenant(tenantA);

        await using var dbA = CreateDbContext(dbName, contextA);

        // 1. Attempting to add an entity explicitly belonging to Tenant B on Tenant A's scoped DbContext
        var mismatchedContact = Contact.Create(tenantB, "Malicious", "Infiltrator", "infiltrator@tenant-b.com");
        dbA.Contacts.Add(mismatchedContact);

        var act = async () => await dbA.SaveChangesAsync();
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "Attempting to write an entity with a mismatched TenantId must throw UnauthorizedAccessException");

        // 2. Attempting to add CodOrder belonging to Tenant B on Tenant A's DbContext
        await using var dbA2 = CreateDbContext(dbName, contextA);
        var mismatchedOrder = new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB,
            OrderNumber = "ORD-ILLEGAL-CROSS",
            PhoneNumber = "+21600000000",
            TotalAmount = 999m
        };
        dbA2.CodOrders.Add(mismatchedOrder);

        var actOrder = async () => await dbA2.SaveChangesAsync();
        await actOrder.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
