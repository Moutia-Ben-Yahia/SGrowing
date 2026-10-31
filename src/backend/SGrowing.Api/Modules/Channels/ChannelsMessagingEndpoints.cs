using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Channels.WhatsApp.Catalog;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels;

public static class ChannelsMessagingEndpoints
{
    public static IEndpointRouteBuilder MapChannelsMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        // WhatsApp Templates & Broadcasts
        app.MapGet("/api/channels/whatsapp/templates", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var templates = await db.WhatsAppTemplates.Where(t => t.TenantId == tenantId).ToListAsync();
            return Results.Ok(templates);
        });

        app.MapPost("/api/channels/whatsapp/templates", async ([FromBody] WhatsAppTemplate template, SGrowingDbContext db) =>
        {
            db.WhatsAppTemplates.Add(template);
            await db.SaveChangesAsync();
            return Results.Ok(template);
        });

        app.MapGet("/api/channels/broadcasts", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var broadcasts = await db.BroadcastCampaigns.Where(b => b.TenantId == tenantId).ToListAsync();
            return Results.Ok(broadcasts);
        });

        app.MapPost("/api/channels/broadcasts", async ([FromBody] BroadcastCampaign campaign, SGrowingDbContext db) =>
        {
            db.BroadcastCampaigns.Add(campaign);
            await db.SaveChangesAsync();
            return Results.Ok(campaign);
        });

        app.MapPost("/api/channels/broadcasts/{id:guid}/dispatch", async (Guid id, [FromQuery] Guid tenantId, BroadcastDispatcherService dispatcher) =>
        {
            var res = await dispatcher.DispatchBroadcastAsync(tenantId, id);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        // WhatsApp Interactive Catalog & Inbound Cart
        app.MapPost("/api/channels/whatsapp/catalog/sync", async ([FromBody] SyncMetaCatalogDto req, IWhatsAppCatalogEngine catalogEngine) =>
        {
            var feed = await catalogEngine.SyncProductsToMetaCatalogAsync(req.TenantId, req.CatalogId);
            return Results.Ok(feed);
        });

        app.MapGet("/api/channels/whatsapp/catalog/feed", async ([FromQuery] Guid tenantId, IWhatsAppCatalogEngine catalogEngine) =>
        {
            var feedJson = await catalogEngine.GenerateMetaCatalogFeedJsonAsync(tenantId);
            return Results.Content(feedJson, "application/json");
        });

        app.MapPost("/api/channels/whatsapp/catalog/order-webhook", async ([FromBody] WhatsAppInboundOrderWebhookDto req, IWhatsAppCatalogEngine catalogEngine) =>
        {
            var order = await catalogEngine.ProcessInboundWhatsAppOrderWebhookAsync(req.TenantId, req.CustomerPhone, req.CustomerName, req.CatalogId, req.Items, req.ShippingAddress, req.ShippingGovernorate);
            return Results.Ok(order);
        });

        // Live Social Commerce
        app.MapPost("/api/channels/live/process-comment", async ([FromBody] ProcessLiveCommentDto req, ILiveCommerceOverlayService liveService) =>
        {
            var result = await liveService.ProcessLiveCommentAsync(req.TenantId, req.SessionId, req.CommenterUsername, req.CommentText, req.CustomerPhone, req.Governorate);
            return Results.Ok(result);
        });

        app.MapGet("/api/channels/live/{sessionId}/overlay", async (Guid sessionId, [FromQuery] Guid tenantId, ILiveCommerceOverlayService liveService) =>
        {
            var overlay = await liveService.GetLiveOverlayStockFeedAsync(tenantId, sessionId);
            return Results.Ok(overlay);
        });

        // WhatsApp Flow Engine
        app.MapGet("/api/channels/flows", async ([FromQuery] Guid tenantId, IWhatsAppFlowEngineService flowService) =>
        {
            var flows = await flowService.ListFlowsAsync(tenantId);
            return Results.Ok(flows);
        });

        app.MapPost("/api/channels/flows", async ([FromBody] FlowDefinition flow, IWhatsAppFlowEngineService flowService) =>
        {
            var created = await flowService.CreateFlowDefinitionAsync(flow.TenantId, flow);
            return Results.Ok(created);
        });

        app.MapPost("/api/channels/flows/execute-turn", async ([FromBody] ExecuteFlowTurnDto req, IWhatsAppFlowEngineService flowService) =>
        {
            var turn = await flowService.ExecuteFlowTurnAsync(req.TenantId, req.FlowId, req.CustomerPhone, req.Message);
            return Results.Ok(turn);
        });

        return app;
    }
}
