using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Funnels;

public record CreateFunnelDto(Guid TenantId, string Name, string Slug, string Description = "", string Currency = "TND");
public record AddFunnelStepDto(Guid TenantId, string StepName, FunnelStepType StepType, int StepOrder, string Slug = "", string TemplateJson = "{}");
public record CreateOrderBumpDto(Guid TenantId, Guid StepId, string Title, string Headline, string Sku, decimal OriginalPrice, decimal DiscountedPrice, string BadgeText = "SPECIAL IN-CHECKOUT BUMP");
public record CreateUpsellDto(Guid TenantId, Guid StepId, string Title, string Sku, string Headline, decimal OriginalPrice, decimal UpsellPrice, string ImageUrl = "");
public record RecordFunnelEventDto(Guid TenantId, FunnelEventType EventType, decimal Value = 0m, Guid? StepId = null, Guid? OrderId = null, string? CustomerPhone = null);
public record ResolveVariantRequest(Guid TenantId, Guid StepId, string VisitorCookieId, int VariantAWeight = 50);
public record OneClickUpsellRequest(Guid TenantId, Guid OrderId, Guid UpsellOfferId, string VaultToken);

public static class FunnelEndpoints
{
    public static IEndpointRouteBuilder MapFunnelEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/funnels").WithTags("AI Sales Funnel Builder & 1-Click Upsells");

        group.MapPost("/", async ([FromBody] CreateFunnelDto req, ISalesFunnelService service) =>
        {
            var funnel = await service.CreateFunnelAsync(req.TenantId, req.Name, req.Slug, req.Description, req.Currency);
            return Results.Ok(funnel);
        });

        group.MapGet("/", async ([FromQuery] Guid tenantId, ISalesFunnelService service) =>
        {
            var funnels = await service.GetFunnelsAsync(tenantId);
            return Results.Ok(funnels);
        });

        group.MapGet("/{id:guid}", async (Guid id, [FromQuery] Guid tenantId, ISalesFunnelService service) =>
        {
            var funnel = await service.GetFunnelByIdAsync(tenantId, id);
            return funnel != null ? Results.Ok(funnel) : Results.NotFound(new { error = "Funnel not found" });
        });

        group.MapPost("/{id:guid}/steps", async (Guid id, [FromBody] AddFunnelStepDto req, ISalesFunnelService service) =>
        {
            var step = await service.AddFunnelStepAsync(req.TenantId, id, req.StepName, req.StepType, req.StepOrder, req.Slug, req.TemplateJson);
            return Results.Ok(step);
        });

        group.MapGet("/{id:guid}/steps", async (Guid id, [FromQuery] Guid tenantId, ISalesFunnelService service) =>
        {
            var steps = await service.GetFunnelStepsAsync(tenantId, id);
            return Results.Ok(steps);
        });

        group.MapPost("/{id:guid}/order-bumps", async (Guid id, [FromBody] CreateOrderBumpDto req, ISalesFunnelService service) =>
        {
            var bump = await service.CreateOrderBumpAsync(req.TenantId, id, req.StepId, req.Title, req.Headline, req.Sku, req.OriginalPrice, req.DiscountedPrice, req.BadgeText);
            return Results.Ok(bump);
        });

        group.MapGet("/{id:guid}/order-bumps", async (Guid id, [FromQuery] Guid tenantId, ISalesFunnelService service) =>
        {
            var bumps = await service.GetOrderBumpsAsync(tenantId, id);
            return Results.Ok(bumps);
        });

        group.MapPost("/{id:guid}/upsells", async (Guid id, [FromBody] CreateUpsellDto req, ISalesFunnelService service) =>
        {
            var upsell = await service.CreateOneClickUpsellAsync(req.TenantId, id, req.StepId, req.Title, req.Sku, req.Headline, req.OriginalPrice, req.UpsellPrice, req.ImageUrl);
            return Results.Ok(upsell);
        });

        group.MapGet("/{id:guid}/upsells", async (Guid id, [FromQuery] Guid tenantId, ISalesFunnelService service) =>
        {
            var upsells = await service.GetOneClickUpsellsAsync(tenantId, id);
            return Results.Ok(upsells);
        });

        group.MapPost("/{id:guid}/events", async (Guid id, [FromBody] RecordFunnelEventDto req, ISalesFunnelService service) =>
        {
            var ev = await service.RecordConversionEventAsync(req.TenantId, id, req.EventType, req.Value, req.StepId, req.OrderId, req.CustomerPhone);
            return Results.Ok(ev);
        });

        group.MapGet("/{id:guid}/performance", async (Guid id, [FromQuery] Guid tenantId, ISalesFunnelService service) =>
        {
            var perf = await service.GetFunnelPerformanceAsync(tenantId, id);
            return Results.Ok(perf);
        });

        group.MapPost("/{id:guid}/resolve-variant", async (Guid id, [FromBody] ResolveVariantRequest req, ISalesFunnelService service) =>
        {
            var result = await service.ResolveFunnelVariantAsync(req.TenantId, id, req.StepId, req.VisitorCookieId, req.VariantAWeight);
            return Results.Ok(result);
        });

        group.MapPost("/orders/{orderId:guid}/one-click-upsell", async (Guid orderId, [FromBody] OneClickUpsellRequest req, ISalesFunnelService service) =>
        {
            var result = await service.ProcessOneClickUpsellChargeAsync(req.TenantId, orderId, req.UpsellOfferId, req.VaultToken);
            return Results.Ok(result);
        });

        return endpoints;
    }
}
