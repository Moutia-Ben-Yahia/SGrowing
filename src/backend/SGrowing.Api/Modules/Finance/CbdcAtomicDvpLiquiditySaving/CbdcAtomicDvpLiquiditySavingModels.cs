namespace SGrowing.Api.Modules.Finance.CbdcAtomicDvpLiquiditySaving;

public sealed record CbdcSettlementTelemetry(
    string SettlementBatchId,
    int QueuedTransferCount,
    double GrossLiquidityRequirementMillions,
    double AvailableCentralBankReservesMillions,
    int IdentifiedMultilateralCyclesCount,
    double NetSettledLiquidityMillions,
    double GridlockResolutionTimeMilliseconds,
    bool AtomicHtlcCrossCurrencyPvpEnabled
);

public sealed record CbdcSettlementAnalysisResult(
    string SettlementBatchId,
    double LiquiditySavingsRatio,
    double ReserveCoverageRatio,
    double EffectiveSettlementRatePerSecond,
    string SettlementRegime,
    string LiquidityAction,
    bool IsBatchClearingFeasible
);

public interface ICbdcAtomicDvpLiquiditySavingEngine
{
    CbdcSettlementAnalysisResult AnalyzeSettlementQueue(CbdcSettlementTelemetry telemetry);
}
