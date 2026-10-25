using System.Text.Json;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.AI;

public record AgentToolResult(bool Success, string OutputData, bool RequiresHumanEscalation = false);

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    Task<AgentToolResult> ExecuteAsync(Guid tenantId, Guid contactId, string argumentsJson, CancellationToken cancellationToken = default);
}

public class KnowledgeBaseTool : IAgentTool
{
    private readonly HelpCenterRagService _ragService;

    public KnowledgeBaseTool(HelpCenterRagService ragService)
    {
        _ragService = ragService;
    }

    public string Name => "KnowledgeBaseSearch";
    public string Description => "Searches the Help Center knowledge base for answers to technical and product questions.";

    public async Task<AgentToolResult> ExecuteAsync(Guid tenantId, Guid contactId, string argumentsJson, CancellationToken cancellationToken = default)
    {
        var query = argumentsJson;
        try
        {
            var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("query", out var qProp))
                query = qProp.GetString() ?? argumentsJson;
        }
        catch { }

        var result = await _ragService.GenerateAiDeflectionReplyAsync(tenantId, query, cancellationToken);
        if (result.Deflected)
        {
            return new AgentToolResult(true, result.Answer);
        }

        return new AgentToolResult(false, result.Answer, RequiresHumanEscalation: true);
    }
}

public class OrderLookupTool : IAgentTool
{
    private readonly SGrowingDbContext _db;

    public OrderLookupTool(SGrowingDbContext db)
    {
        _db = db;
    }

    public string Name => "OrderLookup";
    public string Description => "Looks up the contact's open deals, commercial orders, and account status.";

    public async Task<AgentToolResult> ExecuteAsync(Guid tenantId, Guid contactId, string argumentsJson, CancellationToken cancellationToken = default)
    {
        var deals = await _db.Deals
            .Where(d => d.TenantId == tenantId && d.ContactId == contactId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (deals.Count == 0)
        {
            return new AgentToolResult(true, "No recent orders or active deals were found for your account.");
        }

        var latest = deals.First();
        var summary = $"Found order/opportunity '{latest.Title}' with value {latest.Amount:C} in stage '{latest.Stage}'.";
        return new AgentToolResult(true, summary);
    }
}

public class EscalateToHumanTool : IAgentTool
{
    private readonly SGrowingDbContext _db;

    public EscalateToHumanTool(SGrowingDbContext db)
    {
        _db = db;
    }

    public string Name => "EscalateToHuman";
    public string Description => "Transfers the conversation to a human support representative and sets priority to High.";

    public async Task<AgentToolResult> ExecuteAsync(Guid tenantId, Guid contactId, string argumentsJson, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations
            .Where(c => c.TenantId == tenantId && c.ContactId == contactId && c.Status == ConversationStatus.Open)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (conv != null)
        {
            conv.AiMode = AiMode.Copilot;
            conv.Priority = ConversationPriority.High;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new AgentToolResult(true, "Conversation has been successfully escalated to the human agent queue.", RequiresHumanEscalation: true);
    }
}
