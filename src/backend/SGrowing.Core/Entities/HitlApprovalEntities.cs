using System;

namespace SGrowing.Core.Entities;

public enum ApprovalRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Gated operational action awaiting cryptographic human approval.
/// Schema: ai
/// </summary>
public class ApprovalRequestEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string OperationType { get; set; } = string.Empty; // "AdSpendAdjustment", "BulkCampaignDispatch", "WholesaleDiscountOverride", "SchemaMutation"
    public string Description { get; set; } = string.Empty;
    public decimal FinancialImpactUsd { get; set; }
    public ApprovalRiskLevel RiskLevel { get; set; } = ApprovalRiskLevel.High;
    public string PayloadJson { get; set; } = "{}";
    public string ApprovalTokenHash { get; set; } = string.Empty;
    public ToolApprovalStatus Status { get; set; } = ToolApprovalStatus.Pending;
    public string? ApprovedByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(24);
}
