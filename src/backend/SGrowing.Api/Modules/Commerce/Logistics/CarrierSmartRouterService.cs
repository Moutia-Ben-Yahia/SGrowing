using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce.Logistics;

public class CarrierRateSheet
{
    public string CourierName { get; set; } = string.Empty;
    public decimal BaseWeightKg { get; set; } = 1.0m;
    public decimal BaseTariff { get; set; } = 7.0m;
    public decimal ExtraKgTariff { get; set; } = 1.5m;
    public decimal CodFeePercentage { get; set; } = 1.5m;
    public decimal MinCodFee { get; set; } = 1.0m;
    public decimal RemoteCitySurcharge { get; set; } = 3.0m;
    public decimal HistoricalDeliverySuccessRate { get; set; } = 0.94m;
    public decimal HistoricalReturnCost { get; set; } = 3.5m;
    public decimal DefaultAvgTransitDays { get; set; } = 2.0m;
    public HashSet<string> RemoteCities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> CityTransitDays { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsActive { get; set; } = true;
}

public record CarrierQuote(
    string CourierName,
    decimal TariffCost,
    decimal CodCollectionFee,
    decimal TotalShippingCost,
    decimal EstimatedTransitDays,
    decimal OnTimeDeliveryProbability,
    decimal ExpectedNetYield,
    bool MeetsSla,
    string QuoteDetails);

public record CarrierRoutingRecommendation(
    Guid TenantId,
    Guid OrderId,
    string DestinationCity,
    decimal PackageWeightKg,
    decimal CodAmount,
    int MaxSlaDays,
    CarrierQuote OptimalCarrier,
    IReadOnlyList<CarrierQuote> FallbackRanking,
    string RoutingDecisionReason,
    DateTime EvaluatedAtUtc);

public interface ICarrierSmartRouterService
{
    Task<CarrierRoutingRecommendation> RouteOrderAsync(
        Guid tenantId,
        Guid orderId,
        string destinationCity,
        decimal weightKg,
        decimal codAmount,
        int maxSlaDays,
        CancellationToken ct = default);

    void UpsertRateSheet(Guid tenantId, CarrierRateSheet rateSheet);
    IReadOnlyList<CarrierRateSheet> GetRateSheets(Guid tenantId);
}

public class CarrierSmartRouterService : ICarrierSmartRouterService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, CarrierRateSheet>> _tenantRateSheets = new();

    public CarrierSmartRouterService()
    {
    }

    public Task<CarrierRoutingRecommendation> RouteOrderAsync(
        Guid tenantId,
        Guid orderId,
        string destinationCity,
        decimal weightKg,
        decimal codAmount,
        int maxSlaDays,
        CancellationToken ct = default)
    {
        var cleanCity = (destinationCity ?? "Metro").Trim();
        var safeWeight = Math.Max(0.1m, weightKg);
        var safeCod = Math.Max(0m, codAmount);
        var safeMaxSla = Math.Max(1, maxSlaDays);

        var rateSheets = EnsureDefaultRateSheets(tenantId);
        var activeSheets = rateSheets.Values.Where(r => r.IsActive).ToList();

        if (activeSheets.Count == 0)
        {
            throw new InvalidOperationException($"No active carrier rate sheets configured for tenant {tenantId}.");
        }

        var quotes = new List<CarrierQuote>();

        foreach (var sheet in activeSheets)
        {
            bool isRemote = sheet.RemoteCities.Contains(cleanCity);

            // 1. Calculate Tariff Cost
            decimal extraKg = Math.Max(0m, safeWeight - sheet.BaseWeightKg);
            decimal extraTariff = Math.Ceiling(extraKg) * sheet.ExtraKgTariff;
            decimal tariffCost = sheet.BaseTariff + extraTariff + (isRemote ? sheet.RemoteCitySurcharge : 0m);

            // 2. Calculate COD Collection Fee
            decimal codFee = 0m;
            if (safeCod > 0m)
            {
                decimal calculatedCodFee = Math.Round(safeCod * (sheet.CodFeePercentage / 100m), 2);
                codFee = Math.Max(sheet.MinCodFee, calculatedCodFee);
            }

            decimal totalShippingCost = tariffCost + codFee;

            // 3. Estimated Transit Days
            decimal transitDays = sheet.CityTransitDays.TryGetValue(cleanCity, out var cityDays)
                ? cityDays
                : (isRemote ? sheet.DefaultAvgTransitDays + 1.5m : sheet.DefaultAvgTransitDays);

            bool meetsSla = transitDays <= safeMaxSla;

            // 4. On-Time Delivery Probability
            decimal onTimeProb = sheet.HistoricalDeliverySuccessRate;
            if (!meetsSla)
            {
                // Penalize probability proportionally if transit time breaches requested SLA
                decimal slaRatio = safeMaxSla / transitDays;
                onTimeProb = Math.Clamp(Math.Round(onTimeProb * slaRatio, 3), 0.10m, 0.99m);
            }

            // 5. Expected Net Yield Calculation
            // EV = (DeliveryRate * COD Amount) - Shipping Cost - ((1 - DeliveryRate) * Return Fee)
            decimal expectedGrossCollected = onTimeProb * safeCod;
            decimal returnRiskCost = (1m - onTimeProb) * sheet.HistoricalReturnCost;
            decimal expectedNetYield = Math.Round(expectedGrossCollected - totalShippingCost - returnRiskCost, 2);

            var quoteDetails = $"Transit: {transitDays:F1}d (SLA: {safeMaxSla}d). Tariff: ${tariffCost:F2}, COD fee: ${codFee:F2}. On-time prob: {onTimeProb:P0}.";

            quotes.Add(new CarrierQuote(
                sheet.CourierName,
                tariffCost,
                codFee,
                totalShippingCost,
                transitDays,
                onTimeProb,
                expectedNetYield,
                meetsSla,
                quoteDetails));
        }

        // Rank carriers: prioritize SLA compliance, then highest Expected Net Yield
        var ranked = quotes
            .OrderByDescending(q => q.MeetsSla)
            .ThenByDescending(q => q.ExpectedNetYield)
            .ThenBy(q => q.TotalShippingCost)
            .ToList();

        var optimal = ranked[0];
        var fallbacks = ranked.Skip(1).ToList();

        var reason = optimal.MeetsSla
            ? $"Selected {optimal.CourierName} as optimal carrier: Meets SLA of {safeMaxSla} days with {optimal.OnTimeDeliveryProbability:P0} on-time probability and highest net yield (${optimal.ExpectedNetYield:F2})."
            : $"Selected {optimal.CourierName} as closest fallback: None of the carriers meet the tight {safeMaxSla}-day SLA; {optimal.CourierName} delivers best probable yield.";

        var recommendation = new CarrierRoutingRecommendation(
            tenantId,
            orderId,
            cleanCity,
            safeWeight,
            safeCod,
            safeMaxSla,
            optimal,
            fallbacks,
            reason,
            DateTime.UtcNow);

        return Task.FromResult(recommendation);
    }

    public void UpsertRateSheet(Guid tenantId, CarrierRateSheet rateSheet)
    {
        var tenantSheets = _tenantRateSheets.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, CarrierRateSheet>(StringComparer.OrdinalIgnoreCase));
        tenantSheets[rateSheet.CourierName] = rateSheet;
    }

    public IReadOnlyList<CarrierRateSheet> GetRateSheets(Guid tenantId)
    {
        var sheets = EnsureDefaultRateSheets(tenantId);
        return sheets.Values.ToList();
    }

    private ConcurrentDictionary<string, CarrierRateSheet> EnsureDefaultRateSheets(Guid tenantId)
    {
        return _tenantRateSheets.GetOrAdd(tenantId, _ =>
        {
            var dict = new ConcurrentDictionary<string, CarrierRateSheet>(StringComparer.OrdinalIgnoreCase);

            // 1. Yalidine (Algeria / Maghreb leader)
            dict["Yalidine"] = new CarrierRateSheet
            {
                CourierName = "Yalidine",
                BaseWeightKg = 1.0m,
                BaseTariff = 6.50m,
                ExtraKgTariff = 1.20m,
                CodFeePercentage = 1.0m,
                MinCodFee = 0.80m,
                RemoteCitySurcharge = 2.50m,
                HistoricalDeliverySuccessRate = 0.94m,
                HistoricalReturnCost = 3.00m,
                DefaultAvgTransitDays = 2.0m,
                RemoteCities = ["Tamanrasset", "Adrar", "Illizi", "Tindouf", "Bechar"],
                CityTransitDays = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Algiers"] = 1.0m,
                    ["Oran"] = 1.5m,
                    ["Constantine"] = 1.5m,
                    ["Annaba"] = 1.8m,
                    ["Setif"] = 1.2m
                }
            };

            // 2. Aramex (MENA express & regional)
            dict["Aramex"] = new CarrierRateSheet
            {
                CourierName = "Aramex",
                BaseWeightKg = 1.0m,
                BaseTariff = 8.00m,
                ExtraKgTariff = 1.80m,
                CodFeePercentage = 1.5m,
                MinCodFee = 1.20m,
                RemoteCitySurcharge = 3.00m,
                HistoricalDeliverySuccessRate = 0.95m,
                HistoricalReturnCost = 3.50m,
                DefaultAvgTransitDays = 2.0m,
                RemoteCities = ["Tataouine", "Tozeur", "Kebili", "Aswan", "Marsa Matruh"],
                CityTransitDays = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Tunis"] = 1.0m,
                    ["Sousse"] = 1.2m,
                    ["Sfax"] = 1.5m,
                    ["Cairo"] = 1.2m,
                    ["Alexandria"] = 1.5m,
                    ["Dubai"] = 1.0m,
                    ["Riyadh"] = 1.5m
                }
            };

            // 3. DHL (Global Express premium, highest on-time probability)
            dict["DHL"] = new CarrierRateSheet
            {
                CourierName = "DHL",
                BaseWeightKg = 1.0m,
                BaseTariff = 16.00m,
                ExtraKgTariff = 3.50m,
                CodFeePercentage = 2.0m,
                MinCodFee = 2.50m,
                RemoteCitySurcharge = 5.00m,
                HistoricalDeliverySuccessRate = 0.98m,
                HistoricalReturnCost = 6.00m,
                DefaultAvgTransitDays = 1.2m,
                RemoteCities = ["RemoteZoneAlpha", "RemoteZoneBeta"],
                CityTransitDays = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Tunis"] = 1.0m,
                    ["Algiers"] = 1.0m,
                    ["Cairo"] = 1.0m,
                    ["Dubai"] = 1.0m,
                    ["Casablanca"] = 1.0m
                }
            };

            // 4. FedEx (Cross-border reliable SLA)
            dict["FedEx"] = new CarrierRateSheet
            {
                CourierName = "FedEx",
                BaseWeightKg = 1.0m,
                BaseTariff = 14.00m,
                ExtraKgTariff = 3.00m,
                CodFeePercentage = 1.8m,
                MinCodFee = 2.00m,
                RemoteCitySurcharge = 4.50m,
                HistoricalDeliverySuccessRate = 0.97m,
                HistoricalReturnCost = 5.00m,
                DefaultAvgTransitDays = 1.5m,
                RemoteCities = ["RemoteDesertSector"],
                CityTransitDays = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Tunis"] = 1.2m,
                    ["Algiers"] = 1.2m,
                    ["Cairo"] = 1.2m,
                    ["Dubai"] = 1.0m
                }
            };

            // 5. LocalExpress (Low-cost metro courier)
            dict["LocalExpress"] = new CarrierRateSheet
            {
                CourierName = "LocalExpress",
                BaseWeightKg = 1.0m,
                BaseTariff = 4.50m,
                ExtraKgTariff = 1.00m,
                CodFeePercentage = 0.8m,
                MinCodFee = 0.50m,
                RemoteCitySurcharge = 7.00m,
                HistoricalDeliverySuccessRate = 0.91m,
                HistoricalReturnCost = 2.50m,
                DefaultAvgTransitDays = 3.0m,
                RemoteCities = ["Tataouine", "Tozeur", "Adrar", "Tamanrasset", "Siwa"],
                CityTransitDays = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Tunis"] = 1.0m,
                    ["Algiers"] = 1.0m,
                    ["Cairo"] = 1.0m,
                    ["Casablanca"] = 1.0m
                }
            };

            return dict;
        });
    }
}
