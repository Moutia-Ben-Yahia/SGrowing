namespace SGrowing.Api.Modules.AI.SyntheticData;

public enum NoiseMechanism
{
    Laplace,
    Gaussian
}

public sealed record PrivacyBudget(
    string TenantId,
    double TotalEpsilon,
    double ConsumedEpsilon,
    double Delta,
    DateTime ResetDateUtc
);

public sealed record PerturbQueryRequest(
    string TenantId,
    string QueryIdentifier,
    double RawValue,
    double SensitivityDeltaF,
    double RequestedEpsilon,
    NoiseMechanism Mechanism = NoiseMechanism.Laplace,
    double RequestedDelta = 1e-5
);

public sealed record PerturbedQueryResult(
    string QueryIdentifier,
    double PerturbedValue,
    double NoiseAdded,
    double EpsilonCost,
    double RemainingEpsilon,
    NoiseMechanism MechanismUsed,
    DateTime ComputedAtUtc
);

public sealed record SyntheticLeadProfile(
    string SyntheticId,
    string FullName,
    string SyntheticCompany,
    string Industry,
    string Region,
    decimal EstimatedBudgetEur,
    double EngagementScore,
    string LeadStatus
);

public sealed record GenerateSyntheticDatasetRequest(
    string TenantId,
    int RecordCount,
    double EpsilonBudget = 0.5,
    string IndustryFocus = "Omnichannel Commerce"
);
