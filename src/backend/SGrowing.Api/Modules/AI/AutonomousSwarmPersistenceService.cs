using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Common.Observability;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Governance;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI;

public record SwarmGoalRequest(
    Guid TenantId,
    string GoalTitle,
    string HighLevelObjective,
    string TargetPersona,
    decimal BudgetUsd = 100.0m,
    string AutonomyMode = "Human-in-the-Loop"
);

public record SwarmStepExecutionResult(
    Guid MissionId,
    Guid StepId,
    int StepIndex,
    string StepTitle,
    AgentMissionStepStatus Status,
    bool RequiresHumanApproval,
    string? ApprovalToken,
    decimal CostUsd,
    string TraceNote
);

public record SwarmMissionSummary(
    Guid MissionId,
    Guid TenantId,
    string Title,
    AgentMissionStatus Status,
    int TotalSteps,
    int CompletedSteps,
    decimal SpentBudgetUsd,
    decimal TotalBudgetUsd,
    DateTime CreatedAtUtc
);

public interface IAutonomousSwarmPersistenceService
{
    Task<AgentMission> LaunchSwarmMissionAsync(SwarmGoalRequest request, CancellationToken ct = default);
    Task<SwarmStepExecutionResult> ExecuteNextStepAsync(Guid missionId, CancellationToken ct = default);
    Task<bool> ApprovePausedStepWithTokenAsync(Guid missionId, Guid stepId, Guid approvalRequestId, string cryptographicToken, string approverUserId, CancellationToken ct = default);
    Task<IReadOnlyList<SwarmMissionSummary>> GetActiveMissionsAsync(Guid tenantId, CancellationToken ct = default);
    Task<AgentMission?> GetMissionDetailsAsync(Guid missionId, CancellationToken ct = default);
}

/// <summary>
/// Autonomous Agent Swarm & Mission Persistence Service.
/// Coordinates multi-agent goal decomposition, persistent mission tracking,
/// cryptographic HITL approval gating, and tool audit logging (PRD Section 5.8 & 7.4).
/// </summary>
public class AutonomousSwarmPersistenceService : IAutonomousSwarmPersistenceService
{
    private readonly SGrowingDbContext _db;
    private readonly IApprovalGatekeeperService _gatekeeper;
    private readonly ILogger<AutonomousSwarmPersistenceService>? _logger;

    public AutonomousSwarmPersistenceService(
        SGrowingDbContext db,
        IApprovalGatekeeperService gatekeeper,
        ILogger<AutonomousSwarmPersistenceService>? logger = null)
    {
        _db = db;
        _gatekeeper = gatekeeper;
        _logger = logger;
    }

    public async Task<AgentMission> LaunchSwarmMissionAsync(SwarmGoalRequest request, CancellationToken ct = default)
    {
        var mission = AgentMission.Create(
            tenantId: request.TenantId,
            title: request.GoalTitle,
            targetGoal: request.HighLevelObjective,
            autonomyMode: request.AutonomyMode,
            budgetUsd: request.BudgetUsd
        );

        _db.AgentMissions.Add(mission);

        // Decompose goal into multi-agent specialist steps
        var steps = DecomposeSwarmGoal(mission.Id, request.HighLevelObjective, request.TargetPersona);
        mission.Steps.AddRange(steps);

        await _db.SaveChangesAsync(ct);

        _logger?.LogInformation("Launched autonomous swarm mission {MissionId} for tenant {TenantId} with {Steps} steps",
            mission.Id, request.TenantId, steps.Count);

        return mission;
    }

    public async Task<SwarmStepExecutionResult> ExecuteNextStepAsync(Guid missionId, CancellationToken ct = default)
    {
        var mission = await _db.AgentMissions
            .Include(m => m.Steps)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct);

        if (mission == null)
        {
            throw new InvalidOperationException($"Mission {missionId} not found.");
        }

        if (mission.Status is AgentMissionStatus.Completed or AgentMissionStatus.Failed or AgentMissionStatus.Canceled)
        {
            return new SwarmStepExecutionResult(missionId, Guid.Empty, 0, "No-op", AgentMissionStepStatus.Completed, false, null, 0m, "Mission already concluded.");
        }

        // Budget Enforcement
        if (mission.SpentBudgetUsd >= mission.TotalBudgetUsd)
        {
            mission.Status = AgentMissionStatus.Failed;
            mission.ReasoningTrace += $"\n[Budget Exceeded] Spent ${mission.SpentBudgetUsd} >= limit ${mission.TotalBudgetUsd}. Halted.";
            await _db.SaveChangesAsync(ct);
            return new SwarmStepExecutionResult(missionId, Guid.Empty, 0, "Budget Halt", AgentMissionStepStatus.Failed, false, null, 0m, "Budget limit exceeded.");
        }

        var nextStep = mission.Steps
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault(s => s.Status == AgentMissionStepStatus.Pending || s.Status == AgentMissionStepStatus.AwaitingApproval);

        if (nextStep == null)
        {
            mission.Status = AgentMissionStatus.Completed;
            mission.CompletedAtUtc = DateTime.UtcNow;
            mission.ReasoningTrace += "\n[Done] All swarm mission steps completed successfully.";
            await _db.SaveChangesAsync(ct);
            return new SwarmStepExecutionResult(missionId, Guid.Empty, mission.Steps.Count, "Completed", AgentMissionStepStatus.Completed, false, null, 0m, "All steps finished.");
        }

        // Evaluate Risk Gating via ApprovalGatekeeperService
        decimal estimatedFinancialImpact = ExtractFinancialImpact(nextStep.InputArgumentsJson);
        var risk = _gatekeeper.CalculateRiskLevel(nextStep.ToolName, estimatedFinancialImpact, nextStep.InputArgumentsJson);
        bool requiresApproval = (risk is ApprovalRiskLevel.High or ApprovalRiskLevel.Critical) &&
                                mission.AutonomyMode != "Auto-Approve";

        if (requiresApproval && string.IsNullOrEmpty(nextStep.ApprovedBy))
        {
            // Register cryptographic approval request
            var approvalDecision = await _gatekeeper.EvaluateAndRegisterActionAsync(
                tenantId: mission.TenantId,
                operationType: nextStep.ToolName,
                description: $"Swarm Mission Step {nextStep.StepIndex}: {nextStep.ToolName}",
                financialImpactUsd: estimatedFinancialImpact,
                payloadJson: nextStep.InputArgumentsJson,
                ct: ct
            );

            nextStep.Status = AgentMissionStepStatus.AwaitingApproval;
            nextStep.ApprovalRequired = true;
            mission.Status = AgentMissionStatus.PausedForApproval;
            mission.ReasoningTrace += $"\n[HITL Gate] Step {nextStep.StepIndex} ({nextStep.ToolName}) exceeds safety threshold. Paused for human token sign-off.";

            await _db.SaveChangesAsync(ct);

            return new SwarmStepExecutionResult(
                MissionId: missionId,
                StepId: nextStep.Id,
                StepIndex: nextStep.StepIndex,
                StepTitle: nextStep.ToolName,
                Status: AgentMissionStepStatus.AwaitingApproval,
                RequiresHumanApproval: true,
                ApprovalToken: approvalDecision.OneTimeApprovalToken,
                CostUsd: 0m,
                TraceNote: "Action requires cryptographic human approval."
            );
        }

        // Execute Tool Step
        nextStep.Status = AgentMissionStepStatus.Executing;
        mission.Status = AgentMissionStatus.Running;

        var start = DateTime.UtcNow;
        var stepCost = 0.05m;
        var tokensConsumed = 350;

        // Simulate execution result
        nextStep.OutputResultJson = JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Autonomous action '{nextStep.ToolName}' dispatched successfully.",
            timestamp = DateTime.UtcNow
        });

        nextStep.ExecutionDurationMs = (DateTime.UtcNow - start).TotalMilliseconds + 25.0;
        nextStep.TokensConsumed = tokensConsumed;
        nextStep.CostUsd = stepCost;
        nextStep.ExecutedAtUtc = DateTime.UtcNow;
        nextStep.Status = AgentMissionStepStatus.Completed;

        mission.SpentBudgetUsd += stepCost;
        mission.TokensConsumed += tokensConsumed;
        mission.CurrentStepIndex = nextStep.StepIndex;
        mission.ReasoningTrace += $"\n[Step {nextStep.StepIndex}] Executed {nextStep.ToolName} successfully. Tokens: {tokensConsumed}.";

        // Audit Logging to ai.AiToolExecutionLogs
        var toolLog = AiToolExecutionLog.Create(
            tenantId: mission.TenantId,
            agentName: "AutonomousSwarmOrchestrator",
            toolName: nextStep.ToolName,
            argumentsJson: nextStep.InputArgumentsJson,
            resultJson: nextStep.OutputResultJson,
            durationMs: (int)nextStep.ExecutionDurationMs,
            tokens: tokensConsumed,
            costUsd: stepCost,
            approvalStatus: requiresApproval ? ToolApprovalStatus.Approved : ToolApprovalStatus.AutoApproved,
            approvedBy: nextStep.ApprovedBy
        );
        _db.AiToolExecutionLogs.Add(toolLog);

        if (mission.Steps.All(s => s.Status == AgentMissionStepStatus.Completed))
        {
            mission.Status = AgentMissionStatus.Completed;
            mission.CompletedAtUtc = DateTime.UtcNow;
            mission.ReasoningTrace += "\n[Done] All swarm mission steps completed successfully.";
        }

        await _db.SaveChangesAsync(ct);

        return new SwarmStepExecutionResult(
            MissionId: missionId,
            StepId: nextStep.Id,
            StepIndex: nextStep.StepIndex,
            StepTitle: nextStep.ToolName,
            Status: nextStep.Status,
            RequiresHumanApproval: false,
            ApprovalToken: null,
            CostUsd: stepCost,
            TraceNote: $"Step {nextStep.StepIndex} completed."
        );
    }

    public async Task<bool> ApprovePausedStepWithTokenAsync(
        Guid missionId,
        Guid stepId,
        Guid approvalRequestId,
        string cryptographicToken,
        string approverUserId,
        CancellationToken ct = default)
    {
        var verifyResult = await _gatekeeper.ApproveWithTokenAsync(
            approvalRequestId,
            cryptographicToken,
            approverUserId,
            ct
        );

        if (!verifyResult.Success)
        {
            _logger?.LogWarning("Verification failed for step {StepId} of mission {MissionId}", stepId, missionId);
            return false;
        }

        var mission = await _db.AgentMissions
            .Include(m => m.Steps)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct);

        if (mission == null) return false;

        var step = mission.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step == null) return false;

        step.ApprovedBy = approverUserId;
        step.ApprovedAtUtc = DateTime.UtcNow;
        step.Status = AgentMissionStepStatus.Pending; // Ready to execute

        mission.Status = AgentMissionStatus.Running;
        mission.ReasoningTrace += $"\n[Approved] Step {step.StepIndex} approved by {approverUserId} with cryptographic verification. Resuming execution.";

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<SwarmMissionSummary>> GetActiveMissionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.AgentMissions
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new SwarmMissionSummary(
                m.Id,
                m.TenantId,
                m.Title,
                m.Status,
                m.Steps.Count,
                m.Steps.Count(s => s.Status == AgentMissionStepStatus.Completed),
                m.SpentBudgetUsd,
                m.TotalBudgetUsd,
                m.CreatedAtUtc
            ))
            .ToListAsync(ct);
    }

    public async Task<AgentMission?> GetMissionDetailsAsync(Guid missionId, CancellationToken ct = default)
    {
        return await _db.AgentMissions
            .Include(m => m.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == missionId, ct);
    }

    private static List<AgentMissionStep> DecomposeSwarmGoal(Guid missionId, string objective, string persona)
    {
        var steps = new List<AgentMissionStep>
        {
            AgentMissionStep.Create(
                missionId: missionId,
                stepIndex: 1,
                toolName: "crm_segment_audience",
                argumentsJson: JsonSerializer.Serialize(new { query = objective, limit = 500 }),
                reasoningThought: "Specialist analyzes customer segments matching high-level growth objective."
            ),
            AgentMissionStep.Create(
                missionId: missionId,
                stepIndex: 2,
                toolName: "AdSpendAdjustment",
                argumentsJson: JsonSerializer.Serialize(new { budgetUsd = 1200.0, channel = "MetaAds", targetRoas = 3.5 }),
                reasoningThought: "Scale ad spend to $1,200 to capture peak conversion window (requires cryptographic human gate)."
            ),
            AgentMissionStep.Create(
                missionId: missionId,
                stepIndex: 3,
                toolName: "analytics_generate_roas_projection",
                argumentsJson: JsonSerializer.Serialize(new { forecastWindowDays = 7 }),
                reasoningThought: "Compute Bayesian ROAS confidence bounds and update dashboard."
            )
        };

        return steps;
    }

    private static decimal ExtractFinancialImpact(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.TryGetProperty("budgetUsd", out var budgetProp))
            {
                return budgetProp.GetDecimal();
            }
            if (doc.RootElement.TryGetProperty("financialImpactUsd", out var impactProp))
            {
                return impactProp.GetDecimal();
            }
        }
        catch { }
        return 0m;
    }
}
