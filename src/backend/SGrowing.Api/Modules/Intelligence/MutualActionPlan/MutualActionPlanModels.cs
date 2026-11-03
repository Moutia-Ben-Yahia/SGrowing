using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Intelligence.MutualActionPlan;

public enum BuyerMilestoneStatus
{
    Pending,
    InProgress,
    Blocked,
    CompletedVerified
}

public record PlanMilestone(
    string MilestoneId,
    string Title,
    string OwnerRole,
    DateTime TargetDueDate,
    BuyerMilestoneStatus Status,
    bool IsCriticalPath,
    string? VerificationArtifactUrl
);

public record MutualActionPlan(
    string PlanId,
    string DealId,
    string AccountName,
    decimal ContractValueUsd,
    int ChampionHealthScore,
    List<PlanMilestone> Milestones,
    DateTime TargetCloseDate,
    bool IsAtRiskOfSlipping,
    string SlippageRiskRationale
);

public record CreateMapRequest(
    string DealId,
    string AccountName,
    decimal ContractValueUsd,
    DateTime TargetCloseDate
);

public record UpdateMilestoneRequest(
    string PlanId,
    string MilestoneId,
    BuyerMilestoneStatus NewStatus,
    string? VerificationArtifactUrl
);
