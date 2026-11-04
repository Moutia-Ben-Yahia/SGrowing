using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class PostPurchaseRetentionTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"RetentionTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task PostPurchase_Should_Schedule_Survey_And_Replenishment_Card()
    {
        var db = CreateDbContext();
        var service = new PostPurchaseRetentionService(db);
        var tenantId = Guid.NewGuid();

        // 1. Configure Consumable Burn Rate (45 days consumption, 10 days reminder lead)
        db.ProductBurnRateConfigs.Add(new ProductBurnRateConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Sku = "OUD-CARTHAGE-50ML",
            ProductName = "Oud Carthage 50ml",
            EstimatedDaysOfUse = 45,
            ReminderLeadDays = 10,
            IncentiveDiscountPercent = 15.0m,
            IsActive = true
        });

        var order = CodOrder.Create(
            tenantId,
            "ORD-RET-01",
            "Anis Bouazizi",
            "+21620112233",
            "La Marsa",
            "Tunis",
            "Marsa",
            140.0m,
            itemsJson: "[{\"sku\":\"OUD-CARTHAGE-50ML\",\"title\":\"Oud Carthage 50ml\",\"quantity\":1}]");
        order.Status = CodOrderStatus.Delivered;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 2. Schedule Retention
        var schedule = await service.SchedulePostPurchaseRetentionAsync(tenantId, order.Id);

        Assert.NotNull(schedule);
        Assert.Equal("OUD-CARTHAGE-50ML", schedule.Sku);
        // Scheduled at day 35 (45 - 10)
        Assert.True(schedule.ScheduledReminderUtc > DateTime.UtcNow.AddDays(30));
        Assert.False(schedule.IsTriggered);

        // 3. Verify Survey created and respond as Promoter
        var survey = await db.PostPurchaseSurveys.FirstOrDefaultAsync(s => s.OrderId == order.Id);
        Assert.NotNull(survey);

        var ratedSurvey = await service.RecordSurveyResponseAsync(tenantId, survey.Id, rating: 5, notes: "Excellent parfum!");
        Assert.True(ratedSurvey.IsPromoter);
        Assert.NotNull(ratedSurvey.DiscountCodeIssued);
        Assert.StartsWith("VIP-MERCI15-", ratedSurvey.DiscountCodeIssued);

        // 4. Simulate Reaching Day 35: Process Due Replenishment Reminders
        var dueTime = schedule.ScheduledReminderUtc.AddHours(1);
        var reminders = await service.ProcessDueReplenishmentRemindersAsync(tenantId, dueTime);

        Assert.Single(reminders);
        Assert.Equal(order.PhoneNumber, reminders[0].CustomerPhone);
        Assert.Contains("touche bientôt à sa fin", reminders[0].MessageText);
        Assert.Contains(schedule.DiscountCode!, reminders[0].WhatsAppCardJson);

        var updatedSchedule = await db.ReplenishmentSchedules.FindAsync(schedule.Id);
        Assert.True(updatedSchedule!.IsTriggered);
    }

    [Fact]
    public async Task SchedulePostPurchaseRetentionAsync_Should_Throw_When_Order_Not_Found()
    {
        var db = CreateDbContext();
        var service = new PostPurchaseRetentionService(db);
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SchedulePostPurchaseRetentionAsync(tenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task SchedulePostPurchaseRetentionAsync_Should_Use_Defaults_When_No_Config_Matches()
    {
        var db = CreateDbContext();
        var service = new PostPurchaseRetentionService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(
            tenantId,
            "ORD-NO-CONFIG",
            "Mouna Trabelsi",
            "+21699887766",
            "Sousse",
            "Sousse",
            "Centre",
            85.0m,
            itemsJson: "[]");
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var schedule = await service.SchedulePostPurchaseRetentionAsync(tenantId, order.Id);

        Assert.NotNull(schedule);
        Assert.Equal("PERFUME-50ML", schedule.Sku);
        Assert.Equal("Prestige Fragrance", schedule.ProductName);
        // Default burn days 45 - lead days 10 = 35 days
        Assert.True(schedule.ScheduledReminderUtc > DateTime.UtcNow.AddDays(30));
    }

    [Fact]
    public async Task RecordSurveyResponseAsync_Should_Throw_When_Survey_Not_Found()
    {
        var db = CreateDbContext();
        var service = new PostPurchaseRetentionService(db);
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordSurveyResponseAsync(tenantId, Guid.NewGuid(), 5));
    }

    [Fact]
    public async Task RecordSurveyResponseAsync_Should_Clamp_Ratings_And_Handle_Detractor()
    {
        var db = CreateDbContext();
        var service = new PostPurchaseRetentionService(db);
        var tenantId = Guid.NewGuid();

        var survey1 = new PostPurchaseSurvey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            CustomerPhone = "+21622334455",
            CustomerName = "Karim",
            SentAtUtc = DateTime.UtcNow
        };
        var survey2 = new PostPurchaseSurvey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            CustomerPhone = "+21633445566",
            CustomerName = "Sonia",
            SentAtUtc = DateTime.UtcNow
        };
        db.PostPurchaseSurveys.AddRange(survey1, survey2);
        await db.SaveChangesAsync();

        // Rating 0 clamped to 1 (detractor)
        var updated1 = await service.RecordSurveyResponseAsync(tenantId, survey1.Id, rating: 0, notes: "Colis endommagé");
        Assert.Equal(1, updated1.Rating);
        Assert.False(updated1.IsPromoter);
        Assert.Null(updated1.DiscountCodeIssued);

        // Rating 10 clamped to 5 (promoter)
        var updated2 = await service.RecordSurveyResponseAsync(tenantId, survey2.Id, rating: 10, notes: "Top service");
        Assert.Equal(5, updated2.Rating);
        Assert.True(updated2.IsPromoter);
        Assert.NotNull(updated2.DiscountCodeIssued);
    }

    [Fact]
    public async Task ProcessWinBackCampaignAsync_Should_Target_Customers_With_Multiple_Orders_After_Inactivity()
    {
        var db = CreateDbContext();
        var service = new PostPurchaseRetentionService(db);
        var tenantId = Guid.NewGuid();
        var oldDate = DateTime.UtcNow.AddDays(-100);

        // Customer 1: 2 delivered orders, last one 100 days ago -> Eligible for 90-day winback
        var cust1Order1 = CodOrder.Create(tenantId, "ORD-WIN-1", "Ali", "+21620000001", "Tunis", "Tunis", "Tunis", 50m);
        cust1Order1.Status = CodOrderStatus.Delivered;
        cust1Order1.CreatedAtUtc = oldDate.AddDays(-10);

        var cust1Order2 = CodOrder.Create(tenantId, "ORD-WIN-2", "Ali", "+21620000001", "Tunis", "Tunis", "Tunis", 70m);
        cust1Order2.Status = CodOrderStatus.Delivered;
        cust1Order2.CreatedAtUtc = oldDate;

        // Customer 2: Only 1 order (even if old) -> NOT eligible (requires >= 2 orders)
        var cust2Order1 = CodOrder.Create(tenantId, "ORD-WIN-3", "Nadia", "+21620000002", "Sousse", "Sousse", "Sousse", 90m);
        cust2Order1.Status = CodOrderStatus.Delivered;
        cust2Order1.CreatedAtUtc = oldDate;

        // Customer 3: 2 orders, but one was placed yesterday -> NOT eligible
        var cust3Order1 = CodOrder.Create(tenantId, "ORD-WIN-4", "Omar", "+21620000003", "Sfax", "Sfax", "Sfax", 40m);
        cust3Order1.Status = CodOrderStatus.Delivered;
        cust3Order1.CreatedAtUtc = oldDate;

        var cust3Order2 = CodOrder.Create(tenantId, "ORD-WIN-5", "Omar", "+21620000003", "Sfax", "Sfax", "Sfax", 60m);
        cust3Order2.Status = CodOrderStatus.Delivered;
        cust3Order2.CreatedAtUtc = DateTime.UtcNow.AddDays(-1);

        db.CodOrders.AddRange(cust1Order1, cust1Order2, cust2Order1, cust3Order1, cust3Order2);
        await db.SaveChangesAsync();

        var winBackTriggers = await service.ProcessWinBackCampaignAsync(tenantId, inactiveDays: 90);

        Assert.Single(winBackTriggers);
        Assert.Equal("+21620000001", winBackTriggers[0].CustomerPhone);
        Assert.Equal("Ali", winBackTriggers[0].CustomerName);
        Assert.StartsWith("WINBACK-VIP20-", winBackTriggers[0].DiscountCode);
        Assert.Contains("Vous nous manquez, Ali", winBackTriggers[0].MessageText);
        Assert.Contains("winback_card", winBackTriggers[0].WhatsAppCardJson);
    }
}
