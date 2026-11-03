namespace SGrowing.Api.Modules.Sales.SpiffGamification;

public static class SpiffGamificationEndpoints
{
    public static IServiceCollection AddSpiffGamificationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISpiffGamificationEngine, SpiffGamificationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSpiffGamificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/spiff-gamification")
            .WithTags("Sales - Performance SPIFF & Deal Gamification");

        group.MapGet("/overview", (ISpiffGamificationEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/create-campaign", (CreateSpiffCampaignRequest req, ISpiffGamificationEngine engine) =>
        {
            var camp = engine.CreateCampaign(req.Name, req.RewardType, req.RewardPerDeal, req.TotalBudget);
            return Results.Ok(camp);
        });

        group.MapPost("/award-spiff", (AwardSpiffRequest req, ISpiffGamificationEngine engine) =>
        {
            var escrow = engine.AwardDealSpiff(req.CampaignId, req.DealId, req.SalesRepName);
            return Results.Ok(escrow);
        });

        group.MapGet("/leaderboard", (ISpiffGamificationEngine engine) =>
            Results.Ok(engine.GetLeaderboard()));

        return endpoints;
    }
}

public sealed record CreateSpiffCampaignRequest(string Name, SpiffRewardType RewardType, double RewardPerDeal, double TotalBudget);
public sealed record AwardSpiffRequest(string CampaignId, string DealId, string SalesRepName);
