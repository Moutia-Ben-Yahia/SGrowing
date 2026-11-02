using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.BessArbitrage;

public static class BessArbitrageEndpoints
{
    public static IServiceCollection AddBessArbitrageServices(this IServiceCollection services)
    {
        services.AddSingleton<BessPeakShavingArbitrageEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBessArbitrageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/bess-arbitrage")
            .WithTags("Autonomous Energy Grid Peak Shaving & Battery Energy Storage (BESS) Arbitrage");

        group.MapPost("/optimize-schedule", (
            OptimizeBessScheduleRequest request,
            BessPeakShavingArbitrageEngine engine) =>
        {
            var plan = engine.OptimizeSchedule(request);
            return Results.Ok(plan);
        });

        return app;
    }
}
