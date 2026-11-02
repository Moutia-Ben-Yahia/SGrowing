using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.CashOnDeliverySettlement;

public static class CodSettlementEndpoints
{
    public static IServiceCollection AddCodSettlementServices(this IServiceCollection services)
    {
        services.AddSingleton<ICodSettlementEngine, CodSettlementEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCodSettlementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/logistics/cod/settlement").WithTags("COD Carrier Settlement");

        group.MapPost("/reconcile", (CodSettlementRequest request, ICodSettlementEngine engine) =>
        {
            var result = engine.ReconcileSettlement(request);
            return Results.Ok(result);
        }).WithName("ReconcileCodSettlement");

        return endpoints;
    }
}
