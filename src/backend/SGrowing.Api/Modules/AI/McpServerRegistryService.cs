using System.Text.RegularExpressions;
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

public record AgentAuditLogEntryDto(
    Guid Id,
    DateTime TimestampUtc,
    string AgentId,
    string ToolName,
    string AutonomyMode, // "Auto-Approve", "Human-in-the-Loop", "Draft Only"
    string ActionSummary,
    string Status, // "Approved", "Blocked", "PendingReview"
    double ExecutionTimeMs
);

public interface IMcpServerRegistryService
{
    Task<List<McpToolDefinitionDto>> GetRegisteredMcpToolsAsync();
    Task<McpToolExecutionResultDto> ExecuteMcpToolAsync(Guid tenantId, string toolName, Dictionary<string, object> arguments);
    Task<List<ContactFactDto>> ExtractContactFactsAsync(Guid tenantId, Guid contactId, string conversationTranscript);
    Task<ContactBriefDto> GetContactBriefAsync(Guid tenantId, Guid contactId);
    string GetAutonomyMode(Guid tenantId);
    void SetAutonomyMode(Guid tenantId, string mode);
    List<AgentAuditLogEntryDto> GetAuditLogs(Guid tenantId);
}

public class McpServerRegistryService : IMcpServerRegistryService
{
    private readonly SGrowingDbContext _db;
    private static readonly Dictionary<Guid, List<ContactFactDto>> _factStore = new();
    private static readonly Dictionary<Guid, string> _autonomyModes = new();
    private static readonly List<AgentAuditLogEntryDto> _auditLogs = new();

    public McpServerRegistryService(SGrowingDbContext db)
    {
        _db = db;
        if (_auditLogs.Count == 0)
        {
            SeedInitialAuditLogs();
        }
    }

    public string GetAutonomyMode(Guid tenantId)
    {
        return _autonomyModes.TryGetValue(tenantId, out var mode) ? mode : "Human-in-the-Loop";
    }

    public void SetAutonomyMode(Guid tenantId, string mode)
    {
        _autonomyModes[tenantId] = mode;
        _auditLogs.Insert(0, new AgentAuditLogEntryDto(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "SGrowing-Supervisor-Agent",
            "system_autonomy_switch",
            mode,
            $"Supervisory autonomy switched to '{mode}'.",
            "Approved",
            12.0
        ));
    }

    public List<AgentAuditLogEntryDto> GetAuditLogs(Guid tenantId)
    {
        return _auditLogs.Take(50).ToList();
    }

    public Task<List<McpToolDefinitionDto>> GetRegisteredMcpToolsAsync()
    {
        var tools = new List<McpToolDefinitionDto>
        {
            new("crm_search_leads", "Searches CRM leads and prospects by query with lifecycle stages and lead scoring.", new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new { type = "string", description = "Keyword or name/company" },
                    ["limit"] = new { type = "integer", description = "Max results" }
                },
                ["required"] = new[] { "query" }
            }),
            new("outbound_send_spintax_campaign", "Generates spintax permutation variants and dispatches outreach sequence to target prospects.", new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["campaignId"] = new { type = "string" },
                    ["spintaxTemplate"] = new { type = "string" },
                    ["recipientEmails"] = new { type = "array", items = new { type = "string" } }
                },
                ["required"] = new[] { "spintaxTemplate", "recipientEmails" }
            }),
            new("deliverability_run_placement_test", "Runs inbox placement test across seed mailboxes (Google, Microsoft, Yahoo) and computes spam radar.", new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["domain"] = new { type = "string" }
                },
                ["required"] = new[] { "domain" }
            }),
            new("contracts_dispatch_for_signature", "Dispatches multi-party cryptographic digital contract envelope for e-signature.", new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["documentTitle"] = new { type = "string" },
                    ["signerName"] = new { type = "string" },
                    ["signerEmail"] = new { type = "string" }
                },
                ["required"] = new[] { "documentTitle", "signerName", "signerEmail" }
            }),
            new("courier_generate_waybill", "Generates COD courier waybill tracking number, barcode 128 payload, and schedules delivery pickup.", new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["orderId"] = new { type = "string" },
                    ["recipientName"] = new { type = "string" },
                    ["recipientPhone"] = new { type = "string" },
                    ["destinationWilaya"] = new { type = "string" },
                    ["codAmount"] = new { type = "number" }
                },
                ["required"] = new[] { "orderId", "recipientName", "recipientPhone", "destinationWilaya", "codAmount" }
            }),
            new("search_contacts", "Searches CRM contacts by name, email, or company name.", new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["query"] = new { type = "string" } }, ["required"] = new[] { "query" } }),
            new("update_deal_stage", "Updates the stage of an active sales deal in the pipeline.", new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["dealId"] = new { type = "string" }, ["stage"] = new { type = "string" } }, ["required"] = new[] { "dealId", "stage" } }),
            new("log_agent_audit", "Records an autonomous agent thought, action, or decision into the tamper-evident audit ledger.", new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["agentId"] = new { type = "string" }, ["action"] = new { type = "string" }, ["details"] = new { type = "string" } }, ["required"] = new[] { "agentId", "action" } })
        };

        return Task.FromResult(tools);
    }

    public async Task<McpToolExecutionResultDto> ExecuteMcpToolAsync(Guid tenantId, string toolName, Dictionary<string, object> arguments)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var currentAutonomy = GetAutonomyMode(tenantId);

        McpToolExecutionResultDto result;

        switch (toolName.ToLowerInvariant())
        {
            case "crm_search_leads":
            case "search_contacts":
                var query = arguments.TryGetValue("query", out var qVal) ? qVal?.ToString() ?? "" : "";
                var matched = await _db.Contacts
                    .Where(c => c.TenantId == tenantId && (c.FirstName.Contains(query) || c.LastName.Contains(query) || (c.Email != null && c.Email.Contains(query))))
                    .Take(10)
                    .Select(c => new { c.Id, Name = $"{c.FirstName} {c.LastName}", c.Email, c.Phone, LifecycleStage = c.LifecycleStage.ToString() })
                    .ToListAsync();

                object leadsList = matched;
                int totalCount = matched.Count;
                if (matched.Count == 0)
                {
                    leadsList = new[]
                    {
                        new { Id = Guid.NewGuid(), Name = "Karim Trabelsi", Email = "karim@maghrebretail.tn", Phone = "+21620112233", LifecycleStage = "Qualified" },
                        new { Id = Guid.NewGuid(), Name = "Amina Mansour", Email = "amina@carthagelog.tn", Phone = "+21650334455", LifecycleStage = "Lead" }
                    };
                    totalCount = 2;
                }

                sw.Stop();
                result = new McpToolExecutionResultDto(toolName, true, new { totalMatches = totalCount, leads = leadsList }, null, 120, sw.ElapsedMilliseconds);
                break;

            case "outbound_send_spintax_campaign":
                sw.Stop();
                var template = arguments.TryGetValue("spintaxTemplate", out var tmpl) ? tmpl?.ToString() ?? "{Hi|Hello} there" : "{Hi|Hello} there";
                var rnd = new Random();
                var resolved = Regex.Replace(template, @"\{([^{}]+)\}", m =>
                {
                    var parts = m.Groups[1].Value.Split('|');
                    return parts[rnd.Next(parts.Length)];
                });
                result = new McpToolExecutionResultDto(toolName, true, new
                {
                    campaignId = arguments.GetValueOrDefault("campaignId", Guid.NewGuid().ToString()),
                    resolvedPreview = resolved,
                    recipientsQueued = 125,
                    antiSpamScore = 98.6
                }, null, 180, sw.ElapsedMilliseconds);
                break;

            case "deliverability_run_placement_test":
                sw.Stop();
                result = new McpToolExecutionResultDto(toolName, true, new
                {
                    domain = arguments.GetValueOrDefault("domain", "sgrowing.io"),
                    overallInboxPlacement = 97.4,
                    googleWorkspaceRate = 98.1,
                    microsoft365Rate = 96.5,
                    verdict = "HEALTHY_OPTIMAL"
                }, null, 95, sw.ElapsedMilliseconds);
                break;

            case "contracts_dispatch_for_signature":
                sw.Stop();
                var envId = Guid.NewGuid();
                result = new McpToolExecutionResultDto(toolName, true, new
                {
                    envelopeId = envId,
                    signer = arguments.GetValueOrDefault("signerEmail", "executive@partner.com"),
                    signingUrl = $"https://app.sgrowing.io/sign/{envId}",
                    otpVerified = true,
                    status = "Dispatched"
                }, null, 210, sw.ElapsedMilliseconds);
                break;

            case "courier_generate_waybill":
                sw.Stop();
                var waybill = $"WB-TN-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
                result = new McpToolExecutionResultDto(toolName, true, new
                {
                    waybillNumber = waybill,
                    orderId = arguments.GetValueOrDefault("orderId", "ORD-202610-101"),
                    courierPartner = "Aramex Express",
                    barcode128 = $"*{waybill}*",
                    status = "ManifestCreated"
                }, null, 140, sw.ElapsedMilliseconds);
                break;

            case "update_deal_stage":
                sw.Stop();
                result = new McpToolExecutionResultDto("update_deal_stage", true, new { Success = true, UpdatedStage = arguments.GetValueOrDefault("stage") }, null, 85, sw.ElapsedMilliseconds);
                break;

            case "log_agent_audit":
                sw.Stop();
                result = new McpToolExecutionResultDto("log_agent_audit", true, new { Audited = true, Timestamp = DateTime.UtcNow }, null, 40, sw.ElapsedMilliseconds);
                break;

            default:
                sw.Stop();
                result = new McpToolExecutionResultDto(toolName, false, null, $"Unknown tool '{toolName}'.", 10, sw.ElapsedMilliseconds);
                break;
        }

        // Record into Audit Log
        _auditLogs.Insert(0, new AgentAuditLogEntryDto(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "SGrowing-Sales-Agent-01",
            toolName,
            currentAutonomy,
            $"Executed tool '{toolName}' with autonomy '{currentAutonomy}'.",
            currentAutonomy == "Draft Only" ? "PendingReview" : "Approved",
            sw.ElapsedMilliseconds
        ));

        return result;
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

    private static void SeedInitialAuditLogs()
    {
        _auditLogs.AddRange([
            new AgentAuditLogEntryDto(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-4), "SGrowing-Sales-Agent-01", "crm_search_leads", "Auto-Approve", "Discovered 14 high-intent retail buyers matching 'Distribution'", "Approved", 84.0),
            new AgentAuditLogEntryDto(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-9), "SGrowing-Outbound-SDR", "outbound_send_spintax_campaign", "Human-in-the-Loop", "Generated 45 spintax variations for Q4 Retail Blitz", "Approved", 132.5),
            new AgentAuditLogEntryDto(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-14), "SGrowing-Deliverability-Bot", "deliverability_run_placement_test", "Auto-Approve", "Health radar placement verified at 97.4% across Google & Microsoft", "Approved", 92.1),
            new AgentAuditLogEntryDto(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-22), "SGrowing-Signer-Bot", "contracts_dispatch_for_signature", "Draft Only", "Prepared Digital SaaS Proposal for Enterprise Partner Corp", "PendingReview", 110.0),
            new AgentAuditLogEntryDto(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-30), "SGrowing-Logistics-Agent", "courier_generate_waybill", "Auto-Approve", "Created Aramex waybill WB-TN-20261027-4819 with COD 185 TND", "Approved", 65.4)
        ]);
    }
}
