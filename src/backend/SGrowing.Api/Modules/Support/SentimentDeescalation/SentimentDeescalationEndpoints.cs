using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Support.SentimentDeescalation;

public static class SentimentDeescalationEndpoints
{
    public static IServiceCollection AddSentimentDeescalationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISentimentDeescalationEngine, SentimentDeescalationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSentimentDeescalationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/support/sentiment-deescalation")
            .WithTags("Support - Sentiment De-escalation & CSAT Swarm");

        group.MapPost("/submit-csat", (SubmitCsatRequest request, ISentimentDeescalationEngine engine) =>
        {
            var result = engine.SubmitCsat(request);
            return Results.Ok(result);
        })
        .WithName("SubmitCustomerCsat")
        .WithSummary("Ingests CSAT rating and automatically classifies sentiment and triage urgency");

        group.MapGet("/alerts", (ISentimentDeescalationEngine engine) =>
        {
            var alerts = engine.GetDeescalationAlerts();
            return Results.Ok(alerts);
        })
        .WithName("GetSentimentDeescalationAlerts")
        .WithSummary("Retrieves active customer dissatisfaction triage alerts");

        group.MapPost("/issue-goodwill", (IssueGoodwillRequest request, ISentimentDeescalationEngine engine) =>
        {
            var result = engine.IssueGoodwill(request);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("IssueCustomerGoodwillCompensation")
        .WithSummary("Issues account credits and priority SLAs to de-escalate at-risk customers");

        return endpoints;
    }
}
