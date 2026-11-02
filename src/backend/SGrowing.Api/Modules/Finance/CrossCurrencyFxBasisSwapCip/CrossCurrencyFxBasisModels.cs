namespace SGrowing.Api.Modules.Finance.CrossCurrencyFxBasisSwapCip;

public sealed record CrossCurrencyFxBasisTelemetry(
    string CurrencyPair, // e.g. "EURUSD", "USDJPY"
    double SpotRate, // S: e.g. 1.0850
    double ForwardRate, // F: e.g. 1.1025
    double BaseCurrencyRatePercent, // r_base: e.g. 3.65% (EUR)
    double QuoteCurrencyRatePercent, // r_quote: e.g. 5.25% (USD)
    double TenorYears, // tau: e.g. 1.0 yr
    double BankBalanceSheetCapitalCostBps, // e.g. 12.0 bps
    bool CentralBankSwapLineFacilityActive // e.g. false
);

public sealed record CrossCurrencyFxBasisAnalysisResult(
    string CurrencyPair,
    double TheoreticalCipForwardRate,
    double ObservedCrossCurrencyBasisBps,
    double NetArbitrageSpreadBps,
    string MarketLiquidityRegime,
    string MoneyMarketExecutionGuidance,
    bool IsArbitrageProfitable
);

public interface ICrossCurrencyFxBasisSwapCipEngine
{
    CrossCurrencyFxBasisAnalysisResult AnalyzeCipDeviation(CrossCurrencyFxBasisTelemetry telemetry);
}
