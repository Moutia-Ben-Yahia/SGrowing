using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Conversations.Macros;

public class CannedMacroService : ICannedMacroService
{
    private static readonly ConcurrentDictionary<Guid, List<CannedMacroDto>> _macroStore = new();
    private static readonly Regex VariableRegex = new(@"\{\{([a-zA-Z0-9_.]+)\}\}", RegexOptions.Compiled);

    public Task<CannedMacroDto> CreateMacroAsync(Guid tenantId, CreateCannedMacroRequest req, CancellationToken ct = default)
    {
        var list = _macroStore.GetOrAdd(tenantId, _ => GetDefaultMacros(tenantId));
        var cleanKey = req.TriggerKey.Trim();
        if (!cleanKey.StartsWith('/')) cleanKey = "/" + cleanKey;

        var variables = ExtractVariables(req.ContentTemplate);

        var macro = new CannedMacroDto(
            Id: Guid.NewGuid(),
            TenantId: tenantId,
            TriggerKey: cleanKey.ToLowerInvariant(),
            Title: req.Title,
            Category: req.Category,
            ContentTemplate: req.ContentTemplate,
            RequiredVariables: variables,
            CreatedAtUtc: DateTime.UtcNow
        );

        list.RemoveAll(m => m.TriggerKey.Equals(macro.TriggerKey, StringComparison.OrdinalIgnoreCase));
        list.Add(macro);

        return Task.FromResult(macro);
    }

    public Task<List<CannedMacroDto>> GetMacrosAsync(Guid tenantId, string? category = null, CancellationToken ct = default)
    {
        var list = _macroStore.GetOrAdd(tenantId, _ => GetDefaultMacros(tenantId));
        var query = list.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult(query.OrderBy(m => m.TriggerKey).ToList());
    }

    public Task<List<CannedMacroDto>> SearchMacrosByHotkeyAsync(Guid tenantId, string hotkeyQuery, CancellationToken ct = default)
    {
        var list = _macroStore.GetOrAdd(tenantId, _ => GetDefaultMacros(tenantId));
        var cleanQuery = (hotkeyQuery ?? "").Trim().ToLowerInvariant();

        var matches = list
            .Where(m => m.TriggerKey.Contains(cleanQuery) || m.Title.ToLowerInvariant().Contains(cleanQuery))
            .OrderBy(m => m.TriggerKey)
            .ToList();

        return Task.FromResult(matches);
    }

    public Task<CannedMacroResolutionDto> ResolveMacroAsync(
        Guid tenantId,
        string triggerKey,
        MacroSubstitutionContext context,
        CancellationToken ct = default)
    {
        var list = _macroStore.GetOrAdd(tenantId, _ => GetDefaultMacros(tenantId));
        var cleanKey = triggerKey.Trim().ToLowerInvariant();
        if (!cleanKey.StartsWith('/')) cleanKey = "/" + cleanKey;

        var macro = list.FirstOrDefault(m => m.TriggerKey.Equals(cleanKey, StringComparison.OrdinalIgnoreCase));
        if (macro == null)
        {
            throw new KeyNotFoundException($"No canned macro found for trigger '{cleanKey}'.");
        }

        var variableMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contact.name"] = context.ContactName ?? "there",
            ["contact.email"] = context.ContactEmail ?? string.Empty,
            ["agent.name"] = context.AgentName ?? "Support Specialist",
            ["company.name"] = context.CompanyName ?? "SGrowing",
            ["order.number"] = context.OrderNumber ?? "N/A",
            ["order.amount"] = context.OrderAmount.HasValue ? $"{context.OrderAmount:N2} {context.Currency}" : "N/A"
        };

        if (context.CustomVariables != null)
        {
            foreach (var (k, v) in context.CustomVariables)
            {
                variableMap[k] = v;
            }
        }

        var missingVariables = new List<string>();
        var resolved = VariableRegex.Replace(macro.ContentTemplate, match =>
        {
            var key = match.Groups[1].Value;
            if (variableMap.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) && val != "N/A")
            {
                return val;
            }
            missingVariables.Add(key);
            return match.Value;
        });

        return Task.FromResult(new CannedMacroResolutionDto(
            TriggerKey: macro.TriggerKey,
            Title: macro.Title,
            ResolvedContent: resolved,
            MissingVariables: missingVariables
        ));
    }

    private static List<string> ExtractVariables(string template)
    {
        var matches = VariableRegex.Matches(template);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in matches)
        {
            set.Add(m.Groups[1].Value);
        }
        return set.ToList();
    }

    private static List<CannedMacroDto> GetDefaultMacros(Guid tenantId)
    {
        return
        [
            new CannedMacroDto(
                Guid.NewGuid(),
                tenantId,
                "/pricing",
                "Enterprise Tier Pricing Overview",
                "Sales",
                "Hi {{contact.name}},\n\nOur Growth tier starts at $299/mo including 50,000 cold outreach emails and full CRM sync. The Enterprise tier ($799/mo) includes dedicated IP warmup and unlimited custom domain DKIM pools.\n\nBest,\n{{agent.name}}",
                ["contact.name", "agent.name"],
                DateTime.UtcNow
            ),
            new CannedMacroDto(
                Guid.NewGuid(),
                tenantId,
                "/refund",
                "Cash on Delivery Refund Instructions",
                "Support",
                "Hello {{contact.name}},\n\nRegarding order {{order.number}} (totaling {{order.amount}}), we have approved your doorstep exchange request. Our regional courier will collect the package within 48 hours and disburse your cash refund on site.\n\nRegards,\n{{agent.name}}",
                ["contact.name", "order.number", "order.amount", "agent.name"],
                DateTime.UtcNow
            ),
            new CannedMacroDto(
                Guid.NewGuid(),
                tenantId,
                "/faq",
                "Knowledge Base FAQ Links",
                "Support",
                "Hi {{contact.name}},\n\nYou can find our complete setup guide and video tutorials at https://docs.sgrowing.io. If you need any further assistance, feel free to ask me here.\n\nWarm regards,\n{{agent.name}}",
                ["contact.name", "agent.name"],
                DateTime.UtcNow
            ),
            new CannedMacroDto(
                Guid.NewGuid(),
                tenantId,
                "/onboarding",
                "Platform Onboarding Checklist",
                "General",
                "Welcome to {{company.name}}, {{contact.name}}!\n\nHere are the 3 quick steps to complete onboarding:\n1. Verify DNS records (SPF, DKIM, DMARC)\n2. Connect your first mailbox\n3. Upload your prospect list\n\nCheers,\n{{agent.name}}",
                ["company.name", "contact.name", "agent.name"],
                DateTime.UtcNow
            )
        ];
    }
}
