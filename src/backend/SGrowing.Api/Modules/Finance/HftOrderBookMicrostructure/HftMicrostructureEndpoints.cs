using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.HftOrderBookMicrostructure;

public static class HftMicrostructureEndpoints
{
    public static IServiceCollection AddHftOrderBookMicrostructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IHftOrderBookMicrostructureEngine, HftOrderBookMicrostructureEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHftOrderBookMicrostructureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/hft")
            .WithTags("Finance - High-Frequency Trading Order Book Microstructure");

        group.MapPost("/evaluate-microstructure", (
            MicrostructureAnalysisRequest request,
            IHftOrderBookMicrostructureEngine engine) =>
        {
            var result = engine.EvaluateMicrostructure(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateHftOrderBookMicrostructure")
        .WithSummary("Calculates queue-imbalance micro-price, order flow imbalance (OFI), and cross-venue latency arbitrage signals")
        .Produces<MicrostructureEvaluationResult>(StatusCodes.Status200OK);

        return endpoints;
    }
}
