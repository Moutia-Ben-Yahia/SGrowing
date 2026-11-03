namespace SGrowing.Api.Modules.Finance.CapitalAllocation;

public record TreasuryAssetAllocation(
    string AllocationId,
    string AssetClass, // SOVEREIGN_TBILLS_USD, OVERNIGHT_REPO_EUR, MONEY_MARKET_FUND, WORKING_CAPITAL_OPERATING
    string CurrencyIso,
    decimal AllocatedAmount,
    double AnnualYieldPercentage, // e.g. 5.15% on 3-month US T-bills
    double MaturityDays,
    string CounterpartyInstitution,
    string Status // ACTIVE_YIELDING, MATURING_TODAY, REDEEMED
);

public record WorkingCapitalOptimizationRecord(
    string OptimizationId,
    decimal TotalLiquidTreasuryDollars,
    decimal CurrentMonthlyBurnRateDollars,
    double RunwayMonths,
    decimal OptimizedArDiscountingCapital,
    decimal ProjectedInterestIncomeAnnualDollars,
    DateTime ComputedAt
);

public record CapitalAllocationOverview(
    decimal TotalPortfolioValueDollars,
    double WeightedAverageYieldPercentage,
    double NetCashRunwayMonths,
    int ActiveTreasuryPositionsCount,
    List<TreasuryAssetAllocation> ActiveAllocations,
    List<WorkingCapitalOptimizationRecord> RecentOptimizations
);

public record AllocateCapitalRequest(
    string AssetClass,
    string CurrencyIso,
    decimal AllocatedAmount,
    double AnnualYieldPercentage,
    double MaturityDays,
    string CounterpartyInstitution
);

public record OptimizeRunwayRequest(
    decimal MonthlyBurnRateDollars,
    decimal TargetSafetyBufferMonths
);
