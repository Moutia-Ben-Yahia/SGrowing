using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Common.RateLimiting;

public static class SGrowingRateLimitingExtensions
{
    public const string PolicyFixedStrict = "fixed-strict";
    public const string PolicySlidingTenant = "sliding-tenant";
    public const string PolicyConcurrencyHeavy = "concurrency-heavy";

    public static IServiceCollection AddSGrowingRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var retryAfterSeconds = 60;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterSpan))
                {
                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfterSpan.TotalSeconds));
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                var problemDetails = new ProblemDetails
                {
                    Type = "https://sgrowing.io/errors/rate-limit-exceeded",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = $"Rate limit quota exceeded. Please retry after {retryAfterSeconds} seconds.",
                    Instance = context.HttpContext.Request.Path
                };

                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                problemDetails.Extensions["retryAfterSeconds"] = retryAfterSeconds;

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(problemDetails),
                    cancellationToken);
            };

            // 1. Strict fixed window for sensitive/auth endpoints
            options.AddPolicy(PolicyFixedStrict, httpContext =>
            {
                var partitionKey = ResolveClientPartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 15,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // 2. Sliding window partition by Tenant ID
            options.AddPolicy(PolicySlidingTenant, httpContext =>
            {
                var partitionKey = ResolveTenantPartitionKey(httpContext);
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    });
            });

            // 3. Concurrency limiter for heavy AI / Media / Synthesis
            options.AddPolicy(PolicyConcurrencyHeavy, httpContext =>
            {
                var partitionKey = ResolveTenantPartitionKey(httpContext);
                return RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey,
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 20,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    });
            });
        });

        return services;
    }

    public static string ResolveClientPartitionKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
        {
            var headerValue = forwarded.ToString();
            var commaIdx = headerValue.IndexOf(',', StringComparison.Ordinal);
            return commaIdx > 0 ? headerValue[..commaIdx].Trim() : headerValue.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
    }

    public static string ResolveTenantPartitionKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) && !string.IsNullOrWhiteSpace(tenantHeader))
        {
            return $"tenant:{tenantHeader}";
        }

        if (context.User?.FindFirst("tenant_id")?.Value is { Length: > 0 } tenantClaim)
        {
            return $"tenant:{tenantClaim}";
        }

        return $"ip:{ResolveClientPartitionKey(context)}";
    }
}
