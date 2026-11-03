namespace SGrowing.Api.Modules.Sales.ContractRedline;

public record ClauseRedlineRecord(
    string ClauseId,
    string ContractReference,
    string ClauseCategory, // LIMITATION_OF_LIABILITY, INDEMNIFICATION, IP_OWNERSHIP, GOVERNING_LAW, SLA_PENALTIES
    string ProposedRedlineText,
    string PlaybookStandardText,
    string RecommendedCompromiseText,
    double DeviationRiskScore, // 0 to 100
    string RiskClassification, // STANDARD_ACCEPT, ACCEPTABLE_COMPROMISE, UNACCEPTABLE_ESCALATE
    string Status // PENDING_REVIEW, COMPROMISE_OFFERED, APPROVED, REJECTED
);

public record ContractDeviationApproval(
    string ApprovalId,
    string ClauseId,
    string ApprovedByLegalCounsel,
    string BusinessJustification,
    DateTime ApprovedAt,
    string CryptographicAuditSeal
);

public record ContractRedlineOverview(
    string AccountName,
    int TotalClausesReviewedCount,
    int PendingRedlinesCount,
    int AcceptableCompromisesCount,
    double AverageDeviationRiskScore,
    List<ClauseRedlineRecord> ActiveRedlines,
    List<ContractDeviationApproval> RecentApprovals
);

public record NegotiateClauseRequest(
    string ContractReference,
    string ClauseCategory,
    string ProposedRedlineText
);

public record ApproveDeviationRequest(
    string ClauseId,
    string ApprovedByLegalCounsel,
    string BusinessJustification
);
