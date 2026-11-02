namespace SGrowing.Api.Modules.Finance.AlmgrenChrissOptimalLiquidation;

public interface IAlmgrenChrissLiquidationEngine
{
    AlmgrenChrissLiquidationEvaluation AnalyzeLiquidation(AlmgrenChrissLiquidationTelemetry telemetry);
}

public class AlmgrenChrissLiquidationEngine : IAlmgrenChrissLiquidationEngine
{
    public AlmgrenChrissLiquidationEvaluation AnalyzeLiquidation(AlmgrenChrissLiquidationTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Urgency parameter kappa = sqrt(lambda * sigma^2 / eta)
        double sigmaDaily = telemetry.DailyAssetVolatilityPercent / 100.0;
        double sigmaHourly = sigmaDaily / Math.Sqrt(6.5); // 6.5 trading hours/day
        double sigma2 = sigmaHourly * sigmaHourly;
        double eta = Math.Max(1e-9, telemetry.TemporaryPriceImpactEta);
        double kappa = Math.Sqrt((telemetry.RiskAversionParameterLambda * sigma2) / eta);

        // Expected Implementation Shortfall E[IS] ~ 0.5 * gamma * Q^2 + eta * Q^2 / (2 * T) * (coth(kappa*T))
        double q = telemetry.TotalOrderQuantityShares;
        double t = Math.Max(0.1, telemetry.ExecutionHorizonHours);
        double cothFactor = (Math.Exp(2.0 * kappa * t) + 1.0) / Math.Max(1e-4, Math.Exp(2.0 * kappa * t) - 1.0);

        double permanentCost = 0.5 * telemetry.PermanentPriceImpactGamma * q * q;
        double temporaryCost = (eta * q * q / (2.0 * t)) * cothFactor;
        double totalCostUsd = permanentCost + temporaryCost;

        // In basis points relative to total notional
        double totalNotional = q * telemetry.RealizedArrivalPriceUsd;
        double expectedIsBps = (totalCostUsd / Math.Max(1.0, totalNotional)) * 10000.0;

        // Realized slippage in basis points
        double priceDiff = Math.Abs(telemetry.RealizedArrivalPriceUsd - telemetry.RealizedAverageExecutionPriceUsd);
        double realizedSlippageBps = (priceDiff / Math.Max(1.0, telemetry.RealizedArrivalPriceUsd)) * 10000.0;

        bool isParticipationSafe = telemetry.MarketParticipationRatePercent <= 15.0;
        bool isSlippageOk = realizedSlippageBps <= 25.0;
        bool isTrajectoryOptimal = isParticipationSafe && isSlippageOk && kappa >= 0.005 && kappa <= 3.0;

        if (telemetry.MarketParticipationRatePercent > 15.0)
        {
            regimes.Add("PARTICIPATION_RATE_CAP_EXCEEDANCE");
            mitigations.Add("Throttle slicing rate to cap volume footprint below 12% of prevailing order book volume to avoid predatory front-running.");
        }

        if (realizedSlippageBps > 25.0)
        {
            regimes.Add("ADVERSE_MARKET_IMPACT_SLIPPAGE_BLOWOUT");
            mitigations.Add("Switch execution benchmark to dynamic volume-adaptive VWAP with liquidity dark-pool routing.");
        }

        if (kappa > 0.50)
        {
            regimes.Add("INVENTORY_HOLDING_VOLATILITY_RISK");
            mitigations.Add("Increase liquidation urgency or employ index futures delta proxy hedge to suppress market volatility inventory risk.");
        }

        return new AlmgrenChrissLiquidationEvaluation
        {
            OrderExecutionId = telemetry.OrderExecutionId,
            CalculatedUrgencyParameterKappa = Math.Round(kappa, 4),
            ExpectedImplementationShortfallBps = Math.Round(expectedIsBps, 2),
            TotalEstimatedCostUsd = Math.Round(totalCostUsd, 2),
            RealizedSlippageBps = Math.Round(realizedSlippageBps, 2),
            IsParticipationRateSafe = isParticipationSafe,
            IsSlippageWithinTolerance = isSlippageOk,
            IsTrajectoryRiskOptimal = isTrajectoryOptimal,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
