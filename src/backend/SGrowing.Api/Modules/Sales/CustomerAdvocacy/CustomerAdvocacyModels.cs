namespace SGrowing.Api.Modules.Sales.CustomerAdvocacy;

public record CustomerAdvocateProfile(
    string AdvocateId,
    string ExecutiveName,
    string CompanyName,
    string IndustryVertical,
    int CompanyEmployeeCount,
    int ReferenceCallsThisQuarter,
    int MaxCallsQuarterlyQuota, // default 2
    double AdvocateSatisfactionScore,
    int RewardPointsBalance,
    string Status // AVAILABLE, QUOTA_REACHED, VACATION_HOLD
);

public record ReferenceMatchRecord(
    string MatchId,
    string ProspectCompanyName,
    string ProspectDealSizeCategory, // MID_MARKET, ENTERPRISE, STRATEGIC_GLOBAL
    string TargetIndustryVertical,
    string MatchedAdvocateId,
    string MatchedAdvocateCompany,
    double MatchCompatibilityScore, // 0.0 to 100.0
    string ReferenceCallStatus, // SCHEDULED, COMPLETED, NO_SHOW, CANCELLED
    DateTime MatchedAt
);

public record CustomerAdvocacyOverview(
    int TotalActiveAdvocatesCount,
    int ReferenceCallsCompletedQuarter,
    double WinRateLiftPct, // e.g. +34.5% win rate when reference call occurs
    int TotalRewardPointsDistributed,
    List<CustomerAdvocateProfile> TopAdvocates,
    List<ReferenceMatchRecord> RecentMatches
);

public record MatchReferenceRequest(
    string ProspectCompanyName,
    string ProspectDealSizeCategory,
    string TargetIndustryVertical,
    int ProspectEmployeeCount
);

public record RecordReferenceCallRequest(
    string MatchId,
    string CallOutcome,
    int CallDurationMinutes,
    int FeedbackRating // 1 to 5
);
