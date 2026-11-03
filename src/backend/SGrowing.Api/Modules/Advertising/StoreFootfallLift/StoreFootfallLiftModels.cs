namespace SGrowing.Api.Modules.Advertising.StoreFootfallLift;

public record GeoCoordinate(double Latitude, double Longitude);

public record RetailStoreGeofence(
    string StoreId,
    string StoreName,
    string City,
    string Country,
    int MinimumDwellMinutes,
    List<GeoCoordinate> BoundingVertices,
    decimal BaselineDailyFootfall,
    DateTime CreatedAtUtc
);

public record CalculateLiftRequest(
    string StoreId,
    string CampaignId,
    long ExposedAudience,
    long ControlAudience
);

public record StoreLiftAnalysis(
    string AnalysisId,
    string StoreId,
    string CampaignId,
    long ExposedAudience,
    long ControlAudience,
    long ExposedStoreVisits,
    long ControlStoreVisits,
    decimal ExposedVisitRatePercentage,
    decimal ControlVisitRatePercentage,
    decimal IncrementalLiftPercentage,
    decimal CostPerIncrementalVisitEur,
    decimal TotalAttributedRevenueEur,
    DateTime ComputedAtUtc
);

public record StoreFootfallMetrics(
    int TotalMonitoredRetailStores,
    long TotalVerifiedStoreVisits,
    decimal TotalAttributedStoreRevenueEur,
    decimal AverageCostPerIncrementalVisitEur,
    DateTime LastUpdatedUtc
);
