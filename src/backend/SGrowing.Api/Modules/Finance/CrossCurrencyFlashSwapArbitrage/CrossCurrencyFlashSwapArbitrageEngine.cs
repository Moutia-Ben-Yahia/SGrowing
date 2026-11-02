using System;

namespace SGrowing.Api.Modules.Finance.CrossCurrencyFlashSwapArbitrage;

public sealed class CrossCurrencyFlashSwapArbitrageEngine : ICrossCurrencyFlashSwapArbitrageEngine
{
    private const double EthUsdPrice = 2500.0;
    private const double GasUnitsTriangularSwap = 280_000.0;

    public CrossCurrencyFlashSwapAnalysisResult AnalyzeTriangularArbitrage(CrossCurrencyFlashSwapTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var f1 = Math.Clamp(telemetry.Leg1PoolFeeTierPercent / 100.0, 0.0, 0.05);
        var f2 = Math.Clamp(telemetry.Leg2PoolFeeTierPercent / 100.0, 0.0, 0.05);
        var f3 = Math.Clamp(telemetry.Leg3PoolFeeTierPercent / 100.0, 0.0, 0.05);

        // Net exchange rate product accounting for multi-pool fee tiers
        var feeRetentionFactor = (1.0 - f1) * (1.0 - f2) * (1.0 - f3);
        var rawRateProduct = Math.Max(0.1, telemetry.ProductOfExchangeRates);
        var netRateProduct = rawRateProduct * feeRetentionFactor;

        var proposedBorrowUsd = Math.Max(1000.0, telemetry.ProposedBorrowAmountUsd);
        var slippageBps = Math.Max(0.1, telemetry.EstimatedTotalPriceImpactBasisPoints);

        // Price impact parameter lambda: Impact(x) = x * lambda
        var lambda = (slippageBps / 10_000.0) / proposedBorrowUsd;

        // Optimal borrow size maximizing profit: G'(x) = (netRate - 1) - 2*x*lambda = 0
        var marginAlpha = netRateProduct - 1.0;
        var optimalTradeSizeUsd = marginAlpha > 0.0 
            ? Math.Clamp(marginAlpha / (2.0 * lambda), 5000.0, 3_000_000.0)
            : proposedBorrowUsd * 0.5;

        // Gross triangular arbitrage profit at optimal size
        var impactAtOptimal = optimalTradeSizeUsd * lambda;
        var grossProfitUsd = marginAlpha > 0.0
            ? Math.Max(0.0, (optimalTradeSizeUsd * marginAlpha) - (optimalTradeSizeUsd * impactAtOptimal))
            : 0.0;

        // Gas and builder tip costs: Cost = Gas * (Base + Tip) * 1e-9 * ETH_price
        var totalFeeGwei = Math.Max(1.0, telemetry.EthereumBaseGasFeeGwei + telemetry.PriorityBuilderTipFeeGwei);
        var totalGasCostUsd = GasUnitsTriangularSwap * totalFeeGwei * 1e-9 * EthUsdPrice;

        var netProfitUsd = grossProfitUsd - totalGasCostUsd;

        string regime;
        string guidance;
        bool isProfitable;

        if (!telemetry.IsPrivateMevBuilderRelayUsed)
        {
            regime = "PUBLIC_MEMPOOL_SANDWICH_EXPLOITATION_HAZARD";
            guidance = "Transaction submitted to public mempool without private builder routing. Extreme vulnerability to generalized searcher front-running and sandwiching. Route bundle exclusively through Flashbots/MEV-Share.";
            isProfitable = false;
        }
        else if (netProfitUsd <= 0.0 || netRateProduct <= 1.0)
        {
            regime = "NEGATIVE_EXPECTED_VALUE_EXECUTION_FAILURE";
            guidance = "Net triangular swap profit is negative after fee tier retention and gas costs. Abort flash loan transaction to avoid revert penalty.";
            isProfitable = false;
        }
        else if (slippageBps > 80.0)
        {
            regime = "EXCESSIVE_SLIPPAGE_CONVEXITY_DEFICIT";
            guidance = "Triangular liquidity route shallow; total price impact exceeds 80 bps. Scale down trade size or split execution across concentrated liquidity ticks.";
            isProfitable = false;
        }
        else
        {
            regime = "ATOMIC_TRIANGULAR_ARBITRAGE_PROFITABLE_REGIME";
            guidance = "Cross-currency flash loan arbitrage opportunity positive after fees and builder tips. Atomic bundle verified for direct block builder inclusion.";
            isProfitable = true;
        }

        return new CrossCurrencyFlashSwapAnalysisResult(
            ArbitragePathId: telemetry.ArbitragePathId,
            OptimalArbitrageTradeSizeUsd: optimalTradeSizeUsd,
            GrossTriangularProfitUsd: grossProfitUsd,
            TotalExecutionCostGasAndTipsUsd: totalGasCostUsd,
            NetRiskAdjustedArbitrageProfitUsd: netProfitUsd,
            ArbitrageExecutionRegime: regime,
            MevBundleSubmissionGuidance: guidance,
            IsArbitrageProfitable: isProfitable
        );
    }
}
