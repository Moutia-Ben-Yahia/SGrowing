using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce.DynamicPricing;

#region DTOs & Models

/// <summary>
/// Scraped competitor pricing signal for a specific SKU.
/// </summary>
public record CompetitorPriceObservation(
    Guid ObservationId,
    Guid TenantId,
    string Sku,
    string CompetitorName,
    decimal ObservedPrice,
    bool InStock,
    decimal? PromotionalPrice = null,
    decimal? ShippingFee = null,
    string Currency = "DZD",
    DateTime? ObservedAtUtc = null,
    string? ProxyNodeUsed = null,
    double? LatencyMs = null,
    decimal? SellerRating = null)
{
    /// <summary>
    /// Landed price observed by the customer including promotion and shipping.
    /// </summary>
    public decimal EffectivePrice => (PromotionalPrice ?? ObservedPrice) + (ShippingFee ?? 0m);
}

/// <summary>
/// Aggregated market intelligence metrics for an SKU across all observed competitors.
/// </summary>
public record MarketMetrics(
    string Sku,
    int TotalCompetitorsObserved,
    int InStockCount,
    int OutOfStockCount,
    decimal LowestPrice,
    decimal HighestPrice,
    decimal MedianPrice,
    decimal AveragePrice,
    decimal? LowestInStockPrice,
    decimal? HighestInStockPrice,
    decimal? MedianInStockPrice,
    decimal PriceSpreadPercentage,
    bool HasStockOutCompetitor,
    bool AreAllCompetitorsOutOfStock,
    bool StockOutOpportunityDetected,
    IReadOnlyList<CompetitorPriceObservation> Competitors,
    DateTime ComputedAtUtc);

/// <summary>
/// Simulated proxy node in the scraping mesh network.
/// </summary>
public record ProxyNode(
    string Id,
    string IpAddress,
    int Port,
    string Region,
    bool IsActive,
    int FailureCount,
    long TotalRequests,
    double LatencyMs);

/// <summary>
/// Request to simulate scraping a competitor's product listing.
/// </summary>
public record ScrapeTargetRequest(
    string CompetitorName,
    string Sku,
    string TargetUrl,
    decimal SimulatedPrice,
    bool InStock,
    decimal? PromotionalPrice = null,
    decimal? ShippingFee = null,
    string Currency = "DZD",
    bool SimulateTransientFailure = false);

/// <summary>
/// Result of an automated scraper mesh execution.
/// </summary>
public record ScrapeExecutionResult(
    bool Success,
    CompetitorPriceObservation? Observation,
    string ProxyUsed,
    int RetriesAttempted,
    double LatencyMs,
    string? ErrorMessage);

#endregion

/// <summary>
/// High-throughput competitor scraping mesh and market price intelligence service.
/// </summary>
public interface ICompetitorScraperMeshService
{
    Task<CompetitorPriceObservation> RecordObservationAsync(
        Guid tenantId,
        CompetitorPriceObservation observation,
        CancellationToken ct = default);

    Task<IReadOnlyList<CompetitorPriceObservation>> RecordObservationsBatchAsync(
        Guid tenantId,
        IEnumerable<CompetitorPriceObservation> observations,
        CancellationToken ct = default);

    Task<MarketMetrics> GetMarketMetricsAsync(
        Guid tenantId,
        string sku,
        CancellationToken ct = default);

    Task<ScrapeExecutionResult> SimulateScrapeTargetAsync(
        Guid tenantId,
        ScrapeTargetRequest request,
        CancellationToken ct = default);

    IReadOnlyList<ProxyNode> GetActiveProxyPool();
    void ResetState(Guid? tenantId = null);
}

/// <summary>
/// Implements the competitor scraper mesh with simulated proxy rotation, exponential backoff,
/// real-time metric aggregation, and stock-out opportunity detection.
/// </summary>
public sealed class CompetitorScraperMeshService : ICompetitorScraperMeshService
{
    // Storage: TenantId + Sku -> CompetitorName -> CompetitorPriceObservation
    private readonly ConcurrentDictionary<(Guid TenantId, string Sku), ConcurrentDictionary<string, CompetitorPriceObservation>> _storage = new();

    // Proxy pool for mesh simulation
    private readonly List<ProxyNode> _proxyPool =
    [
        new("proxy-dz-01", "197.200.14.88", 8080, "Algiers", true, 0, 0, 45.0),
        new("proxy-fr-01", "51.159.22.10", 3128, "Paris", true, 0, 0, 72.0),
        new("proxy-de-01", "159.69.110.42", 8080, "Frankfurt", true, 0, 0, 85.0),
        new("proxy-us-01", "104.244.75.12", 8888, "New York", true, 0, 0, 130.0),
        new("proxy-ae-01", "185.193.64.9", 8080, "Dubai", true, 0, 0, 110.0)
    ];

    private int _proxyRoundRobinIndex;

    /// <summary>
    /// Records a single competitor observation.
    /// </summary>
    public Task<CompetitorPriceObservation> RecordObservationAsync(
        Guid tenantId,
        CompetitorPriceObservation observation,
        CancellationToken ct = default)
    {
        var key = (tenantId, observation.Sku.Trim().ToUpperInvariant());
        var competitorDict = _storage.GetOrAdd(key, _ => new ConcurrentDictionary<string, CompetitorPriceObservation>(StringComparer.OrdinalIgnoreCase));

        var effectiveObservation = observation.ObservedAtUtc.HasValue
            ? observation
            : observation with { ObservedAtUtc = DateTime.UtcNow };

        competitorDict[observation.CompetitorName.Trim()] = effectiveObservation;
        return Task.FromResult(effectiveObservation);
    }

    /// <summary>
    /// Records a batch of competitor observations.
    /// </summary>
    public async Task<IReadOnlyList<CompetitorPriceObservation>> RecordObservationsBatchAsync(
        Guid tenantId,
        IEnumerable<CompetitorPriceObservation> observations,
        CancellationToken ct = default)
    {
        var recorded = new List<CompetitorPriceObservation>();
        foreach (var obs in observations)
        {
            var result = await RecordObservationAsync(tenantId, obs, ct);
            recorded.Add(result);
        }
        return recorded;
    }

    /// <summary>
    /// Computes aggregated market metrics including median, lowest, highest, price spread %,
    /// and detects competitor stock-out pricing opportunities.
    /// </summary>
    public Task<MarketMetrics> GetMarketMetricsAsync(
        Guid tenantId,
        string sku,
        CancellationToken ct = default)
    {
        var normalizedSku = sku.Trim().ToUpperInvariant();
        var key = (tenantId, normalizedSku);

        if (!_storage.TryGetValue(key, out var competitorDict) || competitorDict.IsEmpty)
        {
            return Task.FromResult(new MarketMetrics(
                Sku: normalizedSku,
                TotalCompetitorsObserved: 0,
                InStockCount: 0,
                OutOfStockCount: 0,
                LowestPrice: 0m,
                HighestPrice: 0m,
                MedianPrice: 0m,
                AveragePrice: 0m,
                LowestInStockPrice: null,
                HighestInStockPrice: null,
                MedianInStockPrice: null,
                PriceSpreadPercentage: 0m,
                HasStockOutCompetitor: false,
                AreAllCompetitorsOutOfStock: false,
                StockOutOpportunityDetected: false,
                Competitors: [],
                ComputedAtUtc: DateTime.UtcNow));
        }

        var observations = competitorDict.Values.ToList();
        var allPrices = observations.Select(o => o.EffectivePrice).OrderBy(p => p).ToList();

        decimal lowestPrice = allPrices[0];
        decimal highestPrice = allPrices[^1];
        decimal medianPrice = ComputeMedian(allPrices);
        decimal averagePrice = Math.Round(allPrices.Average(), 2);

        var inStockObs = observations.Where(o => o.InStock).ToList();
        int inStockCount = inStockObs.Count;
        int outOfStockCount = observations.Count - inStockCount;

        decimal? lowestInStockPrice = null;
        decimal? highestInStockPrice = null;
        decimal? medianInStockPrice = null;

        if (inStockCount > 0)
        {
            var inStockPrices = inStockObs.Select(o => o.EffectivePrice).OrderBy(p => p).ToList();
            lowestInStockPrice = inStockPrices[0];
            highestInStockPrice = inStockPrices[^1];
            medianInStockPrice = ComputeMedian(inStockPrices);
        }

        // Price Spread = ((Highest - Lowest) / Median) * 100
        decimal spreadPercentage = medianPrice > 0m
            ? Math.Round(((highestPrice - lowestPrice) / medianPrice) * 100m, 2)
            : 0m;

        bool hasStockOut = outOfStockCount > 0;
        bool allOutOfStock = inStockCount == 0 && observations.Count > 0;

        // Stock-out opportunity is detected if:
        // 1) All competitors are out of stock (monopoly position), OR
        // 2) The lowest-price competitor is out of stock (allowing our price to rise to next in-stock competitor)
        bool stockOutOpportunity = allOutOfStock || (lowestInStockPrice.HasValue && lowestInStockPrice.Value > lowestPrice);

        var metrics = new MarketMetrics(
            Sku: normalizedSku,
            TotalCompetitorsObserved: observations.Count,
            InStockCount: inStockCount,
            OutOfStockCount: outOfStockCount,
            LowestPrice: lowestPrice,
            HighestPrice: highestPrice,
            MedianPrice: medianPrice,
            AveragePrice: averagePrice,
            LowestInStockPrice: lowestInStockPrice,
            HighestInStockPrice: highestInStockPrice,
            MedianInStockPrice: medianInStockPrice,
            PriceSpreadPercentage: spreadPercentage,
            HasStockOutCompetitor: hasStockOut,
            AreAllCompetitorsOutOfStock: allOutOfStock,
            StockOutOpportunityDetected: stockOutOpportunity,
            Competitors: observations,
            ComputedAtUtc: DateTime.UtcNow);

        return Task.FromResult(metrics);
    }

    /// <summary>
    /// Simulates real-world scraper mesh behavior: rotating through proxy nodes,
    /// executing retry loops with exponential backoff on transient HTTP 429/503 signals,
    /// and automatically updating the mesh intelligence store.
    /// </summary>
    public async Task<ScrapeExecutionResult> SimulateScrapeTargetAsync(
        Guid tenantId,
        ScrapeTargetRequest request,
        CancellationToken ct = default)
    {
        int maxAttempts = 3;
        int attempt = 0;
        int retriesAttempted = 0;
        ProxyNode selectedProxy = GetNextProxy();

        while (attempt < maxAttempts)
        {
            attempt++;
            selectedProxy = GetNextProxy();

            // Simulate transient network / rate-limiting failure if requested on first attempt
            if (request.SimulateTransientFailure && attempt == 1)
            {
                retriesAttempted++;
                // Exponential backoff simulation: 10ms * 2^attempt
                await Task.Delay(10 * (1 << attempt), ct);
                continue;
            }

            // Successful simulated scrape
            var observation = new CompetitorPriceObservation(
                ObservationId: Guid.NewGuid(),
                TenantId: tenantId,
                Sku: request.Sku,
                CompetitorName: request.CompetitorName,
                ObservedPrice: request.SimulatedPrice,
                InStock: request.InStock,
                PromotionalPrice: request.PromotionalPrice,
                ShippingFee: request.ShippingFee,
                Currency: request.Currency,
                ObservedAtUtc: DateTime.UtcNow,
                ProxyNodeUsed: $"{selectedProxy.Id} ({selectedProxy.Region})",
                LatencyMs: selectedProxy.LatencyMs + (attempt * 12.0),
                SellerRating: 4.6m);

            await RecordObservationAsync(tenantId, observation, ct);

            return new ScrapeExecutionResult(
                Success: true,
                Observation: observation,
                ProxyUsed: selectedProxy.Id,
                RetriesAttempted: retriesAttempted,
                LatencyMs: observation.LatencyMs ?? selectedProxy.LatencyMs,
                ErrorMessage: null);
        }

        return new ScrapeExecutionResult(
            Success: false,
            Observation: null,
            ProxyUsed: selectedProxy.Id,
            RetriesAttempted: retriesAttempted,
            LatencyMs: 250.0,
            ErrorMessage: "Max scraper retries exceeded: Target host rate-limited requests across proxy mesh.");
    }

    /// <summary>
    /// Returns the active proxy pool configuration.
    /// </summary>
    public IReadOnlyList<ProxyNode> GetActiveProxyPool() => _proxyPool.AsReadOnly();

    /// <summary>
    /// Resets the internal state for testing and multi-tenancy testing isolation.
    /// </summary>
    public void ResetState(Guid? tenantId = null)
    {
        if (tenantId.HasValue)
        {
            var keysToRemove = _storage.Keys.Where(k => k.TenantId == tenantId.Value).ToList();
            foreach (var key in keysToRemove)
            {
                _storage.TryRemove(key, out _);
            }
        }
        else
        {
            _storage.Clear();
        }
    }

    private ProxyNode GetNextProxy()
    {
        int index = Interlocked.Increment(ref _proxyRoundRobinIndex);
        int normalized = Math.Abs(index) % _proxyPool.Count;
        return _proxyPool[normalized];
    }

    private static decimal ComputeMedian(IReadOnlyList<decimal> sorted)
    {
        if (sorted.Count == 0) return 0m;
        int mid = sorted.Count / 2;
        return (sorted.Count % 2 != 0)
            ? sorted[mid]
            : Math.Round((sorted[mid - 1] + sorted[mid]) / 2.0m, 2);
    }
}
