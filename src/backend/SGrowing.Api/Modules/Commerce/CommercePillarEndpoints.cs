using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.Commerce.AntiFraud;
using SGrowing.Api.Modules.Commerce.Shipping;

namespace SGrowing.Api.Modules.Commerce;

public static class CommercePillarEndpoints
{
    public static IEndpointRouteBuilder MapCommercePillarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce")
            .WithTags("COD Logistics & Anti-Fraud");

        group.MapPost("/shipping/waybill", async (Guid tenantId, WaybillGenerationRequest req, ICarrierShippingGateway shipping, CancellationToken ct) =>
        {
            var res = await shipping.GenerateWaybillAsync(tenantId, req, ct);
            return Results.Ok(res);
        })
        .WithName("GenerateCarrierWaybill")
        .WithSummary("Generate carrier waybill manifest and tracking barcode for regional couriers");

        group.MapGet("/shipping/track", async (Guid tenantId, string courierCode, string trackingNumber, ICarrierShippingGateway shipping, CancellationToken ct) =>
        {
            var res = await shipping.TrackShipmentAsync(tenantId, courierCode, trackingNumber, ct);
            return Results.Ok(res);
        })
        .WithName("TrackCarrierShipment")
        .WithSummary("Retrieve standardized tracking checkpoints across courier networks");

        group.MapPost("/shipping/label", async (Guid tenantId, string courierCode, string trackingNumber, LabelFormat format, ICarrierShippingGateway shipping, CancellationToken ct) =>
        {
            var res = await shipping.GenerateShippingLabelAsync(tenantId, courierCode, trackingNumber, format, ct);
            return Results.Ok(res);
        })
        .WithName("GenerateShippingLabel")
        .WithSummary("Generate thermal or PDF shipping label with AWB barcode");

        group.MapPost("/cod/evaluate-fraud", async (Guid tenantId, CodFraudEvaluationRequest req, ICodAntiFraudEngine antiFraud, CancellationToken ct) =>
        {
            var res = await antiFraud.EvaluateOrderRiskAsync(tenantId, req, ct);
            return Results.Ok(res);
        })
        .WithName("EvaluateCodOrderRisk")
        .WithSummary("Multi-factor COD risk scoring and fake order detection engine");

        return app;
    }
}
