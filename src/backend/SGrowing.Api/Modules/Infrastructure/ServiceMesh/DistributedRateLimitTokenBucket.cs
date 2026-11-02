using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.ServiceMesh;

/// <summary>
/// High-performance thread-safe token bucket rate governor protecting
/// microservices from noisy-neighbor spikes while allowing burst capacity.
/// </summary>
public sealed class DistributedRateLimitTokenBucket
{
    private readonly ConcurrentDictionary<string, TokenBucketState> _buckets = new(StringComparer.OrdinalIgnoreCase);

    public RateLimitResult TryAcquire(
        string tenantId,
        string serviceKey,
        double tokenCost = 1.0,
        double capacity = 100.0,
        double refillTokensPerSecond = 20.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceKey);

        var bucketKey = $"{tenantId}:{serviceKey}";
        var now = DateTime.UtcNow;

        var state = _buckets.GetOrAdd(bucketKey, _ => new TokenBucketState(capacity, now, capacity, refillTokensPerSecond));

        lock (state.SyncRoot)
        {
            // Calculate elapsed time and tokens to refill
            var elapsedSec = (now - state.LastRefillUtc).TotalSeconds;
            if (elapsedSec > 0)
            {
                var newTokens = elapsedSec * refillTokensPerSecond;
                state.Tokens = Math.Min(capacity, state.Tokens + newTokens);
                state.LastRefillUtc = now;
            }

            if (state.Tokens >= tokenCost)
            {
                state.Tokens -= tokenCost;
                return new RateLimitResult(
                    TenantId: tenantId,
                    ServiceKey: serviceKey,
                    Allowed: true,
                    RemainingTokens: Math.Round(state.Tokens, 2),
                    BucketCapacity: capacity,
                    RetryAfter: TimeSpan.Zero
                );
            }

            // Denied: compute retry after
            var needed = tokenCost - state.Tokens;
            var secondsToWait = refillTokensPerSecond > 0 ? needed / refillTokensPerSecond : 1.0;

            return new RateLimitResult(
                TenantId: tenantId,
                ServiceKey: serviceKey,
                Allowed: false,
                RemainingTokens: Math.Round(state.Tokens, 2),
                BucketCapacity: capacity,
                RetryAfter: TimeSpan.FromSeconds(Math.Ceiling(secondsToWait))
            );
        }
    }

    private sealed class TokenBucketState
    {
        public readonly object SyncRoot = new();
        public double Tokens { get; set; }
        public DateTime LastRefillUtc { get; set; }
        public double Capacity { get; set; }
        public double RefillRate { get; set; }

        public TokenBucketState(double tokens, DateTime lastRefillUtc, double capacity, double refillRate)
        {
            Tokens = tokens;
            LastRefillUtc = lastRefillUtc;
            Capacity = capacity;
            RefillRate = refillRate;
        }
    }
}
