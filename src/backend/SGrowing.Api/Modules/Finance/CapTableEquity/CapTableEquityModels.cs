using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.CapTableEquity;

public sealed record EquityStakeholderRecord(
    string StakeholderId,
    string LegalName,
    string StakeholderType, // FOUNDER, INVESTOR_PREFERRED, EMPLOYEE_COMMON, ADVISOR
    long SharesCount,
    double OwnershipPercentagePct,
    decimal InvestedCapitalUsd,
    string ShareClass // COMMON, SERIES_SEED, SERIES_A, SERIES_B
);

public sealed record StockOptionGrantRecord(
    string GrantId,
    string EmployeeName,
    long GrantedSharesCount,
    decimal StrikePriceUsd,
    DateTime GrantDate,
    DateTime VestingStartDate,
    int VestingCliffMonths,
    int TotalVestingMonths,
    long VestedSharesCount,
    string Status // VESTING_ACTIVE, FULLY_VESTED, EXERCISED
);

public sealed record CapTableOverview(
    long FullyDilutedSharesTotal,
    decimal Latest409AValuationUsd,
    decimal ShareFairMarketValueUsd,
    int ActiveStakeholdersCount,
    IReadOnlyList<EquityStakeholderRecord> Stakeholders,
    IReadOnlyList<StockOptionGrantRecord> OptionGrants
);

public sealed record GrantStockOptionRequest(
    string EmployeeName,
    long SharesCount,
    decimal StrikePriceUsd,
    int VestingCliffMonths,
    int TotalVestingMonths
);

public sealed record SimulateDilutionRequest(
    decimal NewRoundInvestmentAmountUsd,
    decimal PreMoneyValuationUsd,
    string NewRoundShareClass
);
