using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI.Governance;

public record ApprovalDecisionResult(
    Guid RequestId,
    bool RequiresHumanApproval,
    ApprovalRiskLevel RiskLevel,
    ToolApprovalStatus Status,
    string? OneTimeApprovalToken,
    string Reason
);

public record ApprovalVerificationResult(
    bool Success,
    ToolApprovalStatus Status,
    string Message,
    DateTime? DecidedAtUtc
);

public interface IApprovalGatekeeperService
{
    ApprovalRiskLevel CalculateRiskLevel(string operationType, decimal financialImpactUsd, string? payloadJson = null);

    Task<ApprovalDecisionResult> EvaluateAndRegisterActionAsync(
        Guid tenantId,
        string operationType,
        string description,
        decimal financialImpactUsd,
        string payloadJson,
        CancellationToken ct = default);

    Task<ApprovalVerificationResult> ApproveWithTokenAsync(
        Guid requestId,
        string rawToken,
        string approvedByUserId,
        CancellationToken ct = default);

    Task<ApprovalVerificationResult> RejectActionAsync(
        Guid requestId,
        string rejectedByUserId,
        string reason,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApprovalRequestEntity>> GetPendingApprovalsAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<ApprovalRequestEntity?> GetApprovalByIdAsync(
        Guid requestId,
        CancellationToken ct = default);
}

/// <summary>
/// Supervised AI Autonomy & Human-in-the-Loop (HITL) approval mesh.
/// Implements cryptographic one-time token verification and risk gating thresholds (PRD Section 5.8 & 7.4).
/// </summary>
public class ApprovalGatekeeperService : IApprovalGatekeeperService
{
    private readonly SGrowingDbContext _dbContext;
    private readonly ILogger<ApprovalGatekeeperService>? _logger;

    // Risk Gating Thresholds
    public const decimal AdSpendHighRiskThresholdUsd = 500.0m;
    public const int CampaignRecipientThreshold = 5_000;
    public const decimal WholesaleDiscountThresholdPercent = 25.0m;

    public ApprovalGatekeeperService(
        SGrowingDbContext dbContext,
        ILogger<ApprovalGatekeeperService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public ApprovalRiskLevel CalculateRiskLevel(string operationType, decimal financialImpactUsd, string? payloadJson = null)
    {
        var op = operationType.Trim();

        // Critical operations
        if (op.Equals("SchemaMutation", StringComparison.OrdinalIgnoreCase) ||
            op.Equals("DropTable", StringComparison.OrdinalIgnoreCase) ||
            op.Equals("DatabaseMigration", StringComparison.OrdinalIgnoreCase))
        {
            return ApprovalRiskLevel.Critical;
        }

        // Financial Threshold: Ad Spend > $500
        if (op.Equals("AdSpendAdjustment", StringComparison.OrdinalIgnoreCase) ||
            op.Equals("AdCampaignBudget", StringComparison.OrdinalIgnoreCase))
        {
            if (financialImpactUsd > AdSpendHighRiskThresholdUsd)
                return ApprovalRiskLevel.High;
            if (financialImpactUsd > 100m)
                return ApprovalRiskLevel.Medium;
            return ApprovalRiskLevel.Low;
        }

        // Wholesale Discount: > 25%
        if (op.Equals("WholesaleDiscountOverride", StringComparison.OrdinalIgnoreCase) ||
            op.Equals("DiscountOverride", StringComparison.OrdinalIgnoreCase))
        {
            if (financialImpactUsd >= WholesaleDiscountThresholdPercent)
                return ApprovalRiskLevel.High;
            return ApprovalRiskLevel.Medium;
        }

        // Bulk Outbound Campaign: > 5000 recipients
        if (op.Equals("BulkCampaignDispatch", StringComparison.OrdinalIgnoreCase) ||
            op.Equals("OutboundCampaignBlast", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(payloadJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(payloadJson);
                    if (doc.RootElement.TryGetProperty("recipientCount", out var countProp) &&
                        countProp.GetInt32() > CampaignRecipientThreshold)
                    {
                        return ApprovalRiskLevel.High;
                    }
                }
                catch
                {
                    // Fall back to high on malformed payload
                    return ApprovalRiskLevel.High;
                }
            }

            if (financialImpactUsd > 250m) return ApprovalRiskLevel.High;
            return ApprovalRiskLevel.Medium;
        }

        // General Financial High Impact
        if (financialImpactUsd > 1000m)
        {
            return ApprovalRiskLevel.High;
        }

        return ApprovalRiskLevel.Low;
    }

    public async Task<ApprovalDecisionResult> EvaluateAndRegisterActionAsync(
        Guid tenantId,
        string operationType,
        string description,
        decimal financialImpactUsd,
        string payloadJson,
        CancellationToken ct = default)
    {
        var riskLevel = CalculateRiskLevel(operationType, financialImpactUsd, payloadJson);
        var requiresHumanApproval = riskLevel is ApprovalRiskLevel.High or ApprovalRiskLevel.Critical;

        string? rawToken = null;
        string tokenHash = string.Empty;

        if (requiresHumanApproval)
        {
            // Generate cryptographically secure one-time approval token (32 bytes = 256 bits)
            rawToken = GenerateCryptographicToken();
            tokenHash = ComputeTokenHash(rawToken);
        }

        var requestEntity = new ApprovalRequestEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OperationType = operationType,
            Description = description,
            FinancialImpactUsd = financialImpactUsd,
            RiskLevel = riskLevel,
            PayloadJson = payloadJson,
            ApprovalTokenHash = tokenHash,
            Status = requiresHumanApproval ? ToolApprovalStatus.Pending : ToolApprovalStatus.AutoApproved,
            ApprovedByUserId = requiresHumanApproval ? null : "system:autonomous_gatekeeper",
            CreatedAtUtc = DateTime.UtcNow,
            DecidedAtUtc = requiresHumanApproval ? null : DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };

        _dbContext.ApprovalRequests.Add(requestEntity);
        await _dbContext.SaveChangesAsync(ct);

        _logger?.LogInformation(
            "Registered action {OperationType} for tenant {TenantId}. Risk: {RiskLevel}, RequiresApproval: {RequiresApproval}",
            operationType, tenantId, riskLevel, requiresHumanApproval);

        return new ApprovalDecisionResult(
            RequestId: requestEntity.Id,
            RequiresHumanApproval: requiresHumanApproval,
            RiskLevel: riskLevel,
            Status: requestEntity.Status,
            OneTimeApprovalToken: rawToken,
            Reason: requiresHumanApproval
                ? $"Action exceeds autonomous safety threshold ({riskLevel}). Requires cryptographic human sign-off."
                : "Action within autonomous policy threshold. Auto-approved."
        );
    }

    public async Task<ApprovalVerificationResult> ApproveWithTokenAsync(
        Guid requestId,
        string rawToken,
        string approvedByUserId,
        CancellationToken ct = default)
    {
        var request = await _dbContext.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request == null)
        {
            return new ApprovalVerificationResult(false, ToolApprovalStatus.Rejected, "Approval request not found.", null);
        }

        if (request.Status != ToolApprovalStatus.Pending)
        {
            return new ApprovalVerificationResult(false, request.Status, $"Approval request is not pending (current status: {request.Status}).", request.DecidedAtUtc);
        }

        if (DateTime.UtcNow > request.ExpiresAtUtc)
        {
            request.Status = ToolApprovalStatus.Rejected;
            request.DecisionReason = "Approval token expired.";
            request.DecidedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return new ApprovalVerificationResult(false, ToolApprovalStatus.Rejected, "Approval token has expired.", request.DecidedAtUtc);
        }

        // Verify cryptographic hash
        var presentedHash = ComputeTokenHash(rawToken);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presentedHash),
            Encoding.UTF8.GetBytes(request.ApprovalTokenHash)))
        {
            _logger?.LogWarning("Invalid cryptographic approval token presented for request {RequestId}", requestId);
            return new ApprovalVerificationResult(false, ToolApprovalStatus.Pending, "Cryptographic token verification failed.", null);
        }

        request.Status = ToolApprovalStatus.Approved;
        request.ApprovedByUserId = approvedByUserId;
        request.DecidedAtUtc = DateTime.UtcNow;
        request.DecisionReason = "Approved via cryptographic one-time token sign-off.";

        await _dbContext.SaveChangesAsync(ct);

        _logger?.LogInformation("Action {RequestId} successfully approved by user {UserId}", requestId, approvedByUserId);
        return new ApprovalVerificationResult(true, ToolApprovalStatus.Approved, "Action successfully verified and approved.", request.DecidedAtUtc);
    }

    public async Task<ApprovalVerificationResult> RejectActionAsync(
        Guid requestId,
        string rejectedByUserId,
        string reason,
        CancellationToken ct = default)
    {
        var request = await _dbContext.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request == null)
        {
            return new ApprovalVerificationResult(false, ToolApprovalStatus.Rejected, "Approval request not found.", null);
        }

        request.Status = ToolApprovalStatus.Rejected;
        request.ApprovedByUserId = rejectedByUserId;
        request.DecisionReason = reason;
        request.DecidedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger?.LogInformation("Action {RequestId} rejected by user {UserId}. Reason: {Reason}", requestId, rejectedByUserId, reason);
        return new ApprovalVerificationResult(true, ToolApprovalStatus.Rejected, "Action successfully rejected.", request.DecidedAtUtc);
    }

    public async Task<IReadOnlyList<ApprovalRequestEntity>> GetPendingApprovalsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        return await _dbContext.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Status == ToolApprovalStatus.Pending)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ApprovalRequestEntity?> GetApprovalByIdAsync(
        Guid requestId,
        CancellationToken ct = default)
    {
        return await _dbContext.ApprovalRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
    }

    private static string GenerateCryptographicToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ComputeTokenHash(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
