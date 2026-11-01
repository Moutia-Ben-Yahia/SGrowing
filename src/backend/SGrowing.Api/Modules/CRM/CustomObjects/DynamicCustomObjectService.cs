using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.CRM.CustomObjects;

#region Enums & DTOs

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomObjectFieldType
{
    Text = 1,
    Number = 2,
    Boolean = 3,
    Currency = 4,
    Date = 5,
    Email = 6,
    Select = 7,
    MultiSelect = 8
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomRelationshipType
{
    OneToMany = 1,
    ManyToOne = 2
}

public record CustomFieldDefinitionDto(
    string Name,
    string Label,
    CustomObjectFieldType Type,
    bool IsRequired = false,
    List<string>? Options = null,
    decimal? Min = null,
    decimal? Max = null,
    string? ValidationRegex = null,
    object? DefaultValue = null
);

public record CustomRelationshipDto(
    string TargetObjectSlug,
    CustomRelationshipType RelationshipType,
    string? RelationshipName = null
);

public class CustomObjectSchemaRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ObjectSlug { get; set; } = string.Empty;
    public string SingularName { get; set; } = string.Empty;
    public string PluralName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CustomFieldDefinitionDto> Fields { get; set; } = [];
    public List<CustomRelationshipDto> Relationships { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CustomObjectRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SchemaId { get; set; }
    public string ObjectSlug { get; set; } = string.Empty;
    public Dictionary<string, object?> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CreateCustomObjectSchemaRequest(
    string ObjectSlug,
    string SingularName,
    string PluralName,
    string? Description = null,
    List<CustomFieldDefinitionDto>? Fields = null,
    List<CustomRelationshipDto>? Relationships = null
);

public record CreateCustomObjectRecordRequest(
    Dictionary<string, object?> Data
);

public record UpdateCustomObjectRecordRequest(
    Dictionary<string, object?> Data,
    bool Partial = false
);

#endregion

#region Service Contract & Implementation

public interface IDynamicCustomObjectService
{
    Task<CustomObjectSchemaRecord> CreateSchemaAsync(Guid tenantId, CreateCustomObjectSchemaRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<CustomObjectSchemaRecord>> GetSchemasAsync(Guid tenantId, CancellationToken ct = default);
    Task<CustomObjectSchemaRecord?> GetSchemaBySlugAsync(Guid tenantId, string objectSlug, CancellationToken ct = default);
    Task<CustomObjectRecord> CreateRecordAsync(Guid tenantId, string objectSlug, Dictionary<string, object?> data, CancellationToken ct = default);
    Task<PagedResult<CustomObjectRecord>> GetRecordsAsync(Guid tenantId, string objectSlug, CustomObjectQueryOptions? options = null, CancellationToken ct = default);
    Task<CustomObjectRecord?> GetRecordByIdAsync(Guid tenantId, string objectSlug, Guid recordId, CancellationToken ct = default);
    Task<CustomObjectRecord> UpdateRecordAsync(Guid tenantId, string objectSlug, Guid recordId, Dictionary<string, object?> data, bool partialUpdate = false, CancellationToken ct = default);
    Task<bool> DeleteRecordAsync(Guid tenantId, string objectSlug, Guid recordId, CancellationToken ct = default);
}

public class DynamicCustomObjectService(
    IEavDynamicSchemaValidator validator,
    IDynamicCustomObjectQueryBuilder queryBuilder) : IDynamicCustomObjectService
{
    private readonly IEavDynamicSchemaValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly IDynamicCustomObjectQueryBuilder _queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));

    // Key: $"{tenantId}:{slug}"
    private readonly ConcurrentDictionary<string, CustomObjectSchemaRecord> _schemas = new(StringComparer.OrdinalIgnoreCase);

    // Key: recordId
    private readonly ConcurrentDictionary<Guid, CustomObjectRecord> _records = new();

    public static string NormalizeSlug(string rawSlug)
    {
        if (string.IsNullOrWhiteSpace(rawSlug)) return string.Empty;
        var lower = rawSlug.Trim().ToLowerInvariant();
        var clean = Regex.Replace(lower, @"[^a-z0-9\s-]", "");
        return Regex.Replace(clean, @"\s+", "-").Trim('-');
    }

    public Task<CustomObjectSchemaRecord> CreateSchemaAsync(Guid tenantId, CreateCustomObjectSchemaRequest req, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(req.SingularName)) throw new ArgumentException("SingularName is required.", nameof(req));
        if (string.IsNullOrWhiteSpace(req.PluralName)) throw new ArgumentException("PluralName is required.", nameof(req));

        var slug = !string.IsNullOrWhiteSpace(req.ObjectSlug)
            ? NormalizeSlug(req.ObjectSlug)
            : NormalizeSlug(req.PluralName);

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("A valid ObjectSlug could not be determined.", nameof(req));

        var schemaKey = $"{tenantId}:{slug}";

        var cleanedFields = (req.Fields ?? [])
            .Select(f =>
            {
                var fieldName = f.Name.Trim().ToLowerInvariant().Replace(" ", "_");
                return f with
                {
                    Name = fieldName,
                    Label = string.IsNullOrWhiteSpace(f.Label) ? f.Name : f.Label.Trim()
                };
            })
            .ToList();

        var schema = new CustomObjectSchemaRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ObjectSlug = slug,
            SingularName = req.SingularName.Trim(),
            PluralName = req.PluralName.Trim(),
            Description = req.Description?.Trim(),
            Fields = cleanedFields,
            Relationships = req.Relationships ?? [],
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _schemas[schemaKey] = schema;
        return Task.FromResult(schema);
    }

    public Task<IReadOnlyList<CustomObjectSchemaRecord>> GetSchemasAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult<IReadOnlyList<CustomObjectSchemaRecord>>([]);

        var schemas = _schemas.Values
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.SingularName)
            .ToList();

        return Task.FromResult<IReadOnlyList<CustomObjectSchemaRecord>>(schemas);
    }

    public Task<CustomObjectSchemaRecord?> GetSchemaBySlugAsync(Guid tenantId, string objectSlug, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(objectSlug))
            return Task.FromResult<CustomObjectSchemaRecord?>(null);

        var slug = NormalizeSlug(objectSlug);
        var schemaKey = $"{tenantId}:{slug}";

        _schemas.TryGetValue(schemaKey, out var schema);
        return Task.FromResult(schema);
    }

    public async Task<CustomObjectRecord> CreateRecordAsync(Guid tenantId, string objectSlug, Dictionary<string, object?> data, CancellationToken ct = default)
    {
        var schema = await GetSchemaBySlugAsync(tenantId, objectSlug, ct)
            ?? throw new KeyNotFoundException($"Custom object schema '{objectSlug}' not found for tenant '{tenantId}'.");

        var normalizedData = _validator.NormalizeData(data);
        var validation = _validator.Validate(schema, normalizedData);

        if (!validation.IsValid)
        {
            throw new CustomObjectValidationException($"Record validation failed for custom object '{schema.ObjectSlug}'.", validation.Errors);
        }

        var record = new CustomObjectRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SchemaId = schema.Id,
            ObjectSlug = schema.ObjectSlug,
            Data = normalizedData,
            DataJson = JsonSerializer.Serialize(normalizedData),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _records[record.Id] = record;
        return record;
    }

    public async Task<PagedResult<CustomObjectRecord>> GetRecordsAsync(Guid tenantId, string objectSlug, CustomObjectQueryOptions? options = null, CancellationToken ct = default)
    {
        var schema = await GetSchemaBySlugAsync(tenantId, objectSlug, ct)
            ?? throw new KeyNotFoundException($"Custom object schema '{objectSlug}' not found for tenant '{tenantId}'.");

        var matching = _records.Values
            .Where(r => r.TenantId == tenantId && r.ObjectSlug.Equals(schema.ObjectSlug, StringComparison.OrdinalIgnoreCase));

        var paged = _queryBuilder.ExecuteQuery(matching, options);
        return paged;
    }

    public Task<CustomObjectRecord?> GetRecordByIdAsync(Guid tenantId, string objectSlug, Guid recordId, CancellationToken ct = default)
    {
        var slug = NormalizeSlug(objectSlug);

        if (_records.TryGetValue(recordId, out var record)
            && record.TenantId == tenantId
            && record.ObjectSlug.Equals(slug, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<CustomObjectRecord?>(record);
        }

        return Task.FromResult<CustomObjectRecord?>(null);
    }

    public async Task<CustomObjectRecord> UpdateRecordAsync(Guid tenantId, string objectSlug, Guid recordId, Dictionary<string, object?> data, bool partialUpdate = false, CancellationToken ct = default)
    {
        var schema = await GetSchemaBySlugAsync(tenantId, objectSlug, ct)
            ?? throw new KeyNotFoundException($"Custom object schema '{objectSlug}' not found for tenant '{tenantId}'.");

        var slug = NormalizeSlug(objectSlug);

        if (!_records.TryGetValue(recordId, out var existing)
            || existing.TenantId != tenantId
            || !existing.ObjectSlug.Equals(slug, StringComparison.OrdinalIgnoreCase))
        {
            throw new KeyNotFoundException($"Record '{recordId}' not found for custom object '{objectSlug}'.");
        }

        var incomingNormalized = _validator.NormalizeData(data);
        Dictionary<string, object?> targetData;

        if (partialUpdate)
        {
            targetData = new Dictionary<string, object?>(existing.Data, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in incomingNormalized)
            {
                targetData[key] = value;
            }
        }
        else
        {
            targetData = incomingNormalized;
        }

        var validation = _validator.Validate(schema, targetData);
        if (!validation.IsValid)
        {
            throw new CustomObjectValidationException($"Record validation failed for custom object '{schema.ObjectSlug}'.", validation.Errors);
        }

        existing.Data = targetData;
        existing.DataJson = JsonSerializer.Serialize(targetData);
        existing.UpdatedAtUtc = DateTime.UtcNow;

        return existing;
    }

    public Task<bool> DeleteRecordAsync(Guid tenantId, string objectSlug, Guid recordId, CancellationToken ct = default)
    {
        var slug = NormalizeSlug(objectSlug);

        if (_records.TryGetValue(recordId, out var existing)
            && existing.TenantId == tenantId
            && existing.ObjectSlug.Equals(slug, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(_records.TryRemove(recordId, out _));
        }

        return Task.FromResult(false);
    }
}

#endregion
