using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Deliverability;

public record CheckDnsDto(Guid TenantId, string DomainName);
public record AnalyzeSpamDto(Guid TenantId, string Subject, string BodyHtml);
public record StartWarmupDto(Guid TenantId, string SenderEmail, int DailyTarget = 40, string RampCurve = "Linear");

public static class DeliverabilityEndpoints
{
    public static IEndpointRouteBuilder MapDeliverabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/deliverability").WithTags("Email Deliverability Guardian & Mailbox Warmup");

        group.MapPost("/dns-check", async ([FromBody] CheckDnsDto req, IEmailDeliverabilityService service) =>
        {
            var res = await service.CheckDomainDnsHealthAsync(req.TenantId, req.DomainName);
            return Results.Ok(res);
        });

        group.MapGet("/dns", async ([FromQuery] Guid tenantId, [FromQuery] string domain, IEmailDeliverabilityService service) =>
        {
            var res = await service.GetLatestDomainDnsAsync(tenantId, domain);
            return res != null ? Results.Ok(res) : Results.NotFound(new { error = "Domain record not found" });
        });

        group.MapPost("/spam-analyze", async ([FromBody] AnalyzeSpamDto req, IEmailDeliverabilityService service) =>
        {
            var report = await service.AnalyzeEmailSpamRiskAsync(req.TenantId, req.Subject, req.BodyHtml);
            return Results.Ok(report);
        });

        group.MapPost("/warmup/start", async ([FromBody] StartWarmupDto req, IEmailDeliverabilityService service) =>
        {
            var cadence = await service.StartWarmupCadenceAsync(req.TenantId, req.SenderEmail, req.DailyTarget, req.RampCurve);
            return Results.Ok(cadence);
        });

        group.MapGet("/warmup", async ([FromQuery] Guid tenantId, [FromQuery] string senderEmail, IEmailDeliverabilityService service) =>
        {
            var cadence = await service.GetWarmupCadenceAsync(tenantId, senderEmail);
            return Results.Ok(cadence);
        });

        group.MapPost("/warmup/{id:guid}/simulate-step", async (Guid id, [FromQuery] Guid tenantId, IEmailDeliverabilityService service) =>
        {
            var updated = await service.SimulateWarmupStepAsync(tenantId, id);
            return Results.Ok(updated);
        });

        group.MapGet("/warmup/{id:guid}/logs", async (Guid id, [FromQuery] Guid tenantId, IEmailDeliverabilityService service) =>
        {
            var logs = await service.GetWarmupLogsAsync(tenantId, id);
            return Results.Ok(logs);
        });

        return endpoints;
    }
}
