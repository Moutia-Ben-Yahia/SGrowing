namespace SGrowing.Api.Modules.Sales.CustomerAdvisoryBoard;

public record ExecutiveSponsorPairing(
    string PairingId,
    string AccountName,
    string CustomerExecutiveName,
    string CustomerTitle,
    string VendorSponsorName,
    string VendorTitle,
    int DaysSinceLastTouchpoint,
    double EngagementHealthScore, // 0 to 100
    string Status // STRONGLY_ALIGNED, MODERATE, AT_RISK_STALE
);

public record CabRoadmapVote(
    string VoteId,
    string FeatureName,
    string AccountName,
    int PriorityRank, // 1 to 5
    decimal AnnualContractValueWeightUsd,
    DateTime VotedAt
);

public record CabMatrixOverview(
    int TotalActiveCabMembersCount,
    double AverageEngagementHealthScore,
    int AtRiskPairingsCount,
    decimal TotalInfluencedArrUsd,
    List<ExecutiveSponsorPairing> SponsorPairings,
    List<CabRoadmapVote> RecentRoadmapVotes
);

public record AlignSponsorRequest(
    string AccountName,
    string CustomerExecutiveName,
    string CustomerTitle,
    string VendorSponsorName,
    string VendorTitle
);

public record VoteRoadmapRequest(
    string FeatureName,
    string AccountName,
    int PriorityRank,
    decimal AnnualContractValueWeightUsd
);
