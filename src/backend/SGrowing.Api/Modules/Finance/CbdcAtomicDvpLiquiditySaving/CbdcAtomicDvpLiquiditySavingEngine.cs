using System;

namespace SGrowing.Api.Modules.Finance.CbdcAtomicDvpLiquiditySaving;

public sealed class CbdcAtomicDvpLiquiditySavingEngine : ICbdcAtomicDvpLiquiditySavingEngine
{
    public CbdcSettlementAnalysisResult AnalyzeSettlementQueue(CbdcSettlementTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var gross = Math.Max(0.01, telemetry.GrossLiquidityRequirementMillions);
        var net = Math.Max(0.01, telemetry.NetSettledLiquidityMillions);
        var savingsRatio = Math.Round(Math.Clamp((gross - net) / gross, 0.0, 1.0), 3);

        var reserves = Math.Max(0.0, telemetry.AvailableCentralBankReservesMillions);
        var reserveCoverage = Math.Round(reserves / net, 3);

        var resolutionSeconds = Math.Max(0.001, telemetry.GridlockResolutionTimeMilliseconds / 1000.0);
        var settlementRate = Math.Round(telemetry.QueuedTransferCount / resolutionSeconds, 1);

        string regime;
        string action;
        bool isFeasible;

        if (reserveCoverage < 1.0)
        {
            regime = "GRIDLOCK_COLLAPSE_CAPITAL_SHORTAGE";
            action = "Available central bank reserves insufficient to settle netted queue. Trigger automated intraday standing liquidity facility or repo injection.";
            isFeasible = false;
        }
        else if (!telemetry.AtomicHtlcCrossCurrencyPvpEnabled && telemetry.IdentifiedMultilateralCyclesCount == 0)
        {
            regime = "ATOMIC_PVP_DESYNCHRONIZATION_RISK";
            action = "Cross-border settlement lacking atomic HTLC locks and cycle netting. Enable cryptographic escrow to prevent Herstatt settlement risk.";
            isFeasible = false;
        }
        else if (savingsRatio < 0.35)
        {
            regime = "BILATERAL_OFFSET_CONSERVATIVE";
            action = "Multilateral cycle opportunities limited. Process pairwise FIFO bilateral offsets while holding high-value gross transfers in queue.";
            isFeasible = true;
        }
        else
        {
            regime = "MULTILATERAL_NETTING_OPTIMAL";
            action = "Cycle-cancelling LSM algorithm achieved high liquidity conservation (>35%). Atomic DvP/PvP state update committed across participating central banks.";
            isFeasible = true;
        }

        return new CbdcSettlementAnalysisResult(
            SettlementBatchId: telemetry.SettlementBatchId,
            LiquiditySavingsRatio: savingsRatio,
            ReserveCoverageRatio: reserveCoverage,
            EffectiveSettlementRatePerSecond: settlementRate,
            SettlementRegime: regime,
            LiquidityAction: action,
            IsBatchClearingFeasible: isFeasible
        );
    }
}
