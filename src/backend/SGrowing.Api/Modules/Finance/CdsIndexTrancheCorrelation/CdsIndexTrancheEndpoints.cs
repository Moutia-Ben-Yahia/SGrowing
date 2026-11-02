using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CdsIndexTrancheCorrelation;

public static class CdsIndexTrancheEndpoints
{
    public static IServiceCollection AddCdsIndexTrancheServices(this IServiceCollection services)
    {
        services.AddScoped<ICdsIndexTrancheEngine, CdsIndexTrancheEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCdsIndexTrancheEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/cds-index-tranche-correlation")
            .WithTags("Finance CDS Index Tranches, Base Correlation & Index-CDX Basis");

        group.MapPost("/analyze", (CdsIndexTrancheTelemetry telemetry, ICdsIndexTrancheEngine engine) =>
        {
            var evaluation = engine.AnalyzeTranche(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeCdsIndexTranche")
        .WithSummary("Analyzes CDX/iTraxx tranche pricing, base correlation smile consistency, and index-constituent CDS basis arbitrage");

        return endpoints;
    }
}
