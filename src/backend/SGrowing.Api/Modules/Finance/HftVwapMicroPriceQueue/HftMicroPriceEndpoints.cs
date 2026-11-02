using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.HftVwapMicroPriceQueue;

public static class HftMicroPriceEndpoints
{
    public static IServiceCollection AddHftMicroPriceServices(this IServiceCollection services)
    {
        services.AddScoped<IHftVwapMicroPriceQueueEngine, HftVwapMicroPriceQueueEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHftMicroPriceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/finance/hft-micro-price")
            .WithTags("Finance - HFT VWAP Micro-Price & Queue Position");

        group.MapPost("/analyze-queue", (
            LimitOrderBookQueueTelemetry telemetry,
            IHftVwapMicroPriceQueueEngine engine) =>
        {
            var result = engine.AnalyzeQueuePosition(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeLOBQueuePosition")
        .WithSummary("Analyzes L2 order book Stoikov micro-price, adverse selection index, and queue fill probability.");

        return app;
    }
}
