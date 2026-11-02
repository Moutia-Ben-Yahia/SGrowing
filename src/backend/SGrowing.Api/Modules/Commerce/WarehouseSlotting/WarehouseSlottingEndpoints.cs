using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.WarehouseSlotting;

public static class WarehouseSlottingEndpoints
{
    public static IServiceCollection AddWarehouseSlottingServices(this IServiceCollection services)
    {
        services.AddSingleton<DynamicSlottingOptimizerEngine>();
        services.AddSingleton<PalletLoadPhysicsPacker>();
        return services;
    }

    public static IEndpointRouteBuilder MapWarehouseSlottingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/warehouse-slotting")
            .WithTags("Autonomous High-Throughput Warehouse Slotting & Gravity-Feed Pallet Optimization");

        group.MapPost("/optimize", (
            OptimizeSlottingRequest request,
            DynamicSlottingOptimizerEngine engine) =>
        {
            var assignments = engine.OptimizeSlotting(request);
            return Results.Ok(assignments);
        });

        group.MapPost("/pallets/pack", (
            PalletPackRequest request,
            PalletLoadPhysicsPacker packer) =>
        {
            var plan = packer.PackPallet(request);
            return Results.Ok(plan);
        });

        return app;
    }
}
