using FluentAssertions;
using Microsoft.AspNetCore.Builder;
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
}
