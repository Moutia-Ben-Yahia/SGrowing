using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Intelligence.WaterfallEnrichment;

public enum EnrichmentProvider
{
    Clearbit,
    Apollo,
    ZoomInfo,
    PeopleDataLabs,
    InternalCache
}

public record WaterfallEnrichRequest(
    string EmailOrDomain,
    string? FullName = null,
    string? CompanyName = null
);

public record ProviderEnrichmentAttempt(
    EnrichmentProvider Provider,
    bool Found,
    double MatchConfidence,
    double LatencyMs
);

public record EnrichedCompanyProfile(
    string Domain,
    string LegalName,
    string Industry,
    string EmployeeRange,
    int? EstimatedEmployees,
    string? HeadquartersCity,
    string? HeadquartersCountry,
    string? EstimatedAnnualRevenue,
    List<string> TechnologiesUsed,
    string? PrimaryNaicsCode
);

public record EnrichedContactProfile(
    string Email,
    string? FullName,
    string? JobTitle,
    string? SeniorityLevel, // C-Suite, VP, Director, Manager, IndividualContributor
    string? LinkedInUrl,
    string DeliverabilityStatus // Valid, Risky, Invalid, Unknown
);

public record WaterfallEnrichResult(
    string QueryKey,
    bool Success,
    EnrichmentProvider WinningProvider,
    double OverallConfidence,
    EnrichedCompanyProfile? Company,
    EnrichedContactProfile? Contact,
    List<ProviderEnrichmentAttempt> WaterfallSteps,
    DateTime EnrichedAtUtc
);

public record ProviderMetric(
    EnrichmentProvider Provider,
    string Name,
    bool IsActive,
    double HistoricalMatchRate,
    double AverageLatencyMs,
    int TotalQueriesToday
);

public record VerifyDeliverabilityRequest(
    string Email
);

public record VerifyDeliverabilityResult(
    string Email,
    string Status, // Valid, DeliverableWithMx, CatchAll, SpamTrap, Bounced
    bool CanSendWithoutBounceRisk,
    DateTime VerifiedAtUtc
);
