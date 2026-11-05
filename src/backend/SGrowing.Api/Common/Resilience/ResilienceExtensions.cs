using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace SGrowing.Api.Common.Resilience;

public static class ResilienceExtensions
{
    /// <summary>
    /// Configures enterprise-grade HTTP resilience pipelines (Retry with exponential backoff & jitter,
    /// Circuit Breaker, Rate Limiting, and Total Request Timeout) for outbound API clients.
    /// Aligns with PRD Section 5.1 & 5.10.
    /// </summary>
    public static IHttpClientBuilder AddGrowthOsStandardResilience(
        this IHttpClientBuilder builder,
        TimeSpan? attemptTimeout = null,
        TimeSpan? totalRequestTimeout = null,
        int maxRetries = 3)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            // Attempt Timeout
            options.AttemptTimeout.Timeout = attemptTimeout ?? TimeSpan.FromSeconds(5);

            // Total Request Timeout (including all retries)
            options.TotalRequestTimeout.Timeout = totalRequestTimeout ?? TimeSpan.FromSeconds(20);

            // Retry Strategy with Exponential Backoff and Jitter
            options.Retry.MaxRetryAttempts = maxRetries;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.UseJitter = true;

            // Circuit Breaker Strategy
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.5; // Trip if 50% fail
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });

        return builder;
    }
}
