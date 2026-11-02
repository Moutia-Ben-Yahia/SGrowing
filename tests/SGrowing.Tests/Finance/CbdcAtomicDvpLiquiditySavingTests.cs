using FluentAssertions;
using SGrowing.Api.Modules.Finance.CbdcAtomicDvpLiquiditySaving;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class CbdcAtomicDvpLiquiditySavingTests
{
    private readonly CbdcAtomicDvpLiquiditySavingEngine _engine = new();

    [Fact]
    public void AnalyzeSettlementQueue_NominalLsmCycleNetting_ReturnsMultilateralNettingOptimal()
    {
        var telemetry = new CbdcSettlementTelemetry(
            SettlementBatchId: "BATCH-BIS-EURAUD-001",
            QueuedTransferCount: 2400,
            GrossLiquidityRequirementMillions: 950.0,
            AvailableCentralBankReservesMillions: 300.0,
            IdentifiedMultilateralCyclesCount: 58,
            NetSettledLiquidityMillions: 195.0, // 79.5% savings
            GridlockResolutionTimeMilliseconds: 45.0,
            AtomicHtlcCrossCurrencyPvpEnabled: true
        );

        var result = _engine.AnalyzeSettlementQueue(telemetry);

        result.SettlementRegime.Should().Be("MULTILATERAL_NETTING_OPTIMAL");
        result.IsBatchClearingFeasible.Should().BeTrue();
        result.LiquiditySavingsRatio.Should().BeGreaterThan(0.70);
        result.ReserveCoverageRatio.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void AnalyzeSettlementQueue_InsufficientReserves_ReturnsGridlockShortage()
    {
        var telemetry = new CbdcSettlementTelemetry(
            SettlementBatchId: "BATCH-BIS-DEFICIT-002",
            QueuedTransferCount: 850,
            GrossLiquidityRequirementMillions: 600.0,
            AvailableCentralBankReservesMillions: 120.0, // Deficit vs net
            IdentifiedMultilateralCyclesCount: 15,
            NetSettledLiquidityMillions: 280.0, // reserves / net = 120 / 280 = 0.428 < 1.0
            GridlockResolutionTimeMilliseconds: 25.0,
            AtomicHtlcCrossCurrencyPvpEnabled: true
        );

        var result = _engine.AnalyzeSettlementQueue(telemetry);

        result.SettlementRegime.Should().Be("GRIDLOCK_COLLAPSE_CAPITAL_SHORTAGE");
        result.IsBatchClearingFeasible.Should().BeFalse();
        result.ReserveCoverageRatio.Should().BeLessThan(1.0);
        result.LiquidityAction.Should().Contain("standing liquidity facility");
    }

    [Fact]
    public void AnalyzeSettlementQueue_NoHtlcAndNoCycles_ReturnsDesynchronizationRisk()
    {
        var telemetry = new CbdcSettlementTelemetry(
            SettlementBatchId: "BATCH-BIS-UNLOCKED-003",
            QueuedTransferCount: 120,
            GrossLiquidityRequirementMillions: 100.0,
            AvailableCentralBankReservesMillions: 150.0,
            IdentifiedMultilateralCyclesCount: 0,
            NetSettledLiquidityMillions: 90.0,
            GridlockResolutionTimeMilliseconds: 10.0,
            AtomicHtlcCrossCurrencyPvpEnabled: false // Disabled
        );

        var result = _engine.AnalyzeSettlementQueue(telemetry);

        result.SettlementRegime.Should().Be("ATOMIC_PVP_DESYNCHRONIZATION_RISK");
        result.IsBatchClearingFeasible.Should().BeFalse();
        result.LiquidityAction.Should().Contain("Herstatt settlement risk");
    }

    [Fact]
    public void AnalyzeSettlementQueue_LowSavingsRatio_ReturnsBilateralOffsetConservative()
    {
        var telemetry = new CbdcSettlementTelemetry(
            SettlementBatchId: "BATCH-BIS-LOWSAVING-004",
            QueuedTransferCount: 450,
            GrossLiquidityRequirementMillions: 500.0,
            AvailableCentralBankReservesMillions: 480.0,
            IdentifiedMultilateralCyclesCount: 4,
            NetSettledLiquidityMillions: 420.0, // Savings = (500-420)/500 = 16% < 35%
            GridlockResolutionTimeMilliseconds: 18.0,
            AtomicHtlcCrossCurrencyPvpEnabled: true
        );

        var result = _engine.AnalyzeSettlementQueue(telemetry);

        result.SettlementRegime.Should().Be("BILATERAL_OFFSET_CONSERVATIVE");
        result.IsBatchClearingFeasible.Should().BeTrue();
        result.LiquiditySavingsRatio.Should().BeLessThan(0.35);
    }
}
