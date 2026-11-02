using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.DemandSensing;

public static class DemandSensingEndpoints
{
    public static IServiceCollection AddDemandSensingServices(this IServiceCollection services)
    {
        services.AddSingleton<WeatherDrivenDemandSensingEngine>();
        services.AddSingleton<HyperlocalInventoryStagingCoordinator>();
        return services;
    }

    public static IEndpointRouteBuilder MapDemandSensingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/supply-chain/demand-sensing")
            .WithTags("Real-Time Algorithmic Demand Sensing & Weather-Driven Hyperlocal Inventory Allocation");

        group.MapPost("/evaluate", (
            EvaluateRegionalDemandRequest request,
            WeatherDrivenDemandSensingEngine engine) =>
        {
            var forecasts = engine.SenseDemand(request);
            return Results.Ok(forecasts);
        });

        group.MapPost("/staging/coordinate", (
            CoordinateStagingApiRequest request,
            HyperlocalInventoryStagingCoordinator coordinator) =>
        {
            var recommendations = coordinator.CoordinateStaging(
                request.Forecasts,
                request.SourceWarehouseId ?? "WH-ALGIERS-CENTRAL",
                request.TargetWarehouseId ?? "WH-OUARGLA-SAHARA");

            return Results.Ok(recommendations);
        });

        return app;
    }
}

public sealed record CoordinateStagingApiRequest(
    IReadOnlyList<SensedDemandForecast> Forecasts,
    string? SourceWarehouseId = null,
    string? TargetWarehouseId = null
);
