using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface ISmartCourierRoutingService
{
    Task<List<OptimalCarrierRecommendation>> RankCarriersForOrderAsync(Guid tenantId, string governorate, decimal packageWeightKg, decimal codAmount);
    Task<OptimalCarrierRecommendation> AutoAssignOptimalCarrierAsync(Guid tenantId, Guid orderId, decimal packageWeightKg = 1.0m);
    Task<CodOrder> HandlePickupSlaBreachFallbackAsync(Guid tenantId, string currentTrackingNumber);
}

public class SmartCourierRoutingService : ISmartCourierRoutingService
{
    private readonly SGrowingDbContext _db;

    public SmartCourierRoutingService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<List<OptimalCarrierRecommendation>> RankCarriersForOrderAsync(
        Guid tenantId,
        string governorate,
        decimal packageWeightKg,
        decimal codAmount)
    {
        var cleanGov = governorate.Trim();
        var rateCards = await _db.CarrierRateCards
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .ToListAsync();

        var metrics = await _db.CarrierPerformanceMetrics
            .Where(m => m.TenantId == tenantId && m.GovernorateOrWilaya == cleanGov)
            .ToListAsync();

        var remoteGovernorates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Tataouine", "Tozeur", "Kebili", "Gafsa", "Kasserine", "Sidi Bouzid", "Siliana", "Jendouba", "Kef"
        };
        bool isRemote = remoteGovernorates.Contains(cleanGov);

        var recommendations = new List<OptimalCarrierRecommendation>();

        foreach (var card in rateCards)
        {
            var metric = metrics.FirstOrDefault(m => m.CourierProviderId == card.CourierProviderId);
            decimal deliveryRate = metric != null ? metric.DeliveryRatePercentage / 100m : 0.85m; // default 85%
            decimal returnFee = metric?.ReturnFee ?? 3.5m;
            decimal shippingCost = card.CalculateShippingCost(packageWeightKg, isRemote, codAmount);

            // Expected Value Formula:
            // EV = (DeliveryRate * COD) - ShippingCost - ((1 - DeliveryRate) * ReturnFee)
            decimal expectedGross = deliveryRate * codAmount;
            decimal expectedReturnCost = (1m - deliveryRate) * returnFee;
            decimal expectedNet = expectedGross - shippingCost - expectedReturnCost;

            var reasoning = $"Delivery Rate: {deliveryRate * 100:F1}% in {cleanGov}. Shipping: {shippingCost:F2} TND. Return risk: -{expectedReturnCost:F2} TND.";

            recommendations.Add(new OptimalCarrierRecommendation(
                CourierProviderId: card.CourierProviderId,
                CourierName: card.CourierName,
                EstimatedShippingCost: shippingCost,
                HistoricalDeliveryRate: deliveryRate * 100m,
                ExpectedNetMargin: Math.Round(expectedNet, 2),
                Reasoning: reasoning));
        }

        // Rank by highest expected net margin
        return recommendations.OrderByDescending(r => r.ExpectedNetMargin).ToList();
    }

    public async Task<OptimalCarrierRecommendation> AutoAssignOptimalCarrierAsync(Guid tenantId, Guid orderId, decimal packageWeightKg = 1.0m)
    {
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId);
        if (order == null)
            throw new InvalidOperationException($"Order {orderId} not found.");

        var ranked = await RankCarriersForOrderAsync(tenantId, order.GovernorateOrWilaya, packageWeightKg, order.TotalAmount);
        if (!ranked.Any())
        {
            throw new InvalidOperationException("No active carrier rate cards configured for smart routing.");
        }

        var optimal = ranked.First();
        order.Notes = (order.Notes ?? "") + $" [Smart Routed to {optimal.CourierName} (Est. Delivery: {optimal.HistoricalDeliveryRate:F1}%, Fee: {optimal.EstimatedShippingCost:F2} TND)]";
        order.ShippingFee = optimal.EstimatedShippingCost;

        await _db.SaveChangesAsync();
        return optimal;
    }

    public async Task<CodOrder> HandlePickupSlaBreachFallbackAsync(Guid tenantId, string currentTrackingNumber)
    {
        var cleanTracking = currentTrackingNumber.Trim();
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && (o.Notes != null && o.Notes.Contains(cleanTracking)));
        if (order == null)
            throw new InvalidOperationException($"Order with tracking {cleanTracking} not found.");

        var ranked = await RankCarriersForOrderAsync(tenantId, order.GovernorateOrWilaya, 1.0m, order.TotalAmount);
        // Find alternative carrier (skip the one that breached)
        var fallback = ranked.Skip(1).FirstOrDefault() ?? ranked.First();

        var newTracking = $"{fallback.CourierName[..Math.Min(3, fallback.CourierName.Length)].ToUpperInvariant()}-TN-FALLBACK-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        order.Notes = (order.Notes ?? "") + $" [SLA Breach on {cleanTracking}: Auto-Reassigned to {fallback.CourierName} with Tracking #{newTracking}]";
        order.ShippingFee = fallback.EstimatedShippingCost;

        await _db.SaveChangesAsync();
        return order;
    }
}
