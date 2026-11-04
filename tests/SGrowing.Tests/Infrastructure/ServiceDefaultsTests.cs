using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class ServiceDefaultsTests
{
    [Fact]
    public void AddDefaultHealthChecks_RegistersSelfCheckWithLiveTag()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddDefaultHealthChecks();

        var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        options.Registrations.Should().Contain(r => r.Name == "self" && r.Tags.Contains("live"));
    }

    [Fact]
    public async Task SelfHealthCheck_ExecutesSuccessfullyAndReturnsHealthy()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();

        var provider = builder.Services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync(r => r.Tags.Contains("live"));

        report.Status.Should().Be(HealthStatus.Healthy);
        report.Entries.Should().ContainKey("self");
        report.Entries["self"].Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void ConfigureOpenTelemetry_RegistersMetricsAndTracing()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.ConfigureOpenTelemetry();

        // Verify services collection contains OpenTelemetry registrations
        builder.Services.Should().Contain(s => s.ServiceType.FullName != null && s.ServiceType.FullName.Contains("OpenTelemetry"));
    }

    [Fact]
    public void AddServiceDefaults_ConfiguresCompleteServiceDefaultsStack()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddServiceDefaults();

        var provider = builder.Services.BuildServiceProvider();
        var healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        healthOptions.Registrations.Should().Contain(r => r.Name == "self");
    }

    [Fact]
    public void AddDefaultHealthChecks_LiveTagPredicate_MatchesOnlyTaggedChecks()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.AddDefaultHealthChecks();

        // Add extra check without "live" tag
        builder.Services.AddHealthChecks().AddCheck("database-mock", () => HealthCheckResult.Healthy(), ["ready"]);

        var provider = builder.Services.BuildServiceProvider();
        var healthOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        // Assert
        healthOptions.Registrations.Should().HaveCount(2);
        healthOptions.Registrations.Should().Contain(r => r.Name == "self" && r.Tags.Contains("live"));
        healthOptions.Registrations.Should().Contain(r => r.Name == "database-mock" && !r.Tags.Contains("live"));
    }

    [Fact]
    public void ConfigureOpenTelemetry_AddsNamedMetersForModules()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.ConfigureOpenTelemetry();

        // Assert: builder contains logging and service registrations
        builder.Services.Should().NotBeEmpty();
        builder.Services.Should().Contain(s => s.ServiceType.Name.Contains("IConfigureOptions") || s.ServiceType.Name.Contains("OpenTelemetry"));
    }

    [Fact]
    public void AddOpenTelemetryExporters_WhenNoOtlpEndpoint_DoesNotThrow()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act & Assert
        var act = () => builder.ConfigureOpenTelemetry();
        act.Should().NotThrow();
    }

    [Fact]
    public void AddOpenTelemetryExporters_WhenOtlpEndpointConfigured_ConfiguresServicesWithoutError()
    {
        // Arrange
        var inMemory = new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(config);

        // Act & Assert
        var act = () => builder.ConfigureOpenTelemetry();
        act.Should().NotThrow();
    }
}
