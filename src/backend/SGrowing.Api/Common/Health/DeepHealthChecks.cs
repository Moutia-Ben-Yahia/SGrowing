using Microsoft.Extensions.Diagnostics.HealthChecks;
using SGrowing.Api.Data;
using StackExchange.Redis;

namespace SGrowing.Api.Common.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly SGrowingDbContext _db;

    public DatabaseHealthCheck(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL / In-Memory database connection is operational.")
                : HealthCheckResult.Unhealthy("Database connection failed: CanConnect returned false.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection check threw an exception.", ex);
        }
    }
}

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;

    public RedisHealthCheck(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Redis multiplexer not configured (in-memory stream broker active)."));
        }

        try
        {
            if (_redis.IsConnected)
            {
                return Task.FromResult(HealthCheckResult.Healthy("Redis distributed stream broker is connected."));
            }

            return Task.FromResult(HealthCheckResult.Degraded("Redis is configured but reported disconnected status."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Redis health check threw an exception.", ex));
        }
    }
}
