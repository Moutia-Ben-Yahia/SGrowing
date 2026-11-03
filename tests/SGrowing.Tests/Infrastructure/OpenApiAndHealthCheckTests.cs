using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using SGrowing.Api.Common.Health;
using SGrowing.Api.Common.OpenApi;
using SGrowing.Api.Data;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class OpenApiAndHealthCheckTests
{
    [Fact]
    public async Task OpenApiSecurityTransformer_AddsJwtBearerAndApiKeySchemes()
    {
        var transformer = new OpenApiSecurityTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, null!, CancellationToken.None);

        Assert.NotNull(document.Info);
        Assert.Equal("v1", document.Info.Version);
        Assert.Contains("SGrowing Enterprise", document.Info.Title);

        Assert.NotNull(document.Components);
        Assert.NotNull(document.Components.SecuritySchemes);

        Assert.True(document.Components.SecuritySchemes.ContainsKey("Bearer"));
        var bearer = document.Components.SecuritySchemes["Bearer"];
        Assert.Equal(SecuritySchemeType.Http, bearer.Type);

        Assert.True(document.Components.SecuritySchemes.ContainsKey("ApiKey"));
        var apiKey = document.Components.SecuritySchemes["ApiKey"];
        Assert.Equal(SecuritySchemeType.ApiKey, apiKey.Type);
    }

    [Fact]
    public async Task DatabaseHealthCheck_ReturnsHealthy_WhenDatabaseIsAccessible()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"HealthCheckDb_{Guid.NewGuid()}")
            .Options;

        await using var db = new SGrowingDbContext(options);
        var healthCheck = new DatabaseHealthCheck(db);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("operational", result.Description);
    }

    [Fact]
    public async Task RedisHealthCheck_ReturnsHealthy_WhenRedisMultiplexerIsNull()
    {
        var healthCheck = new RedisHealthCheck(null);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("in-memory", result.Description);
    }
}
