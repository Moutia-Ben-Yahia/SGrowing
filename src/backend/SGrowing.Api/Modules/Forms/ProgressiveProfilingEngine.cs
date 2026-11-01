using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Forms;

public class ProgressiveAttributeDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Tier { get; set; } // 1, 2, or 3
    public int Priority { get; set; } // 1..N
    public FormFieldType Type { get; set; } = FormFieldType.Text;
    public List<string> Options { get; set; } = [];
    public string? Placeholder { get; set; }
    public string? HelpText { get; set; }
}

public class ProgressiveContactRecord
{
    public Guid ContactId { get; set; }
    public Guid TenantId { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double CompletenessPercentage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ProgressiveAnswerResult
{
    public Guid ContactId { get; set; }
    public Guid TenantId { get; set; }
    public string AttributeKey { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public double CompletenessPercentage { get; set; }
    public bool IsProfileComplete { get; set; }
    public ProgressiveAttributeDefinition? NextAttribute { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ProgressiveContactProfileDto
{
    public Guid ContactId { get; set; }
    public Guid TenantId { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public double CompletenessPercentage { get; set; }
    public bool IsComplete { get; set; }
    public ProgressiveAttributeDefinition? NextRecommendedAttribute { get; set; }
    public int AnsweredCount { get; set; }
    public int TotalAttributesCount { get; set; }
}

public class SaveProgressiveAnswerRequest
{
    public string AttributeKey { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public interface IProgressiveProfilingEngine
{
    Task<ProgressiveAttributeDefinition?> GetNextUnansweredAttributeAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default);
    Task<ProgressiveAnswerResult> SaveProgressiveAnswerAsync(Guid tenantId, Guid contactId, string attributeKey, string value, CancellationToken cancellationToken = default);
    Task<ProgressiveContactProfileDto> GetContactProfileAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default);
    Task<ProgressiveContactRecord> SetContactAttributesAsync(Guid tenantId, Guid contactId, Dictionary<string, string> attributes, CancellationToken cancellationToken = default);
    IReadOnlyList<ProgressiveAttributeDefinition> GetAttributeSchema();
}

public class ProgressiveProfilingEngine : IProgressiveProfilingEngine
{
    private readonly ConcurrentDictionary<(Guid TenantId, Guid ContactId), ProgressiveContactRecord> _contacts = new();

    private static readonly List<ProgressiveAttributeDefinition> TrackedAttributes =
    [
        // Tier 1: Core Identification (High Priority)
        new()
        {
            Key = "Email",
            Label = "Work Email",
            Tier = 1,
            Priority = 1,
            Type = FormFieldType.Email,
            Placeholder = "alex@company.com",
            HelpText = "Your primary business communication channel."
        },
        new()
        {
            Key = "Name",
            Label = "Full Name",
            Tier = 1,
            Priority = 2,
            Type = FormFieldType.Text,
            Placeholder = "Alex Johnson",
            HelpText = "First and last name."
        },
        new()
        {
            Key = "Phone",
            Label = "Phone Number",
            Tier = 1,
            Priority = 3,
            Type = FormFieldType.Tel,
            Placeholder = "+1 (555) 019-2834",
            HelpText = "Direct contact or mobile phone number."
        },

        // Tier 2: Firmographic Context (Medium Priority)
        new()
        {
            Key = "CompanySize",
            Label = "Company Size",
            Tier = 2,
            Priority = 4,
            Type = FormFieldType.Select,
            Options = ["1-10", "11-50", "51-200", "201-1000", "1000+"],
            Placeholder = "Select company size",
            HelpText = "Total headcount across your organization."
        },
        new()
        {
            Key = "Industry",
            Label = "Industry",
            Tier = 2,
            Priority = 5,
            Type = FormFieldType.Select,
            Options = ["SaaS & Software", "Ecommerce & Retail", "Financial Services", "Healthcare", "Manufacturing", "Professional Services", "Other"],
            Placeholder = "Select primary industry",
            HelpText = "Your organization's primary domain."
        },

        // Tier 3: Buying Intent & Qualification (Lower Priority)
        new()
        {
            Key = "Budget",
            Label = "Annual Technology Budget",
            Tier = 3,
            Priority = 6,
            Type = FormFieldType.Select,
            Options = ["< $10,000", "$10,000 - $50,000", "$50,000 - $250,000", "$250,000+"],
            Placeholder = "Select expected budget range",
            HelpText = "Allocated annual budget for growth software."
        },
        new()
        {
            Key = "Role",
            Label = "Job Role / Title",
            Tier = 3,
            Priority = 7,
            Type = FormFieldType.Text,
            Placeholder = "e.g. VP of Growth / Marketing Director",
            HelpText = "Your official function within the organization."
        },
        new()
        {
            Key = "Timeline",
            Label = "Implementation Timeline",
            Tier = 3,
            Priority = 8,
            Type = FormFieldType.Select,
            Options = ["Immediately (< 1 mo)", "1-3 months", "3-6 months", "Just researching"],
            Placeholder = "Select rollout timeframe",
            HelpText = "Expected timeline to implement and deploy."
        }
    ];

    public ProgressiveProfilingEngine()
    {
        SeedDefaultContacts();
    }

    private void SeedDefaultContacts()
    {
        var defaultTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Fresh contact: No attributes answered yet
        var contact1 = new ProgressiveContactRecord
        {
            ContactId = Guid.Parse("00000000-0000-0000-0000-000000000010"),
            TenantId = defaultTenant,
            Attributes = new(StringComparer.OrdinalIgnoreCase),
            CompletenessPercentage = 0.0
        };

        // Tier 1 completed contact
        var contact2 = new ProgressiveContactRecord
        {
            ContactId = Guid.Parse("00000000-0000-0000-0000-000000000011"),
            TenantId = defaultTenant,
            Attributes = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Email"] = "sarah.connor@cyberdyne.io",
                ["Name"] = "Sarah Connor",
                ["Phone"] = "+1 415-555-0142"
            },
            CompletenessPercentage = CalculateCompleteness(3)
        };

        // Fully completed contact
        var contact3 = new ProgressiveContactRecord
        {
            ContactId = Guid.Parse("00000000-0000-0000-0000-000000000012"),
            TenantId = defaultTenant,
            Attributes = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Email"] = "john.wick@continental.com",
                ["Name"] = "John Wick",
                ["Phone"] = "+1 212-555-0199",
                ["CompanySize"] = "51-200",
                ["Industry"] = "Professional Services",
                ["Budget"] = "$50,000 - $250,000",
                ["Role"] = "Executive Director",
                ["Timeline"] = "Immediately (< 1 mo)"
            },
            CompletenessPercentage = 100.0
        };

        _contacts[(contact1.TenantId, contact1.ContactId)] = contact1;
        _contacts[(contact2.TenantId, contact2.ContactId)] = contact2;
        _contacts[(contact3.TenantId, contact3.ContactId)] = contact3;
    }

    public IReadOnlyList<ProgressiveAttributeDefinition> GetAttributeSchema() => TrackedAttributes;

    public Task<ProgressiveAttributeDefinition?> GetNextUnansweredAttributeAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = GetOrCreateContact(tenantId, contactId);

        // Sort by Tier asc, then Priority asc
        var sortedAttributes = TrackedAttributes
            .OrderBy(a => a.Tier)
            .ThenBy(a => a.Priority)
            .ToList();

        foreach (var attr in sortedAttributes)
        {
            if (!contact.Attributes.TryGetValue(attr.Key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return Task.FromResult<ProgressiveAttributeDefinition?>(attr);
            }
        }

        // All attributes answered
        return Task.FromResult<ProgressiveAttributeDefinition?>(null);
    }

    public async Task<ProgressiveAnswerResult> SaveProgressiveAnswerAsync(Guid tenantId, Guid contactId, string attributeKey, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attributeKey))
        {
            throw new ArgumentException("Attribute key cannot be null or empty.", nameof(attributeKey));
        }

        var contact = GetOrCreateContact(tenantId, contactId);

        // Match against known attribute definition (case-insensitive)
        var attrDef = TrackedAttributes.FirstOrDefault(a => string.Equals(a.Key, attributeKey, StringComparison.OrdinalIgnoreCase));
        var canonicalKey = attrDef?.Key ?? attributeKey.Trim();

        contact.Attributes[canonicalKey] = value?.Trim() ?? string.Empty;
        contact.UpdatedAt = DateTimeOffset.UtcNow;

        int answeredCount = CountAnsweredAttributes(contact);
        contact.CompletenessPercentage = CalculateCompleteness(answeredCount);

        var nextAttr = await GetNextUnansweredAttributeAsync(tenantId, contactId, cancellationToken);
        bool isComplete = contact.CompletenessPercentage >= 100.0 || nextAttr == null;

        return new ProgressiveAnswerResult
        {
            ContactId = contactId,
            TenantId = tenantId,
            AttributeKey = canonicalKey,
            Value = contact.Attributes[canonicalKey],
            CompletenessPercentage = contact.CompletenessPercentage,
            IsProfileComplete = isComplete,
            NextAttribute = nextAttr,
            Message = isComplete
                ? "Contact profile is 100% enriched."
                : $"Answer saved. Next attribute to capture: '{nextAttr?.Label}'."
        };
    }

    public Task<ProgressiveContactProfileDto> GetContactProfileAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = GetOrCreateContact(tenantId, contactId);
        int answeredCount = CountAnsweredAttributes(contact);
        var completeness = CalculateCompleteness(answeredCount);
        contact.CompletenessPercentage = completeness;

        var nextAttr = TrackedAttributes
            .OrderBy(a => a.Tier)
            .ThenBy(a => a.Priority)
            .FirstOrDefault(a => !contact.Attributes.TryGetValue(a.Key, out var val) || string.IsNullOrWhiteSpace(val));

        return Task.FromResult(new ProgressiveContactProfileDto
        {
            ContactId = contactId,
            TenantId = tenantId,
            Attributes = new Dictionary<string, string>(contact.Attributes, StringComparer.OrdinalIgnoreCase),
            CompletenessPercentage = completeness,
            IsComplete = completeness >= 100.0,
            NextRecommendedAttribute = nextAttr,
            AnsweredCount = answeredCount,
            TotalAttributesCount = TrackedAttributes.Count
        });
    }

    public Task<ProgressiveContactRecord> SetContactAttributesAsync(Guid tenantId, Guid contactId, Dictionary<string, string> attributes, CancellationToken cancellationToken = default)
    {
        var contact = GetOrCreateContact(tenantId, contactId);
        foreach (var (k, v) in attributes)
        {
            var attrDef = TrackedAttributes.FirstOrDefault(a => string.Equals(a.Key, k, StringComparison.OrdinalIgnoreCase));
            var canonicalKey = attrDef?.Key ?? k.Trim();
            contact.Attributes[canonicalKey] = v;
        }

        contact.UpdatedAt = DateTimeOffset.UtcNow;
        contact.CompletenessPercentage = CalculateCompleteness(CountAnsweredAttributes(contact));
        return Task.FromResult(contact);
    }

    private ProgressiveContactRecord GetOrCreateContact(Guid tenantId, Guid contactId)
    {
        return _contacts.GetOrAdd((tenantId, contactId), key => new ProgressiveContactRecord
        {
            ContactId = key.ContactId,
            TenantId = key.TenantId,
            Attributes = new(StringComparer.OrdinalIgnoreCase),
            CompletenessPercentage = 0.0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private static int CountAnsweredAttributes(ProgressiveContactRecord contact)
    {
        return TrackedAttributes.Count(attr =>
            contact.Attributes.TryGetValue(attr.Key, out var val) && !string.IsNullOrWhiteSpace(val));
    }

    private static double CalculateCompleteness(int answeredCount)
    {
        if (TrackedAttributes.Count == 0) return 100.0;
        return Math.Round(Math.Clamp((double)answeredCount / TrackedAttributes.Count * 100.0, 0.0, 100.0), 1);
    }
}
