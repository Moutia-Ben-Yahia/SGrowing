using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Funnels;

public record FunnelPerformanceSummary(
    Guid FunnelId,
    string FunnelName,
    int TotalVisitors,
    int TotalOrders,
    decimal TotalRevenue,
    double ConversionRatePercentage,
    int BumpOrdersCount,
    decimal BumpRevenue,
    int UpsellOrdersCount,
    decimal UpsellRevenue,
    List<StepPerformanceMetric> StepMetrics
);

public record StepPerformanceMetric(
    Guid StepId,
    string StepName,
    FunnelStepType StepType,
    int StepOrder,
    int Visitors,
    int Conversions,
    double DropoffRatePercentage
);

public interface ISalesFunnelService
{
    Task<SalesFunnel> CreateFunnelAsync(Guid tenantId, string name, string slug, string description = "", string currency = "TND");
    Task<List<SalesFunnel>> GetFunnelsAsync(Guid tenantId);
    Task<SalesFunnel?> GetFunnelByIdAsync(Guid tenantId, Guid funnelId);
    Task<FunnelStep> AddFunnelStepAsync(Guid tenantId, Guid funnelId, string stepName, FunnelStepType stepType, int stepOrder, string slug = "", string templateJson = "{}");
    Task<List<FunnelStep>> GetFunnelStepsAsync(Guid tenantId, Guid funnelId);
    Task<OrderBumpOffer> CreateOrderBumpAsync(Guid tenantId, Guid funnelId, Guid stepId, string title, string headline, string sku, decimal originalPrice, decimal discountedPrice, string badgeText = "SPECIAL IN-CHECKOUT BUMP");
    Task<List<OrderBumpOffer>> GetOrderBumpsAsync(Guid tenantId, Guid funnelId);
    Task<OneClickUpsellOffer> CreateOneClickUpsellAsync(Guid tenantId, Guid funnelId, Guid stepId, string title, string sku, string headline, decimal originalPrice, decimal upsellPrice, string imageUrl = "");
    Task<List<OneClickUpsellOffer>> GetOneClickUpsellsAsync(Guid tenantId, Guid funnelId);
    Task<FunnelConversionEvent> RecordConversionEventAsync(Guid tenantId, Guid funnelId, FunnelEventType eventType, decimal value, Guid? stepId = null, Guid? orderId = null, string? customerPhone = null);
    Task<FunnelPerformanceSummary> GetFunnelPerformanceAsync(Guid tenantId, Guid funnelId);
}

public class SalesFunnelService : ISalesFunnelService
{
    private readonly SGrowingDbContext _db;

    public SalesFunnelService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<SalesFunnel> CreateFunnelAsync(Guid tenantId, string name, string slug, string description = "", string currency = "TND")
    {
        var existing = await _db.SalesFunnels
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Slug == slug.ToLower().Trim());

        if (existing != null)
        {
            return existing;
        }

        var funnel = SalesFunnel.Create(tenantId, name, slug, description, currency);
        _db.SalesFunnels.Add(funnel);
        await _db.SaveChangesAsync();
        return funnel;
    }

    public async Task<List<SalesFunnel>> GetFunnelsAsync(Guid tenantId)
    {
        return await _db.SalesFunnels
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<SalesFunnel?> GetFunnelByIdAsync(Guid tenantId, Guid funnelId)
    {
        return await _db.SalesFunnels
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == funnelId);
    }

    public async Task<FunnelStep> AddFunnelStepAsync(Guid tenantId, Guid funnelId, string stepName, FunnelStepType stepType, int stepOrder, string slug = "", string templateJson = "{}")
    {
        var step = FunnelStep.Create(tenantId, funnelId, stepName, stepType, stepOrder, slug, templateJson);
        _db.FunnelSteps.Add(step);
        await _db.SaveChangesAsync();
        return step;
    }

    public async Task<List<FunnelStep>> GetFunnelStepsAsync(Guid tenantId, Guid funnelId)
    {
        return await _db.FunnelSteps
            .Where(s => s.TenantId == tenantId && s.FunnelId == funnelId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<OrderBumpOffer> CreateOrderBumpAsync(Guid tenantId, Guid funnelId, Guid stepId, string title, string headline, string sku, decimal originalPrice, decimal discountedPrice, string badgeText = "SPECIAL IN-CHECKOUT BUMP")
    {
        var bump = OrderBumpOffer.Create(tenantId, funnelId, stepId, title, headline, sku, originalPrice, discountedPrice, badgeText);
        _db.OrderBumpOffers.Add(bump);
        await _db.SaveChangesAsync();
        return bump;
    }

    public async Task<List<OrderBumpOffer>> GetOrderBumpsAsync(Guid tenantId, Guid funnelId)
    {
        return await _db.OrderBumpOffers
            .Where(b => b.TenantId == tenantId && b.FunnelId == funnelId && b.IsActive)
            .ToListAsync();
    }

    public async Task<OneClickUpsellOffer> CreateOneClickUpsellAsync(Guid tenantId, Guid funnelId, Guid stepId, string title, string sku, string headline, decimal originalPrice, decimal upsellPrice, string imageUrl = "")
    {
        var upsell = OneClickUpsellOffer.Create(tenantId, funnelId, stepId, title, sku, headline, originalPrice, upsellPrice, imageUrl);
        _db.OneClickUpsellOffers.Add(upsell);
        await _db.SaveChangesAsync();
        return upsell;
    }

    public async Task<List<OneClickUpsellOffer>> GetOneClickUpsellsAsync(Guid tenantId, Guid funnelId)
    {
        return await _db.OneClickUpsellOffers
            .Where(u => u.TenantId == tenantId && u.FunnelId == funnelId && u.IsActive)
            .ToListAsync();
    }

    public async Task<FunnelConversionEvent> RecordConversionEventAsync(Guid tenantId, Guid funnelId, FunnelEventType eventType, decimal value, Guid? stepId = null, Guid? orderId = null, string? customerPhone = null)
    {
        var ev = FunnelConversionEvent.Create(tenantId, funnelId, eventType, value, stepId, orderId, customerPhone);
        _db.FunnelConversionEvents.Add(ev);

        var funnel = await _db.SalesFunnels.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == funnelId);
        if (funnel != null)
        {
            if (eventType == FunnelEventType.PageView)
            {
                funnel.TotalVisitors++;
            }
            else if (eventType == FunnelEventType.FunnelOrderCompleted)
            {
                funnel.TotalOrders++;
                funnel.TotalRevenue += value;
            }
            else if (eventType == FunnelEventType.OrderBumpAccepted || eventType == FunnelEventType.UpsellAccepted)
            {
                funnel.TotalRevenue += value;
            }
        }

        if (stepId.HasValue)
        {
            var step = await _db.FunnelSteps.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == stepId.Value);
            if (step != null)
            {
                if (eventType == FunnelEventType.PageView)
                {
                    step.VisitorsCount++;
                }
                else if (eventType == FunnelEventType.CheckoutInitiated || eventType == FunnelEventType.OrderBumpAccepted || eventType == FunnelEventType.UpsellAccepted || eventType == FunnelEventType.FunnelOrderCompleted)
                {
                    step.ConversionsCount++;
                }
            }
        }

        await _db.SaveChangesAsync();
        return ev;
    }

    public async Task<FunnelPerformanceSummary> GetFunnelPerformanceAsync(Guid tenantId, Guid funnelId)
    {
        var funnel = await _db.SalesFunnels.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == funnelId);
        if (funnel == null)
        {
            throw new InvalidOperationException($"Funnel {funnelId} not found for tenant {tenantId}.");
        }

        var steps = await _db.FunnelSteps
            .Where(s => s.TenantId == tenantId && s.FunnelId == funnelId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        var events = await _db.FunnelConversionEvents
            .Where(e => e.TenantId == tenantId && e.FunnelId == funnelId)
            .ToListAsync();

        int bumpOrders = events.Count(e => e.EventType == FunnelEventType.OrderBumpAccepted);
        decimal bumpRevenue = events.Where(e => e.EventType == FunnelEventType.OrderBumpAccepted).Sum(e => e.EventValue);

        int upsellOrders = events.Count(e => e.EventType == FunnelEventType.UpsellAccepted);
        decimal upsellRevenue = events.Where(e => e.EventType == FunnelEventType.UpsellAccepted).Sum(e => e.EventValue);

        double convRate = funnel.TotalVisitors > 0
            ? Math.Round(((double)funnel.TotalOrders / funnel.TotalVisitors) * 100, 2)
            : 0.0;

        var stepMetrics = new List<StepPerformanceMetric>();
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            int visitors = step.VisitorsCount;
            int conversions = step.ConversionsCount;
            double dropoff = visitors > 0
                ? Math.Round(((double)(visitors - conversions) / visitors) * 100, 1)
                : 0.0;

            stepMetrics.Add(new StepPerformanceMetric(step.Id, step.StepName, step.StepType, step.StepOrder, visitors, conversions, dropoff));
        }

        return new FunnelPerformanceSummary(
            funnel.Id,
            funnel.Name,
            funnel.TotalVisitors,
            funnel.TotalOrders,
            funnel.TotalRevenue,
            convRate,
            bumpOrders,
            bumpRevenue,
            upsellOrders,
            upsellRevenue,
            stepMetrics
        );
    }
}
