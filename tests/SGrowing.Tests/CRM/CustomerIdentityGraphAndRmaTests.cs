using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.CRM;

public class CustomerIdentityGraphAndRmaTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CrmRmaTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CustomerIdentityGraph_Should_Resolve_And_Merge_CrossChannel_Identities()
    {
        var db = CreateDbContext();
        var idGraph = new CustomerIdentityGraphService(db);
        var tenantId = Guid.NewGuid();

        // 1. Initial resolution via phone (Tunisian 8 digits normalized to +216)
        var customer1 = await idGraph.ResolveCustomerAsync(tenantId, "Yassine Mansour", "98123456", "yassine@gmail.com");
        Assert.Equal("+21698123456", customer1.PrimaryPhone);
        Assert.Equal("yassine@gmail.com", customer1.PrimaryEmail);

        // 2. Customer chats on Instagram with handle @yassine_m
        var customer2 = await idGraph.ResolveCustomerAsync(tenantId, "Yassine M", "+21698123456", channelType: CustomerChannelIdentityType.InstagramHandle, channelValue: "@yassine_m");
        Assert.Equal(customer1.Id, customer2.Id); // Same customer profile!

        var detail = await idGraph.GetCustomer360Async(tenantId, customer1.Id);
        Assert.Equal(3, detail.LinkedIdentities.Count); // Phone, Email, Instagram
        Assert.Contains(detail.LinkedIdentities, i => i.IdentityValue == "@yassine_m");

        // 3. Second duplicate profile created under typo phone number
        var duplicate = await idGraph.ResolveCustomerAsync(tenantId, "Yassine M. Mansour", "+21698123457", "yassine.work@company.com");
        duplicate.TotalOrdersCount = 2;
        duplicate.TotalSpent = 350m;
        await db.SaveChangesAsync();

        // Merge duplicate into primary
        var merged = await idGraph.MergeCustomerProfilesAsync(tenantId, customer1.Id, duplicate.Id, "AdminUser");
        Assert.Equal(2, merged.TotalOrdersCount);
        Assert.Equal(350m, merged.TotalSpent);

        // Verify duplicate is gone
        var exists = await db.UnifiedCustomerProfiles.AnyAsync(c => c.Id == duplicate.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task PublicTracking_And_RmaRequest_Should_Verify_Phone_And_Process_Return()
    {
        var db = CreateDbContext();
        var portal = new CustomerSelfServicePortalService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(tenantId, "ORD-EG-990", "Omar Farouk", "+201012345678", "25 El Nasr St", "Cairo", "Nasr City", 450m, countryCode: "EG", codCurrency: "EGP");
        order.Status = CodOrderStatus.Dispatched;
        order.ConfirmedAtUtc = DateTime.UtcNow;
        db.CodOrders.Add(order);

        var shipment = new ShipmentRecord
        {
            TenantId = tenantId,
            OrderId = order.Id,
            TrackingNumber = "TRK-EG-990",
            CourierName = "Bosta Egypt",
            Status = CourierShipmentStatus.InTransit,
            DispatchedAtUtc = DateTime.UtcNow
        };
        db.ShipmentRecords.Add(shipment);
        await db.SaveChangesAsync();

        // Public tracking lookup authenticated with phone suffix
        var tracking = await portal.LookupTrackingAsync("ORD-EG-990", "5678");
        Assert.Equal("ORD-EG-990", tracking.OrderNumber);
        Assert.Equal("Bosta Egypt", tracking.CourierName);
        Assert.True(tracking.TrackingTimeline.Count >= 2);

        // Submit RMA Request
        var rma = await portal.SubmitRmaRequestAsync(tenantId, "ORD-EG-990", "01012345678", RmaReason.SizeExchange, "Need size L instead of M");
        Assert.Equal(RmaStatus.Requested, rma.Status);
        Assert.StartsWith("RMA-ORD-EG-990", rma.RmaNumber);

        // Merchant Review and Reverse Courier Dispatch
        var approvedRma = await portal.ReviewRmaRequestAsync(tenantId, rma.Id, isApproved: true, resolutionNotes: "Approved size exchange", dispatchReverseCourier: true, courierId: Guid.NewGuid());
        Assert.Equal(RmaStatus.ReverseCourierDispatched, approvedRma.Status);
    }

    [Theory]
    [InlineData("98123456", "+21698123456")]
    [InlineData("01012345678", "+201012345678")]
    [InlineData("+212612345678", "+212612345678")]
    [InlineData("216 20 123 456", "+21620123456")]
    [InlineData("+20-111-222-3333", "+201112223333")]
    public void NormalizePhoneToE164_Should_Correctly_Format_Various_Country_Formats(string rawPhone, string expectedNormalized)
    {
        var result = CustomerIdentityGraphService.NormalizePhoneToE164(rawPhone);
        Assert.Equal(expectedNormalized, result);
    }

    [Fact]
    public async Task CustomerIdentityGraph_RecordTimelineEvent_ShouldPersistOrderedEvents()
    {
        var db = CreateDbContext();
        var idGraph = new CustomerIdentityGraphService(db);
        var tenantId = Guid.NewGuid();

        var customer = await idGraph.ResolveCustomerAsync(tenantId, "Fatma Ben Salem", "97654321");
        await idGraph.RecordTimelineEventAsync(tenantId, customer.Id, "OrderPlaced", "Order #ORD-101 created", "{\"amount\":120.5}");
        await idGraph.RecordTimelineEventAsync(tenantId, customer.Id, "CallLogged", "Inbound inquiry regarding delivery", "{\"duration_sec\":45}");

        var detail = await idGraph.GetCustomer360Async(tenantId, customer.Id);

        Assert.NotNull(detail);
        Assert.Equal(customer.Id, detail.Profile.Id);
        Assert.Equal(2, detail.Timeline.Count);
        Assert.Contains(detail.Timeline, t => t.EventType == "OrderPlaced");
        Assert.Contains(detail.Timeline, t => t.EventType == "CallLogged");
    }

    [Fact]
    public async Task PublicTracking_WithInvalidPhone_ThrowsUnauthorizedAccessException()
    {
        var db = CreateDbContext();
        var portal = new CustomerSelfServicePortalService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(tenantId, "ORD-TUN-777", "Sami Trabelsi", "+21698765432", "Avenue Habib Bourguiba", "Tunis", "Centre Ville", 180m, countryCode: "TN", codCurrency: "TND");
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Phone suffix 9999 does not match 5432
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            portal.LookupTrackingAsync("ORD-TUN-777", "9999"));
    }

    [Fact]
    public async Task CustomerIdentityGraph_GetCustomer360_NonExistentCustomer_ThrowsInvalidOperationException()
    {
        var db = CreateDbContext();
        var idGraph = new CustomerIdentityGraphService(db);
        var tenantId = Guid.NewGuid();
        var nonExistentId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            idGraph.GetCustomer360Async(tenantId, nonExistentId));
    }
}
