namespace SGrowing.Api.Modules.Advertising.StoreFootfallLift;

public interface IStoreFootfallLiftEngine
{
    IReadOnlyList<RetailStoreGeofence> GetStores();
    RetailStoreGeofence? GetStore(string storeId);
    StoreLiftAnalysis CalculateLift(CalculateLiftRequest request);
    StoreFootfallMetrics GetMetrics();
}

public class StoreFootfallLiftEngine : IStoreFootfallLiftEngine
{
    private readonly List<RetailStoreGeofence> _stores = new()
    {
        new RetailStoreGeofence(
            StoreId: "STORE-NYC-5TH-AVE",
            StoreName: "Flagship Manhattan 5th Avenue",
            City: "New York",
            Country: "US",
            MinimumDwellMinutes: 7,
            BoundingVertices: new List<GeoCoordinate>
            {
                new(40.7614, -73.9776),
                new(40.7620, -73.9768),
                new(40.7612, -73.9758),
                new(40.7606, -73.9766)
            },
            BaselineDailyFootfall: 1420m,
            CreatedAtUtc: DateTime.UtcNow
        ),
        new RetailStoreGeofence(
            StoreId: "STORE-LON-OXFORD-ST",
            StoreName: "Central London Oxford Street",
            City: "London",
            Country: "UK",
            MinimumDwellMinutes: 6,
            BoundingVertices: new List<GeoCoordinate>
            {
                new(51.5154, -0.1419),
                new(51.5160, -0.1410),
                new(51.5152, -0.1401),
                new(51.5146, -0.1410)
            },
            BaselineDailyFootfall: 1850m,
            CreatedAtUtc: DateTime.UtcNow
        ),
        new RetailStoreGeofence(
            StoreId: "STORE-PARIS-CHAMPS",
            StoreName: "Paris Champs-Élysées Luxury",
            City: "Paris",
            Country: "FR",
            MinimumDwellMinutes: 8,
            BoundingVertices: new List<GeoCoordinate>
            {
                new(48.8698, 2.3075),
                new(48.8705, 2.3085),
                new(48.8696, 2.3095),
                new(48.8689, 2.3085)
            },
            BaselineDailyFootfall: 1650m,
            CreatedAtUtc: DateTime.UtcNow
        )
    };

    public IReadOnlyList<RetailStoreGeofence> GetStores() => _stores.AsReadOnly();

    public RetailStoreGeofence? GetStore(string storeId) =>
        _stores.FirstOrDefault(s => s.StoreId == storeId);

    public StoreLiftAnalysis CalculateLift(CalculateLiftRequest request)
    {
        // Deterministic lift computation
        long exposedVisits = (long)(request.ExposedAudience * 0.048); // 4.8% visit rate
        long controlVisits = (long)(request.ControlAudience * 0.032); // 3.2% visit rate
        long incrementalVisits = exposedVisits - controlVisits;

        decimal exposedRate = (decimal)exposedVisits / request.ExposedAudience * 100m;
        decimal controlRate = (decimal)controlVisits / request.ControlAudience * 100m;
        decimal liftPercentage = ((exposedRate - controlRate) / controlRate) * 100m;

        decimal assumedCampaignCost = 12000m;
        decimal cpiv = incrementalVisits > 0 ? assumedCampaignCost / incrementalVisits : 0m;
        decimal attributedRevenue = incrementalVisits * 145m; // Average in-store order basket

        return new StoreLiftAnalysis(
            AnalysisId: $"LIFT-{Guid.NewGuid().ToString("N")[..8]}",
            StoreId: request.StoreId,
            CampaignId: request.CampaignId,
            ExposedAudience: request.ExposedAudience,
            ControlAudience: request.ControlAudience,
            ExposedStoreVisits: exposedVisits,
            ControlStoreVisits: controlVisits,
            ExposedVisitRatePercentage: Math.Round(exposedRate, 2),
            ControlVisitRatePercentage: Math.Round(controlRate, 2),
            IncrementalLiftPercentage: Math.Round(liftPercentage, 2),
            CostPerIncrementalVisitEur: Math.Round(cpiv, 2),
            TotalAttributedRevenueEur: attributedRevenue,
            ComputedAtUtc: DateTime.UtcNow
        );
    }

    public StoreFootfallMetrics GetMetrics()
    {
        return new StoreFootfallMetrics(
            TotalMonitoredRetailStores: _stores.Count,
            TotalVerifiedStoreVisits: 148920,
            TotalAttributedStoreRevenueEur: 18240000m,
            AverageCostPerIncrementalVisitEur: 8.42m,
            LastUpdatedUtc: DateTime.UtcNow
        );
    }
}
