using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.CRM.RetentionConcierge;

public static class RetentionConciergeEndpoints
{
    public static IServiceCollection AddRetentionConciergeServices(this IServiceCollection services)
    {
        services.AddSingleton<PredictiveChurnSurvivalEngine>();
        services.AddSingleton<RetentionOfferBanditService>();
        services.AddSingleton<ProactiveDelightConciergeService>();
        return services;
    }

    public static IEndpointRouteBuilder MapRetentionConciergeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/retention-concierge")
            .WithTags("Autonomous AI Customer Retention Concierge");

        group.MapPost("/analyze", (
            AnalyzeChurnRiskRequest request,
            PredictiveChurnSurvivalEngine engine) =>
        {
            var prediction = engine.PredictChurn(request.Profile);
            return Results.Ok(prediction);
        })
        .WithName("AnalyzeCustomerChurnRisk")
        .WithSummary("Calculates survival curve, median time to churn, and risk factors using Cox/Weibull regression.");

        group.MapPost("/delight/evaluate", (
            DispatchDelightRequest request,
            ProactiveDelightConciergeService concierge) =>
        {
            var action = concierge.EvaluateAndDispatchDelight(request);
            return action is not null ? Results.Ok(action) : Results.Ok(new { Dispatched = false, Reason = "Customer hazard below retention threshold" });
        })
        .WithName("EvaluateAndDispatchDelight")
        .WithSummary("Autonomously triggers personalized delight package if churn hazard threshold is exceeded.");

        group.MapPost("/delight/{actionId}/redeem", (
            string actionId,
            decimal? retainedLtvDzd,
            ProactiveDelightConciergeService concierge) =>
        {
            var success = concierge.RedeemAction(actionId, retainedLtvDzd ?? 0m);
            return success ? Results.Ok(new { Redeemed = true }) : Results.NotFound();
        })
        .WithName("RedeemDelightAction")
        .WithSummary("Records redemption of a retention goodwill package and trains the contextual bandit.");

        group.MapGet("/bandit/stats", (
            RetentionOfferBanditService bandit) =>
        {
            var stats = bandit.GetArmStatistics();
            return Results.Ok(stats);
        })
        .WithName("GetBanditStats")
        .WithSummary("Retrieves Bayesian Thompson Sampling arm conversion rates and retained LTV metrics.");

        return app;
    }
}
