using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Intelligence.WinLoss;

public static class WinLossEndpoints
{
    public static IServiceCollection AddWinLossServices(this IServiceCollection services)
    {
        services.AddSingleton<IWinLossEngine, WinLossEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWinLossEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/intelligence/win-loss")
            .WithTags("Intelligence - Win-Loss Post-Mortem Intelligence");

        group.MapPost("/analyze", (AnalyzeDealRequest request, IWinLossEngine engine) =>
        {
            var result = engine.AnalyzeDeal(request);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDealPostMortem")
        .WithSummary("Performs root-cause analysis on won or lost enterprise deal");

        group.MapGet("/{dealId}", (string dealId, IWinLossEngine engine) =>
        {
            var pm = engine.GetPostMortemByDealId(dealId);
            return pm is not null ? Results.Ok(pm) : Results.NotFound();
        })
        .WithName("GetDealPostMortemById")
        .WithSummary("Retrieves post-mortem RCA report and competitor insights for a deal");

        group.MapGet("/all", (IWinLossEngine engine) =>
        {
            var all = engine.GetAllPostMortems();
            return Results.Ok(all);
        })
        .WithName("GetAllDealPostMortems")
        .WithSummary("Retrieves aggregated win/loss deals for trend intelligence");

        return endpoints;
    }
}
