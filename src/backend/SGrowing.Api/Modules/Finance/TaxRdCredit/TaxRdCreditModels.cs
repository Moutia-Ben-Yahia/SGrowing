namespace SGrowing.Api.Modules.Finance.TaxRdCredit;

public record QualifyingRdProject(
    string ProjectId,
    string ProjectName,
    string TechnicalHypothesis,
    bool MeetsPermissiblePurpose,
    bool MeetsTechnologicalInNature,
    bool MeetsEliminationOfUncertainty,
    bool MeetsProcessOfExperimentation,
    decimal QualifiedWagesUsd,
    decimal QualifiedCloudComputeUsd,
    decimal TotalQreUsd,
    string FourPartTestStatus // FULLY_QUALIFIED, PARTIALLY_QUALIFIED, DISQUALIFIED
);

public record Form6765TaxCreditClaim(
    string ClaimId,
    string TaxYear,
    decimal TotalQreUsd,
    decimal BaseAmountUsd,
    decimal AscCreditCalculatedUsd, // 14% of excess QRE
    decimal PayrollTaxOffsetEligibleUsd, // Up to $500K Section 41(h)
    DateTime GeneratedAt,
    string AuditBinderHash
);

public record TaxRdCreditOverview(
    string FiscalYear,
    int TotalQualifyingProjectsCount,
    decimal TotalCompanyQreUsd,
    decimal TotalEstimatedTaxCreditUsd,
    decimal PayrollTaxOffsetClaimableUsd,
    List<QualifyingRdProject> Projects,
    List<Form6765TaxCreditClaim> RecentClaims
);

public record QualifyProjectRequest(
    string ProjectName,
    string TechnicalHypothesis,
    decimal QualifiedWagesUsd,
    decimal QualifiedCloudComputeUsd,
    bool MeetsUncertaintyAndExperimentation
);

public record ComputeCreditRequest(
    string TaxYear,
    decimal BaseAmountUsd
);
