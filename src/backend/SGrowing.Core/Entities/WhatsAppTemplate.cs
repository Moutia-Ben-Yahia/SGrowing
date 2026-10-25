namespace SGrowing.Core.Entities;

public enum WhatsAppTemplateCategory
{
    Marketing = 1,
    Utility = 2,
    Authentication = 3
}

public enum WhatsAppTemplateStatus
{
    Draft = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4
}

public class WhatsAppTemplate
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public WhatsAppTemplateCategory Category { get; set; } = WhatsAppTemplateCategory.Marketing;
    public string Language { get; set; } = "en_US";
    public WhatsAppTemplateStatus Status { get; set; } = WhatsAppTemplateStatus.Draft;
    public string BodyText { get; set; } = string.Empty;
    public string ButtonsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; private set; }

    private WhatsAppTemplate() { }

    public static WhatsAppTemplate Create(
        Guid tenantId,
        string name,
        WhatsAppTemplateCategory category,
        string bodyText,
        string language = "en_US",
        string buttonsJson = "[]")
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        return new WhatsAppTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.ToLowerInvariant().Trim().Replace(" ", "_"),
            Category = category,
            Language = language,
            Status = WhatsAppTemplateStatus.Approved, // Auto-marked approved in enterprise sandbox
            BodyText = bodyText,
            ButtonsJson = buttonsJson,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
