using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Modules.Conversations.Macros;
using SGrowing.Api.Modules.Conversations.Mime;
using SGrowing.Api.Modules.Conversations.Sla;

namespace SGrowing.Api.Modules.Conversations;

public record ParseMimeRequest(string RawMime);
public record ResolveMacroRequest(string TriggerKey, MacroSubstitutionContext Context);
public record TriggerCsatRequest(string Channel = "email", string BaseUrl = "https://feedback.sgrowing.io");

public static class OmnichannelEndpoints
{
    public static IEndpointRouteBuilder MapOmnichannelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Omnichannel Inbox & Support");

        group.MapPost("/mime/parse", (ParseMimeRequest req, IMimeEmailParserService parser) =>
        {
            var parsed = parser.ParseRawMime(req.RawMime);
            return Results.Ok(parsed);
        })
        .WithName("ParseRawMimeEmail")
        .WithSummary("Parse RFC 822 MIME multipart email, strip quoted history, and extract inline attachments");

        group.MapGet("/macros", async (Guid tenantId, string? category, ICannedMacroService macroService, CancellationToken ct) =>
        {
            var list = await macroService.GetMacrosAsync(tenantId, category, ct);
            return Results.Ok(list);
        })
        .WithName("GetCannedMacros")
        .WithSummary("List canned macro responses for tenant with optional category filter");

        group.MapPost("/macros", async (Guid tenantId, CreateCannedMacroRequest req, ICannedMacroService macroService, CancellationToken ct) =>
        {
            var created = await macroService.CreateMacroAsync(tenantId, req, ct);
            return Results.Ok(created);
        })
        .WithName("CreateCannedMacro")
        .WithSummary("Register a canned macro with hotkey trigger (e.g. /pricing, /refund)");

        group.MapPost("/macros/resolve", async (Guid tenantId, ResolveMacroRequest req, ICannedMacroService macroService, CancellationToken ct) =>
        {
            var res = await macroService.ResolveMacroAsync(tenantId, req.TriggerKey, req.Context, ct);
            return Results.Ok(res);
        })
        .WithName("ResolveCannedMacro")
        .WithSummary("Resolve macro trigger with dynamic contact/order variable interpolation");

        group.MapPost("/{id:guid}/csat/trigger", async (Guid tenantId, Guid id, TriggerCsatRequest req, ISlaEscalationEngineService slaService, CancellationToken ct) =>
        {
            var result = await slaService.TriggerCsatSurveyAsync(tenantId, id, req.Channel, req.BaseUrl, ct);
            return Results.Ok(result);
        })
        .WithName("TriggerConversationCsatSurvey")
        .WithSummary("Trigger customer CSAT survey link upon conversation resolution");

        group.MapPost("/sla/escalate", async (Guid tenantId, ISlaEscalationEngineService slaService, CancellationToken ct) =>
        {
            var result = await slaService.EvaluateEscalationPoliciesAsync(tenantId, ct);
            return Results.Ok(result);
        })
        .WithName("EvaluateSlaEscalations")
        .WithSummary("Run SLA escalation engine across active conversations");

        return app;
    }
}
