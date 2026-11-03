namespace SGrowing.Api.Modules.Finance.SecondaryLiquidity;

public enum TenderOfferStatus
{
    Draft,
    ActiveOpenWindow,
    Oversubscribed,
    ClearancePending,
    SettledClosed
}

public sealed record TenderOfferWindow(
    string OfferId,
    string ProgramName,
    double MaxTotalTransactionVolumeUsd,
    double SharePriceUsd,
    DateTime WindowOpensUtc,
    DateTime WindowClosesUtc,
    TenderOfferStatus Status,
    double CurrentCommittedVolumeUsd
);

public sealed record SecondaryShareLot(
    string LotId,
    string OfferId,
    string ShareholderId,
    string ShareholderName,
    int SharesOfferedCount,
    double AggregateValueUsd,
    bool RofrClearedByCompany,
    double EstimatedTaxWithholdingUsd,
    string Status
);

public sealed record RofrClearanceToken(
    string TokenId,
    string LotId,
    string ShareholderId,
    bool CompanyExercisedRofr,
    string ApprovedPurchaserEntity,
    DateTime ClearedAtUtc,
    string CryptographicSignatureSha256
);

public sealed record DilutionImpactSimulation(
    string SimulationId,
    double PreTransactionFullyDilutedShares,
    double PostTransactionFullyDilutedShares,
    double FounderOwnershipPercentageBefore,
    double FounderOwnershipPercentageAfter,
    double EffectiveDilutionPercentage,
    double OptionPoolUnallocatedPercentage
);

public sealed record SecondaryLiquidityOverview(
    int ActiveTenderOffersCount,
    double TotalSecondaryVolumeSettledUsd,
    int TotalParticipatingHoldersCount,
    double AverageSharePriceUsd,
    IReadOnlyList<TenderOfferWindow> ActiveOffers,
    IReadOnlyList<SecondaryShareLot> RecentLots
);
