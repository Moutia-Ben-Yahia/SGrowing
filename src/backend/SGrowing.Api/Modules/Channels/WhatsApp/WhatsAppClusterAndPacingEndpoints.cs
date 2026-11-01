using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public record CreateClusterInstanceApiRequest(
    string InstanceName,
    string PhoneNumber,
    string Role = "General",
    string? AuthCredentialsJson = null);

public record CalculateDelayApiRequest(
    string PhoneNumber,
    string MessageText,
    int DayOfWarmup = 1,
    double Lambda = 3.2,
    double MinRange = 1.8,
    double MaxRange = 5.4);

public record RenderFlowApiRequest(
    string FlowType,
    string? RecipientPhone = null,
    string? Title = null,
    string? Body = null,
    string? ButtonText = null,
    string? Header = null,
    string? Footer = null,
    List<InteractiveListSection>? Sections = null,
    List<QuickReplyButton>? Buttons = null,
    List<CatalogSectionDto>? CatalogSections = null,
    string? CatalogId = null);

public record SendPriorityApiRequest(
    string RecipientPhone,
    string? RecipientEmail,
    string Subject,
    string Body,
    PriorityMessageType MessageType = PriorityMessageType.Otp,
    int TimeoutSeconds = 60,
    string? PreferredRole = null,
    int? PreferredSimSlot = null);

public static class WhatsAppClusterAndPacingEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapWhatsAppClusterAndPacingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/whatsapp/cluster")
            .WithTags("WhatsApp Baileys Cluster & Anti-Ban Pacing");

        // 1. POST /api/v1/whatsapp/cluster/instances
        group.MapPost("/instances", async (
            [FromBody] CreateClusterInstanceApiRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppSessionClusterService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var session = await service.RegisterInstanceAsync(
                effectiveTenantId,
                new RegisterClusterInstanceRequest(
                    req.InstanceName,
                    req.PhoneNumber,
                    req.Role,
                    req.AuthCredentialsJson),
                ct);

            return Results.Ok(session);
        })
        .WithName("RegisterWhatsAppClusterInstance")
        .WithSummary("Register a concurrent WhatsApp Baileys session instance in the multi-device cluster");

        // 2. GET /api/v1/whatsapp/cluster/instances
        group.MapGet("/instances", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWhatsAppSessionClusterService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var instances = await service.GetInstancesAsync(effectiveTenantId, ct);
            return Results.Ok(instances);
        })
        .WithName("GetWhatsAppClusterInstances")
        .WithSummary("List active and registered instances in the WhatsApp cluster for tenant");

        // 3. POST /api/v1/whatsapp/cluster/pacing/calculate-delay
        group.MapPost("/pacing/calculate-delay", (
            [FromBody] CalculateDelayApiRequest req,
            IAntiBanMessagePacingService pacingService) =>
        {
            var plan = pacingService.CalculatePacingPlan(
                req.PhoneNumber,
                req.MessageText,
                req.DayOfWarmup,
                req.Lambda,
                req.MinRange,
                req.MaxRange);

            return Results.Ok(plan);
        })
        .WithName("CalculateAntiBanPacingDelay")
        .WithSummary("Calculate Poisson-distributed pacing jitter, human typing duration, and warmup quotas");

        // 4. POST /api/v1/whatsapp/cluster/flows/render
        group.MapPost("/flows/render", (
            [FromBody] RenderFlowApiRequest req,
            IWhatsAppFlowsAndCatalogService flowsService) =>
        {
            string flowType = req.FlowType.Trim().ToLowerInvariant();

            switch (flowType)
            {
                case "address":
                case "address_collection":
                {
                    var schema = flowsService.GenerateAddressCollectionFlowSchema();
                    return Results.Content(schema, "application/json");
                }

                case "appointment":
                case "appointment_booking":
                {
                    var schema = flowsService.GenerateAppointmentBookingFlowSchema();
                    return Results.Content(schema, "application/json");
                }

                case "variant":
                case "product_variant":
                {
                    var schema = flowsService.GenerateProductVariantSelectionFlowSchema();
                    return Results.Content(schema, "application/json");
                }

                case "list":
                case "interactive_list":
                {
                    if (string.IsNullOrWhiteSpace(req.RecipientPhone) || string.IsNullOrWhiteSpace(req.Body))
                        return Results.BadRequest(new { error = "RecipientPhone and Body are required for interactive lists." });

                    var rendered = flowsService.RenderInteractiveListMenu(new InteractiveListMenuRequest(
                        req.RecipientPhone,
                        req.Title ?? "Select an Option",
                        req.Body,
                        req.ButtonText ?? "View Menu",
                        req.Sections ?? [],
                        req.Header,
                        req.Footer));

                    return Results.Content(rendered, "application/json");
                }

                case "buttons":
                case "quick_replies":
                {
                    if (string.IsNullOrWhiteSpace(req.RecipientPhone) || string.IsNullOrWhiteSpace(req.Body))
                        return Results.BadRequest(new { error = "RecipientPhone and Body are required for quick replies." });

                    var rendered = flowsService.RenderDynamicQuickReplies(new QuickReplyButtonsRequest(
                        req.RecipientPhone,
                        req.Body,
                        req.Buttons ?? [],
                        req.Header,
                        req.Footer));

                    return Results.Content(rendered, "application/json");
                }

                case "catalog":
                case "catalog_cart":
                {
                    if (string.IsNullOrWhiteSpace(req.RecipientPhone))
                        return Results.BadRequest(new { error = "RecipientPhone is required for catalog cart." });

                    var rendered = flowsService.RenderNativeCatalogCart(new CatalogCartRenderRequest(
                        req.RecipientPhone,
                        req.CatalogId ?? "default_catalog",
                        req.Header ?? "Featured Catalog",
                        req.Body ?? "Order directly from our catalog cart",
                        req.CatalogSections ?? [],
                        req.Footer));

                    return Results.Content(rendered, "application/json");
                }

                default:
                    return Results.BadRequest(new { error = $"Unsupported flow type '{req.FlowType}'. Supported types: 'address', 'appointment', 'variant', 'list', 'buttons', 'catalog'" });
            }
        })
        .WithName("RenderWhatsAppFlowOrInteractive")
        .WithSummary("Generate WhatsApp Flow JSON schemas or render interactive lists, quick replies, and catalog carts");

        // 5. POST /api/v1/whatsapp/cluster/failover/send-priority
        group.MapPost("/failover/send-priority", async (
            [FromBody] SendPriorityApiRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IOmnichannelFailoverService failoverService,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var priorityReq = new PriorityMessageRequest(
                effectiveTenantId,
                req.RecipientPhone,
                req.RecipientEmail,
                req.Subject,
                req.Body,
                req.MessageType,
                TimeSpan.FromSeconds(req.TimeoutSeconds),
                req.PreferredRole,
                req.PreferredSimSlot);

            var result = await failoverService.SendPriorityTransactionalMessageAsync(priorityReq, ct);
            return Results.Ok(result);
        })
        .WithName("SendOmnichannelPriorityTransactionalMessage")
        .WithSummary("Send priority transactional message via cascading failover ladder (WhatsApp -> SMS -> Email)");

        return app;
    }
}
