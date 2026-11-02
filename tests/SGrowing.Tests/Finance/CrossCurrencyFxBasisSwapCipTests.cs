using System;
using SGrowing.Api.Modules.Finance.CrossCurrencyFxBasisSwapCip;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class CrossCurrencyFxBasisSwapCipTests
{
    private readonly CrossCurrencyFxBasisSwapCipEngine _engine = new();

    [Fact]
    public void AnalyzeCipDeviation_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeCipDeviation(null!));
    }

    [Fact]
    public void AnalyzeCipDeviation_EmptyPair_ThrowsArgumentException()
    {
        var telemetry = new CrossCurrencyFxBasisTelemetry(
            CurrencyPair: "",
            SpotRate: 1.0850,
            ForwardRate: 1.1000,
            BaseCurrencyRatePercent: 3.5,
            QuoteCurrencyRatePercent: 5.0,
            TenorYears: 1.0,
            BankBalanceSheetCapitalCostBps: 10.0,
            CentralBankSwapLineFacilityActive: false
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeCipDeviation(telemetry));
    }

    [Fact]
    public void AnalyzeCipDeviation_ProfitableArbitrage_ReturnsProfitableOpportunityRegime()
    {
        // S = 1.0800, r_base = 3.0%, r_quote = 5.0%, tau = 1.0
        // F_CIP = 1.08 * (1.05 / 1.03) = 1.10097
        // Observed F = 1.0965 -> difference = -0.00447
        // basis = (-0.00447 / 1.08) * 10000 = -41.4 bps
        // capital cost = 12 bps -> Net spread = 41.4 - 12 = 29.4 bps > 15 bps
        var telemetry = new CrossCurrencyFxBasisTelemetry(
            CurrencyPair: "EURUSD-1Y-SWAP",
            SpotRate: 1.0800,
            ForwardRate: 1.0965,
            BaseCurrencyRatePercent: 3.0,
            QuoteCurrencyRatePercent: 5.0,
            TenorYears: 1.0,
            BankBalanceSheetCapitalCostBps: 12.0,
            CentralBankSwapLineFacilityActive: false
        );

        var result = _engine.AnalyzeCipDeviation(telemetry);

        Assert.NotNull(result);
        Assert.Equal("EURUSD-1Y-SWAP", result.CurrencyPair);
        Assert.True(result.NetArbitrageSpreadBps > 15.0);
        Assert.True(result.IsArbitrageProfitable);
        Assert.Equal("PROFITABLE_POST_REGULATION_CIP_ARBITRAGE_OPPORTUNITY", result.MarketLiquidityRegime);
        Assert.Contains("deploy balance sheet arbitrage", result.MoneyMarketExecutionGuidance);
    }

    [Fact]
    public void AnalyzeCipDeviation_SevereFundingSqueeze_ReturnsDollarSqueezeRegime()
    {
        // S = 150.0, F = 145.0, huge basis dislocation > 65 bps
        var telemetry = new CrossCurrencyFxBasisTelemetry(
            CurrencyPair: "USDJPY-3M-DISLOCATED",
            SpotRate: 150.0,
            ForwardRate: 147.0, // Large shift
            BaseCurrencyRatePercent: 5.25,
            QuoteCurrencyRatePercent: 0.25,
            TenorYears: 0.25, // 3 months
            BankBalanceSheetCapitalCostBps: 15.0,
            CentralBankSwapLineFacilityActive: true
        );

        var result = _engine.AnalyzeCipDeviation(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SEVERE_GLOBAL_DOLLAR_LIQUIDITY_SQUEEZE_REGIME", result.MarketLiquidityRegime);
        Assert.True(result.IsArbitrageProfitable);
        Assert.Contains("Tap Fed central bank liquidity swap line", result.MoneyMarketExecutionGuidance);
    }

    [Fact]
    public void AnalyzeCipDeviation_FrictionlessCip_ReturnsEquilibriumRegime()
    {
        // Spot = 1.1000, rates equal -> F_CIP = 1.1000, F = 1.1000
        var telemetry = new CrossCurrencyFxBasisTelemetry(
            CurrencyPair: "GBPUSD-TIGHT",
            SpotRate: 1.3000,
            ForwardRate: 1.3000,
            BaseCurrencyRatePercent: 4.5,
            QuoteCurrencyRatePercent: 4.5,
            TenorYears: 1.0,
            BankBalanceSheetCapitalCostBps: 10.0,
            CentralBankSwapLineFacilityActive: false
        );

        var result = _engine.AnalyzeCipDeviation(telemetry);

        Assert.NotNull(result);
        Assert.Equal("FRICTIONLESS_CLASSICAL_CIP_EQUILIBRIUM_REGIME", result.MarketLiquidityRegime);
        Assert.False(result.IsArbitrageProfitable);
    }
}
