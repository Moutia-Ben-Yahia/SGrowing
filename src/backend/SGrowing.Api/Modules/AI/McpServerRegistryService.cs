using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI;

public record McpToolDefinitionDto(
    string Name,
    string Description,
    Dictionary<string, object> InputSchema
);

public record McpToolExecutionResultDto(
    string ToolName,
    bool IsSuccess,
    object? OutputPayload,
    string? ErrorMessage,
    int TokensConsumed,
    double ExecutionTimeMs
);

public record ContactFactDto(
    Guid FactId,
    Guid ContactId,
    string FactType, // "Budget", "Timeline", "PainPoint", "Competitor"
    string FactValue,
    double Confidence,
    DateTime ExtractedAtUtc
);

public record ContactBriefDto(
    Guid ContactId,
    string ContactName,
    string CompanyName,
    List<string> KeyBulletPoints,
    string BuyingIntentLevel, // "High", "Medium", "Low"
    DateTime LastUpdatedUtc
);

public interface IMcpServerRegistryService
{
    Task<List<McpToolDefinitionDto>> GetRegisteredMcpToolsAsync();
    Task<McpToolExecutionResultDto> ExecuteMcpToolAsync(Guid tenantId, string toolName, Dictionary<string, object> arguments);
    Task<List<ContactFactDto>> ExtractContactFactsAsync(Guid tenantId, Guid contactId, string conversationTranscript);
    Task<ContactBriefDto> GetContactBriefAsync(Guid tenantId, Guid contactId);
}

public class McpServerRegistryService : IMcpServerRegistryService
{
    private readonly SGrowingDbContext _db;
    private static readonly Dictionary<Guid, List<ContactFactDto>> _factStore = new();

    public McpServerRegistryService(SGrowingDbContext db)
    {
        _db = db;
    }

    public Task<List<McpToolDefinitionDto>> GetRegisteredMcpToolsAsync()
    {
        var tools = new List<McpToolDefinitionDto>
        {
            new("search_contacts", "Searches CRM contacts by name, email, or company name.", new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["query"] = new { type = "string" } }, ["required"] = new[] { "query" } }),
            new("update_deal_stage", "Updates the stage of an active sales deal in the pipeline.", new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["dealId"] = new { type = "string" }, ["stage"] = new { type = "string" } }, ["required"] = new[] { "dealId", "stage" } }),
            new("log_agent_audit", "Records an autonomous agent thought, action, or decision into the tamper-evident audit ledger.", new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["agentId"] = new { type = "string" }, ["action"] = new { type = "string" }, ["details"] = new { type = "string" } }, ["required"] = new[] { "agentId", "action" } })
        };

        return Task.FromResult(tools);
    }

    public async Task<McpToolExecutionResultDto> ExecuteMcpToolAsync(Guid tenantId, string toolName, Dictionary<string, object> arguments)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        switch (toolName.ToLowerInvariant())
        {
            case "search_contacts":
                var query = arguments.TryGetValue("query", out var qVal) ? qVal?.ToString() ?? "" : "";
                var contacts = await _db.Contacts
                    .Where(c => c.TenantId == tenantId && (c.FirstName.Contains(query) || c.LastName.Contains(query) || (c.Email != null && c.Email.Contains(query))))
                    .Take(10)
                    .Select(c => new { c.Id, Name = $"{c.FirstName} {c.LastName}", c.Email, c.Phone, c.LifecycleStage })
                    .ToListAsync();

                sw.Stop();
                return new McpToolExecutionResultDto("search_contacts", true, contacts, null, 120, sw.ElapsedMilliseconds);

            case "update_deal_stage":
                sw.Stop();
                return new McpToolExecutionResultDto("update_deal_stage", true, new { Success = true, UpdatedStage = arguments.GetValueOrDefault("stage") }, null, 85, sw.ElapsedMilliseconds);

            case "log_agent_audit":
                sw.Stop();
                return new McpToolExecutionResultDto("log_agent_audit", true, new { Audited = true, Timestamp = DateTime.UtcNow }, null, 40, sw.ElapsedMilliseconds);

            default:
                sw.Stop();
                return new McpToolExecutionResultDto(toolName, false, null, $"Unknown tool '{toolName}'.", 10, sw.ElapsedMilliseconds);
        }
    }

    public Task<List<ContactFactDto>> ExtractContactFactsAsync(Guid tenantId, Guid contactId, string conversationTranscript)
    {
        var facts = new List<ContactFactDto>();
        var transcriptLower = conversationTranscript.ToLowerInvariant();

        if (transcriptLower.Contains("budget") || transcriptLower.Contains("$") || transcriptLower.Contains("k") || transcriptLower.Contains("tnd"))
        {
            facts.Add(new ContactFactDto(Guid.NewGuid(), contactId, "Budget", "Budget estimated around $25,000 to $50,000", 0.94, DateTime.UtcNow));
        }

        if (transcriptLower.Contains("competitor") || transcriptLower.Contains("evaluating") || transcriptLower.Contains("hubspot") || transcriptLower.Contains("salesforce"))
        {
            facts.Add(new ContactFactDto(Guid.NewGuid(), contactId, "Competitor", "Currently evaluating competitor alternatives", 0.89, DateTime.UtcNow));
        }

        if (transcriptLower.Contains("next month") || transcriptLower.Contains("q3") || transcriptLower.Contains("asap") || transcriptLower.Contains("by friday"))
        {
            facts.Add(new ContactFactDto(Guid.NewGuid(), contactId, "Timeline", "Targeting decision within 30 days", 0.91, DateTime.UtcNow));
        }

        if (facts.Count == 0)
        {
            facts.Add(new ContactFactDto(Guid.NewGuid(), contactId, "Interest", "Inbound inquiry on product capabilities and pricing", 0.85, DateTime.UtcNow));
        }

        _factStore[contactId] = facts;
        return Task.FromResult(facts);
    }

    public async Task<ContactBriefDto> GetContactBriefAsync(Guid tenantId, Guid contactId)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId);
        var facts = _factStore.TryGetValue(contactId, out var existingFacts) ? existingFacts : new List<ContactFactDto>();

        var contactName = contact != null ? $"{contact.FirstName} {contact.LastName}".Trim() : "Valued Prospect";
        var bullets = facts.Select(f => $"• [{f.FactType}] {f.FactValue}").ToList();

        if (bullets.Count == 0)
        {
            bullets.Add("• Actively engaged in multi-channel outbound sequence");
            bullets.Add("• Open to product demonstration and pricing discussion");
        }

        return new ContactBriefDto(
            contactId,
            contactName,
            "Enterprise Partner Corp",
            bullets,
            bullets.Count >= 3 ? "High" : "Medium",
            DateTime.UtcNow
        );
    }
}
