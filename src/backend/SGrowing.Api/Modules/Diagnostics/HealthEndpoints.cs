using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.Security;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Diagnostics;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", framework = ".NET 10", timestamp = DateTime.UtcNow }));
        app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));
        app.MapGet("/healthz/live", () => Results.Ok(new { status = "Healthy", timestampUtc = DateTime.UtcNow }));
        app.MapGet("/readyz", () => Results.Ok(new { status = "Ready", timestamp = DateTime.UtcNow }));
        app.MapGet("/healthz/ready", async (SGrowingDbContext db) =>
        {
            var canConnect = await db.Database.CanConnectAsync();
            return canConnect 
                ? Results.Ok(new { status = "Ready", database = "Connected", timestampUtc = DateTime.UtcNow }) 
                : Results.Problem("Database unavailable", statusCode: 503);
        });

        app.MapGet("/health/daemons", async (IDaemonHeartbeatService heartbeat) =>
        {
            var report = await heartbeat.GetHealthReportAsync();
            return Results.Ok(report);
        });

        app.MapGet("/api/v1/health/daemons", async (IDaemonHeartbeatService heartbeat) =>
        {
            var report = await heartbeat.GetHealthReportAsync();
            return Results.Ok(report);
        });

        // SLA Policies & Evaluation
        app.MapGet("/api/sla/policies", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
        {
            var policies = await db.SlaPolicies.Where(p => p.TenantId == tenantId).ToListAsync();
            return Results.Ok(policies);
        });

        app.MapPost("/api/sla/policies", async ([FromBody] SlaPolicy policy, SGrowingDbContext db) =>
        {
            db.SlaPolicies.Add(policy);
            await db.SaveChangesAsync();
            return Results.Ok(policy);
        });

        app.MapPost("/api/sla/evaluate", async ([FromQuery] Guid tenantId, SlaMonitoringService slaService) =>
        {
            var count = await slaService.EvaluateSlaBreachesAsync(tenantId);
            return Results.Ok(new { breachedCount = count });
        });

        // Security Audit Logs & Masking
        app.MapGet("/api/security/audit-logs", async ([FromQuery] Guid tenantId, [FromQuery] string? resource, ISecurityAuditAndRbacService secService) =>
        {
            var logs = await secService.ListAuditLogsAsync(tenantId, resource);
            return Results.Ok(logs);
        });

        app.MapGet("/api/security/orders/{orderId}/masked", async (Guid orderId, [FromQuery] Guid tenantId, [FromQuery] UserSystemRole userRole, ISecurityAuditAndRbacService secService) =>
        {
            var view = await secService.GetMaskedCustomerOrderAsync(tenantId, orderId, userRole);
            return Results.Ok(view);
        });

        // Dev Database Seeding
        app.MapPost("/api/dev/seed", async ([FromQuery] Guid? tenantId, DatabaseSeeder seeder) =>
        {
            var seededTenantId = await seeder.SeedEnterpriseDemoAsync(tenantId);
            return Results.Ok(new { status = "Success", tenantId = seededTenantId });
        });

        return app;
    }
}
