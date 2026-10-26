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
        var courierHub = new CourierLogisticsHubService(db);
        var portal = new CustomerSelfServicePortalService(db, courierHub);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider { TenantId = tenantId, Name = "Bosta Egypt", ApiCode = "BST", IsActive = true };
        db.CourierProviders.Add(courier);

        var order = CodOrder.Create(tenantId, "ORD-EG-990", "Omar Farouk", "+201012345678", "25 El Nasr St", "Cairo", "Nasr City", 450m, countryCode: "EG", codCurrency: "EGP");
        order.Status = CodOrderStatus.Confirmed;
        order.ConfirmedAtUtc = DateTime.UtcNow;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Dispatch
        await courierHub.DispatchOrderAsync(tenantId, order.Id, courier.Id);

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
        var approvedRma = await portal.ReviewRmaRequestAsync(tenantId, rma.Id, isApproved: true, resolutionNotes: "Approved size exchange", dispatchReverseCourier: true, courierId: courier.Id);
        Assert.Equal(RmaStatus.ReverseCourierDispatched, approvedRma.Status);
    }
}
