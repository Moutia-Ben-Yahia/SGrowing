using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.VoiceDialect;

public static class VoiceDialectEndpoints
{
    public static IServiceCollection AddVoiceDialectServices(this IServiceCollection services)
    {
        services.AddSingleton<VoiceDialectEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVoiceDialectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/voice-dialect")
            .WithTags("Voice - Real-Time Dialect Adaptation & Cultural Localization");

        group.MapGet("/overview", (VoiceDialectEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetVoiceDialectOverview")
        .WithSummary("Retrieve regional dialect profiles, acoustic cadence configurations, and adapted phrase logs");

        group.MapPost("/adapt-phrase", (AdaptPhraseRequest request, VoiceDialectEngine engine) =>
        {
            var record = engine.AdaptPhrase(request);
            return Results.Ok(record);
        })
        .WithName("AdaptVoicePhrase")
        .WithSummary("Locally contextualizes standard agent dialog with native regional idioms and phonetics");

        group.MapPost("/register-profile", (RegisterDialectProfileRequest request, VoiceDialectEngine engine) =>
        {
            var profile = engine.RegisterProfile(request);
            return Results.Ok(profile);
        })
        .WithName("RegisterVoiceDialectProfile")
        .WithSummary("Registers regional prosody, pitch, cadence, and vernacular mapping for real-time synthesis");

        return endpoints;
    }
}
