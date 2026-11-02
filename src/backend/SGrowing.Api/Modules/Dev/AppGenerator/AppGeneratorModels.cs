using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Dev.AppGenerator;

public sealed record AppFieldSchema(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("dataType")] string DataType,
    [property: JsonPropertyName("isRequired")] bool IsRequired,
    [property: JsonPropertyName("defaultValue")] string? DefaultValue,
    [property: JsonPropertyName("formulaExpression")] string? FormulaExpression,
    [property: JsonPropertyName("enumOptions")] IReadOnlyList<string>? EnumOptions
);

public sealed record AppSchema(
    [property: JsonPropertyName("appId")] string AppId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("primaryField")] string PrimaryField,
    [property: JsonPropertyName("fields")] IReadOnlyList<AppFieldSchema> Fields,
    [property: JsonPropertyName("permissions")] string Permissions,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc
);

public sealed record AppRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("appId")] string AppId,
    [property: JsonPropertyName("data")] IReadOnlyDictionary<string, object> Data,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc
);

public sealed record CreateAppRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("fields")] IReadOnlyList<AppFieldSchema> Fields,
    [property: JsonPropertyName("permissions")] string Permissions = "InternalEmployees"
);

public sealed record InsertRecordRequest(
    [property: JsonPropertyName("appId")] string AppId,
    [property: JsonPropertyName("data")] IReadOnlyDictionary<string, object> Data
);

public sealed record AppBuilderSnapshot(
    [property: JsonPropertyName("totalAppsCount")] int TotalAppsCount,
    [property: JsonPropertyName("totalRecordsCount")] int TotalRecordsCount,
    [property: JsonPropertyName("apps")] IReadOnlyList<AppSchema> Apps
);
