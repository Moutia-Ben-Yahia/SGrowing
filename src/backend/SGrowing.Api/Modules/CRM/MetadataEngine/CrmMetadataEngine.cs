using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.CRM.MetadataEngine;

public interface ICrmMetadataEngine
{
    CustomObjectSchema CreateSchema(CreateSchemaRequest request);
    CustomObjectSchema? GetSchema(string tenantId, string schemaId);
    List<CustomObjectSchema> ListSchemas(string tenantId);
    CustomRecord CreateRecord(CreateRecordRequest request);
    List<CustomRecord> ListRecords(string tenantId, string schemaId);
    List<DuplicateMatchCandidate> DetectDuplicates(string tenantId, string schemaId, string matchingField);
    CustomRecord MergeRecords(MergeRecordsRequest request);
}

public class CrmMetadataEngine : ICrmMetadataEngine
{
    private readonly ConcurrentDictionary<string, CustomObjectSchema> _schemas = new();
    private readonly ConcurrentDictionary<string, CustomRecord> _records = new();

    private static string BuildSchemaKey(string tenantId, string schemaId) => $"{tenantId}::schema::{schemaId}";
    private static string BuildRecordKey(string tenantId, string recordId) => $"{tenantId}::record::{recordId}";

    public CustomObjectSchema CreateSchema(CreateSchemaRequest request)
    {
        var schemaId = "schema_" + request.EntityName.ToLowerInvariant().Replace(" ", "_");
        var key = BuildSchemaKey(request.TenantId, schemaId);

        var schema = new CustomObjectSchema(
            SchemaId: schemaId,
            TenantId: request.TenantId,
            EntityName: request.EntityName,
            PluralName: request.PluralName,
            Icon: request.Icon,
            Description: request.Description,
            Fields: request.Fields,
            CreatedAtUtc: DateTime.UtcNow
        );

        _schemas[key] = schema;
        return schema;
    }

    public CustomObjectSchema? GetSchema(string tenantId, string schemaId)
    {
        _schemas.TryGetValue(BuildSchemaKey(tenantId, schemaId), out var s);
        return s;
    }

    public List<CustomObjectSchema> ListSchemas(string tenantId)
    {
        return _schemas.Values
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.EntityName)
            .ToList();
    }

    public CustomRecord CreateRecord(CreateRecordRequest request)
    {
        var schema = GetSchema(request.TenantId, request.SchemaId)
            ?? throw new KeyNotFoundException($"Schema '{request.SchemaId}' not found for tenant '{request.TenantId}'.");

        // Validate required fields
        foreach (var reqField in schema.Fields.Where(f => f.IsRequired))
        {
            if (!request.Attributes.ContainsKey(reqField.Name) || request.Attributes[reqField.Name] is null)
            {
                throw new ArgumentException($"Missing required field '{reqField.Name}' ({reqField.Label}).");
            }
        }

        var recordId = "rec_" + Guid.NewGuid().ToString("N")[..8];
        var key = BuildRecordKey(request.TenantId, recordId);

        var record = new CustomRecord(
            RecordId: recordId,
            SchemaId: request.SchemaId,
            TenantId: request.TenantId,
            Attributes: new Dictionary<string, object>(request.Attributes),
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow
        );

        _records[key] = record;
        return record;
    }

    public List<CustomRecord> ListRecords(string tenantId, string schemaId)
    {
        return _records.Values
            .Where(r => r.TenantId == tenantId && r.SchemaId == schemaId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();
    }

    public List<DuplicateMatchCandidate> DetectDuplicates(string tenantId, string schemaId, string matchingField)
    {
        var records = ListRecords(tenantId, schemaId);
        var duplicates = new List<DuplicateMatchCandidate>();

        for (var i = 0; i < records.Count; i++)
        {
            for (var j = i + 1; j < records.Count; j++)
            {
                var r1 = records[i];
                var r2 = records[j];

                if (r1.Attributes.TryGetValue(matchingField, out var val1Obj) &&
                    r2.Attributes.TryGetValue(matchingField, out var val2Obj))
                {
                    var val1 = val1Obj?.ToString()?.Trim() ?? string.Empty;
                    var val2 = val2Obj?.ToString()?.Trim() ?? string.Empty;

                    if (val1.Length > 0 && val2.Length > 0)
                    {
                        var sim = CalculateSimilarity(val1, val2);
                        if (sim >= 0.75)
                        {
                            duplicates.Add(new DuplicateMatchCandidate(
                                RecordIdA: r1.RecordId,
                                RecordIdB: r2.RecordId,
                                FieldChecked: matchingField,
                                ValueA: val1,
                                ValueB: val2,
                                SimilarityScore: Math.Round(sim, 2),
                                Reason: sim >= 0.95 ? "Exact / Near-exact match" : "High fuzzy string similarity"
                            ));
                        }
                    }
                }
            }
        }

        return duplicates;
    }

    public CustomRecord MergeRecords(MergeRecordsRequest request)
    {
        var masterKey = BuildRecordKey(request.TenantId, request.MasterRecordId);
        var dupKey = BuildRecordKey(request.TenantId, request.DuplicateRecordId);

        if (!_records.TryGetValue(masterKey, out var masterRecord))
        {
            throw new KeyNotFoundException($"Master record '{request.MasterRecordId}' not found.");
        }

        if (!_records.TryRemove(dupKey, out _))
        {
            throw new KeyNotFoundException($"Duplicate record '{request.DuplicateRecordId}' not found.");
        }

        // Merge attributes with resolved overrides
        var mergedAttrs = new Dictionary<string, object>(masterRecord.Attributes);
        foreach (var (k, v) in request.ResolvedAttributes)
        {
            mergedAttrs[k] = v;
        }

        var updatedMaster = masterRecord with
        {
            Attributes = mergedAttrs,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _records[masterKey] = updatedMaster;
        return updatedMaster;
    }

    private static double CalculateSimilarity(string s1, string s2)
    {
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (s1.Length == 0 || s2.Length == 0) return 0.0;

        var dist = LevenshteinDistance(s1.ToLowerInvariant(), s2.ToLowerInvariant());
        var maxLen = Math.Max(s1.Length, s2.Length);
        return 1.0 - ((double)dist / maxLen);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; d[i, 0] = i++) { }
        for (var j = 0; j <= m; d[0, j] = j++) { }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[n, m];
    }
}
