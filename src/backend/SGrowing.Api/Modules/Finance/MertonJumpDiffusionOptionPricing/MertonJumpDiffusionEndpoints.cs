using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.MertonJumpDiffusionOptionPricing;

public static class MertonJumpDiffusionEndpoints
{
    public static IServiceCollection AddMertonJumpDiffusionServices(this IServiceCollection services)
    {
        services.AddScoped<IMertonJumpDiffusionOptionEngine, MertonJumpDiffusionOptionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMertonJumpDiffusionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/merton-jump-diffusion")
            .WithTags("Finance Merton Jump-Diffusion Option Pricing");

        group.MapPost("/price", (MertonJumpDiffusionTelemetry telemetry, IMertonJumpDiffusionOptionEngine engine) =>
        {
            var result = engine.PriceOptionWithJumps(telemetry);
            return Results.Ok(result);
        })
        .WithName("PriceOptionWithJumps")
        .WithSummary("Prices options using Merton's Jump-Diffusion model, evaluating compound Poisson jump risk, variance decomposition, and tail risk hedging.");

        return endpoints;
    }
}
