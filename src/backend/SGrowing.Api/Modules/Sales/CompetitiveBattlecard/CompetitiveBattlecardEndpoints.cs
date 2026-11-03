using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Sales.CompetitiveBattlecard;

public static class CompetitiveBattlecardEndpoints
{
    public static IServiceCollection AddCompetitiveBattlecardServices(this IServiceCollection services)
    {
        services.AddSingleton<CompetitiveBattlecardEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCompetitiveBattlecardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sales/competitive-battlecard")
            .WithTags("Sales - Competitive Win-Loss Intelligence & Battlecards");

        group.MapGet("/overview", (CompetitiveBattlecardEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetCompetitiveBattlecardOverview")
        .WithSummary("Retrieve tracked competitor win rates, displacement playbooks, and win-loss signals");

        group.MapPost("/generate", (GenerateBattlecardRequest request, CompetitiveBattlecardEngine engine) =>
        {
            var card = engine.GenerateBattlecard(request);
            return Results.Ok(card);
        })
        .WithName("GenerateLiveBattlecard")
        .WithSummary("Dynamically synthesizes tailored objection kill-points and commercial pricing counters");

        group.MapPost("/signal", (SubmitWinLossSignalRequest request, CompetitiveBattlecardEngine engine) =>
        {
            var signal = engine.SubmitWinLossSignal(request);
            return Results.Ok(signal);
        })
        .WithName("SubmitWinLossSignal")
        .WithSummary("Log real-time win or loss deal outcome to recalibrate competitive win probability");

        return endpoints;
    }
}
