namespace SGrowing.Api.Modules.CustomerData.Firmographics;

public record FirmographicCompanyProfile(
    string Domain,
    string CompanyName,
    string Industry,
    int EmployeeCount,
    string RevenueRange,
    string HeadquartersCity,
    string HeadquartersCountry,
    List<string> TechStack
);

public record DeanonymizeIpRequest(
    string TenantId,
    string IpAddress,
    string? AnonymousSessionId = null,
    string? VisitedPage = null
);

public record IcpScoringRule(
    string TargetIndustry,
    int MinEmployees,
    int MaxEmployees,
    decimal Weight
);

public record IcpEvaluationResult(
    string FitTier,
    int CompositeScore,
    bool IsHighIntentTarget,
    string Rationale
);

public record DeanonymizeIpResult(
    bool MatchFound,
    string IpAddress,
    FirmographicCompanyProfile? Company,
    IcpEvaluationResult? IcpFit,
    bool TriggeredSalesAlert,
    string AlertDetails,
    DateTime ProcessedAtUtc
);
