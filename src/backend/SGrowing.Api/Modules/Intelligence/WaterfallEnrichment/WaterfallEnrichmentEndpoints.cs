using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Intelligence.WaterfallEnrichment;

public static class LeadWaterfallEnrichmentEndpoints
{
    public static IServiceCollection AddLeadWaterfallEnrichmentServices(this IServiceCollection services)
    {
        services.AddSingleton<IWaterfallEnrichmentEngine, WaterfallEnrichmentEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapLeadWaterfallEnrichmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/intelligence/waterfall-enrichment")
            .WithTags("Intelligence - Waterfall B2B Lead Enrichment");

        group.MapPost("/enrich", (WaterfallEnrichRequest request, IWaterfallEnrichmentEngine engine) =>
        {
            var result = engine.EnrichLead(request);
            return Results.Ok(result);
        })
        .WithName("EnrichLeadWaterfall")
        .WithSummary("Cascades across multiple B2B data providers to enrich account and contact profiles");

        group.MapGet("/providers", (IWaterfallEnrichmentEngine engine) =>
        {
            var metrics = engine.GetProviderMetrics();
            return Results.Ok(metrics);
        })
        .WithName("GetEnrichmentProviderMetrics")
        .WithSummary("Retrieves historical match rates and latency across Clearbit, Apollo, ZoomInfo, and PDL");

        group.MapPost("/verify-deliverability", (VerifyDeliverabilityRequest request, IWaterfallEnrichmentEngine engine) =>
        {
            var result = engine.VerifyDeliverability(request);
            return Results.Ok(result);
        })
        .WithName("VerifyLeadDeliverability")
        .WithSummary("Validates email deliverability via simulated SMTP MX lookup to prevent sender reputation decay");

        return endpoints;
    }
}
