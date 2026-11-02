using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.WarehouseBinCycleCounting;

public static class WarehouseBinAuditEndpoints
{
    public static IServiceCollection AddWarehouseBinAuditServices(this IServiceCollection services)
    {
        services.AddSingleton<IWarehouseBinAuditEngine, WarehouseBinAuditEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWarehouseBinAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/supply-chain/warehouses/cycle-count").WithTags("Warehouse Bin Cycle Counting");

        group.MapPost("/audit", (CycleCountAuditRequest request, IWarehouseBinAuditEngine engine) =>
        {
            var result = engine.ProcessCycleCountAudit(request);
            return Results.Ok(result);
        }).WithName("ProcessCycleCountAudit");

        return endpoints;
    }
}
