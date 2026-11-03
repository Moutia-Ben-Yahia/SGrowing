using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Advertising.RetailMediaAuction;

public static class RetailMediaAuctionEndpoints
{
    public static IServiceCollection AddRetailMediaAuctionServices(this IServiceCollection services)
    {
        services.AddSingleton<IRetailMediaAuctionEngine, RetailMediaAuctionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRetailMediaAuctionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/advertising/retail-media")
            .WithTags("Advertising - Retail Media Network (RMN) Sponsored Ad Auction");

        group.MapGet("/overview", (IRetailMediaAuctionEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetRetailMediaOverview")
        .WithSummary("Retrieves RMN auction volumes, average clearing CPC, merchant ROAS, and recent ad winners");

        group.MapPost("/auction", (RunAuctionCommand command, IRetailMediaAuctionEngine engine) =>
        {
            try
            {
                var result = engine.RunAuction(command);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RunRetailMediaAuction")
        .WithSummary("Executes a real-time Generalized Second-Price (GSP) ad auction for sponsored search and product grids");

        return endpoints;
    }
}
