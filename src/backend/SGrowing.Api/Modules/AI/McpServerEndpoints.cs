using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.AI;

public static class McpServerEndpoints
{
    public static IEndpointRouteBuilder MapMcpServerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/mcp")
            .WithTags("Model Context Protocol (MCP) & AI Autonomous Agent Hub");

        group.MapGet("/tools", async (IMcpServerRegistryService service) =>
        {
            var result = await service.GetRegisteredMcpToolsAsync();
            return Results.Ok(result);
        });

        group.MapPost("/tools/{toolName}/invoke", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            string toolName,
            [FromBody] Dictionary<string, object> arguments,
            IMcpServerRegistryService service) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var result = await service.ExecuteMcpToolAsync(effectiveTenantId, toolName, arguments);
            return Results.Ok(result);
        });

        group.MapGet("/autonomy", (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMcpServerRegistryService service) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var mode = service.GetAutonomyMode(effectiveTenantId);
            return Results.Ok(new { autonomyMode = mode });
        });

        group.MapPost("/autonomy", (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            [FromBody] SetAutonomyRequest request,
            IMcpServerRegistryService service) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            service.SetAutonomyMode(effectiveTenantId, request.Mode);
            return Results.Ok(new { success = true, autonomyMode = request.Mode });
        });

        group.MapGet("/audit-logs", (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IMcpServerRegistryService service) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var logs = service.GetAuditLogs(effectiveTenantId);
            return Results.Ok(logs);
        });

        group.MapPost("/contacts/{id:guid}/extract-facts", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            [FromBody] ExtractFactsRequest request,
            IMcpServerRegistryService service) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var result = await service.ExtractContactFactsAsync(effectiveTenantId, id, request.Transcript);
            return Results.Ok(result);
        });

        group.MapGet("/contacts/{id:guid}/brief", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            Guid id,
            IMcpServerRegistryService service) =>
        {
            var effectiveTenantId = tenantId ?? headerTenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var result = await service.GetContactBriefAsync(effectiveTenantId, id);
            return Results.Ok(result);
        });

        return app;
    }
}

public record SetAutonomyRequest(string Mode);
public record ExtractFactsRequest(string Transcript);
