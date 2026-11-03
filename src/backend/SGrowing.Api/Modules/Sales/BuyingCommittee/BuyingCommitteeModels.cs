namespace SGrowing.Api.Modules.Sales.BuyingCommittee;

public record StakeholderMember(
    string StakeholderId,
    string FullName,
    string RoleTitle,
    string BuyingRole, // ECONOMIC_BUYER, TECHNICAL_EVALUATOR, CHAMPION, LEGAL_GATEKEEPER, PROCUREMENT_BLOCKER
    double InfluenceWeight, // 0.1 to 1.0
    double SentimentScore, // -1.0 to +1.0
    string Department,
    List<string> DirectInfluencedContactIds,
    string CurrentStatus // ALIGNED, SKEPTICAL, BLOCKED, UNDECIDED
);

public record CommitteeInterventionRecord(
    string InterventionId,
    string TargetStakeholderId,
    string PlaybookType, // EXEC_SPONSOR_ALIGNMENT, SECURITY_DEEP_DIVE, ROI_CALCULATOR_SESSION, LEGAL_REDLINE_ACCELERATOR
    string AssignedExecutive,
    DateTime ScheduledDate,
    string ExpectedOutcome,
    string Status // PENDING, EXECUTED, CONVERTED
);

public record BuyingCommitteeOverview(
    string AccountId,
    string AccountName,
    double CoalitionConsensusScore, // 0 to 100
    int TotalStakeholdersCount,
    int AlignedChampionsCount,
    int ActiveBlockersCount,
    List<StakeholderMember> Stakeholders,
    List<CommitteeInterventionRecord> Interventions
);

public record AddStakeholderRequest(
    string AccountId,
    string FullName,
    string RoleTitle,
    string BuyingRole,
    double InfluenceWeight,
    double SentimentScore,
    string Department
);

public record TriggerInterventionRequest(
    string AccountId,
    string StakeholderId,
    string PlaybookType,
    string AssignedExecutive
);
