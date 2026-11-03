using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.Uid2Tokenizer;

public static class Uid2TokenizerEndpoints
{
    public static IServiceCollection AddUid2TokenizerServices(this IServiceCollection services)
    {
        services.AddSingleton<IUid2TokenizerEngine, Uid2TokenizerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapUid2TokenizerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/uid2-tokenizer")
            .WithTags("Advertising - UID2 Tokenizer & Identity Graph");

        group.MapGet("/overview", (IUid2TokenizerEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetUid2TokenizerOverview")
        .WithSummary("Displays active token generation metrics, salt rotation posture, and graph resolution accuracy");

        group.MapPost("/generate-token", (GenerateTokenCommand command, IUid2TokenizerEngine engine) =>
        {
            var result = engine.GenerateToken(command);
            return Results.Ok(result);
        })
        .WithName("GenerateUid2Token")
        .WithSummary("Converts normalized PII (email/phone) into encrypted UID2 advertising tokens");

        group.MapPost("/stitch-identity", (StitchIdentityCommand command, IUid2TokenizerEngine engine) =>
        {
            var result = engine.StitchIdentity(command);
            return Results.Ok(result);
        })
        .WithName("StitchFirstPartyIdentity")
        .WithSummary("Merges anonymous web session identifiers with verified enterprise UID2 profiles");

        return app;
    }
}
