using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI;

public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string OutputJson { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class DynamicAgentToolRegistry
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<DynamicAgentToolRegistry>? _logger;

    public DynamicAgentToolRegistry(SGrowingDbContext db, ILogger<DynamicAgentToolRegistry>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DynamicAgentTool> RegisterToolAsync(
        Guid tenantId,
        string name,
        string description,
        string schemaJson,
        string targetUrl,
        string httpMethod = "POST",
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.DynamicAgentTools
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Name == name, cancellationToken);

        if (existing != null)
        {
            existing.Description = description;
            existing.ParametersSchemaJson = schemaJson;
            existing.TargetEndpointUrl = targetUrl;
            existing.HttpMethod = httpMethod;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var tool = DynamicAgentTool.Create(tenantId, name, description, schemaJson, targetUrl, httpMethod);
        await _db.DynamicAgentTools.AddAsync(tool, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Registered dynamic tool {Name} ({Url}) for tenant {TenantId}", name, targetUrl, tenantId);
        return tool;
    }

    public async Task<List<DynamicAgentTool>> GetActiveToolsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.DynamicAgentTools
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ToolExecutionResult> ExecuteToolAsync(
        Guid tenantId,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        var tool = await _db.DynamicAgentTools
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Name == toolName && t.IsActive, cancellationToken);

        if (tool == null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                ToolName = toolName,
                Error = $"Tool '{toolName}' is not registered or active for tenant."
            };
        }

        // Mock/simulated HTTP execution of dynamic endpoint
        _logger?.LogInformation("Executing dynamic tool {ToolName} at {Url} with args: {Args}",
            toolName, tool.TargetEndpointUrl, argumentsJson);

        var simulatedResponse = $"{{\"status\":\"success\",\"tool\":\"{toolName}\",\"received\":{argumentsJson}}}";

        return new ToolExecutionResult
        {
            Success = true,
            ToolName = toolName,
            OutputJson = simulatedResponse
        };
    }
}
