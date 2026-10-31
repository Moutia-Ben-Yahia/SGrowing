using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Journeys;

public static class JourneysFeedbackEndpoints
{
    public static IEndpointRouteBuilder MapJourneysFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        // Customer Journeys & Events
        app.MapPost("/api/events", async ([FromQuery] Guid tenantId, [FromBody] IngestEventDto req, JourneyExecutionService service) =>
        {
            var res = await service.IngestEventAsync(tenantId, req.ContactId, req.EventName, req.PayloadJson ?? "{}");
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        // Feedback & CSAT
        app.MapGet("/api/feedback/surveys", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var forms = await db.SurveyForms.Where(f => f.TenantId == tenantId).ToListAsync();
            return Results.Ok(forms);
        });

        app.MapPost("/api/feedback/submit", async ([FromQuery] Guid tenantId, [FromBody] SubmitFeedbackDto req, FeedbackService service) =>
        {
            var res = await service.SubmitFeedbackAsync(tenantId, req.SurveyFormId, req.ContactId, req.Score, req.FeedbackText);
            return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
        });

        app.MapGet("/api/feedback/responses", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var responses = await db.SurveyResponses.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
            return Results.Ok(responses);
        });

        // Knowledge Base & RAG
        app.MapGet("/api/kb/articles", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var articles = await db.HelpArticles.Where(a => a.TenantId == tenantId && a.IsPublished).ToListAsync();
            return Results.Ok(articles);
        });

        app.MapPost("/api/kb/query", async ([FromQuery] Guid tenantId, [FromBody] KbQueryDto req, HelpCenterRagService ragService) =>
        {
            var res = await ragService.GenerateAiDeflectionReplyAsync(tenantId, req.Question);
            return Results.Ok(res);
        });

        // Visual Workflows & DLQ
        app.MapGet("/api/workflows", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var workflows = await db.WorkflowGraphs.Where(w => w.TenantId == tenantId).ToListAsync();
            return Results.Ok(workflows);
        });

        app.MapPost("/api/workflows", async ([FromBody] WorkflowGraph graph, SGrowingDbContext db) =>
        {
            db.WorkflowGraphs.Add(graph);
            await db.SaveChangesAsync();
            return Results.Ok(graph);
        });

        app.MapPost("/api/workflows/{id:guid}/trigger", async (Guid id, [FromQuery] Guid tenantId, [FromBody] TriggerWorkflowDto req, WorkflowEngineService engine) =>
        {
            var execution = await engine.ExecuteWorkflowAsync(tenantId, id, req.InputPayloadJson);
            return Results.Ok(execution);
        });

        app.MapGet("/api/workflows/dlq", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var dlq = await db.WorkflowDeadLetterLogs.Where(d => d.TenantId == tenantId).ToListAsync();
            return Results.Ok(dlq);
        });

        app.MapPost("/api/workflows/quiet-hours/evaluate", async ([FromBody] EvaluateQuietHoursDto req, QuietHoursGuardService guard) =>
        {
            var result = await guard.EvaluateDispatchTimeAsync(req.TenantId, req.RecipientLocalTime, req.IsUrgent);
            return Results.Ok(result);
        });

        return app;
    }
}
