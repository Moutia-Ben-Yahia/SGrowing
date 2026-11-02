namespace SGrowing.Api.Modules.Channels.WhatsAppBroadcast;

public static class WhatsAppBroadcastEndpoints
{
    public static IServiceCollection AddWhatsAppBroadcastServices(this IServiceCollection services)
    {
        services.AddSingleton<IWhatsAppBroadcastEngine, WhatsAppBroadcastEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWhatsAppBroadcastEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/channels/whatsapp/broadcast")
            .WithTags("WhatsApp Cloud Template Broadcast Mesh");

        group.MapPost("/campaigns", (IWhatsAppBroadcastEngine engine, CreateCampaignRequest request) =>
        {
            var campaign = engine.CreateCampaign(request);
            return Results.Created($"/api/v1/channels/whatsapp/broadcast/campaigns/{campaign.CampaignId}", campaign);
        })
        .WithName("CreateWhatsAppCampaign")
        .WithSummary("Create a new high-volume WhatsApp template broadcast campaign");

        group.MapGet("/campaigns/{campaignId}", (IWhatsAppBroadcastEngine engine, string campaignId) =>
        {
            var campaign = engine.GetCampaign(campaignId);
            return campaign is not null ? Results.Ok(campaign) : Results.NotFound(new { error = $"Campaign '{campaignId}' not found" });
        })
        .WithName("GetWhatsAppCampaign")
        .WithSummary("Retrieve live delivery, open, and read metrics for a broadcast campaign");

        group.MapPost("/campaigns/{campaignId}/trigger", (IWhatsAppBroadcastEngine engine, string campaignId) =>
        {
            try
            {
                var completed = engine.TriggerBroadcast(campaignId);
                return Results.Ok(completed);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("TriggerWhatsAppBroadcast")
        .WithSummary("Dispatch queued broadcast campaign across Meta Cloud API cluster with rate throttling");

        group.MapGet("/campaigns", (IWhatsAppBroadcastEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var list = engine.ListCampaigns(targetTenant);
            return Results.Ok(list);
        })
        .WithName("ListWhatsAppCampaigns")
        .WithSummary("List all historical and active broadcast campaigns for a tenant");

        group.MapGet("/templates", (IWhatsAppBroadcastEngine engine, string? tenantId) =>
        {
            var targetTenant = tenantId ?? "tenant_default";
            var templates = engine.ListTemplates(targetTenant);
            return Results.Ok(templates);
        })
        .WithName("ListWhatsAppTemplates")
        .WithSummary("List all synchronized and approved Meta WhatsApp message templates");

        group.MapPost("/templates", (IWhatsAppBroadcastEngine engine, WhatsAppTemplate template) =>
        {
            var registered = engine.RegisterOrUpdateTemplate(template);
            return Results.Created($"/api/v1/channels/whatsapp/broadcast/templates/{registered.TemplateId}", registered);
        })
        .WithName("RegisterWhatsAppTemplate")
        .WithSummary("Register or sync a message template with Meta Cloud API approval status");

        return app;
    }
}
