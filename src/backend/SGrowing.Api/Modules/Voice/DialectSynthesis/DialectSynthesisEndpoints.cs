using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Voice.DialectSynthesis;

public static class DialectSynthesisEndpoints
{
    public static IServiceCollection AddDialectSynthesisServices(this IServiceCollection services)
    {
        services.AddSingleton<IDialectSynthesisEngine, DialectSynthesisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDialectSynthesisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/voice/dialect-synthesis")
            .WithTags("Real-Time Dialect Synthesis & Vernacular Tone Adapter Engine");

        group.MapGet("/profiles", ([FromServices] IDialectSynthesisEngine engine) =>
        {
            var profiles = engine.GetProfiles();
            return Results.Ok(profiles);
        })
        .WithName("GetDialectProfiles")
        .WithSummary("Retrieves all regional dialect acoustic and idiom profiles");

        group.MapPost("/detect", ([FromBody] DetectDialectRequest request, [FromServices] IDialectSynthesisEngine engine) =>
        {
            var result = engine.DetectDialect(request);
            return Results.Ok(result);
        })
        .WithName("DetectDialectVernacular")
        .WithSummary("Identifies regional accents and cultural vernacular tokens in real time");

        group.MapPost("/synthesize", ([FromBody] SynthesizeDialectRequest request, [FromServices] IDialectSynthesisEngine engine) =>
        {
            var utterance = engine.SynthesizeDialect(request);
            return Results.Ok(utterance);
        })
        .WithName("SynthesizeDialectResponse")
        .WithSummary("Synthesizes dialect-adapted speech with sub-45ms latency and pitch modulation");

        return app;
    }
}
