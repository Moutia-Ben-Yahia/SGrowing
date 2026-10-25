using System.Text.Json;

namespace MarketingStudio.Core.Entities;

public enum LifecycleStage
{
    Lead = 1,
    Prospect = 2,
    Customer = 3,
    Churned = 4
}

public class Contact
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public LifecycleStage LifecycleStage { get; set; } = LifecycleStage.Lead;
    public Guid? CompanyId { get; set; }
    public string CustomFieldsJson { get; set; } = "{}";
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; set; }

    private Contact() { }

    public static Contact Create(
        Guid tenantId,
        string firstName,
        string lastName,
        string? email = null,
        string? phone = null,
        LifecycleStage stage = LifecycleStage.Lead,
        Dictionary<string, object>? customFields = null,
        List<string>? tags = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        return new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone,
            LifecycleStage = stage,
            CustomFieldsJson = customFields != null ? JsonSerializer.Serialize(customFields) : "{}",
            Tags = tags ?? new List<string>(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public Dictionary<string, object> GetCustomFields()
    {
        if (string.IsNullOrWhiteSpace(CustomFieldsJson) || CustomFieldsJson == "{}")
            return new Dictionary<string, object>();
        return JsonSerializer.Deserialize<Dictionary<string, object>>(CustomFieldsJson) ?? new Dictionary<string, object>();
    }

    public void SetCustomFields(Dictionary<string, object> fields)
    {
        CustomFieldsJson = JsonSerializer.Serialize(fields);
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
