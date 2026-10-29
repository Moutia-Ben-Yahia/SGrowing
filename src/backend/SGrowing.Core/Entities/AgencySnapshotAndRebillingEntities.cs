namespace SGrowing.Core.Entities;

public class TenantSnapshotRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SnapshotName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Version { get; set; } = "1.0.0";
    public string BlueprintJson { get; set; } = "{}";
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static TenantSnapshotRecord Create(
        Guid tenantId,
        string snapshotName,
        string category,
        string blueprintJson,
        string version = "1.0.0",
        string description = "")
    {
        return new TenantSnapshotRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SnapshotName = snapshotName.Trim(),
            Category = category.Trim(),
            Version = version.Trim(),
            BlueprintJson = blueprintJson,
            Description = description.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class TenantRebillingConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgencyTenantId { get; set; }
    public Guid SubAccountTenantId { get; set; }
    public decimal SmsMarkupMultiplier { get; set; } = 1.30m; // 30% margin
    public decimal VoiceMarkupMultiplier { get; set; } = 1.25m; // 25% margin
    public decimal AiTokenMarkupMultiplier { get; set; } = 1.50m; // 50% margin
    public decimal AutoRebillThresholdUsd { get; set; } = 50.0m;
    public decimal AutoRebillAmountUsd { get; set; } = 100.0m;
    public bool IsAutoRebillEnabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public static TenantRebillingConfig Create(
        Guid agencyTenantId,
        Guid subAccountTenantId,
        decimal smsMultiplier = 1.30m,
        decimal voiceMultiplier = 1.25m,
        decimal aiMultiplier = 1.50m)
    {
        return new TenantRebillingConfig
        {
            Id = Guid.NewGuid(),
            AgencyTenantId = agencyTenantId,
            SubAccountTenantId = subAccountTenantId,
            SmsMarkupMultiplier = Math.Max(1.0m, smsMultiplier),
            VoiceMarkupMultiplier = Math.Max(1.0m, voiceMultiplier),
            AiTokenMarkupMultiplier = Math.Max(1.0m, aiMultiplier),
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public decimal CalculateRetailCost(string serviceType, decimal wholesaleCost)
    {
        var multiplier = serviceType.ToLowerInvariant() switch
        {
            "sms" => SmsMarkupMultiplier,
            "voice" => VoiceMarkupMultiplier,
            "ai" or "aitoken" or "aitokens" => AiTokenMarkupMultiplier,
            _ => 1.20m
        };
        return Math.Round(wholesaleCost * multiplier, 4);
    }
}
