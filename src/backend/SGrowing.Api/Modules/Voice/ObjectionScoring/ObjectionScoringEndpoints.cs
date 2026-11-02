using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Voice.ObjectionScoring;

public static class ObjectionScoringEndpoints
{
    public static IServiceCollection AddObjectionScoringServices(this IServiceCollection services)
    {
        services.AddSingleton<IObjectionScoringEngine, ObjectionScoringEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapObjectionScoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/voice/objection-scoring")
            .WithTags("Voice - Sales Objection Handling & Call Scoring");

        group.MapPost("/classify-and-rebut", (ObjectionRebuttalRequest request, IObjectionScoringEngine engine) =>
        {
            var result = engine.ClassifyAndRebut(request);
            return Results.Ok(result);
        })
        .WithName("ClassifyAndRebut")
        .WithSummary("Classifies spoken objection and provides psychological rebuttal with sub-200ms latency");

        group.MapPost("/score-call", (ScoreCallRequest request, IObjectionScoringEngine engine) =>
        {
            var result = engine.ScoreCall(request);
            return Results.Ok(result);
        })
        .WithName("ScoreCall")
        .WithSummary("Scores call transcript against enterprise sales rubrics (discovery, objection handling, closing)");

        group.MapGet("/recent-calls", (IObjectionScoringEngine engine) =>
        {
            var calls = engine.GetRecentScoredCalls();
            return Results.Ok(calls);
        })
        .WithName("GetRecentScoredCalls")
        .WithSummary("Retrieves recently scored voice calls with qualification breakdown");

        return endpoints;
    }
}
