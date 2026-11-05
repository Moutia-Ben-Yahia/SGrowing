using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SGrowing.Api.Common.Tenancy;

public interface IRlsPolicySqlGenerator
{
    string GenerateTableRlsDdl(string schema, string tableName);
    string GenerateSchemaAwareRlsMigrationDdl(IEnumerable<(string Schema, string Table)> tables);
    string GenerateAllSchemasRlsDdl(IModel model);
    IReadOnlyList<(string Schema, string Table)> DiscoverTenantPartitionedTables(IModel model);
}

/// <summary>
/// Generates idempotent PostgreSQL Row-Level Security (RLS) DDL scripts across all 12 enterprise schemas.
/// Enforces kernel-level tenant isolation (PRD Section 5.1 & 5.2).
/// </summary>
public class RlsPolicySqlGenerator : IRlsPolicySqlGenerator
{
    public static readonly string[] RecognizedSchemas =
    [
        "platform", "identity", "crm", "ai", "commerce", "voice",
        "marketing", "analytics", "compliance", "automation", "support", "integrations"
    ];

    public string GenerateTableRlsDdl(string schema, string tableName)
    {
        if (string.IsNullOrWhiteSpace(schema)) schema = "public";
        var safeSchema = schema.Trim();
        var safeTable = tableName.Trim();
        var policyName = $"{safeTable.ToLowerInvariant()}_tenant_isolation_policy";

        var sb = new StringBuilder();
        sb.AppendLine($"-- RLS Policy for \"{safeSchema}\".\"{safeTable}\"");
        sb.AppendLine($"ALTER TABLE \"{safeSchema}\".\"{safeTable}\" ENABLE ROW LEVEL SECURITY;");
        sb.AppendLine($"ALTER TABLE \"{safeSchema}\".\"{safeTable}\" FORCE ROW LEVEL SECURITY;");
        sb.AppendLine($"DROP POLICY IF EXISTS \"{policyName}\" ON \"{safeSchema}\".\"{safeTable}\";");
        sb.AppendLine($"CREATE POLICY \"{policyName}\" ON \"{safeSchema}\".\"{safeTable}\"");
        sb.AppendLine("FOR ALL");
        sb.AppendLine("USING (");
        sb.AppendLine("    current_setting('app.bypass_rls', true) = 'true'");
        sb.AppendLine("    OR \"TenantId\" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid");
        sb.AppendLine(");");

        return sb.ToString();
    }

    public string GenerateSchemaAwareRlsMigrationDdl(IEnumerable<(string Schema, string Table)> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine("-- SGrowing Enterprise 12-Schema PostgreSQL Row-Level Security (RLS) Hardening");
        sb.AppendLine("-- Zero-Cross-Tenant Data Leakage Kernel Isolation");
        sb.AppendLine("-- =============================================================================");
        sb.AppendLine();
        sb.AppendLine("DO $$");
        sb.AppendLine("BEGIN");
        sb.AppendLine("    PERFORM set_config('app.current_tenant_id', '', false);");
        sb.AppendLine("    PERFORM set_config('app.bypass_rls', 'false', false);");
        sb.AppendLine("END $$;");
        sb.AppendLine();

        var distinctTables = tables
            .Distinct()
            .OrderBy(t => t.Schema)
            .ThenBy(t => t.Table);

        foreach (var (schema, table) in distinctTables)
        {
            sb.AppendLine(GenerateTableRlsDdl(schema, table));
        }

        return sb.ToString();
    }

    public IReadOnlyList<(string Schema, string Table)> DiscoverTenantPartitionedTables(IModel model)
    {
        var results = new List<(string Schema, string Table)>();

        foreach (var entityType in model.GetEntityTypes())
        {
            // Check if entity has TenantId property
            var tenantProp = entityType.FindProperty("TenantId");
            if (tenantProp == null) continue;

            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName)) continue;

            var schemaName = entityType.GetSchema() ?? "public";
            results.Add((schemaName, tableName));
        }

        return results
            .Distinct()
            .OrderBy(t => t.Schema)
            .ThenBy(t => t.Table)
            .ToList();
    }

    public string GenerateAllSchemasRlsDdl(IModel model)
    {
        var discovered = DiscoverTenantPartitionedTables(model);
        return GenerateSchemaAwareRlsMigrationDdl(discovered);
    }
}
