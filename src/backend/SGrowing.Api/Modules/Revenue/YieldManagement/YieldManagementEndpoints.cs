using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Revenue.YieldManagement;

public static class YieldManagementEndpoints
{
    public static IServiceCollection AddYieldManagementServices(this IServiceCollection services)
    {
        services.AddSingleton<DynamicYieldManagementEngine>();
        services.AddSingleton<PriceElasticityDemandForecaster>();
        services.AddSingleton<SpotCapacityAuctioneerService>();
        return services;
    }

    public static IEndpointRouteBuilder MapYieldManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/revenue/yield-management")
            .WithTags("Autonomous Dynamic Revenue Yield Management");

        group.MapPost("/evaluate", (
            EvaluateBookingRequest request,
            DynamicYieldManagementEngine engine) =>
        {
            var result = engine.EvaluateBooking(request);
            return Results.Ok(result);
        })
        .WithName("EvaluateBookingYield")
        .WithSummary("Evaluates booking request against EMSRb capacity protection levels.");

        group.MapGet("/pools/{poolId}/protection", (
            string poolId,
            DynamicYieldManagementEngine engine) =>
        {
            var levels = engine.CalculateProtectionLevels(poolId);
            return Results.Ok(levels);
        })
        .WithName("GetCapacityProtectionLevels")
        .WithSummary("Calculates nested capacity protection limits for all configured fare tiers.");

        group.MapPost("/elasticity", (
            CapacityType capacityType,
            decimal price1,
            double demand1,
            decimal price2,
            double demand2,
            decimal marginalCost,
            PriceElasticityDemandForecaster forecaster) =>
        {
            var report = forecaster.CalculateElasticity(capacityType, price1, demand1, price2, demand2, marginalCost);
            return Results.Ok(report);
        })
        .WithName("CalculatePriceElasticity")
        .WithSummary("Computes arc price elasticity and optimal revenue-maximizing price point.");

        group.MapPost("/auctions", (
            CreateSpotAuctionRequest request,
            SpotCapacityAuctioneerService auctioneer) =>
        {
            var lot = auctioneer.CreateAuctionLot(request);
            return Results.Created($"/api/v1/revenue/yield-management/auctions/{lot.LotId}", lot);
        })
        .WithName("CreateSpotCapacityAuction")
        .WithSummary("Creates a high-velocity Dutch descending auction for expiring spot capacity.");

        group.MapGet("/auctions/{lotId}", (
            string lotId,
            SpotCapacityAuctioneerService auctioneer) =>
        {
            var lot = auctioneer.GetCurrentLotState(lotId);
            return Results.Ok(lot);
        })
        .WithName("GetSpotAuctionState")
        .WithSummary("Retrieves current real-time decayed price and status of a spot auction lot.");

        group.MapPost("/auctions/bid", (
            SubmitAuctionBidRequest request,
            SpotCapacityAuctioneerService auctioneer) =>
        {
            var settled = auctioneer.SubmitBid(request);
            return Results.Ok(settled);
        })
        .WithName("SubmitSpotAuctionBid")
        .WithSummary("Submits a winning bid on an active Dutch spot auction lot.");

        return app;
    }
}
