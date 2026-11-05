using System;

namespace SGrowing.Core.Entities;

public class DevSandboxSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public bool IsSandboxMode { get; set; } = true;
    public int SimulatedLatencyMs { get; set; } = 45;
    public bool SimulateFailures { get; set; } = false;
    public double FailureRatePercent { get; set; } = 0.0;
    public string MockHeadersJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DevRateLimitBucketEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BucketKey { get; set; } = string.Empty;
    public double CurrentTokens { get; set; }
    public DateTime LastRefillUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
