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
}
