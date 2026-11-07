using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.AI.Evaluation;

/// <summary>
/// Service contract for multi-tenant AI autonomous agent quality benchmarking, supervised autonomy gating, and regression telemetry.
/// </summary>
public interface IAiAgentEvaluationService
{
    /// <summary>
    /// Generates the global multi-agent benchmark report and supervised autonomy metrics for a tenant.
    /// </summary>
    GlobalEvaluationRadarReport GetGlobalRadarReport(Guid tenantId);

    /// <summary>
    /// Retrieves the evaluation scorecard for a specific agent role within a tenant workspace.
    /// </summary>
    AgentRadarScorecard? GetAgentScorecard(Guid tenantId, string agentRole);

    /// <summary>
    /// Records an execution evaluation run or human feedback review into the tenant registry.
    /// </summary>
    AgentRadarScorecard RecordEvaluation(RecordEvaluationRequest request);

    /// <summary>
    /// Evaluates whether an agent qualifies for autonomy promotion or requires safety demotion.
    /// </summary>
    AutonomyPromotionCheckResult EvaluateAutonomyPromotion(Guid tenantId, string agentRole);

    /// <summary>
    /// Retrieves all active agent scorecards for a tenant.
    /// </summary>
    IReadOnlyList<AgentRadarScorecard> GetAllScorecards(Guid tenantId);
}

/// <summary>
/// Thread-safe, multi-tenant evaluation registry implementing the autonomous quality benchmark radar.
/// </summary>
public class AiAgentEvaluationService : IAiAgentEvaluationService
{
    private readonly ConcurrentDictionary<Guid, TenantEvaluationState> _tenantStates = new();

    /// <summary>
    /// Initializes a new instance of <see cref="AiAgentEvaluationService"/>.
    /// </summary>
    public AiAgentEvaluationService()
    {
    }

    /// <inheritdoc />
    public GlobalEvaluationRadarReport GetGlobalRadarReport(Guid tenantId)
    {
        var tenantState = GetOrCreateTenantState(tenantId);
        var scorecards = tenantState.Agents.Values
            .Select(a => a.ToScorecard())
            .OrderByDescending(s => s.CompositeScore)
            .ToList();

        var totalTasks = tenantState.Agents.Values.Sum(a => a.TotalTasksEvaluated);
        var fullyAutonomous = scorecards.Count(s => s.AutonomyLevel == AutonomyReadinessLevel.Execute);
        var supervised = scorecards.Count(s => s.AutonomyLevel != AutonomyReadinessLevel.Execute);

        var globalScore = scorecards.Count > 0
            ? Math.Round(scorecards.Average(s => s.CompositeScore), 1)
            : 0.0;

        var averageCost = scorecards.Count > 0
            ? Math.Round(scorecards.Average(s => s.CostPerTaskSar), 2)
            : 0m;

        var topRejections = tenantState.RejectionReasons
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => kvp.Key)
            .ToList();

        return new GlobalEvaluationRadarReport(
            GlobalScore: globalScore,
            TotalTasksEvaluated: totalTasks,
            FullyAutonomousAgentCount: fullyAutonomous,
            SupervisedAgentCount: supervised,
            AverageCostPerTaskSar: averageCost,
            TopRejectionReasons: topRejections,
            AgentScorecards: scorecards,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    /// <inheritdoc />
    public AgentRadarScorecard? GetAgentScorecard(Guid tenantId, string agentRole)
    {
        var tenantState = GetOrCreateTenantState(tenantId);
        if (tenantState.Agents.TryGetValue(agentRole, out var agent))
        {
            return agent.ToScorecard();
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentRadarScorecard> GetAllScorecards(Guid tenantId)
    {
        var tenantState = GetOrCreateTenantState(tenantId);
        return tenantState.Agents.Values
            .Select(a => a.ToScorecard())
            .OrderByDescending(s => s.CompositeScore)
            .ToList();
    }

    /// <inheritdoc />
    public AgentRadarScorecard RecordEvaluation(RecordEvaluationRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var tenantState = GetOrCreateTenantState(request.TenantId);

        var agentState = tenantState.Agents.GetOrAdd(request.AgentRole, role => new AgentInternalState(
            agentRole: role,
            autonomyLevel: AutonomyReadinessLevel.Draft,
            taskSuccessRate: request.WasSuccessful ? 100.0 : 0.0,
            toolPrecisionRate: request.ToolExecutionValid ? 100.0 : 0.0,
            approvalRate: request.HumanApproved ? 100.0 : 0.0,
            hallucinationIndex: request.HadHallucination ? 1.0 : 0.0,
            costPerTaskSar: request.CostSar,
            averageLatencyMs: request.LatencyMs,
            totalTasksEvaluated: 1,
            trend: RegressionTrend.Stable,
            recommendation: "Initial baseline run recorded."
        ));

        lock (agentState)
        {
            var prevComposite = agentState.CalculateCompositeScore();

            agentState.ApplyEvaluationRun(
                wasSuccessful: request.WasSuccessful,
                toolExecutionValid: request.ToolExecutionValid,
                humanApproved: request.HumanApproved,
                hadHallucination: request.HadHallucination,
                costSar: request.CostSar,
                latencyMs: request.LatencyMs
            );

            var newComposite = agentState.CalculateCompositeScore();

            if (newComposite > prevComposite + 0.3)
            {
                agentState.Trend = RegressionTrend.Improving;
            }
            else if (newComposite < prevComposite - 0.3)
            {
                agentState.Trend = RegressionTrend.Regressing;
            }
            else
            {
                agentState.Trend = RegressionTrend.Stable;
            }

            // Record rejection reason clustering if provided
            if (!string.IsNullOrWhiteSpace(request.RejectionReason))
            {
                tenantState.RejectionReasons.AddOrUpdate(request.RejectionReason.Trim(), 1, (_, count) => count + 1);
            }

            // Sync recommendation with updated vitals
            agentState.UpdateRecommendation();
        }

        return agentState.ToScorecard();
    }

    /// <inheritdoc />
    public AutonomyPromotionCheckResult EvaluateAutonomyPromotion(Guid tenantId, string agentRole)
    {
        var tenantState = GetOrCreateTenantState(tenantId);
        if (!tenantState.Agents.TryGetValue(agentRole, out var agentState))
        {
            throw new KeyNotFoundException($"Agent role '{agentRole}' does not exist in evaluation registry for tenant '{tenantId}'.");
        }

        lock (agentState)
        {
            var currentLevel = agentState.AutonomyLevel;
            var composite = agentState.CalculateCompositeScore();
            var approvalRate = agentState.ApprovalRate;

            var recommendedLevel = currentLevel;
            var levelChanged = false;
            string reason;

            // Promotion criteria:
            // - Read -> Draft when composite > 60
            // - Draft -> Approve when composite > 80
            // - Approve -> Execute when composite > 90 and human approval > 95%
            // Demotion criteria:
            // - Execute -> Approve when composite <= 90 or human approval <= 95%
            // - Approve -> Draft when composite <= 80
            // - Draft -> Read when composite <= 60

            switch (currentLevel)
            {
                case AutonomyReadinessLevel.Read:
                    if (composite > 60.0)
                    {
                        recommendedLevel = AutonomyReadinessLevel.Draft;
                        levelChanged = true;
                        reason = $"Composite score ({composite:F1}%) exceeds the 60.0% threshold. Promoted to Draft.";
                    }
                    else
                    {
                        reason = $"Composite score ({composite:F1}%) does not meet the 60.0% threshold for drafting capabilities.";
                    }
                    break;

                case AutonomyReadinessLevel.Draft:
                    if (composite > 80.0)
                    {
                        recommendedLevel = AutonomyReadinessLevel.Approve;
                        levelChanged = true;
                        reason = $"Composite score ({composite:F1}%) exceeds the 80.0% threshold. Promoted to Approve (supervised autonomy).";
                    }
                    else if (composite <= 60.0)
                    {
                        recommendedLevel = AutonomyReadinessLevel.Read;
                        levelChanged = true;
                        reason = $"Composite score ({composite:F1}%) fell to or below 60.0%. Demoted to Read-only mode.";
                    }
                    else
                    {
                        reason = $"Operating reliably in Draft mode ({composite:F1}% composite). Requires > 80.0% for supervised approval gating.";
                    }
                    break;

                case AutonomyReadinessLevel.Approve:
                    if (composite > 90.0 && approvalRate > 95.0)
                    {
                        recommendedLevel = AutonomyReadinessLevel.Execute;
                        levelChanged = true;
                        reason = $"Composite score ({composite:F1}% > 90.0%) and human approval rate ({approvalRate:F1}% > 95.0%) qualify for fully autonomous execution.";
                    }
                    else if (composite <= 80.0)
                    {
                        recommendedLevel = AutonomyReadinessLevel.Draft;
                        levelChanged = true;
                        reason = $"Composite score ({composite:F1}%) dropped to or below 80.0%. Demoted to Draft mode.";
                    }
                    else
                    {
                        reason = composite > 90.0
                            ? $"Composite score ({composite:F1}%) is qualified, but human approval rate ({approvalRate:F1}%) is <= 95.0%. Retained in supervised Approve mode."
                            : $"Operating in supervised Approve mode ({composite:F1}% composite, {approvalRate:F1}% approval). Target > 90.0% composite and > 95.0% approval for autonomous execution.";
                    }
                    break;

                case AutonomyReadinessLevel.Execute:
                    if (composite <= 90.0 || approvalRate <= 95.0)
                    {
                        recommendedLevel = AutonomyReadinessLevel.Approve;
                        levelChanged = true;
                        reason = composite <= 90.0
                            ? $"Composite score ({composite:F1}%) dropped to or below 90.0% safety baseline. Demoted from Execute to Approve."
                            : $"Human approval rate ({approvalRate:F1}%) dipped to or below 95.0%. Demoted from Execute to Approve for supervised verification.";
                    }
                    else
                    {
                        reason = $"Operating at peak autonomous reliability ({composite:F1}% composite, {approvalRate:F1}% approval). Full autonomous execution maintained.";
                    }
                    break;

                default:
                    reason = "Autonomy readiness level evaluated.";
                    break;
            }

            if (levelChanged)
            {
                agentState.AutonomyLevel = recommendedLevel;
                agentState.UpdateRecommendation();
            }

            return new AutonomyPromotionCheckResult(
                AgentRole: agentRole,
                CurrentLevel: currentLevel,
                RecommendedLevel: recommendedLevel,
                LevelChanged: levelChanged,
                Reason: reason,
                Scorecard: agentState.ToScorecard()
            );
        }
    }

    private TenantEvaluationState GetOrCreateTenantState(Guid tenantId)
    {
        return _tenantStates.GetOrAdd(tenantId, tid => CreateDefaultTenantState(tid));
    }

    private static TenantEvaluationState CreateDefaultTenantState(Guid tenantId)
    {
        var state = new TenantEvaluationState(tenantId);

        // 1. ResearchAgent (Autonomy: Execute, 98% success, 0.02 hallucination, 4.20 SAR/task)
        // Composite = (98*0.4)+(97*0.3)+(97*0.2)+(9.8) = 97.5
        var researchAgent = new AgentInternalState(
            agentRole: "ResearchAgent",
            autonomyLevel: AutonomyReadinessLevel.Execute,
            taskSuccessRate: 98.0,
            toolPrecisionRate: 97.0,
            approvalRate: 97.0,
            hallucinationIndex: 0.02,
            costPerTaskSar: 4.20m,
            averageLatencyMs: 820.0,
            totalTasksEvaluated: 2400,
            trend: RegressionTrend.Improving,
            recommendation: "Autonomous execution verified. Hallucination rate is at an ultra-low 2.0% with strong multi-source grounding."
        );
        state.Agents[researchAgent.AgentRole] = researchAgent;

        // 2. SeoAgent (Autonomy: Execute, 96% success, 0.04 hallucination, 6.50 SAR/task)
        // Composite = (96*0.4)+(94*0.3)+(95*0.2)+(9.6) = 95.2
        var seoAgent = new AgentInternalState(
            agentRole: "SeoAgent",
            autonomyLevel: AutonomyReadinessLevel.Execute,
            taskSuccessRate: 96.0,
            toolPrecisionRate: 94.0,
            approvalRate: 95.0,
            hallucinationIndex: 0.04,
            costPerTaskSar: 6.50m,
            averageLatencyMs: 1150.0,
            totalTasksEvaluated: 1850,
            trend: RegressionTrend.Stable,
            recommendation: "Maintains high programmatic SEO precision. Autonomous keyword clustering and canonical schema generation."
        );
        state.Agents[seoAgent.AgentRole] = seoAgent;

        // 3. AdvertisingAgent (Autonomy: Approve, 92% success, 0.08 hallucination, 12.00 SAR/task)
        // Composite = (92*0.4)+(84*0.3)+(88*0.2)+(9.2) = 88.8
        var advertisingAgent = new AgentInternalState(
            agentRole: "AdvertisingAgent",
            autonomyLevel: AutonomyReadinessLevel.Approve,
            taskSuccessRate: 92.0,
            toolPrecisionRate: 84.0,
            approvalRate: 88.0,
            hallucinationIndex: 0.08,
            costPerTaskSar: 12.00m,
            averageLatencyMs: 1420.0,
            totalTasksEvaluated: 920,
            trend: RegressionTrend.Improving,
            recommendation: "Requires human approval before ad spend reallocation. Approval rate currently at 88.0% (target > 95.0% for Execute)."
        );
        state.Agents[advertisingAgent.AgentRole] = advertisingAgent;

        // 4. ContentAgent (Autonomy: Approve, 94% success, 0.05 hallucination, 8.40 SAR/task)
        // Composite = (94*0.4)+(84*0.3)+(88*0.2)+(9.5) = 89.9
        var contentAgent = new AgentInternalState(
            agentRole: "ContentAgent",
            autonomyLevel: AutonomyReadinessLevel.Approve,
            taskSuccessRate: 94.0,
            toolPrecisionRate: 84.0,
            approvalRate: 88.0,
            hallucinationIndex: 0.05,
            costPerTaskSar: 8.40m,
            averageLatencyMs: 980.0,
            totalTasksEvaluated: 1540,
            trend: RegressionTrend.Stable,
            recommendation: "Supervised mode active. Creative briefs pass review; Arabic cultural dialect validation required prior to publishing."
        );
        state.Agents[contentAgent.AgentRole] = contentAgent;

        // 5. ApprovalGatekeeper (Autonomy: Execute, 100% precision, 0.00 hallucination, 1.10 SAR/task)
        // Composite = 100.0
        // Global average composite = (97.5 + 95.5 + 88.0 + 89.8 + 100.0) / 5 = 470.8 / 5 = 94.2%
        // Average cost = (4.20 + 6.50 + 12.00 + 8.40 + 1.10) / 5 = 6.44 SAR
        var gatekeeper = new AgentInternalState(
            agentRole: "ApprovalGatekeeper",
            autonomyLevel: AutonomyReadinessLevel.Execute,
            taskSuccessRate: 100.0,
            toolPrecisionRate: 100.0,
            approvalRate: 100.0,
            hallucinationIndex: 0.00,
            costPerTaskSar: 1.10m,
            averageLatencyMs: 190.0,
            totalTasksEvaluated: 4890,
            trend: RegressionTrend.Stable,
            recommendation: "Deterministic governance gatekeeper running with 0% hallucination and zero tolerance on budget policy infractions."
        );
        state.Agents[gatekeeper.AgentRole] = gatekeeper;

        // Seeded realistic rejection clusters
        state.RejectionReasons["Unverified pricing claim against ERP price list"] = 42;
        state.RejectionReasons["Tool schema validation failure on WhatsApp payload"] = 28;
        state.RejectionReasons["Exceeded tone guidelines (over-promising ROI in Saudi market)"] = 19;
        state.RejectionReasons["Hallucinated citation or non-existent discount policy"] = 14;
        state.RejectionReasons["Safety filter flagged competitor mention"] = 8;

        return state;
    }

    private sealed class TenantEvaluationState
    {
        public Guid TenantId { get; }
        public ConcurrentDictionary<string, AgentInternalState> Agents { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, int> RejectionReasons { get; } = new(StringComparer.OrdinalIgnoreCase);

        public TenantEvaluationState(Guid tenantId)
        {
            TenantId = tenantId;
        }
    }

    private sealed class AgentInternalState
    {
        public string AgentRole { get; }
        public AutonomyReadinessLevel AutonomyLevel { get; set; }
        public double TaskSuccessRate { get; set; }
        public double ToolPrecisionRate { get; set; }
        public double ApprovalRate { get; set; }
        public double HallucinationIndex { get; set; }
        public decimal CostPerTaskSar { get; set; }
        public double AverageLatencyMs { get; set; }
        public int TotalTasksEvaluated { get; set; }
        public RegressionTrend Trend { get; set; }
        public string Recommendation { get; set; }

        private double _successSum;
        private double _precisionSum;
        private double _approvalSum;
        private double _hallucinationSum;
        private decimal _costSum;
        private double _latencySum;

        public AgentInternalState(
            string agentRole,
            AutonomyReadinessLevel autonomyLevel,
            double taskSuccessRate,
            double toolPrecisionRate,
            double approvalRate,
            double hallucinationIndex,
            decimal costPerTaskSar,
            double averageLatencyMs,
            int totalTasksEvaluated,
            RegressionTrend trend,
            string recommendation)
        {
            AgentRole = agentRole;
            AutonomyLevel = autonomyLevel;
            TaskSuccessRate = taskSuccessRate;
            ToolPrecisionRate = toolPrecisionRate;
            ApprovalRate = approvalRate;
            HallucinationIndex = hallucinationIndex;
            CostPerTaskSar = costPerTaskSar;
            AverageLatencyMs = averageLatencyMs;
            TotalTasksEvaluated = totalTasksEvaluated;
            Trend = trend;
            Recommendation = recommendation;

            _successSum = taskSuccessRate * totalTasksEvaluated;
            _precisionSum = toolPrecisionRate * totalTasksEvaluated;
            _approvalSum = approvalRate * totalTasksEvaluated;
            _hallucinationSum = hallucinationIndex * totalTasksEvaluated;
            _costSum = costPerTaskSar * totalTasksEvaluated;
            _latencySum = averageLatencyMs * totalTasksEvaluated;
        }

        public void ApplyEvaluationRun(
            bool wasSuccessful,
            bool toolExecutionValid,
            bool humanApproved,
            bool hadHallucination,
            decimal costSar,
            double latencyMs)
        {
            TotalTasksEvaluated++;
            _successSum += wasSuccessful ? 100.0 : 0.0;
            _precisionSum += toolExecutionValid ? 100.0 : 0.0;
            _approvalSum += humanApproved ? 100.0 : 0.0;
            _hallucinationSum += hadHallucination ? 1.0 : 0.0;
            _costSum += costSar;
            _latencySum += latencyMs;

            TaskSuccessRate = Math.Round(_successSum / TotalTasksEvaluated, 2);
            ToolPrecisionRate = Math.Round(_precisionSum / TotalTasksEvaluated, 2);
            ApprovalRate = Math.Round(_approvalSum / TotalTasksEvaluated, 2);
            HallucinationIndex = Math.Round(_hallucinationSum / TotalTasksEvaluated, 4);
            CostPerTaskSar = Math.Round(_costSum / TotalTasksEvaluated, 2);
            AverageLatencyMs = Math.Round(_latencySum / TotalTasksEvaluated, 1);
        }

        public double CalculateCompositeScore()
        {
            // Composite score weighted formula:
            // 40% Task Success + 30% Tool Precision + 20% Approval Rate + 10% Inverse Hallucination Penalty
            var hallucinationPenalty = Math.Clamp(HallucinationIndex, 0.0, 1.0);
            var hallucinationFactor = (1.0 - hallucinationPenalty) * 100.0;

            var composite = (TaskSuccessRate * 0.40) +
                            (ToolPrecisionRate * 0.30) +
                            (ApprovalRate * 0.20) +
                            (hallucinationFactor * 0.10);

            return Math.Round(Math.Clamp(composite, 0.0, 100.0), 1);
        }

        public void UpdateRecommendation()
        {
            var composite = CalculateCompositeScore();
            if (AutonomyLevel == AutonomyReadinessLevel.Execute)
            {
                Recommendation = $"Full autonomous execution verified. Running at {composite:F1}% composite quality with {ApprovalRate:F1}% approval confidence.";
            }
            else if (AutonomyLevel == AutonomyReadinessLevel.Approve)
            {
                Recommendation = ApprovalRate > 95.0
                    ? $"Approaching autonomous promotion ({composite:F1}% composite, {ApprovalRate:F1}% approval). Final safety validation in progress."
                    : $"Supervised approval active. Current human approval is {ApprovalRate:F1}% (requires > 95.0% for autonomous execution).";
            }
            else if (AutonomyLevel == AutonomyReadinessLevel.Draft)
            {
                Recommendation = composite > 80.0
                    ? $"Draft quality verified ({composite:F1}% composite). Ready for promotion to supervised approval."
                    : $"Drafting workflows under evaluation ({composite:F1}% composite). Needs > 80.0% composite for approval staging.";
            }
            else
            {
                Recommendation = $"Read-only inspection mode. Evaluated tasks: {TotalTasksEvaluated}.";
            }
        }

        public AgentRadarScorecard ToScorecard()
        {
            return new AgentRadarScorecard(
                AgentRole: AgentRole,
                AutonomyLevel: AutonomyLevel,
                CompositeScore: CalculateCompositeScore(),
                TaskSuccessRate: TaskSuccessRate,
                ToolPrecisionRate: ToolPrecisionRate,
                ApprovalRate: ApprovalRate,
                HallucinationIndex: HallucinationIndex,
                CostPerTaskSar: CostPerTaskSar,
                AverageLatencyMs: AverageLatencyMs,
                Trend: Trend,
                Recommendation: Recommendation
            );
        }
    }
}
