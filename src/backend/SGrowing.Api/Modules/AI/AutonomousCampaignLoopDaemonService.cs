using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Modules.AI.Governance;
using SGrowing.Api.Modules.Analytics;

namespace SGrowing.Api.Modules.AI;

public record CampaignRoasThresholdAlert(
    Guid TenantId,
    string Channel,
    string CampaignId,
    decimal CurrentRoas,
    decimal TargetRoas,
    decimal AdSpend,
    decimal Revenue,
    DateTime DetectedAtUtc
);

public record CampaignInterventionPlan(
    Guid MissionId,
    Guid TenantId,
    string Channel,
    string CampaignId,
    decimal RecommendedSpendAdjustment,
    bool RequiresHitlApproval,
    Guid? ApprovalRequestId,
    string? ApprovalToken,
    string InterventionStatus // "Executing", "PausedForApproval", "Completed"
);

public interface IAutonomousCampaignLoopDaemonService
{
    Task<IReadOnlyList<CampaignRoasThresholdAlert>> AuditCampaignRoasAsync(
        Guid tenantId,
        decimal minTargetRoas = 2.5m,
        CancellationToken ct = default);

    Task<CampaignInterventionPlan> ExecuteInterventionAsync(
        CampaignRoasThresholdAlert alert,
        CancellationToken ct = default);

    Task<bool> ApproveAndPublishInterventionAsync(
        Guid tenantId,
        Guid missionId,
        string approvalToken,
        CancellationToken ct = default);
}

/// <summary>
/// Autonomous Closed-Loop Campaign Execution Engine (PRD Domain J & 5.10).
/// Detects underperforming campaigns in ClickHouse OLAP, triggers multi-agent swarm remediation
/// (GrowthDirector, Copywriter, AdOptimizer), and enforces cryptographic HITL gating for spend rebalancing.
/// </summary>
public class AutonomousCampaignLoopDaemonService : IAutonomousCampaignLoopDaemonService
{
    private readonly IClickHouseAnalyticsProjectionService _olap;
    private readonly IAutonomousSwarmPersistenceService _swarm;
    private readonly IApprovalGatekeeperService _gatekeeper;
    private readonly ILogger<AutonomousCampaignLoopDaemonService>? _logger;

    private readonly ConcurrentDictionary<Guid, CampaignInterventionPlan> _activePlans = new();

    public AutonomousCampaignLoopDaemonService(
        IClickHouseAnalyticsProjectionService olap,
        IAutonomousSwarmPersistenceService swarm,
        IApprovalGatekeeperService gatekeeper,
        ILogger<AutonomousCampaignLoopDaemonService>? logger = null)
    {
        _olap = olap;
        _swarm = swarm;
        _gatekeeper = gatekeeper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CampaignRoasThresholdAlert>> AuditCampaignRoasAsync(
        Guid tenantId,
        decimal minTargetRoas = 2.5m,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var channels = new[] { "meta", "google", "tiktok" };
        var alerts = new List<CampaignRoasThresholdAlert>();

        foreach (var channel in channels)
        {
            var metrics = await _olap.GetRealtimeRoasAsync(tenantId, now.AddDays(-7), now, channel, ct);
            if (metrics.TotalSpend > 100m && metrics.Roas < minTargetRoas)
            {
                alerts.Add(new CampaignRoasThresholdAlert(
                    TenantId: tenantId,
                    Channel: channel,
                    CampaignId: $"camp_{channel}_auto_perf",
                    CurrentRoas: metrics.Roas,
                    TargetRoas: minTargetRoas,
                    AdSpend: metrics.TotalSpend,
                    Revenue: metrics.TotalRevenue,
                    DetectedAtUtc: now
                ));
            }
        }

        return alerts;
    }

    public async Task<CampaignInterventionPlan> ExecuteInterventionAsync(
        CampaignRoasThresholdAlert alert,
        CancellationToken ct = default)
    {
        // 1. Calculate proposed spend adjustment
        decimal recommendedAdjustment = alert.CurrentRoas < 1.0m ? -600m : 750m;
        bool requiresHitl = Math.Abs(recommendedAdjustment) >= 500m;

        // 2. Initiate Swarm Mission
        var missionGoal = $"Autonomous remediation for {alert.Channel} campaign {alert.CampaignId} (Current ROAS: {alert.CurrentRoas:F2}, Target: {alert.TargetRoas:F2})";
        var mission = await _swarm.LaunchSwarmMissionAsync(new SwarmGoalRequest(
            TenantId: alert.TenantId,
            GoalTitle: $"Autonomous ROAS Recovery for {alert.Channel}",
            HighLevelObjective: missionGoal,
            TargetPersona: "GrowthDirector",
            BudgetUsd: 1000m
        ), ct);

        Guid? approvalRequestId = null;
        string? approvalToken = null;
        string status = "Executing";

        if (requiresHitl)
        {
            var decision = await _gatekeeper.EvaluateAndRegisterActionAsync(
                alert.TenantId,
                "AdSpendAdjustment",
                $"Rebalance {alert.Channel} ad spend by {recommendedAdjustment:C} to recover ROAS",
                Math.Abs(recommendedAdjustment),
                $"{{\"campaignId\":\"{alert.CampaignId}\",\"adjustment\":{recommendedAdjustment}}}",
                ct);

            approvalRequestId = decision.RequestId;
            approvalToken = decision.OneTimeApprovalToken;
            status = "PausedForApproval";
        }

        var plan = new CampaignInterventionPlan(
            MissionId: mission.Id,
            TenantId: alert.TenantId,
            Channel: alert.Channel,
            CampaignId: alert.CampaignId,
            RecommendedSpendAdjustment: recommendedAdjustment,
            RequiresHitlApproval: requiresHitl,
            ApprovalRequestId: approvalRequestId,
            ApprovalToken: approvalToken,
            InterventionStatus: status
        );

        _activePlans[mission.Id] = plan;

        _logger?.LogInformation(
            "Spawned intervention plan for mission {MissionId} on {Channel}: Adjustment={Adj}, RequiresHitl={Hitl}",
            mission.Id,
            alert.Channel,
            recommendedAdjustment,
            requiresHitl);

        return plan;
    }

    public async Task<bool> ApproveAndPublishInterventionAsync(
        Guid tenantId,
        Guid missionId,
        string approvalToken,
        CancellationToken ct = default)
    {
        if (!_activePlans.TryGetValue(missionId, out var plan))
        {
            return false;
        }

        if (plan.ApprovalRequestId.HasValue)
        {
            var result = await _gatekeeper.ApproveWithTokenAsync(
                plan.ApprovalRequestId.Value,
                approvalToken,
                "Operator",
                ct);

            if (!result.Success)
            {
                return false;
            }
        }

        var updatedPlan = plan with { InterventionStatus = "Completed" };
        _activePlans[missionId] = updatedPlan;

        _logger?.LogInformation("Successfully approved and executed intervention for mission {MissionId}", missionId);
        return true;
    }
}
