using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Marketing.AdSpendRedistribution;

public static class AdSpendRedistributionEndpoints
{
    public static IServiceCollection AddAdSpendRedistributionServices(this IServiceCollection services)
    {
        services.AddSingleton<IAdSpendRedistributionEngine, AdSpendRedistributionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdSpendRedistributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/marketing/ad-spend-redistribution")
            .WithTags("Marketing - Shapley Value Attribution & Ad Spend Redistribution Autopilot");

        group.MapGet("/overview", (IAdSpendRedistributionEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetAdSpendRedistributionOverview")
        .WithSummary("Retrieves cooperative game theory Shapley attribution, heuristic bias, and budget shift recommendations");

        group.MapPost("/reallocate", (ReallocateBudgetCommand command, IAdSpendRedistributionEngine engine) =>
        {
            try
            {
                var result = engine.ReallocateBudget(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Channel {command.ChannelId} not found." });
            }
        })
        .WithName("ReallocateAdSpendRedistributionBudget")
        .WithSummary("Executes budget shift recommendation across marketing channels based on Shapley marginal yield");

        return endpoints;
    }
}
