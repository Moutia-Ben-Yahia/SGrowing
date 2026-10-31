using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Outbound;

public static class OutboundSdrEndpoints
{
    public static IEndpointRouteBuilder MapOutboundSdrEndpoints(this IEndpointRouteBuilder app)
    {
        // Mailboxes
        app.MapGet("/api/outbound/mailboxes", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var mailboxes = await db.SenderAccounts.Where(s => s.TenantId == tenantId).ToListAsync();
            return Results.Ok(mailboxes);
        });

        app.MapPost("/api/outbound/mailboxes", async ([FromBody] SenderAccount sender, SGrowingDbContext db) =>
        {
            db.SenderAccounts.Add(sender);
            await db.SaveChangesAsync();
            return Results.Ok(sender);
        });

        // Campaigns & Cadences
        app.MapGet("/api/outbound/campaigns", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var campaigns = await db.OutboundCampaigns.Where(c => c.TenantId == tenantId).ToListAsync();
            return Results.Ok(campaigns);
        });

        app.MapPost("/api/outbound/campaigns", async ([FromBody] CreateCampaignRequest req, CreateCampaignHandler handler) =>
        {
            var res = await handler.HandleAsync(req);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapPost("/api/outbound/campaigns/{id:guid}/enroll", async (Guid id, [FromBody] List<Guid> contactIds, [FromQuery] Guid tenantId, EnrollProspectsHandler handler) =>
        {
            var res = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, id, contactIds));
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapPost("/api/outbound/dispatch", async ([FromQuery] Guid tenantId, CadenceDispatcherService dispatcher) =>
        {
            var count = await dispatcher.ProcessDueCadencesAsync(tenantId);
            return Results.Ok(new { processed = count });
        });

        // Multi-Touch AI SDR Outreach
        app.MapPost("/api/outbound/multitouch/dispatch", async ([FromQuery] Guid tenantId, MultiTouchCadenceOrchestrator orchestrator) =>
        {
            var count = await orchestrator.ProcessDueMultiTouchStepsAsync(tenantId);
            return Results.Ok(new { processed = count });
        });

        // Sales Cadences & Stop-on-Reply
        app.MapPost("/api/cadences", async ([FromBody] CreateCadenceDto req, ISalesCadenceEngine cadenceEngine) =>
        {
            var cadence = await cadenceEngine.CreateCadenceAsync(req.TenantId, req.Name, req.Description, req.TargetAudience);
            return Results.Ok(cadence);
        });

        app.MapPost("/api/cadences/{id:guid}/steps", async (Guid id, [FromBody] AddCadenceStepDto req, ISalesCadenceEngine cadenceEngine) =>
        {
            var step = await cadenceEngine.AddCadenceStepAsync(id, req.StepNumber, req.DayOffset, req.Channel, req.SubjectTemplate, req.BodyTemplate);
            return Results.Ok(step);
        });

        app.MapPost("/api/cadences/enroll", async ([FromBody] EnrollCadenceLeadDto req, ISalesCadenceEngine cadenceEngine) =>
        {
            var enrollment = await cadenceEngine.EnrollLeadAsync(req.TenantId, req.CadenceId, req.LeadName, req.Email, req.PhoneNumber, req.Company);
            return Results.Ok(enrollment);
        });

        app.MapPost("/api/cadences/process-due", async ([FromBody] ProcessDueStepsDto req, ISalesCadenceEngine cadenceEngine) =>
        {
            var count = await cadenceEngine.ProcessDueCadenceStepsAsync(req.TenantId);
            return Results.Ok(new { ProcessedCount = count });
        });

        app.MapPost("/api/cadences/reply-stop", async ([FromBody] CadenceReplyStopDto req, ISalesCadenceEngine cadenceEngine) =>
        {
            var stopped = await cadenceEngine.HandleProspectReplyAsync(req.TenantId, req.EmailOrPhone, req.Channel, req.ReplyText);
            return Results.Ok(new { Stopped = stopped });
        });

        app.MapGet("/api/cadences/sdr-tasks", async ([FromQuery] Guid tenantId, ISalesCadenceEngine cadenceEngine) =>
        {
            var tasks = await cadenceEngine.GetPendingSdrTasksAsync(tenantId);
            return Results.Ok(tasks);
        });

        app.MapPost("/api/cadences/sdr-tasks/{id:guid}/complete", async (Guid id, [FromBody] CompleteTaskDto req, ISalesCadenceEngine cadenceEngine) =>
        {
            var task = await cadenceEngine.CompleteSdrTaskAsync(req.TenantId, id, req.Outcome);
            return Results.Ok(task);
        });

        return app;
    }
}
