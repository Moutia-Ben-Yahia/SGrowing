using System;

namespace SGrowing.Api.Modules.Finance.LsdDepegArbitrageRedemption;

public sealed class LsdDepegArbitrageRedemptionEngine : ILsdDepegArbitrageRedemptionEngine
{
    public LsdArbitrageAnalysisResult AnalyzeLsdDepegArbitrage(LsdArbitrageTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var canonicalPeg = Math.Max(0.0001, telemetry.CanonicalRedemptionRate);
        var secondaryPrice = Math.Max(0.0001, telemetry.SecondaryMarketPrice);
        var tradeSize = Math.Max(1.0, telemetry.SimulatedTradeSizeEth);

        var discount = (canonicalPeg - secondaryPrice) / canonicalPeg;
        var discountBps = discount * 10_000.0;

        var grossProfitEth = tradeSize * (canonicalPeg - secondaryPrice);
        var slippageCostEth = tradeSize * (Math.Max(0.0, telemetry.EstimatedPriceImpactPercent) / 100.0);
        var gasCostEth = Math.Max(0.0, telemetry.EstimatedGasCostEth);

        var queueDays = Math.Max(0.2, telemetry.ValidatorExitQueueDelayDays);
        var queueYears = queueDays / 365.0;
        var hurdleFraction = Math.Max(0.0, telemetry.AnnualizedCapitalCostHurdleRatePercent) / 100.0;
        var capitalCostEth = tradeSize * hurdleFraction * queueYears;

        var netProfitEth = grossProfitEth - slippageCostEth - gasCostEth - capitalCostEth;
        var investedCapital = tradeSize * secondaryPrice;
        var netReturnFraction = netProfitEth / investedCapital;
        var annualizedYieldPercent = (netReturnFraction / queueYears) * 100.0;

        string regime;
        string strategy;
        bool isViable;

        if (discountBps > 600.0 || telemetry.SecondaryPoolImbalanceRatio > 0.85)
        {
            regime = "SYSTEMIC_LST_RUN_AND_DEPEG_CONTAGION";
            strategy = "Severe secondary depeg (>600 bps) or extreme Curve pool imbalance. Suspend automated flash loan arbitrage and engage contagion circuit breaker.";
            isViable = false;
        }
        else if (netProfitEth <= 0.0 || annualizedYieldPercent <= 0.0)
        {
            regime = "NEGATIVE_NET_YIELD_POST_EXPENSES";
            strategy = "Arbitrage margin consumed by gas, AMM slippage, and queue holding cost. Abort transaction execution.";
            isViable = false;
        }
        else if (telemetry.ValidatorExitQueueDelayDays > 14.0)
        {
            regime = "EXTENDED_VALIDATOR_EXIT_QUEUE_EXPOSURE";
            strategy = "Exit queue latency exceeds 14 days. Execute arbitrage with delta-neutral perpetual futures hedge on CEX/DEX.";
            isViable = true;
        }
        else
        {
            regime = "PROFITABLE_DEPEG_ARBITRAGE_OPPORTUNITY";
            strategy = "Attractive depeg discount with brief validator exit queue. Route flash loan swap and submit validator unstaking withdrawal ticket.";
            isViable = true;
        }

        return new LsdArbitrageAnalysisResult(
            PoolId: telemetry.PoolId,
            LsdTokenSymbol: telemetry.LsdTokenSymbol,
            DepegDiscountBasisPoints: discountBps,
            NetArbitrageYieldAnnualizedPercent: annualizedYieldPercent,
            ExpectedProfitPerTradeEth: netProfitEth,
            ArbitrageRegime: regime,
            ExecutionStrategy: strategy,
            IsArbitrageViable: isViable
        );
    }
}
