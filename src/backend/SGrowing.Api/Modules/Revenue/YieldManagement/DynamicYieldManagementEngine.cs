using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Revenue.YieldManagement;

/// <summary>
/// Revenue yield management engine implementing Expected Marginal Seat Revenue (EMSRb)
/// and Littlewood's rule for capacity protection and dynamic booking control.
/// </summary>
public sealed class DynamicYieldManagementEngine
{
    private readonly ConcurrentDictionary<string, CapacityResourcePool> _pools = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _lock = new();

    /// <summary>
    /// Registers or updates a capacity resource pool with demand forecasts.
    /// </summary>
    public void RegisterResourcePool(CapacityResourcePool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pools[pool.PoolId] = pool;
    }

    /// <summary>
    /// Computes EMSRb nested protection levels for each fare tier in the capacity pool.
    /// </summary>
    public IReadOnlyList<CapacityProtectionLevel> CalculateProtectionLevels(string poolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);

        if (!_pools.TryGetValue(poolId, out var pool))
        {
            throw new KeyNotFoundException($"Capacity pool '{poolId}' not found.");
        }

        var sortedClasses = pool.FareClasses
            .OrderByDescending(f => f.FareDzd)
            .ToList();

        if (sortedClasses.Count == 0)
        {
            return [];
        }

        var protectionLevels = new List<CapacityProtectionLevel>();
        var cumulativeMean = 0.0;
        var cumulativeVariance = 0.0;
        var cumulativeRevenue = 0m;

        for (var j = 0; j < sortedClasses.Count - 1; j++)
        {
            var cls = sortedClasses[j];
            cumulativeMean += cls.ForecastedDemandMean;
            cumulativeVariance += Math.Pow(cls.ForecastedDemandStdDev, 2);
            cumulativeRevenue += cls.FareDzd * (decimal)cls.ForecastedDemandMean;

            var weightedAverageFare = cumulativeMean > 0.0 ? cumulativeRevenue / (decimal)cumulativeMean : cls.FareDzd;
            var nextClass = sortedClasses[j + 1];

            // Ratio of next fare to weighted average higher fare
            var ratio = weightedAverageFare > 0m ? (double)(nextClass.FareDzd / weightedAverageFare) : 1.0;
            ratio = Math.Clamp(ratio, 0.0001, 0.9999);

            var z = InverseStandardNormalCdf(1.0 - ratio);
            var stdDev = Math.Sqrt(cumulativeVariance);

            var rawProtection = cumulativeMean + (z * stdDev);
            var clampedProtection = (int)Math.Clamp(Math.Round(rawProtection), 0, pool.TotalCapacityUnits);

            protectionLevels.Add(new CapacityProtectionLevel(
                Tier: cls.Tier,
                ProtectedCapacityUnits: clampedProtection,
                FareDzd: cls.FareDzd,
                CutoffProbability: Math.Round(1.0 - ratio, 4)
            ));
        }

        // Lowest class has 0 protection against lower (no lower class exists)
        var lowestClass = sortedClasses[^1];
        protectionLevels.Add(new CapacityProtectionLevel(
            Tier: lowestClass.Tier,
            ProtectedCapacityUnits: 0,
            FareDzd: lowestClass.FareDzd,
            CutoffProbability: 0.0
        ));

        return protectionLevels;
    }

    /// <summary>
    /// Evaluates whether an incoming booking request should be accepted or rejected
    /// to preserve high-yield capacity under EMSRb rules.
    /// </summary>
    public BookingEvaluationResult EvaluateBooking(EvaluateBookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_pools.TryGetValue(request.PoolId, out var pool))
        {
            throw new KeyNotFoundException($"Capacity pool '{request.PoolId}' not found.");
        }

        if (request.RequestedUnits <= 0)
        {
            throw new ArgumentException("Requested units must be positive.", nameof(request));
        }

        lock (_lock)
        {
            var availableUnits = pool.AvailableCapacityUnits;

            if (request.RequestedUnits > availableUnits)
            {
                return new BookingEvaluationResult(
                    PoolId: pool.PoolId,
                    RequestedTier: request.RequestedTier,
                    RequestedUnits: request.RequestedUnits,
                    Accepted: false,
                    DecisionReason: $"Requested units ({request.RequestedUnits}) exceed total available pool capacity ({availableUnits}).",
                    TotalQuoteDzd: 0m,
                    RemainingAvailableUnits: availableUnits
                );
            }

            var fareProfile = pool.FareClasses.FirstOrDefault(f => f.Tier == request.RequestedTier);
            if (fareProfile == null)
            {
                throw new InvalidOperationException($"Requested tier '{request.RequestedTier}' is not configured for pool '{pool.PoolId}'.");
            }

            var protectionLevels = CalculateProtectionLevels(pool.PoolId);
            var sortedClasses = pool.FareClasses.OrderByDescending(f => f.FareDzd).ToList();
            var classIndex = sortedClasses.FindIndex(f => f.Tier == request.RequestedTier);

            // If highest class (index 0), accept as long as capacity exists
            if (classIndex == 0)
            {
                var quote = fareProfile.FareDzd * request.RequestedUnits;
                _pools[pool.PoolId] = pool with { BookedUnits = pool.BookedUnits + request.RequestedUnits };

                return new BookingEvaluationResult(
                    PoolId: pool.PoolId,
                    RequestedTier: request.RequestedTier,
                    RequestedUnits: request.RequestedUnits,
                    Accepted: true,
                    DecisionReason: "Accepted premium full-fare booking.",
                    TotalQuoteDzd: quote,
                    RemainingAvailableUnits: availableUnits - request.RequestedUnits
                );
            }

            // For lower classes, check if available capacity after booking respects higher-tier protection level
            var higherTierProtection = protectionLevels[classIndex - 1].ProtectedCapacityUnits;
            var capacityRemainingAfterBooking = availableUnits - request.RequestedUnits;

            if (capacityRemainingAfterBooking < higherTierProtection)
            {
                return new BookingEvaluationResult(
                    PoolId: pool.PoolId,
                    RequestedTier: request.RequestedTier,
                    RequestedUnits: request.RequestedUnits,
                    Accepted: false,
                    DecisionReason: $"Rejected discount booking to protect {higherTierProtection} units for higher-yield demand under EMSRb policy.",
                    TotalQuoteDzd: 0m,
                    RemainingAvailableUnits: availableUnits
                );
            }

            // Booking accepted
            var acceptedQuote = fareProfile.FareDzd * request.RequestedUnits;
            _pools[pool.PoolId] = pool with { BookedUnits = pool.BookedUnits + request.RequestedUnits };

            return new BookingEvaluationResult(
                PoolId: pool.PoolId,
                RequestedTier: request.RequestedTier,
                RequestedUnits: request.RequestedUnits,
                Accepted: true,
                DecisionReason: "Accepted booking within unprotected capacity allocation.",
                TotalQuoteDzd: acceptedQuote,
                RemainingAvailableUnits: availableUnits - request.RequestedUnits
            );
        }
    }

    /// <summary>
    /// Inverse Cumulative Distribution Function for standard normal distribution (Beasley-Springer-Moro approximation).
    /// </summary>
    public static double InverseStandardNormalCdf(double p)
    {
        p = Math.Clamp(p, 0.00001, 0.99999);
        var t = Math.Sqrt(-2.0 * Math.Log(p < 0.5 ? p : 1.0 - p));
        var c0 = 2.515517;
        var c1 = 0.802853;
        var c2 = 0.010328;
        var d1 = 1.432788;
        var d2 = 0.189269;
        var d3 = 0.001308;

        var z = t - ((c0 + (c1 * t) + (c2 * t * t)) / (1.0 + (d1 * t) + (d2 * t * t) + (d3 * t * t * t)));
        return p < 0.5 ? -z : z;
    }
}
