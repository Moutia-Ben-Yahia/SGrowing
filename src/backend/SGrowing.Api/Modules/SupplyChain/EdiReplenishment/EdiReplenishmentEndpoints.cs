using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.EdiReplenishment;

public static class EdiReplenishmentEndpoints
{
    public static IServiceCollection AddEdiReplenishmentServices(this IServiceCollection services)
    {
        services.AddSingleton<EdiReplenishmentEngine>();
        return services;
    }

    public static RouteGroupBuilder MapEdiReplenishmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/supply-chain/replenishment")
            .WithTags("SupplyChainEdiReplenishment");

        group.MapGet("/recommendations", async Task<Ok<IReadOnlyList<ReplenishmentRecommendation>>> (
            [FromQuery] string? warehouseId,
            [FromServices] EdiReplenishmentEngine engine,
            CancellationToken ct) =>
        {
            var recommendations = await engine.GetReplenishmentRecommendationsAsync(warehouseId, ct);
            return TypedResults.Ok(recommendations);
        })
        .WithName("GetReplenishmentRecommendations")
        .WithSummary("Calculate stock depletion velocity and dynamic replenishment needs");

        group.MapPost("/orders", async Task<Results<Ok<EdiPurchaseOrder>, BadRequest<string>, NotFound<string>>> (
            [FromBody] GeneratePoRequest request,
            [FromServices] EdiReplenishmentEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var po = await engine.GeneratePurchaseOrderAsync(request, ct);
                return TypedResults.Ok(po);
            }
            catch (KeyNotFoundException ex)
            {
                return TypedResults.NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        })
        .WithName("GenerateEdi850PurchaseOrder")
        .WithSummary("Generate ANSI X12 EDI 850 purchase order payload for supplier");

        group.MapPost("/orders/{poNumber}/dispatch", async Task<Results<Ok<EdiPurchaseOrder>, NotFound<string>>> (
            [FromRoute] string poNumber,
            [FromServices] EdiReplenishmentEngine engine,
            CancellationToken ct) =>
        {
            try
            {
                var dispatched = await engine.DispatchPurchaseOrderAsync(poNumber, ct);
                return TypedResults.Ok(dispatched);
            }
            catch (KeyNotFoundException)
            {
                return TypedResults.NotFound($"Purchase order '{poNumber}' not found.");
            }
        })
        .WithName("DispatchEdiPurchaseOrder")
        .WithSummary("Transmit staged EDI 850 purchase order to supplier endpoint");

        group.MapGet("/suppliers/scorecards", async Task<Ok<IReadOnlyList<SupplierScorecard>>> (
            [FromServices] EdiReplenishmentEngine engine,
            CancellationToken ct) =>
        {
            var scorecards = await engine.GetSupplierScorecardsAsync(ct);
            return TypedResults.Ok(scorecards);
        })
        .WithName("GetSupplierScorecards")
        .WithSummary("Retrieve supplier reliability scorecards and SLA fill rates");

        return group;
    }
}
