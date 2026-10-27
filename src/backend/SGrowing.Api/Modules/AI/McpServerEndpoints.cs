using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.AI;

public static class McpServerEndpoints
{
    public static IEndpointRouteBuilder MapMcpServerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/mcp")
            .WithTags("Model Context Protocol (MCP) & AI Facts");

        group.MapGet("/tools", async (IMcpServerRegistryService service) =>
        {
            var result = await service.GetRegisteredMcpToolsAsync();
            return Results.Ok(result);
        });

        group.MapPost("/tools/{toolName}/invoke", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            string toolName,
            [FromBody] Dictionary<string, object> arguments,
            IMcpServerRegistryService service) =>
        {
            var result = await service.ExecuteMcpToolAsync(tenantId, toolName, arguments);
            return Results.Ok(result);
        });

        group.MapPost("/contacts/{id:guid}/extract-facts", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            [FromBody] ExtractFactsRequest request,
            IMcpServerRegistryService service) =>
        {
            var result = await service.ExtractContactFactsAsync(tenantId, id, request.Transcript);
            return Results.Ok(result);
        });

        group.MapGet("/contacts/{id:guid}/brief", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
            Guid id,
            IMcpServerRegistryService service) =>
        {
            var result = await service.GetContactBriefAsync(tenantId, id);
            return Results.Ok(result);
        });

        return app;
    }
}

public record ExtractFactsRequest(
    string Transcript
);
