using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.SpendAutopilot;

public static class AdSpendAutopilotEndpoints
{
    public static IServiceCollection AddAdSpendAutopilotServices(this IServiceCollection services)
    {
        services.AddSingleton<AdSpendAutopilotEngine>();
        return services;
    }

    public static RouteGroupBuilder MapAdSpendAutopilotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/autopilot")
            .WithTags("AdvertisingSpendAutopilot");

        group.MapGet("/campaigns", async Task<Ok<IReadOnlyList<AdCampaignChannel>>> (
            [FromServices] AdSpendAutopilotEngine engine,
            CancellationToken ct) =>
        {
            var campaigns = await engine.GetCampaignsAsync(ct);
            return TypedResults.Ok(campaigns);
        })
        .WithName("GetAutopilotCampaigns")
        .WithSummary("Retrieve multi-channel campaigns managed by ad spend autopilot");

        group.MapGet("/recommendations", async Task<Ok<IReadOnlyList<BudgetRebalanceRecommendation>>> (
            [FromServices] AdSpendAutopilotEngine engine,
            CancellationToken ct) =>
        {
            var recs = await engine.GetRebalanceRecommendationsAsync(ct);
            return TypedResults.Ok(recs);
        })
        .WithName("GetBudgetRebalanceRecommendations")
        .WithSummary("Calculate marginal ROAS budget shift recommendations");

        group.MapPost("/rebalance", async Task<Results<Ok<AdCampaignChannel>, NotFound<string>, BadRequest<string>>> (
            [FromBody] RebalanceBudgetRequest request,
            [FromServices] AdSpendAutopilotEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var result = await engine.RebalanceBudgetAsync(request, ct);
                return TypedResults.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("ExecuteBudgetRebalance")
        .WithSummary("Autonomous re-allocation of daily budget between campaigns");

        group.MapPost("/bid-shading", async Task<Results<Ok<BidShadingTelemetry>, NotFound<string>>> (
            [FromBody] ExecuteBidShadingRequest request,
            [FromServices] AdSpendAutopilotEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var telemetry = await engine.ExecuteBidShadingAsync(request.CampaignId, request.RawBid, ct);
                return TypedResults.Ok(telemetry);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(ex.Message);
            }
        })
        .WithName("ExecuteBidShading")
        .WithSummary("Simulate first-price auction bid shaving to maximize win-rate efficiency");

        group.MapGet("/summary", async Task<Ok<AutopilotEngineSummary>> (
            [FromServices] AdSpendAutopilotEngine engine,
            CancellationToken ct) =>
        {
            var summary = await engine.GetSummaryAsync(ct);
            return TypedResults.Ok(summary);
        })
        .WithName("GetAutopilotSummary")
        .WithSummary("Retrieve blended ROAS, managed spend, and total bid shading savings");

        return group;
    }
}

public record ExecuteBidShadingRequest(
    string CampaignId,
    decimal RawBid
);
