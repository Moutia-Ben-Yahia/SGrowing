using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.TreasuryYield;

public record CashForecastPeriod(
    string PeriodLabel,
    int DaysOut,
    decimal ProjectedInflows,
    decimal ProjectedOutflows,
    decimal NetCashChange,
    decimal ProjectedEndingBalance,
    double ConfidenceIntervalPct
);

public record TreasuryYieldAllocation(
    string InstrumentId,
    string InstrumentName,
    decimal CurrentAllocatedAmount,
    decimal RecommendedAllocationAmount,
    decimal TargetApyPercentage,
    decimal ProjectedAnnualYield,
    int LiquidityDaysTPlus
);

public record TreasuryYieldOverview(
    decimal CurrentCashReserve,
    decimal RequiredOperatingBuffer,
    decimal SurplusDeployableCapital,
    double BlendedPortfolioApy,
    decimal ProjectedTotalAnnualYield,
    double RunwayMonths,
    List<CashForecastPeriod> Projections,
    List<TreasuryYieldAllocation> Allocations
);

public record SweepTreasurySurplusCommand(
    string InstrumentId,
    decimal SweepAmount,
    string AuthorizationScope
);

public record SweepExecutionResult(
    string InstrumentId,
    decimal PreviousAllocation,
    decimal NewAllocation,
    decimal RemainingSurplus,
    decimal AdditionalYieldEarned,
    string ConfirmationReference
);
