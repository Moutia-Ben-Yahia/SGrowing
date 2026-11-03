using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.CapTableEquity;

public static class CapTableEquityEndpoints
{
    public static IServiceCollection AddCapTableEquityServices(this IServiceCollection services)
    {
        services.AddSingleton<CapTableEquityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCapTableEquityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/cap-table-equity")
            .WithTags("Finance - Cap Table Equity Dilution & Stock Option Grants");

        group.MapGet("/overview", (CapTableEquityEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetCapTableEquityOverview")
        .WithSummary("Retrieve fully diluted share capitalization, 409A valuations, and employee option vesting ledger");

        group.MapPost("/grant-options", (GrantStockOptionRequest request, CapTableEquityEngine engine) =>
        {
            var grant = engine.GrantStockOption(request);
            return Results.Ok(grant);
        })
        .WithName("GrantStockOption")
        .WithSummary("Issues statutory employee incentive stock options with configurable cliff and vesting schedule");

        group.MapPost("/simulate-dilution", (SimulateDilutionRequest request, CapTableEquityEngine engine) =>
        {
            var stakeholder = engine.SimulateDilution(request);
            return Results.Ok(stakeholder);
        })
        .WithName("SimulateEquityDilution")
        .WithSummary("Simulates post-money dilution and option pool expansion for venture funding rounds");

        return endpoints;
    }
}
