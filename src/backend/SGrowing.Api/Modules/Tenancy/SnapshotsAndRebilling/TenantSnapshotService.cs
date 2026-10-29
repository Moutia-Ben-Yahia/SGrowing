using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;

public class TenantSnapshotBlueprint
{
    public string SnapshotName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<SnapshotCustomFieldDto> CustomFields { get; set; } = new();
    public List<SnapshotWorkflowDto> Workflows { get; set; } = new();
    public List<SnapshotFunnelDto> Funnels { get; set; } = new();
}

public class SnapshotCustomFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";
    public string EntityType { get; set; } = "Contact";
}

public class SnapshotWorkflowDto
{
    public string Name { get; set; } = string.Empty;
    public string TriggerType { get; set; } = "LeadCreated";
    public string GraphJson { get; set; } = "{}";
}

public class SnapshotFunnelDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = "launch-funnel";
}

public class TenantSnapshotService : ITenantSnapshotService
{
    private readonly SGrowingDbContext _db;

    public TenantSnapshotService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<TenantSnapshotRecord> CreateSnapshotAsync(
        Guid tenantId,
        string snapshotName,
        string category,
        string description = "")
    {
        // Extract real tenant configurations or defaults
        var customFields = await _db.CustomFieldDefinitions
            .Where(f => f.TenantId == tenantId)
            .Select(f => new SnapshotCustomFieldDto { Name = f.FieldName, FieldType = f.FieldType.ToString(), EntityType = f.EntityType })
            .ToListAsync();

        var workflows = await _db.WorkflowDefinitionRecords
            .Where(w => w.TenantId == tenantId)
            .Select(w => new SnapshotWorkflowDto { Name = w.Name, TriggerType = w.TriggerType, GraphJson = w.NodesJson })
            .ToListAsync();

        var funnels = await _db.SalesFunnels
            .Where(f => f.TenantId == tenantId)
            .Select(f => new SnapshotFunnelDto { Name = f.Name, Slug = f.Slug })
            .ToListAsync();

        // If tenant has no custom entities yet, package a standard agency starter blueprint
        if (customFields.Count == 0 && workflows.Count == 0 && funnels.Count == 0)
        {
            customFields.Add(new SnapshotCustomFieldDto { Name = "PreferredContactChannel", FieldType = "Text", EntityType = "Contact" });
            customFields.Add(new SnapshotCustomFieldDto { Name = "CustomerLtvTier", FieldType = "Text", EntityType = "Contact" });

            workflows.Add(new SnapshotWorkflowDto
            {
                Name = "Onboarding Welcome Drip - {{location.name}}",
                TriggerType = "CustomerRegistered",
                GraphJson = "[{\"id\":\"node-1\",\"name\":\"Trigger\",\"type\":1,\"subtype\":\"inbound\"}]"
            });

            funnels.Add(new SnapshotFunnelDto
            {
                Name = "Core Conversion Funnel - {{location.name}}",
                Slug = "welcome-offer"
            });
        }

        var blueprint = new TenantSnapshotBlueprint
        {
            SnapshotName = snapshotName,
            Category = category,
            CustomFields = customFields,
            Workflows = workflows,
            Funnels = funnels
        };

        var blueprintJson = JsonSerializer.Serialize(blueprint);
        var snapshot = TenantSnapshotRecord.Create(tenantId, snapshotName, category, blueprintJson, "1.0.0", description);

        _db.TenantSnapshotRecords.Add(snapshot);
        await _db.SaveChangesAsync();

        return snapshot;
    }

    public async Task<SnapshotDeploymentResultDto> DeploySnapshotAsync(
        Guid targetTenantId,
        Guid snapshotId,
        string targetTenantName)
    {
        var snapshot = await _db.TenantSnapshotRecords
            .FirstOrDefaultAsync(s => s.Id == snapshotId);

        if (snapshot == null)
            throw new KeyNotFoundException($"Snapshot with ID '{snapshotId}' not found.");

        var blueprint = JsonSerializer.Deserialize<TenantSnapshotBlueprint>(snapshot.BlueprintJson)
            ?? new TenantSnapshotBlueprint();

        int fieldsCreated = 0;
        int workflowsCreated = 0;
        int funnelsCreated = 0;

        // 1. Deploy Custom Fields
        foreach (var cf in blueprint.CustomFields)
        {
            var exists = await _db.CustomFieldDefinitions
                .AnyAsync(f => f.TenantId == targetTenantId && f.FieldName == cf.Name);

            if (!exists)
            {
                var ft = Enum.TryParse<CustomFieldType>(cf.FieldType, out var parsedFt) ? parsedFt : CustomFieldType.Text;
                var newField = new CustomFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    TenantId = targetTenantId,
                    FieldName = cf.Name,
                    FieldLabel = cf.Name,
                    FieldType = ft,
                    EntityType = cf.EntityType,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.CustomFieldDefinitions.Add(newField);
                fieldsCreated++;
            }
        }

        // 2. Deploy Workflows with token replacement
        foreach (var wf in blueprint.Workflows)
        {
            var resolvedName = wf.Name.Replace("{{location.name}}", targetTenantName);
            var exists = await _db.WorkflowDefinitionRecords
                .AnyAsync(w => w.TenantId == targetTenantId && w.Name == resolvedName);

            if (!exists)
            {
                var newWf = new WorkflowDefinitionRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = targetTenantId,
                    Name = resolvedName,
                    TriggerType = wf.TriggerType,
                    NodesJson = wf.GraphJson,
                    Description = "Cloned from agency snapshot blueprint",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _db.WorkflowDefinitionRecords.Add(newWf);
                workflowsCreated++;
            }
        }

        // 3. Deploy Funnels with token replacement
        foreach (var fn in blueprint.Funnels)
        {
            var resolvedName = fn.Name.Replace("{{location.name}}", targetTenantName);
            var exists = await _db.SalesFunnels
                .AnyAsync(f => f.TenantId == targetTenantId && f.Name == resolvedName);

            if (!exists)
            {
                var newFunnel = SalesFunnel.Create(
                    targetTenantId,
                    resolvedName,
                    $"{fn.Slug}-{targetTenantId.ToString()[..4]}");
                _db.SalesFunnels.Add(newFunnel);
                funnelsCreated++;
            }
        }

        await _db.SaveChangesAsync();

        SGrowingTelemetry.TenantSnapshotsDeployedCounter.Add(1,
            new KeyValuePair<string, object?>("target_tenant_id", targetTenantId.ToString()),
            new KeyValuePair<string, object?>("snapshot_id", snapshotId.ToString()));

        return new SnapshotDeploymentResultDto(
            snapshotId,
            targetTenantId,
            snapshot.SnapshotName,
            fieldsCreated,
            workflowsCreated,
            funnelsCreated,
            DateTime.UtcNow,
            $"Snapshot successfully deployed to tenant '{targetTenantName}'");
    }

    public async Task<List<TenantSnapshotRecord>> ListSnapshotsAsync(Guid tenantId)
    {
        return await _db.TenantSnapshotRecords
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<TenantSnapshotRecord?> GetSnapshotAsync(Guid snapshotId)
    {
        return await _db.TenantSnapshotRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId);
    }
}
