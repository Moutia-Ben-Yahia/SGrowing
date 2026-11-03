namespace SGrowing.Api.Modules.Sales.DealCoaching;

public static class DealCoachingEndpoints
{
    public static IServiceCollection AddDealCoachingServices(this IServiceCollection services)
    {
        services.AddSingleton<IDealCoachingEngine, DealCoachingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDealCoachingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/deal-coaching")
            .WithTags("Sales - Live Deal Coaching");

        group.MapGet("/overview", (IDealCoachingEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/start-session", (StartSessionRequest req, IDealCoachingEngine engine) =>
        {
            var session = engine.StartSession(req.OpportunityId, req.AccountName, req.SalesRepName);
            return Results.Ok(session);
        });

        group.MapPost("/detect-objection", (DetectObjectionRequest req, IDealCoachingEngine engine) =>
        {
            var detected = engine.DetectObjection(req.SessionId, req.TranscribedSnippet);
            return Results.Ok(detected);
        });

        group.MapGet("/scorecard/{repName}", (string repName, IDealCoachingEngine engine) =>
        {
            var scorecard = engine.EvaluateRepScorecard(repName);
            return Results.Ok(scorecard);
        });

        return endpoints;
    }
}

public sealed record StartSessionRequest(string OpportunityId, string AccountName, string SalesRepName);
public sealed record DetectObjectionRequest(string SessionId, string TranscribedSnippet);
