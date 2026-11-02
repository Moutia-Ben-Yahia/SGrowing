using System;
using SGrowing.Api.Modules.Finance.DeFiLendingBadDebtSocialization;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class DeFiLendingBadDebtSocializationTests
{
    private readonly DeFiLendingBadDebtSocializationEngine _engine = new();

    [Fact]
    public void AnalyzeBadDebtResolution_ZeroBadDebt_ReturnsHealthySolvency()
    {
        var telemetry = new LendingPoolRiskTelemetry(
            PoolId: "AAVE-V4-USDC-POOL-01",
            UnderlyingAssetSymbol: "USDC",
            TotalSuppliedLiquidityUsd: 100_000_000.0,
            TotalBorrowedDebtUsd: 75_000_000.0,
            UnderwaterBadDebtUsd: 0.0,
            ProtocolInsuranceReserveUsd: 5_000_000.0,
            StakedGovernanceTokenMarketCapUsd: 25_000_000.0,
            MaxGovernanceSlashFraction: 0.30,
            MinimumAccountHealthFactor: 1.08,
            DexLiquiditySlippagePenaltyPercent: 0.5);

        var result = _engine.AnalyzeBadDebtResolution(telemetry);

        Assert.Equal("COLLATERAL_SOLVENCY_HEALTHY", result.SolvencyResolutionState);
        Assert.Equal("NORMAL_MARKET_OPERATIONS_AND_COLLECT_RESERVE_FEES", result.ProtocolResolutionAction);
        Assert.True(result.IsMarketSolvent);
        Assert.Equal(0.0, result.InsuranceFundDrawnUsd);
        Assert.Equal(0.0, result.SocializedHaircutFractionPercent);
    }

    [Fact]
    public void AnalyzeBadDebtResolution_DeficitCoveredByInsurance_DrawsInsuranceOnly()
    {
        var telemetry = new LendingPoolRiskTelemetry(
            PoolId: "AAVE-V4-ETH-POOL-02",
            UnderlyingAssetSymbol: "WETH",
            TotalSuppliedLiquidityUsd: 80_000_000.0,
            TotalBorrowedDebtUsd: 60_000_000.0,
            UnderwaterBadDebtUsd: 450_000.0,
            ProtocolInsuranceReserveUsd: 2_000_000.0,
            StakedGovernanceTokenMarketCapUsd: 15_000_000.0,
            MaxGovernanceSlashFraction: 0.30,
            MinimumAccountHealthFactor: 0.94,
            DexLiquiditySlippagePenaltyPercent: 1.2);

        var result = _engine.AnalyzeBadDebtResolution(telemetry);

        Assert.Equal("INSURANCE_BACKSTOP_ABSORPTION", result.SolvencyResolutionState);
        Assert.Equal("DRAW_DOWN_INSURANCE_RESERVE_AND_RESTORE_PARITY", result.ProtocolResolutionAction);
        Assert.True(result.IsMarketSolvent);
        Assert.Equal(450_000.0, result.InsuranceFundDrawnUsd);
        Assert.Equal(0.0, result.GovernanceStakeSlashedUsd);
        Assert.Equal(0.0, result.SocializedHaircutFractionPercent);
    }

    [Fact]
    public void AnalyzeBadDebtResolution_DeficitExceedsInsuranceCoveredByGovernance_SlashesGovernance()
    {
        // Bad debt 2.2M > 1.0M insurance, but < (1.0M + 0.30 * 10M = 4.0M)
        var telemetry = new LendingPoolRiskTelemetry(
            PoolId: "AAVE-V4-WBTC-POOL-03",
            UnderlyingAssetSymbol: "WBTC",
            TotalSuppliedLiquidityUsd: 60_000_000.0,
            TotalBorrowedDebtUsd: 45_000_000.0,
            UnderwaterBadDebtUsd: 2_200_000.0,
            ProtocolInsuranceReserveUsd: 1_000_000.0,
            StakedGovernanceTokenMarketCapUsd: 10_000_000.0,
            MaxGovernanceSlashFraction: 0.30,
            MinimumAccountHealthFactor: 0.82,
            DexLiquiditySlippagePenaltyPercent: 3.5);

        var result = _engine.AnalyzeBadDebtResolution(telemetry);

        Assert.Equal("GOVERNANCE_STAKE_SLASH_ABSORPTION", result.SolvencyResolutionState);
        Assert.Equal("SLASH_STAKED_GOVERNANCE_TOKENS_TO_COVER_DEFICIT", result.ProtocolResolutionAction);
        Assert.True(result.IsMarketSolvent);
        Assert.Equal(1_000_000.0, result.InsuranceFundDrawnUsd);
        Assert.Equal(1_200_000.0, result.GovernanceStakeSlashedUsd);
        Assert.Equal(0.0, result.SocializedHaircutFractionPercent);
    }

    [Fact]
    public void AnalyzeBadDebtResolution_DeficitExceedsTotalCapacity_TriggersSocializationHaircut()
    {
        // Bad debt 5.5M > Total backstop 3.5M (1.0M ins + 2.5M gov) -> 2.0M deficit
        // Haircut = 2.0M / 50.0M = 4.0%
        var telemetry = new LendingPoolRiskTelemetry(
            PoolId: "AAVE-V4-CRV-POOL-04",
            UnderlyingAssetSymbol: "CRV",
            TotalSuppliedLiquidityUsd: 50_000_000.0,
            TotalBorrowedDebtUsd: 40_000_000.0,
            UnderwaterBadDebtUsd: 5_500_000.0,
            ProtocolInsuranceReserveUsd: 1_000_000.0,
            StakedGovernanceTokenMarketCapUsd: 8_333_333.33,
            MaxGovernanceSlashFraction: 0.30,
            MinimumAccountHealthFactor: 0.65,
            DexLiquiditySlippagePenaltyPercent: 8.5);

        var result = _engine.AnalyzeBadDebtResolution(telemetry);

        Assert.Equal("SYSTEMIC_INSOLVENCY_HAIRCUT_TRIGGERED", result.SolvencyResolutionState);
        Assert.Equal("EXECUTE_TIER_3_DEPOSITOR_YIELD_HAIRCUT_AND_PAUSE_BORROWING", result.ProtocolResolutionAction);
        Assert.False(result.IsMarketSolvent);
        Assert.True(result.SocializedHaircutFractionPercent >= 3.8);
    }
}
