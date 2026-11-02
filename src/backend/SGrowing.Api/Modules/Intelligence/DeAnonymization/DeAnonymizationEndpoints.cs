using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Intelligence.DeAnonymization;

public static class DeAnonymizationEndpoints
{
    public static IServiceCollection AddDeAnonymizationServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeAnonymizationEngine, DeAnonymizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDeAnonymizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/intelligence/de-anonymization")
            .WithTags("Intelligence - B2B Intent De-Anonymization");

        group.MapPost("/de-anonymize", (DeAnonymizeRequest request, IDeAnonymizationEngine engine) =>
        {
            var result = engine.DeAnonymizeVisitor(request);
            return Results.Ok(result);
        })
        .WithName("DeAnonymizeVisitor")
        .WithSummary("De-anonymizes incoming visitor IP address into verified B2B corporate domain, firmographics, and buyer intent score");

        group.MapGet("/intent-feed", (IDeAnonymizationEngine engine) =>
        {
            var feed = engine.GetActiveIntentFeed();
            return Results.Ok(feed);
        })
        .WithName("GetActiveIntentFeed")
        .WithSummary("Retrieves prioritized B2B account intent feed with buying stage classifications");

        group.MapPost("/dispatch-alert", (DispatchAlertRequest request, IDeAnonymizationEngine engine) =>
        {
            var result = engine.DispatchAccountAlert(request);
            return Results.Ok(result);
        })
        .WithName("DispatchAccountAlert")
        .WithSummary("Dispatches real-time high-intent alert to assigned Account Executive via Slack, Email or WhatsApp");

        return endpoints;
    }
}
