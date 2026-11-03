using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Personalization.ContentPersonalization;

public static class ContentPersonalizationEndpoints
{
    public static IServiceCollection AddContentPersonalizationServices(this IServiceCollection services)
    {
        services.AddSingleton<ContentPersonalizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapContentPersonalizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/personalization/content-personalization")
            .WithTags("Personalization - Dynamic Content Mesh");

        group.MapGet("/overview", (ContentPersonalizationEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetContentPersonalizationOverview")
        .WithSummary("Retrieve active personalization rules, firmographic segments, and conversion metrics");

        group.MapPost("/evaluate", (EvaluatePersonalizationRequest request, ContentPersonalizationEngine engine) =>
        {
            var result = engine.EvaluatePagePersonalization(request);
            return Results.Ok(result);
        })
        .WithName("EvaluatePagePersonalization")
        .WithSummary("Evaluate visiting visitor firmographics and return tailored dynamic content manifest");

        group.MapPost("/record-conversion", (RecordConversionRequest request, ContentPersonalizationEngine engine) =>
        {
            var receipt = engine.RecordConversion(request);
            return Results.Ok(receipt);
        })
        .WithName("RecordPersonalizationConversion")
        .WithSummary("Record visitor conversion and update multi-armed bandit variant weights");

        return endpoints;
    }
}
