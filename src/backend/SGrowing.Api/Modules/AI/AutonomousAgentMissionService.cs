using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI;

public interface IAutonomousAgentMissionService
{
    Task<AgentMission> CreateMissionAsync(Guid tenantId, string title, string targetGoal, string autonomyMode = "Human-in-the-Loop", decimal budgetUsd = 50.0m, CancellationToken cancellationToken = default);
    Task<bool> ExecuteMissionStepAsync(Guid missionId, CancellationToken cancellationToken = default);
    Task<bool> ApproveMissionStepAsync(Guid missionId, Guid stepId, string approvedBy, CancellationToken cancellationToken = default);
    Task<int> ProcessActiveMissionsAsync(CancellationToken cancellationToken = default);
}

public class AutonomousAgentMissionService : IAutonomousAgentMissionService
{
    private readonly SGrowingDbContext _db;
    private readonly IMcpServerRegistryService _mcp;
    private readonly ILogger<AutonomousAgentMissionService>? _logger;

    public AutonomousAgentMissionService(
        SGrowingDbContext db,
        IMcpServerRegistryService mcp,
        ILogger<AutonomousAgentMissionService>? logger = null)
    {
        _db = db;
        _mcp = mcp;
        _logger = logger;
    }

    public async Task<AgentMission> CreateMissionAsync(
        Guid tenantId,
        string title,
        string targetGoal,
        string autonomyMode = "Human-in-the-Loop",
        decimal budgetUsd = 50.0m,
        CancellationToken cancellationToken = default)
    {
        var mission = AgentMission.Create(tenantId, title, targetGoal, autonomyMode, budgetUsd);
        _db.AgentMissions.Add(mission);
        DecomposeGoalIntoSteps(mission);
        await _db.SaveChangesAsync(cancellationToken);

        // Execute initial step
        await ExecuteMissionStepAsync(mission.Id, cancellationToken);

        return await _db.AgentMissions
            .Include(m => m.Steps)
            .FirstAsync(m => m.Id == mission.Id, cancellationToken);
    }

    public async Task<bool> ExecuteMissionStepAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        var mission = await _db.AgentMissions
            .Include(m => m.Steps)
            .FirstOrDefaultAsync(m => m.Id == missionId, cancellationToken);

        if (mission == null || mission.Status == AgentMissionStatus.Completed || mission.Status == AgentMissionStatus.Failed)
        {
            return false;
        }

        // 1. Plan Decomposition: If no steps exist yet, decompose goal into multi-step tool sequence
        if (mission.Steps.Count == 0)
        {
            DecomposeGoalIntoSteps(mission);
            mission.Status = AgentMissionStatus.Running;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // 2. Budget Guardrail Check
        if (mission.SpentBudgetUsd >= mission.TotalBudgetUsd)
        {
            mission.Status = AgentMissionStatus.Failed;
            mission.ReasoningTrace += $"\n[Guardrail Alert] Mission budget limit of ${mission.TotalBudgetUsd:N2} reached. Execution halted.";
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }

        // 3. Find next pending or awaiting-approval step
        var currentStep = mission.Steps
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault(s => s.Status == AgentMissionStepStatus.Pending || s.Status == AgentMissionStepStatus.AwaitingApproval);

        if (currentStep == null)
        {
            if (mission.Steps.Any(s => s.Status == AgentMissionStepStatus.Failed))
            {
                mission.Status = AgentMissionStatus.Failed;
                mission.ReasoningTrace += "\n[Failed] Mission halted due to failed step(s).";
            }
            else
            {
                // All steps completed!
                mission.Status = AgentMissionStatus.Completed;
                mission.CompletedAtUtc = DateTime.UtcNow;
                mission.ReasoningTrace += "\n[Done] All mission steps executed successfully. Goal achieved.";
            }
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        // 4. Human Approval Gate
        bool isSensitiveAction = currentStep.ToolName is "outbound_send_spintax_campaign" 
            or "contracts_dispatch_for_signature" 
            or "courier_generate_waybill";

        if (mission.AutonomyMode == "Human-in-the-Loop" && isSensitiveAction && string.IsNullOrEmpty(currentStep.ApprovedBy))
        {
            if (currentStep.Status != AgentMissionStepStatus.AwaitingApproval)
            {
                currentStep.Status = AgentMissionStepStatus.AwaitingApproval;
                currentStep.ApprovalRequired = true;
                mission.Status = AgentMissionStatus.PausedForApproval;
                mission.ReasoningTrace += $"\n[Approval Gate] Step {currentStep.StepIndex} ({currentStep.ToolName}) requires human review and confirmation before proceeding.";
                await _db.SaveChangesAsync(cancellationToken);
            }
            return false; // Paused waiting for human approval
        }

        // 5. Execute Step Tool
        currentStep.Status = AgentMissionStepStatus.Executing;
        mission.Status = AgentMissionStatus.Running;

        var arguments = ParseArguments(currentStep.InputArgumentsJson);
        var result = await _mcp.ExecuteMcpToolAsync(mission.TenantId, currentStep.ToolName, arguments);

        currentStep.OutputResultJson = JsonSerializer.Serialize(result.OutputPayload);
        currentStep.ExecutionDurationMs = result.ExecutionTimeMs;
        currentStep.TokensConsumed = result.TokensConsumed;
        decimal stepCost = Math.Round((decimal)result.TokensConsumed * 0.000003m, 4);
        currentStep.CostUsd = stepCost > 0 ? stepCost : 0.01m;
        currentStep.ExecutedAtUtc = DateTime.UtcNow;
        currentStep.Status = result.IsSuccess ? AgentMissionStepStatus.Completed : AgentMissionStepStatus.Failed;

        mission.SpentBudgetUsd += currentStep.CostUsd;
        mission.TokensConsumed += currentStep.TokensConsumed;
        mission.CurrentStepIndex = currentStep.StepIndex;

        if (!result.IsSuccess)
        {
            mission.Status = AgentMissionStatus.Failed;
            mission.ReasoningTrace += $"\n[Step {currentStep.StepIndex} Failed] {currentStep.ToolName} execution failed: {result.ErrorMessage ?? "Tool execution failed"}. Mission halted.";
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }

        mission.ReasoningTrace += $"\n[Step {currentStep.StepIndex}] Executed {currentStep.ToolName} in {result.ExecutionTimeMs:N0}ms ({result.TokensConsumed} tokens). Status: {currentStep.Status}. Result: {SummarizeResult(currentStep.OutputResultJson)}";

        // Check if all steps done
        if (mission.Steps.All(s => s.Status == AgentMissionStepStatus.Completed))
        {
            mission.Status = AgentMissionStatus.Completed;
            mission.CompletedAtUtc = DateTime.UtcNow;
            mission.ReasoningTrace += "\n[Done] All autonomous mission steps completed successfully.";
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveMissionStepAsync(
        Guid missionId,
        Guid stepId,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        var mission = await _db.AgentMissions
            .Include(m => m.Steps)
            .FirstOrDefaultAsync(m => m.Id == missionId, cancellationToken);

        if (mission == null) return false;

        var step = mission.Steps.FirstOrDefault(s => s.Id == stepId);
        if (step == null) return false;

        step.ApprovedBy = approvedBy;
        step.ApprovedAtUtc = DateTime.UtcNow;
        step.Status = AgentMissionStepStatus.Pending; // Ready to execute

        mission.Status = AgentMissionStatus.Running;
        mission.ReasoningTrace += $"\n[Approval Granted] Step {step.StepIndex} ({step.ToolName}) approved by {approvedBy}. Resuming mission execution.";

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> ProcessActiveMissionsAsync(CancellationToken cancellationToken = default)
    {
        var activeMissions = await _db.AgentMissions
            .Where(m => m.Status == AgentMissionStatus.Pending || m.Status == AgentMissionStatus.Running)
            .Take(10)
            .ToListAsync(cancellationToken);

        int processed = 0;
        foreach (var mission in activeMissions)
        {
            var executed = await ExecuteMissionStepAsync(mission.Id, cancellationToken);
            if (executed) processed++;
        }

        return processed;
    }

    private void DecomposeGoalIntoSteps(AgentMission mission)
    {
        var steps = new List<AgentMissionStep>
        {
            AgentMissionStep.Create(
                mission.Id,
                0,
                "crm_search_leads",
                JsonSerializer.Serialize(new { query = "Retail", limit = 5 }),
                "Decompose target goal: Query CRM for qualified high-propensity retail leads in target territory.",
                approvalRequired: false),

            AgentMissionStep.Create(
                mission.Id,
                1,
                "deliverability_run_placement_test",
                JsonSerializer.Serialize(new { domain = "sgrowing.io" }),
                "Assess outbound sender reputation and seed mailbox placement across Google & Microsoft prior to dispatch.",
                approvalRequired: false),

            AgentMissionStep.Create(
                mission.Id,
                2,
                "outbound_send_spintax_campaign",
                JsonSerializer.Serialize(new
                {
                    campaignId = "CMP-AUTONOMOUS-REV-101",
                    spintaxTemplate = "{Hello|Greetings|Bonjour} {{firstName}}, noticed your expansion in retail logistics. Let's optimize your courier delivery rates.",
                    recipientEmails = new[] { "karim@maghrebretail.tn", "amina@carthagelog.tn" }
                }),
                "Dispatch personalized spintax cadence variant to identified retail prospects with high deliverability assurance.",
                approvalRequired: true),

            AgentMissionStep.Create(
                mission.Id,
                3,
                "contracts_dispatch_for_signature",
                JsonSerializer.Serialize(new
                {
                    documentTitle = "Commercial SaaS & Logistics Master Services Agreement",
                    signerName = "Karim Trabelsi",
                    signerEmail = "karim@maghrebretail.tn"
                }),
                "Generate and dispatch multi-party cryptographic digital contract envelope for e-signature.",
                approvalRequired: true),

            AgentMissionStep.Create(
                mission.Id,
                4,
                "courier_generate_waybill",
                JsonSerializer.Serialize(new
                {
                    orderId = "ORD-AUTONOMOUS-9901",
                    recipientName = "Karim Trabelsi",
                    recipientPhone = "+21620112233",
                    destinationWilaya = "Tunis",
                    codAmount = 450.0m
                }),
                "Generate COD courier waybill, Code 128 barcode payload, and schedule regional logistics dispatch.",
                approvalRequired: true)
        };

        foreach (var s in steps)
        {
            mission.Steps.Add(s);
            _db.AgentMissionSteps.Add(s);
        }

        mission.ReasoningTrace += $"\n[Planner] Goal decomposed into 5 executable tool DAG steps. Sensitive steps marked for approval gate: outbound dispatch, contract sealing, courier waybill.";
    }

    private static Dictionary<string, object> ParseArguments(string json)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            return dict ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private static string SummarizeResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "Success";
        if (json.Length > 80) return json[..77] + "...";
        return json;
    }
}
