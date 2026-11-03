using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Finance.TreasuryYield;

public static class TreasuryYieldEndpoints
{
    public static IServiceCollection AddTreasuryYieldServices(this IServiceCollection services)
    {
        services.AddSingleton<ITreasuryYieldEngine, TreasuryYieldEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapTreasuryYieldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/treasury-yield")
            .WithTags("Finance - Treasury Cash Flow Forecasting & Yield Optimizer");

        group.MapGet("/overview", (ITreasuryYieldEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetTreasuryYieldOverview")
        .WithSummary("Retrieves 30/60/90-day cash projections, liquidity reserve status, and treasury yield allocations");

        group.MapPost("/sweep", (SweepTreasurySurplusCommand command, ITreasuryYieldEngine engine) =>
        {
            try
            {
                var result = engine.SweepSurplus(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Instrument {command.InstrumentId} not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SweepTreasurySurplus")
        .WithSummary("Executes an automated sweep of surplus working capital into high-yield treasury instruments");

        return endpoints;
    }
}
