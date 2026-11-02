using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Telecom.DirectToCellDopplerTimingAdvance;

public static class DirectToCellEndpoints
{
    public static IServiceCollection AddDirectToCellServices(this IServiceCollection services)
    {
        services.AddScoped<IDirectToCellDopplerTimingAdvanceEngine, DirectToCellDopplerTimingAdvanceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDirectToCellEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/telecom/direct-to-cell")
            .WithTags("Telecom - Direct to Cell LEO Doppler & Timing Advance");

        group.MapPost("/analyze", (
            DirectToCellTelemetry telemetry,
            IDirectToCellDopplerTimingAdvanceEngine engine) =>
        {
            var result = engine.AnalyzeLinkSynchronization(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDirectToCellLink")
        .WithSummary("Analyzes 3GPP Rel-17/18 NTN LEO satellite Doppler shift and timing advance for cellular handsets.");

        return app;
    }
}
