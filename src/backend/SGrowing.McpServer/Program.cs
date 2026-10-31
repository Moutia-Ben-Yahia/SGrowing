using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SGrowing.Api.Data;
using SGrowing.Core.Tenancy;
using SGrowing.McpServer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddOpenApi();

// Database context for live MCP operations
builder.Services.AddDbContext<SGrowingDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseInMemoryDatabase("SGrowingMcpDb");
    }
});

builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<McpToolService>();

// Support CORS for Web dashboards
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.MapDefaultEndpoints();

// ----------------- OpenAPI & Scalar Documentation -----------------
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("SGrowing Enterprise Model Context Protocol (MCP) Server")
           .WithTheme(ScalarTheme.Moon);
});

// ----------------- Standard JSON-RPC 2.0 MCP Endpoint -----------------
app.MapPost("/mcp", async (HttpContext context, McpToolService toolService) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var requestJson = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(requestJson))
    {
        return Results.BadRequest(new { jsonrpc = "2.0", error = new { code = -32700, message = "Parse error: empty request" } });
    }

    var responseJson = await toolService.HandleJsonRpcAsync(requestJson);
    return Results.Content(responseJson, "application/json");
})
.WithName("HandleMcpJsonRpc")
.WithSummary("Process MCP Model Context Protocol requests over HTTP via standard JSON-RPC 2.0");

// ----------------- High-Level REST Proxies for Tools -----------------
var toolsGroup = app.MapGroup("/api/v1/mcp/tools").WithTags("Enterprise MCP Tools");

toolsGroup.MapGet("/", (McpToolService toolService) =>
{
    return Results.Ok(toolService.GetTools());
})
.WithName("ListMcpTools")
.WithSummary("Enumerate all 11 enterprise tools exposed by the MCP server");

toolsGroup.MapPost("/call", async (McpToolCallRequest request, McpToolService toolService) =>
{
    var (result, isError) = await toolService.ExecuteToolAsync(request.Name, request.Arguments);
    if (isError)
    {
        return Results.BadRequest(new { success = false, error = result });
    }
    return Results.Ok(result);
})
.WithName("ExecuteMcpToolDirect")
.WithSummary("Direct HTTP invocation proxy for an MCP tool");

// ----------------- STDIO Transport loop for local desktop AI (Claude Desktop/Cursor) -----------------
if (args.Contains("--stdio"))
{
    _ = Task.Run(async () =>
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var toolService = app.Services.GetRequiredService<McpToolService>();

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var responseJson = await toolService.HandleJsonRpcAsync(line);
            await writer.WriteLineAsync(responseJson);
        }
    });
}

app.Run();

public record McpToolCallRequest(string Name, JsonObject Arguments);
