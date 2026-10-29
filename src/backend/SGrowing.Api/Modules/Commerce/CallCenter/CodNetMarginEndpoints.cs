using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Commerce.CallCenter;

public record CalculateMarginRequest(
    Guid OrderId,
    string OrderNumber,
    decimal GrossRevenue,
    decimal Cogs,
    decimal PackagingCost,
    decimal AdSpendCost,
    decimal CourierShippingFee,
    decimal ReturnRatePercent,
    decimal ReturnPenaltyFee
);

public static class CodNetMarginEndpoints
{
    public static IEndpointRouteBuilder MapCodNetMarginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/commerce/margins")
            .WithTags("CodNetMargins");

        group.MapPost("/calculate", async (
            Guid tenantId,
            CalculateMarginRequest req,
            ICodNetMarginLedgerService service) =>
        {
            var result = await service.CalculateAndRecordMarginAsync(
                tenantId,
                req.OrderId,
                req.OrderNumber,
                req.GrossRevenue,
                req.Cogs,
                req.PackagingCost,
                req.AdSpendCost,
                req.CourierShippingFee,
                req.ReturnRatePercent,
                req.ReturnPenaltyFee);

            return Results.Ok(result);
        });

        group.MapGet("/orders/{orderId:guid}", async (
            Guid tenantId,
            Guid orderId,
            ICodNetMarginLedgerService service) =>
        {
            var result = await service.GetOrderMarginAsync(tenantId, orderId);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/summary", async (
            Guid tenantId,
            DateTime? fromUtc,
            ICodNetMarginLedgerService service) =>
        {
            var summary = await service.GetTenantMarginSummaryAsync(tenantId, fromUtc);
            return Results.Ok(summary);
        });

        return app;
    }
}
