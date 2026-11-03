using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Intelligence.MutualActionPlan;

public interface IMutualActionPlanEngine
{
    MutualActionPlan CreatePlan(CreateMapRequest request);
    MutualActionPlan? GetPlan(string planId);
    MutualActionPlan? GetPlanByDealId(string dealId);
    MutualActionPlan UpdateMilestone(UpdateMilestoneRequest request);
    MutualActionPlan RefreshRiskAnalysis(string planId);
}

public class MutualActionPlanEngine : IMutualActionPlanEngine
{
    private readonly ConcurrentDictionary<string, MutualActionPlan> _plans = new(StringComparer.OrdinalIgnoreCase);

    public MutualActionPlanEngine()
    {
        // Seed initial enterprise MAP
        var seedMilestones = new List<PlanMilestone>
        {
            new("ms_01", "InfoSec Architecture & SOC2 Type II Sign-off", "BuyerSecurityLead", DateTime.UtcNow.AddDays(7), BuyerMilestoneStatus.CompletedVerified, true, "https://trust.sgrowing.internal/audits/soc2.pdf"),
            new("ms_02", "Mutual NDA & Master Services Agreement (MSA) Legal Review", "BuyerLegalCounsel", DateTime.UtcNow.AddDays(14), BuyerMilestoneStatus.InProgress, true, null),
            new("ms_03", "Staging Environment Sandbox Pilot Validation", "BuyerLeadEngineer", DateTime.UtcNow.AddDays(21), BuyerMilestoneStatus.Pending, false, null),
            new("ms_04", "Executive Steering Committee Final Budget Approval", "BuyerCfoChampion", DateTime.UtcNow.AddDays(28), BuyerMilestoneStatus.Pending, true, null)
        };

        var seedPlan = new MutualActionPlan(
            PlanId: "map_enterprise_aurora_01",
            DealId: "deal_aurora_9901",
            AccountName: "Aurora Enterprise Cloud",
            ContractValueUsd: 185000m,
            ChampionHealthScore: 88,
            Milestones: seedMilestones,
            TargetCloseDate: DateTime.UtcNow.AddDays(30),
            IsAtRiskOfSlipping: false,
            SlippageRiskRationale: "All critical path milestones tracking on schedule."
        );

        _plans[seedPlan.PlanId] = seedPlan;
    }

    public MutualActionPlan CreatePlan(CreateMapRequest request)
    {
        var planId = $"map_{DateTime.UtcNow.Ticks % 1000000}";
        var milestones = new List<PlanMilestone>
        {
            new($"ms_{planId}_1", "InfoSec Compliance & Data Governance Review", "BuyerSecurityLead", request.TargetCloseDate.AddDays(-21), BuyerMilestoneStatus.Pending, true, null),
            new($"ms_{planId}_2", "Legal Terms & SLA Annex Execution", "BuyerLegalCounsel", request.TargetCloseDate.AddDays(-14), BuyerMilestoneStatus.Pending, true, null),
            new($"ms_{planId}_3", "Procurement PO Issuance & Signature", "BuyerProcurementLead", request.TargetCloseDate.AddDays(-3), BuyerMilestoneStatus.Pending, true, null)
        };

        var plan = new MutualActionPlan(
            PlanId: planId,
            DealId: request.DealId,
            AccountName: request.AccountName,
            ContractValueUsd: request.ContractValueUsd,
            ChampionHealthScore: 75,
            Milestones: milestones,
            TargetCloseDate: request.TargetCloseDate,
            IsAtRiskOfSlipping: false,
            SlippageRiskRationale: "Plan initialized with standard 3-stage enterprise procurement milestones."
        );

        _plans[planId] = plan;
        return plan;
    }

    public MutualActionPlan? GetPlan(string planId)
    {
        _plans.TryGetValue(planId, out var plan);
        return plan;
    }

    public MutualActionPlan? GetPlanByDealId(string dealId)
    {
        return _plans.Values.FirstOrDefault(p => p.DealId.Equals(dealId, StringComparison.OrdinalIgnoreCase));
    }

    public MutualActionPlan UpdateMilestone(UpdateMilestoneRequest request)
    {
        if (!_plans.TryGetValue(request.PlanId, out var plan))
        {
            throw new KeyNotFoundException($"Mutual Action Plan {request.PlanId} not found.");
        }

        var updatedMilestones = plan.Milestones.Select(m =>
        {
            if (m.MilestoneId.Equals(request.MilestoneId, StringComparison.OrdinalIgnoreCase))
            {
                return m with
                {
                    Status = request.NewStatus,
                    VerificationArtifactUrl = request.VerificationArtifactUrl ?? m.VerificationArtifactUrl
                };
            }
            return m;
        }).ToList();

        var updatedPlan = plan with { Milestones = updatedMilestones };
        _plans[request.PlanId] = updatedPlan;

        return RefreshRiskAnalysis(request.PlanId);
    }

    public MutualActionPlan RefreshRiskAnalysis(string planId)
    {
        if (!_plans.TryGetValue(planId, out var plan))
        {
            throw new KeyNotFoundException($"Mutual Action Plan {planId} not found.");
        }

        var hasBlockedCritical = plan.Milestones.Any(m => m.IsCriticalPath && m.Status == BuyerMilestoneStatus.Blocked);
        var incompleteCount = plan.Milestones.Count(m => m.Status != BuyerMilestoneStatus.CompletedVerified);
        var daysLeft = (plan.TargetCloseDate - DateTime.UtcNow).TotalDays;

        bool atRisk = hasBlockedCritical || (daysLeft < 7 && incompleteCount > 1);
        string rationale = atRisk
            ? (hasBlockedCritical ? "Critical path milestone is blocked by buyer stakeholders." : "Multiple milestones incomplete with fewer than 7 days to scheduled target close.")
            : "Milestones progressing in accordance with procurement schedule.";

        int healthScore = plan.ChampionHealthScore;
        if (hasBlockedCritical) healthScore = Math.Max(20, healthScore - 25);
        if (plan.Milestones.All(m => m.Status == BuyerMilestoneStatus.CompletedVerified)) healthScore = 100;

        var analyzed = plan with
        {
            IsAtRiskOfSlipping = atRisk,
            SlippageRiskRationale = rationale,
            ChampionHealthScore = healthScore
        };

        _plans[planId] = analyzed;
        return analyzed;
    }
}
