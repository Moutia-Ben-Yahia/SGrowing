using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.TaxNexus;

public enum NexusState
{
    SafeBelowThreshold,
    ApproachingNexusLimit,
    NexusTriggeredRegistrationRequired,
    RegisteredAndCollecting
}

public record TaxJurisdictionNexus(
    string JurisdictionCode,
    string JurisdictionName,
    decimal StatutoryRevenueLimitUsd,
    int StatutoryTransactionLimit,
    decimal CurrentTrailingRevenueUsd,
    int CurrentTrailingTransactions,
    double ProximityPercentage,
    NexusState State,
    decimal EstimatedTaxLiabilityUsd
);

public record UpdateNexusRegistrationRequest(
    string JurisdictionCode,
    NexusState NewState
);

public record NexusRegistrationResult(
    string JurisdictionCode,
    NexusState State,
    string StatusMessage
);
