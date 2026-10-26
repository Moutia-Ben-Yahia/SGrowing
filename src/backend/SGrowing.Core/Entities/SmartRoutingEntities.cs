namespace SGrowing.Core.Entities;

public class CarrierPerformanceMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CourierProviderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string GovernorateOrWilaya { get; set; } = string.Empty;
    public int TotalDispatchedParcels { get; set; }
    public int DeliveredParcels { get; set; }
    public int ReturnedParcels { get; set; }
    public decimal DeliveryRatePercentage { get; set; } = 100.0m;
    public decimal AverageTransitDays { get; set; } = 1.5m;
    public decimal ReturnFee { get; set; } = 3.5m;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public static CarrierPerformanceMetric Create(
        Guid tenantId,
        Guid courierProviderId,
        string courierName,
        string governorate,
        int dispatched,
        int delivered,
        int returned,
        decimal avgTransitDays = 1.5m,
        decimal returnFee = 3.5m)
    {
        decimal rate = dispatched > 0 ? Math.Round((decimal)delivered / dispatched * 100m, 1) : 100m;
        return new CarrierPerformanceMetric
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = courierProviderId,
            CourierName = courierName.Trim(),
            GovernorateOrWilaya = governorate.Trim(),
            TotalDispatchedParcels = dispatched,
            DeliveredParcels = delivered,
            ReturnedParcels = returned,
            DeliveryRatePercentage = rate,
            AverageTransitDays = avgTransitDays,
            ReturnFee = returnFee,
            LastUpdatedUtc = DateTime.UtcNow
        };
    }
}

public class CarrierRateCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CourierProviderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public decimal BaseWeightKg { get; set; } = 1.0m;
    public decimal BaseFee { get; set; } = 7.0m;
    public decimal ExtraKgFee { get; set; } = 1.5m;
    public decimal RemoteZoneSurcharge { get; set; } = 2.0m;
    public decimal CodCollectionPercentFee { get; set; } = 0.0m;
    public bool IsActive { get; set; } = true;

    public decimal CalculateShippingCost(decimal weightKg, bool isRemoteZone = false, decimal codAmount = 0m)
    {
        decimal cost = BaseFee;
        if (weightKg > BaseWeightKg)
        {
            cost += Math.Ceiling(weightKg - BaseWeightKg) * ExtraKgFee;
        }

        if (isRemoteZone)
        {
            cost += RemoteZoneSurcharge;
        }

        if (CodCollectionPercentFee > 0 && codAmount > 0)
        {
            cost += Math.Round(codAmount * (CodCollectionPercentFee / 100m), 2);
        }

        return cost;
    }
}

public record OptimalCarrierRecommendation(
    Guid CourierProviderId,
    string CourierName,
    decimal EstimatedShippingCost,
    decimal HistoricalDeliveryRate,
    decimal ExpectedNetMargin,
    string Reasoning);
