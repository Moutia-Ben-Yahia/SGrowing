using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Media.ProsodyDubbing;

public static class ProsodyDubbingEndpoints
{
    public static IServiceCollection AddProsodyDubbingServices(this IServiceCollection services)
    {
        services.AddSingleton<ProsodyDubbingAlignmentEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapProsodyDubbingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/media/prosody-dubbing")
            .WithTags("Autonomous Multilingual AI Dubbing & Speech-To-Lip Synced Prosody Studio");

        group.MapPost("/align", (
            AlignProsodyDubbingRequest request,
            ProsodyDubbingAlignmentEngine engine) =>
        {
            var plan = engine.AlignDubbingProsody(request);
            return Results.Ok(plan);
        });

        return app;
    }
}
