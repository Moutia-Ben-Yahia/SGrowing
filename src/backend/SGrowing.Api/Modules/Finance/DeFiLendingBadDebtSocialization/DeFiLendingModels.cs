using System;

namespace SGrowing.Api.Modules.Finance.DeFiLendingBadDebtSocialization;

public sealed record LendingPoolRiskTelemetry(
    string PoolId,
    string UnderlyingAssetSymbol,
    double TotalSuppliedLiquidityUsd,
    double TotalBorrowedDebtUsd,
    double UnderwaterBadDebtUsd,
    double ProtocolInsuranceReserveUsd,
    double StakedGovernanceTokenMarketCapUsd,
    double MaxGovernanceSlashFraction,
    double MinimumAccountHealthFactor,
    double DexLiquiditySlippagePenaltyPercent);

public sealed record LendingPoolResolutionResult(
    string PoolId,
    double UnderwaterBadDebtUsd,
    double TotalBackstopCapacityUsd,
    double BadDebtCoverageRatio,
    double InsuranceFundDrawnUsd,
    double GovernanceStakeSlashedUsd,
    double SocializedHaircutFractionPercent,
    string SolvencyResolutionState,
    string ProtocolResolutionAction,
    bool IsMarketSolvent,
    DateTimeOffset Timestamp);
