using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Attribution.MultiTouch;

public static class MultiTouchAttributionEndpoints
{
    public static IServiceCollection AddMultiTouchAttributionServices(this IServiceCollection services)
    {
        services.AddSingleton<IMultiTouchAttributionEngine, MultiTouchAttributionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMultiTouchAttributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/attribution/multi-touch")
            .WithTags("Domain I1 & I2 • Multi-Touch Revenue Attribution & Customer LTV Forecasting");

        group.MapPost("/touchpoints", (
            [FromBody] CustomerTouchpoint touchpoint,
            IMultiTouchAttributionEngine engine) =>
        {
            var recorded = engine.RecordTouchpoint(touchpoint);
            return Results.Ok(recorded);
        })
        .WithName("RecordCustomerTouchpoint")
        .WithSummary("Record a customer touchpoint for multi-touch attribution.");

        group.MapGet("/touchpoints/{tenantId}/{customerId}", (
            string tenantId,
            string customerId,
            IMultiTouchAttributionEngine engine) =>
        {
            var touchpoints = engine.GetTouchpoints(tenantId, customerId);
            return Results.Ok(touchpoints);
        })
        .WithName("GetCustomerTouchpoints")
        .WithSummary("Retrieve all customer touchpoints ordered chronologically.");

        group.MapPost("/calculate", (
            [FromBody] AttributionCalculationRequest request,
            IMultiTouchAttributionEngine engine) =>
        {
            var result = engine.CalculateAttribution(request);
            return Results.Ok(result);
        })
        .WithName("CalculateMultiTouchAttribution")
        .WithSummary("Calculate multi-touch revenue attribution across channels.");

        group.MapPost("/pltv/predict", (
            [FromBody] PredictLtvRequest request,
            IMultiTouchAttributionEngine engine) =>
        {
            var result = engine.PredictCustomerLtv(request);
            return Results.Ok(result);
        })
        .WithName("PredictCustomerLtv")
        .WithSummary("Forecast customer predictive LTV and retention risk tier.");

        return endpoints;
    }
}
