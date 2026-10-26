using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public interface IPostPurchaseRetentionService
{
    Task<ReplenishmentSchedule?> SchedulePostPurchaseRetentionAsync(Guid tenantId, Guid orderId);
    Task<PostPurchaseSurvey> RecordSurveyResponseAsync(Guid tenantId, Guid surveyId, int rating, string? notes = null);
    Task<List<ReplenishmentTriggerResult>> ProcessDueReplenishmentRemindersAsync(Guid tenantId, DateTime currentTimeUtc);
    Task<List<ReplenishmentTriggerResult>> ProcessWinBackCampaignAsync(Guid tenantId, int inactiveDays = 90);
}

public class PostPurchaseRetentionService : IPostPurchaseRetentionService
{
    private readonly SGrowingDbContext _db;

    public PostPurchaseRetentionService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<ReplenishmentSchedule?> SchedulePostPurchaseRetentionAsync(Guid tenantId, Guid orderId)
    {
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId);
        if (order == null)
            throw new InvalidOperationException($"Order {orderId} not found.");

        // 1. Create Post-Purchase CSAT Survey record (48h follow-up)
        var survey = new PostPurchaseSurvey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order.Id,
            CustomerPhone = order.PhoneNumber,
            CustomerName = order.CustomerName,
            SentAtUtc = DateTime.UtcNow
        };
        _db.PostPurchaseSurveys.Add(survey);

        // 2. Schedule Predictive Replenishment for Consumable Products
        ReplenishmentSchedule? schedule = null;
        var configs = await _db.ProductBurnRateConfigs
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .ToListAsync();

        // Extract SKU from items JSON or default
        string sku = "PERFUME-50ML";
        string prodName = "Prestige Fragrance";
        if (!string.IsNullOrWhiteSpace(order.ItemsJson) && order.ItemsJson.Contains("sku"))
        {
            try
            {
                using var doc = JsonDocument.Parse(order.ItemsJson);
                var first = doc.RootElement.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    sku = first.TryGetProperty("sku", out var s) ? s.GetString() ?? sku : sku;
                    prodName = first.TryGetProperty("title", out var t) ? t.GetString() ?? prodName : prodName;
                }
            }
            catch { }
        }

        var config = configs.FirstOrDefault(c => string.Equals(c.Sku, sku, StringComparison.OrdinalIgnoreCase))
                     ?? configs.FirstOrDefault();

        int burnDays = config?.EstimatedDaysOfUse ?? 45;
        int leadDays = config?.ReminderLeadDays ?? 10;
        int reminderOffset = Math.Max(1, burnDays - leadDays);

        schedule = new ReplenishmentSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order.Id,
            CustomerPhone = order.PhoneNumber,
            CustomerName = order.CustomerName,
            Sku = sku,
            ProductName = prodName,
            DeliveredAtUtc = DateTime.UtcNow,
            ScheduledReminderUtc = DateTime.UtcNow.AddDays(reminderOffset),
            IsTriggered = false,
            DiscountCode = $"REFILL-{DateTime.UtcNow:yyMM}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}"
        };
        _db.ReplenishmentSchedules.Add(schedule);

        order.Notes = (order.Notes ?? "") + $" [Scheduled 48h CSAT & Day-{reminderOffset} Replenishment ({schedule.DiscountCode})]";
        await _db.SaveChangesAsync();

        return schedule;
    }

    public async Task<PostPurchaseSurvey> RecordSurveyResponseAsync(Guid tenantId, Guid surveyId, int rating, string? notes = null)
    {
        var survey = await _db.PostPurchaseSurveys.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == surveyId);
        if (survey == null)
            throw new InvalidOperationException($"Survey {surveyId} not found.");

        survey.Rating = Math.Clamp(rating, 1, 5);
        survey.FeedbackNotes = notes;
        survey.RespondedAtUtc = DateTime.UtcNow;

        if (survey.IsPromoter)
        {
            // Issue VIP discount promo code
            survey.DiscountCodeIssued = $"VIP-MERCI15-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        }

        await _db.SaveChangesAsync();
        return survey;
    }

    public async Task<List<ReplenishmentTriggerResult>> ProcessDueReplenishmentRemindersAsync(Guid tenantId, DateTime currentTimeUtc)
    {
        var dueSchedules = await _db.ReplenishmentSchedules
            .Where(s => s.TenantId == tenantId && !s.IsTriggered && s.ScheduledReminderUtc <= currentTimeUtc)
            .ToListAsync();

        var results = new List<ReplenishmentTriggerResult>();

        foreach (var s in dueSchedules)
        {
            s.IsTriggered = true;
            s.TriggeredAtUtc = currentTimeUtc;

            var msg = $"Bonjour {s.CustomerName} ! Votre flacon de {s.ProductName} touche bientôt à sa fin ? Recommandez votre recharge aujourd'hui avec -15% et livraison offerte avec le code {s.DiscountCode}.";

            var card = JsonSerializer.Serialize(new
            {
                type = "replenishment_card",
                customer = s.CustomerName,
                product = s.ProductName,
                sku = s.Sku,
                discountCode = s.DiscountCode,
                discountPercent = 15,
                ctaUrl = $"https://store.sgrowing.com/reorder?sku={s.Sku}&code={s.DiscountCode}"
            });

            results.Add(new ReplenishmentTriggerResult(
                CustomerPhone: s.CustomerPhone,
                CustomerName: s.CustomerName,
                ProductName: s.ProductName,
                MessageText: msg,
                DiscountCode: s.DiscountCode ?? "REFILL15",
                WhatsAppCardJson: card));
        }

        await _db.SaveChangesAsync();
        return results;
    }

    public async Task<List<ReplenishmentTriggerResult>> ProcessWinBackCampaignAsync(Guid tenantId, int inactiveDays = 90)
    {
        var cutoff = DateTime.UtcNow.AddDays(-inactiveDays);

        // Find customers whose last delivered order was before cutoff and have >= 2 historical orders
        var eligibleOrders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId && o.Status == CodOrderStatus.Delivered)
            .GroupBy(o => o.PhoneNumber)
            .Where(g => g.Count() >= 2 && g.Max(o => o.CreatedAtUtc) <= cutoff)
            .Select(g => g.OrderByDescending(o => o.CreatedAtUtc).First())
            .ToListAsync();

        var results = new List<ReplenishmentTriggerResult>();

        foreach (var o in eligibleOrders)
        {
            var code = $"WINBACK-VIP20-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            var msg = $"Vous nous manquez, {o.CustomerName} ! Profitez de -20% exclusif sur votre prochaine commande avec le code {code}.";

            var card = JsonSerializer.Serialize(new
            {
                type = "winback_card",
                customer = o.CustomerName,
                discountCode = code,
                discountPercent = 20,
                freeShipping = true
            });

            results.Add(new ReplenishmentTriggerResult(
                CustomerPhone: o.PhoneNumber,
                CustomerName: o.CustomerName,
                ProductName: "Catalogue SGrowing",
                MessageText: msg,
                DiscountCode: code,
                WhatsAppCardJson: card));
        }

        return results;
    }
}
