using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Support.Localization;

public static class SupportLocalizationEndpoints
{
    public static IServiceCollection AddSupportLocalizationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISupportLocalizationEngine, SupportLocalizationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSupportLocalizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/support/localization")
            .WithTags("Support - Multilingual Localization Mesh");

        group.MapPost("/translate", (TranslateSupportMessageRequest request, ISupportLocalizationEngine engine) =>
        {
            var result = engine.TranslateMessage(request);
            return Results.Ok(result);
        })
        .WithName("TranslateSupportMessage")
        .WithSummary("Perform low-latency culturally-attuned support message translation");

        group.MapGet("/glossary", (ISupportLocalizationEngine engine) =>
        {
            var glossary = engine.GetGlossary();
            return Results.Ok(glossary);
        })
        .WithName("GetSupportLocalizationGlossary")
        .WithSummary("Retrieve non-translated protected brand and technical vocabulary");

        group.MapGet("/message/{messageId}", (string messageId, ISupportLocalizationEngine engine) =>
        {
            var msg = engine.GetMessageById(messageId);
            return msg is not null ? Results.Ok(msg) : Results.NotFound();
        })
        .WithName("GetLocalizedMessageById")
        .WithSummary("Retrieve localized message and cultural nuance audit score");

        return endpoints;
    }
}
