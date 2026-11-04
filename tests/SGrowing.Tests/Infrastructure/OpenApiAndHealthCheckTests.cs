using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Moq;
using SGrowing.Api.Common.Health;
using SGrowing.Api.Common.OpenApi;
using SGrowing.Api.Data;
using StackExchange.Redis;
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

        document.Info.Should().NotBeNull();
        document.Info.Version.Should().Be("v1");
        document.Info.Title.Should().Contain("SGrowing Enterprise");

        document.Components.Should().NotBeNull();
        document.Components.SecuritySchemes.Should().NotBeNull();

        document.Components.SecuritySchemes.Should().ContainKey("Bearer");
        var bearer = document.Components.SecuritySchemes["Bearer"];
        bearer.Type.Should().Be(SecuritySchemeType.Http);

        document.Components.SecuritySchemes.Should().ContainKey("ApiKey");
        var apiKey = document.Components.SecuritySchemes["ApiKey"];
        apiKey.Type.Should().Be(SecuritySchemeType.ApiKey);
    }

    [Fact]
    public async Task OpenApiSecurityTransformer_PreservesExistingComponents_WhenAlreadyInitialized()
    {
        // Arrange
        var transformer = new OpenApiSecurityTransformer();
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>()
            }
        };

        // Act
        await transformer.TransformAsync(document, null!, CancellationToken.None);

        // Assert
        document.Components.SecuritySchemes.Should().ContainKeys("Bearer", "ApiKey");
        document.Info.Description.Should().Contain("Omnichannel Commerce");
    }

    [Fact]
    public async Task OpenApiSecurityTransformer_SetsComprehensiveApiDescriptionAndTitle()
    {
        // Arrange
        var transformer = new OpenApiSecurityTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, null!, CancellationToken.None);

        // Assert
        document.Info.Title.Should().Be("SGrowing Enterprise SaaS & Omnichannel Engine API");
        document.Info.Description.Should().Contain("COD Logistics");
        document.Info.Description.Should().Contain("AI SDR Agents");
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

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("operational");
    }

    [Fact]
    public async Task DatabaseHealthCheck_HandlesDisposedContext_ReturnsUnhealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase($"HealthCheckDisposedDb_{Guid.NewGuid()}")
            .Options;

        var db = new SGrowingDbContext(options);
        await db.DisposeAsync();

        var healthCheck = new DatabaseHealthCheck(db);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().NotBeNull();
        result.Description.Should().Contain("exception");
    }

    [Fact]
    public async Task RedisHealthCheck_ReturnsHealthy_WhenRedisMultiplexerIsNull()
    {
        var healthCheck = new RedisHealthCheck(null);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("in-memory");
    }

    [Fact]
    public async Task RedisHealthCheck_WhenConnected_ReturnsHealthy()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.IsConnected).Returns(true);

        var healthCheck = new RedisHealthCheck(redisMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("connected");
    }

    [Fact]
    public async Task RedisHealthCheck_WhenDisconnected_ReturnsDegraded()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.IsConnected).Returns(false);

        var healthCheck = new RedisHealthCheck(redisMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("disconnected");
    }
}
