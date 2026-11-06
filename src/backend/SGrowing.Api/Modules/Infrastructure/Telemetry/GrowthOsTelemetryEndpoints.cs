using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Infrastructure.Telemetry;

/// <summary>
/// Modular Minimal API endpoint definition exposing OpenTelemetry distributed tracing
/// and SRE health radar monitoring capabilities.
/// </summary>
public class GrowthOsTelemetryEndpoints : IEndpointModule
{
    public GrowthOsTelemetryEndpoints()
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGrowthOsTelemetryEndpoints();
    }
}

/// <summary>
/// Extension methods for dependency injection registration and route mapping of GrowthOS telemetry services.
/// </summary>
public static class GrowthOsTelemetryEndpointExtensions
{
    /// <summary>
    /// Registers the <see cref="IGrowthOsTelemetryService"/> singleton within the DI container.
    /// </summary>
    public static IServiceCollection AddGrowthOsTelemetryServices(this IServiceCollection services)
    {
        services.AddSingleton<IGrowthOsTelemetryService, GrowthOsTelemetryService>();
        return services;
    }

    /// <summary>
    /// Maps SRE Health Radar and OpenTelemetry distributed tracing endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapGrowthOsTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/telemetry")
            .WithTags("InfrastructureTelemetry");

        group.MapGet("/sre-radar", (
            [FromQuery] int dlqDepth,
            [FromQuery] int activeMissions,
            [FromServices] IGrowthOsTelemetryService telemetryService) =>
        {
            var report = telemetryService.GetSreHealthRadar(dlqDepth, activeMissions);
            return Results.Ok(report);
        })
        .WithName("GetSreHealthRadar")
        .WithSummary("Retrieves the SRE health radar report including SLO error budget, latency percentiles, and active alerts")
        .Produces<SreHealthRadarReport>(StatusCodes.Status200OK);

        group.MapGet("/trace-context", (
            [FromServices] IGrowthOsTelemetryService telemetryService) =>
        {
            var headers = telemetryService.CreateTraceContextHeaders();
            return Results.Ok(headers);
        })
        .WithName("GetTraceContextHeaders")
        .WithSummary("Generates W3C-compliant traceparent and trace propagation headers for downstream dispatch")
        .Produces<TraceContextPropagationHeaders>(StatusCodes.Status200OK);

        group.MapPost("/record-synthetic-metric", (
            [FromBody] RecordSyntheticMetricRequest request,
            [FromServices] IGrowthOsTelemetryService telemetryService) =>
        {
            if (string.Equals(request.MetricType, "clickstream", StringComparison.OrdinalIgnoreCase))
            {
                telemetryService.RecordClickstreamMetric(request.EventCount);
            }
            else if (string.Equals(request.MetricType, "agent_token", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(request.MetricType, "ai", StringComparison.OrdinalIgnoreCase))
            {
                telemetryService.RecordAgentTokensConsumed(request.AgentRole ?? "GrowthDirector", request.TokenCount);
            }
            else
            {
                telemetryService.RecordWebhookMetric(request.IsSuccess, request.DurationMs);
            }

            return Results.Ok(new
            {
                message = "Synthetic metric recorded successfully",
                metricType = request.MetricType ?? "webhook",
                recordedAtUtc = DateTime.UtcNow
            });
        })
        .WithName("RecordSyntheticMetric")
        .WithSummary("Records synthetic telemetry metrics to exercise OpenTelemetry meters and histograms")
        .Produces(StatusCodes.Status200OK);

        return app;
    }
}
