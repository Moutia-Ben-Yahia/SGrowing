namespace SGrowing.Api.Modules.CRM.MetadataEngine;

public enum FieldDataType
{
    Text,
    Number,
    Currency,
    Select,
    MultiSelect,
    Boolean,
    DateTime,
    Relation
}

public record CustomFieldDefinition(
    string FieldId,
    string Name,
    string Label,
    FieldDataType Type,
    bool IsRequired,
    List<string>? Options = null,
    string? TargetEntityName = null
);

public record CustomObjectSchema(
    string SchemaId,
    string TenantId,
    string EntityName,
    string PluralName,
    string Icon,
    string Description,
    List<CustomFieldDefinition> Fields,
    DateTime CreatedAtUtc
);

public record CustomRecord(
    string RecordId,
    string SchemaId,
    string TenantId,
    Dictionary<string, object> Attributes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record CreateSchemaRequest(
    string TenantId,
    string EntityName,
    string PluralName,
    string Icon,
    string Description,
    List<CustomFieldDefinition> Fields
);

public record CreateRecordRequest(
    string TenantId,
    string SchemaId,
    Dictionary<string, object> Attributes
);

public record DuplicateMatchCandidate(
    string RecordIdA,
    string RecordIdB,
    string FieldChecked,
    string ValueA,
    string ValueB,
    double SimilarityScore,
    string Reason
);

public record MergeRecordsRequest(
    string TenantId,
    string SchemaId,
    string MasterRecordId,
    string DuplicateRecordId,
    Dictionary<string, object> ResolvedAttributes
);
