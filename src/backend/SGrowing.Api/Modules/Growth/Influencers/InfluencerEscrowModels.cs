namespace SGrowing.Api.Modules.Growth.Influencers;

public record CreatorProfile(
    string CreatorId,
    string Handle,
    string Platform,
    int FollowersCount,
    double EngagementRatePercent,
    double AuthenticityScore,
    string NicheCategory,
    bool IsVerified
);

public record EscrowMilestone(
    string MilestoneId,
    string Title,
    decimal PayoutAmount,
    string DeliverableRequirement,
    int TargetViews,
    int ActualViews,
    bool IsVerified,
    bool IsReleased
);

public record EscrowContract(
    string ContractId,
    string CreatorId,
    string CampaignName,
    decimal TotalEscrowAmount,
    decimal ReleasedAmount,
    decimal RemainingAmount,
    IReadOnlyList<EscrowMilestone> Milestones,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc = null
);

public record CreateEscrowContractRequest(
    string CreatorId,
    string CampaignName,
    decimal TotalEscrowAmount,
    IReadOnlyList<EscrowMilestone> Milestones
);

public record ReleaseMilestoneRequest(
    string ContractId,
    string MilestoneId,
    int ActualViewsAchieved,
    string VerificationEvidenceUrl
);

public record CreatorCampaignSummary(
    int TotalActiveContracts,
    decimal TotalEscrowSecured,
    decimal TotalReleasedPayouts,
    decimal AttributedGmv,
    double InfluencerRoas
);
