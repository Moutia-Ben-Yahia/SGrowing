using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SGrowing.Api.Data;
using SGrowing.Core.Tenancy;
using SGrowing.McpServer.Services;

var builder = WebApplication.CreateBuilder(args);
bool isStdio = args.Contains("--stdio");

if (isStdio)
{
    // Ensure 100% clean stdout for MCP JSON-RPC protocol compliance with Claude Desktop & Cursor
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(c => c.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.PostConfigure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.Listen(System.Net.IPAddress.Loopback, 0); // Dynamic local port prevents port collision with running backend APIs
    });
}

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
.WithSummary("Enumerate all 17 enterprise tools exposed by the MCP server");

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
await app.StartAsync();

if (isStdio)
{
    using var reader = new StreamReader(Console.OpenStandardInput());
    using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
    var toolService = app.Services.GetRequiredService<McpToolService>();

    while (await reader.ReadLineAsync() is { } line)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        try
        {
            var responseJson = await toolService.HandleJsonRpcAsync(line);
            if (!string.IsNullOrEmpty(responseJson))
            {
                await writer.WriteLineAsync(responseJson);
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[MCP STDIO ERROR] {ex.Message}");
        }
    }

    await app.StopAsync();
}
else
{
    await app.WaitForShutdownAsync();
}

public record McpToolCallRequest(string Name, JsonObject Arguments);