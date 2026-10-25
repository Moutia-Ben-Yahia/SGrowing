namespace MarketingStudio.Core.Entities;

public class HelpCategory
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int OrderIndex { get; set; } = 0;
    public string Icon { get; set; } = "📚";

    private HelpCategory() { }

    public static HelpCategory Create(Guid tenantId, string name, string slug, int orderIndex = 0, string icon = "📚")
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Slug is required.", nameof(slug));

        return new HelpCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Slug = slug.ToLowerInvariant().Trim(),
            OrderIndex = orderIndex,
            Icon = icon
        };
    }
}
