using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM.CustomFields;

public interface ICustomFieldService
{
    Task<CustomFieldDefinition> CreateDefinitionAsync(Guid tenantId, CreateCustomFieldDefinitionRequest req, CancellationToken ct = default);
    Task<IEnumerable<CustomFieldDefinition>> GetDefinitionsAsync(Guid tenantId, string entityType, CancellationToken ct = default);
    Task<EntityCustomFieldValuesDto> SetValuesAsync(Guid tenantId, string entityType, Guid entityId, List<SetFieldValueDto> values, CancellationToken ct = default);
    Task<EntityCustomFieldValuesDto> GetValuesAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default);
}

public class CustomFieldService : ICustomFieldService
{
    private readonly SGrowingDbContext _db;

    public CustomFieldService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CustomFieldDefinition> CreateDefinitionAsync(Guid tenantId, CreateCustomFieldDefinitionRequest req, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(req.FieldName)) throw new ArgumentException("FieldName is required.", nameof(req));

        var cleanFieldName = req.FieldName.Trim().ToLowerInvariant().Replace(" ", "_");

        var existing = await _db.CustomFieldDefinitions
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.EntityType.ToLower() == req.EntityType.ToLower() && d.FieldName == cleanFieldName, ct);

        if (existing != null)
        {
            return existing;
        }

        var def = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = req.EntityType.Trim(),
            FieldName = cleanFieldName,
            FieldLabel = string.IsNullOrWhiteSpace(req.FieldLabel) ? req.FieldName : req.FieldLabel.Trim(),
            FieldType = req.FieldType,
            ValidationRegex = req.ValidationRegex,
            IsRequired = req.IsRequired,
            DefaultValue = req.DefaultValue,
            OptionsJson = req.Options != null ? JsonSerializer.Serialize(req.Options) : "[]",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.CustomFieldDefinitions.Add(def);
        await _db.SaveChangesAsync(ct);
        return def;
    }

    public async Task<IEnumerable<CustomFieldDefinition>> GetDefinitionsAsync(Guid tenantId, string entityType, CancellationToken ct = default)
    {
        return await _db.CustomFieldDefinitions
            .Where(d => d.TenantId == tenantId && d.EntityType.ToLower() == entityType.ToLower())
            .OrderBy(d => d.FieldName)
            .ToListAsync(ct);
    }

    public async Task<EntityCustomFieldValuesDto> SetValuesAsync(Guid tenantId, string entityType, Guid entityId, List<SetFieldValueDto> values, CancellationToken ct = default)
    {
        var definitions = await _db.CustomFieldDefinitions
            .Where(d => d.TenantId == tenantId && d.EntityType.ToLower() == entityType.ToLower())
            .ToListAsync(ct);

        var existingValues = await _db.CustomFieldValues
            .Where(v => v.TenantId == tenantId && v.EntityId == entityId)
            .ToListAsync(ct);

        foreach (var val in values)
        {
            var def = definitions.FirstOrDefault(d => d.FieldName.Equals(val.FieldName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (def == null) continue;

            // Validate Regex
            if (!string.IsNullOrWhiteSpace(def.ValidationRegex) && val.Value != null)
            {
                var valStr = val.Value.ToString() ?? "";
                if (!Regex.IsMatch(valStr, def.ValidationRegex))
                {
                    throw new ArgumentException($"Value '{valStr}' does not match validation regex '{def.ValidationRegex}' for field '{def.FieldName}'.");
                }
            }

            var record = existingValues.FirstOrDefault(v => v.FieldDefinitionId == def.Id);
            if (record == null)
            {
                record = new CustomFieldValueRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    EntityId = entityId,
                    FieldDefinitionId = def.Id,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _db.CustomFieldValues.Add(record);
                existingValues.Add(record);
            }

            AssignTypedValue(record, def.FieldType, val.Value);
            record.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return await GetValuesAsync(tenantId, entityType, entityId, ct);
    }

    private static void AssignTypedValue(CustomFieldValueRecord record, CustomFieldType type, object? val)
    {
        record.ValueString = null;
        record.ValueNumber = null;
        record.ValueBoolean = null;
        record.ValueDateTime = null;

        if (val == null) return;

        switch (type)
        {
            case CustomFieldType.Number:
            case CustomFieldType.Currency:
                if (decimal.TryParse(val.ToString(), out var num)) record.ValueNumber = num;
                break;
            case CustomFieldType.Boolean:
                if (bool.TryParse(val.ToString(), out var b)) record.ValueBoolean = b;
                break;
            case CustomFieldType.Date:
                if (DateTime.TryParse(val.ToString(), out var dt)) record.ValueDateTime = dt.ToUniversalTime();
                break;
            default:
                record.ValueString = val.ToString();
                break;
        }
    }

    public async Task<EntityCustomFieldValuesDto> GetValuesAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default)
    {
        var definitions = await _db.CustomFieldDefinitions
            .Where(d => d.TenantId == tenantId && d.EntityType.ToLower() == entityType.ToLower())
            .ToListAsync(ct);

        var values = await _db.CustomFieldValues
            .Where(v => v.TenantId == tenantId && v.EntityId == entityId)
            .ToListAsync(ct);

        var displayList = new List<CustomFieldDisplayDto>();

        foreach (var def in definitions)
        {
            var valRecord = values.FirstOrDefault(v => v.FieldDefinitionId == def.Id);
            object? resolvedVal = def.FieldType switch
            {
                CustomFieldType.Number or CustomFieldType.Currency => valRecord?.ValueNumber,
                CustomFieldType.Boolean => valRecord?.ValueBoolean,
                CustomFieldType.Date => valRecord?.ValueDateTime,
                _ => valRecord?.ValueString ?? def.DefaultValue
            };

            displayList.Add(new CustomFieldDisplayDto(
                FieldDefinitionId: def.Id,
                FieldName: def.FieldName,
                FieldLabel: def.FieldLabel,
                FieldType: def.FieldType,
                Value: resolvedVal
            ));
        }

        return new EntityCustomFieldValuesDto(
            EntityId: entityId,
            EntityType: entityType,
            Fields: displayList
        );
    }
}
