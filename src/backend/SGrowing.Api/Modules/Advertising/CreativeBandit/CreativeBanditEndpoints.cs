using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.CreativeBandit;

public static class CreativeBanditEndpoints
{
    public static IServiceCollection AddCreativeBanditServices(this IServiceCollection services)
    {
        services.AddSingleton<ICreativeBanditEngine, CreativeBanditEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCreativeBanditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/creative-bandit")
            .WithTags("Advertising - Creative Variance Bandit");

        group.MapPost("/generate", (GenerateVariantsRequest request, ICreativeBanditEngine engine) =>
        {
            var report = engine.GenerateVariants(request);
            return Results.Ok(report);
        })
        .WithName("GenerateCreativeVariants")
        .WithSummary("Generate orthogonal multimodal ad creative variations for bandit optimization");

        group.MapGet("/{campaignId}", (string campaignId, ICreativeBanditEngine engine) =>
        {
            var report = engine.GetCampaignReport(campaignId);
            return report is not null ? Results.Ok(report) : Results.NotFound();
        })
        .WithName("GetCampaignBanditReport")
        .WithSummary("Get active creative variants and Thompson Sampling allocations");

        group.MapPost("/allocate", (AllocateTrafficRequest request, ICreativeBanditEngine engine) =>
        {
            var report = engine.AllocateTraffic(request);
            return Results.Ok(report);
        })
        .WithName("AllocateBanditTraffic")
        .WithSummary("Run Thompson sampling iteration and allocate impression shares");

        group.MapPost("/{campaignId}/mutate/{parentVariantId}", (string campaignId, string parentVariantId, ICreativeBanditEngine engine) =>
        {
            var mutant = engine.SynthesizeMutant(campaignId, parentVariantId);
            return Results.Ok(mutant);
        })
        .WithName("SynthesizeCreativeMutant")
        .WithSummary("Synthesize AI evolved creative mutant to replace fatigued variants");

        return endpoints;
    }
}
