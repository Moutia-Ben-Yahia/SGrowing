using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Commerce.CommissionSettlement;

public static class CommissionSettlementEndpoints
{
    public static IServiceCollection AddCommissionSettlementServices(this IServiceCollection services)
    {
        services.AddSingleton<ICommissionSettlementEngine, CommissionSettlementEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCommissionSettlementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/commerce/commission-settlement")
            .WithTags("Commerce - Sales Commission Settlement & Rep Escrow");

        group.MapGet("/overview", (ICommissionSettlementEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetCommissionSettlementOverview")
        .WithSummary("Retrieves rep quota attainment, commission payouts, and clawback escrow balances");

        group.MapPost("/calculate", (CalculateCommissionRequest request, ICommissionSettlementEngine engine) =>
        {
            var record = engine.CalculateCommission(request);
            return Results.Ok(record);
        })
        .WithName("CalculateSalesCommission")
        .WithSummary("Calculates tiered commission with accelerators and 80/20 escrow split upon deal closing");

        group.MapPost("/release-escrow", (ReleaseEscrowCommand command, ICommissionSettlementEngine engine) =>
        {
            try
            {
                var result = engine.ReleaseEscrow(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Commission {command.CommissionId} not found." });
            }
        })
        .WithName("ReleaseCommissionEscrow")
        .WithSummary("Releases 90-day clawback escrow to rep upon customer invoice settlement");

        return endpoints;
    }
}
