using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.IoT.ColdChainSla;

public static class ColdChainSlaEndpoints
{
    public static IServiceCollection AddColdChainSlaServices(this IServiceCollection services)
    {
        services.AddSingleton<MeanKineticTemperatureCalculatorEngine>();
        services.AddSingleton<CarrierColdChainSlaArbiterService>();
        return services;
    }

    public static IEndpointRouteBuilder MapColdChainSlaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/iot/cold-chain-sla")
            .WithTags("Decentralized Multi-Carrier Cold-Chain IoT Temperature & Spoilage SLA Arbiter");

        group.MapPost("/adjudicate", (
            AuditShipmentColdChainRequest request,
            CarrierColdChainSlaArbiterService arbiter) =>
        {
            var report = arbiter.AdjudicateShipment(request);
            return Results.Ok(report);
        });

        return app;
    }
}
