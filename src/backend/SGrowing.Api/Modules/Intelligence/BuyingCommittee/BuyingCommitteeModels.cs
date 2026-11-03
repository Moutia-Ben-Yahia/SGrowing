using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Intelligence.BuyingCommittee;

public enum PersonaRole
{
    Champion,
    EconomicBuyer,
    TechnicalEvaluator,
    LegalProcurement,
    SecurityCiso,
    EndUserInfluencer
}

public enum DealReadiness
{
    HighRiskIncomplete,
    ModerateProgress,
    ConsensusReadyToClose
}

public record CommitteeMember(
    string MemberId,
    string FullName,
    string Title,
    string Email,
    string LinkedInUrl,
    PersonaRole Role,
    double EngagementScore,
    bool HasApproved
);

public record PersonaGapAlert(
    PersonaRole MissingRole,
    string RiskLevel,
    string Recommendation
);

public record RecommendedPlaybook(
    PersonaRole TargetRole,
    string CollateralTitle,
    string SuggestedChannel,
    string TalkingPoints
);

public record BuyingCommitteeMap(
    string CompanyDomain,
    string CompanyName,
    List<CommitteeMember> Members,
    double ConsensusPercentage,
    DealReadiness Readiness,
    List<PersonaGapAlert> IdentifiedGaps,
    List<RecommendedPlaybook> Playbooks
);

public record MapCommitteeRequest(
    string CompanyDomain,
    string CompanyName,
    List<CommitteeMember> CandidateMembers
);
