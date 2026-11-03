using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGrowing.Api.Common;
using SGrowing.Api.Common.RateLimiting;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class RateLimiterPolicyTests
{
    [Fact]
    public void AddSGrowingRateLimiting_RegistersRateLimiterAndExpectedPolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddSGrowingRateLimiting();
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        // Assert
        Assert.NotNull(options);
        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
        Assert.NotNull(options.OnRejected);
    }

    [Fact]
    public void ResolveClientPartitionKey_UsesXForwardedFor_WhenPresent()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.195, 70.41.3.18";

        // Act
        var key = SGrowingRateLimitingExtensions.ResolveClientPartitionKey(context);

        // Assert
        Assert.Equal("203.0.113.195", key);
    }

    [Fact]
    public void ResolveTenantPartitionKey_UsesXTenantIdHeader_WhenPresent()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant-enterprise-8899";

        // Act
        var key = SGrowingRateLimitingExtensions.ResolveTenantPartitionKey(context);

        // Assert
        Assert.Equal("tenant:tenant-enterprise-8899", key);
    }

    [Fact]
    public void ResolveTenantPartitionKey_UsesUserClaim_WhenHeaderAbsent()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity([new Claim("tenant_id", "tenant-claim-4422")]);
        context.User = new ClaimsPrincipal(identity);

        // Act
        var key = SGrowingRateLimitingExtensions.ResolveTenantPartitionKey(context);

        // Assert
        Assert.Equal("tenant:tenant-claim-4422", key);
    }

    [Fact]
    public void SGrowingProblemDetails_TooManyRequests_GeneratesCompliant429Result()
    {
        // Act
        var result = SGrowingProblemDetails.TooManyRequests(
            "Quota exceeded for window.",
            "/api/v1/auth/login",
            extensions: new Dictionary<string, object?> { ["retryAfterSeconds"] = 45 });

        // Assert
        Assert.NotNull(result);
    }
}
