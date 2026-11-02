using System;
using SGrowing.Api.Modules.Finance.RestakingCryptoeconomicSlashing;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class RestakingCryptoeconomicSlashingTests
{
    private readonly RestakingCryptoeconomicSlashingEngine _engine = new();

    [Fact]
    public void AnalyzeRestakingSecurity_HealthyConsensus_DistributesRewards()
    {
        var telemetry = new RestakingPoolTelemetry(
            AvsClusterId: "EIGEN-LAYER-AVS-ORACLE-01",
            TotalRestakedEthAmount: 50_000.0,
            ProtectedAvsTvlUsd: 100_000_000.0,
            EthPriceUsd: 3200.0,
            FaultyOperatorStakeEth: 0.0,
            IsEquivocationDoubleSignProven: false,
            BaseSlashingFraction: 0.05,
            GrossAvsYieldPercent: 6.5,
            OperatorFeeCommissionPercent: 10.0,
            ProtocolReserveFeePercent: 5.0);

        var result = _engine.AnalyzeRestakingSecurity(telemetry);

        Assert.Equal("RESTAKING_CONSENSUS_HEALTHY", result.ConsensusSecurityState);
        Assert.Equal("DISPATCH_PERIODIC_CONSENSUS_REWARD_DISTRIBUTIONS", result.SlashingAndRewardAction);
        Assert.True(result.IsAvsSecurityAdequate);
        Assert.True(result.SecurityCoverageRatio >= 1.50);
        Assert.Equal(0.0, result.TotalSlashedEthAmount);
        Assert.True(result.NetStakerYieldPercent >= 5.0);
    }

    [Fact]
    public void AnalyzeRestakingSecurity_CorrelatedEquivocationFault_ExecutesQuadraticSlashing()
    {
        // Faulty = 4,000 ETH (8% of 50,000 ETH >= 5%)
        // SlashRate = 0.05 + 3.0 * (0.08)^2 = 0.05 + 0.0192 = 0.0692 (6.92%)
        var telemetry = new RestakingPoolTelemetry(
            AvsClusterId: "EIGEN-LAYER-AVS-BRIDGE-02",
            TotalRestakedEthAmount: 50_000.0,
            ProtectedAvsTvlUsd: 100_000_000.0,
            EthPriceUsd: 3200.0,
            FaultyOperatorStakeEth: 4_000.0,
            IsEquivocationDoubleSignProven: true,
            BaseSlashingFraction: 0.05,
            GrossAvsYieldPercent: 6.0,
            OperatorFeeCommissionPercent: 10.0,
            ProtocolReserveFeePercent: 5.0);

        var result = _engine.AnalyzeRestakingSecurity(telemetry);

        Assert.Equal("EQUIVOCATION_CORRELATED_SLASH_CRITICAL", result.ConsensusSecurityState);
        Assert.Equal("EXECUTE_CORRELATED_QUADRATIC_SLASH_AND_EJECT_OPERATORS", result.SlashingAndRewardAction);
        Assert.False(result.IsAvsSecurityAdequate);
        Assert.True(result.TotalSlashedEthAmount > 250.0);
        Assert.True(result.CorrelatedSlashingPenaltyRatePercent > 5.0);
    }

    [Fact]
    public void AnalyzeRestakingSecurity_SecurityBudgetDeficit_IssuesSecurityDeficitWarning()
    {
        // 10,000 ETH * 3000 USD = 30M USD vs 50M USD TVL -> Coverage = 0.60 < 1.25
        var telemetry = new RestakingPoolTelemetry(
            AvsClusterId: "EIGEN-LAYER-AVS-COPROCESSOR-03",
            TotalRestakedEthAmount: 10_000.0,
            ProtectedAvsTvlUsd: 50_000_000.0,
            EthPriceUsd: 3000.0,
            FaultyOperatorStakeEth: 0.0,
            IsEquivocationDoubleSignProven: false,
            BaseSlashingFraction: 0.05,
            GrossAvsYieldPercent: 5.5,
            OperatorFeeCommissionPercent: 10.0,
            ProtocolReserveFeePercent: 5.0);

        var result = _engine.AnalyzeRestakingSecurity(telemetry);

        Assert.Equal("AVS_SECURITY_DEFICIT_WARNING", result.ConsensusSecurityState);
        Assert.Equal("BOOST_RESTAKING_YIELD_INCENTIVE_AND_ALERT_AVS_CONSENSUS", result.SlashingAndRewardAction);
        Assert.False(result.IsAvsSecurityAdequate);
        Assert.True(result.SecurityCoverageRatio < 1.25);
    }

    [Fact]
    public void AnalyzeRestakingSecurity_IsolatedMinorFault_MaintainsConsensus()
    {
        // Faulty = 500 ETH (1.0% < 5.0% fault ratio)
        var telemetry = new RestakingPoolTelemetry(
            AvsClusterId: "EIGEN-LAYER-AVS-SEQUENCER-04",
            TotalRestakedEthAmount: 50_000.0,
            ProtectedAvsTvlUsd: 100_000_000.0,
            EthPriceUsd: 3200.0,
            FaultyOperatorStakeEth: 500.0,
            IsEquivocationDoubleSignProven: true,
            BaseSlashingFraction: 0.05,
            GrossAvsYieldPercent: 6.5,
            OperatorFeeCommissionPercent: 10.0,
            ProtocolReserveFeePercent: 5.0);

        var result = _engine.AnalyzeRestakingSecurity(telemetry);

        Assert.Equal("RESTAKING_CONSENSUS_HEALTHY", result.ConsensusSecurityState);
        Assert.True(result.IsAvsSecurityAdequate);
        Assert.True(result.TotalSlashedEthAmount > 20.0);
    }
}
