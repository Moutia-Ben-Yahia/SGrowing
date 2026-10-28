namespace SGrowing.Core.Entities;

public enum CustomFieldType
{
    Text = 1,
    Number = 2,
    Boolean = 3,
    Date = 4,
    DropdownSelect = 5,
    Currency = 6
}

public class CustomFieldDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = "Contact"; // Contact, Company, Deal
    public string FieldName { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public CustomFieldType FieldType { get; set; } = CustomFieldType.Text;
    public string? ValidationRegex { get; set; }
    public bool IsRequired { get; set; } = false;
    public string? DefaultValue { get; set; }
    public string? OptionsJson { get; set; } = "[]"; // For DropdownSelect: ["OptA", "OptB"]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CustomFieldValueRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public Guid FieldDefinitionId { get; set; }
    public string? ValueString { get; set; }
    public decimal? ValueNumber { get; set; }
    public bool? ValueBoolean { get; set; }
    public DateTime? ValueDateTime { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CreateCustomFieldDefinitionRequest(
    string EntityType,
    string FieldName,
    string FieldLabel,
    CustomFieldType FieldType,
    string? ValidationRegex = null,
    bool IsRequired = false,
    string? DefaultValue = null,
    List<string>? Options = null
);

public record SetFieldValueDto(
    string FieldName,
    object? Value
);

public record CustomFieldDisplayDto(
    Guid FieldDefinitionId,
    string FieldName,
    string FieldLabel,
    CustomFieldType FieldType,
    object? Value
);

public record EntityCustomFieldValuesDto(
    Guid EntityId,
    string EntityType,
    List<CustomFieldDisplayDto> Fields
);
