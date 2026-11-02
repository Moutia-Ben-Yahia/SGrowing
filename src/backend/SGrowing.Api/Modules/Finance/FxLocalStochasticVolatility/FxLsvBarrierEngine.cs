namespace SGrowing.Api.Modules.Finance.FxLocalStochasticVolatility;

public interface IFxLsvBarrierEngine
{
    FxLsvBarrierEvaluation AnalyzeBarrierOption(FxLsvBarrierTelemetry telemetry);
}

public class FxLsvBarrierEngine : IFxLsvBarrierEngine
{
    public FxLsvBarrierEvaluation AnalyzeBarrierOption(FxLsvBarrierTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        double s0 = telemetry.SpotExchangeRate;
        double b = telemetry.BarrierLevel;
        double sigmaAtm = telemetry.AtTheMoneyImpliedVolatilityPercent / 100.0;
        double t = Math.Max(0.05, telemetry.OptionMaturityYears);

        // Distance to barrier in percentage
        double distToBarrierPercent = Math.Abs(b - s0) / s0 * 100.0;

        // Approximate hitting probability under geometric Brownian motion + smile:
        // P(hit) ~ 2 * (1 - Phi(d)) where d = ln(B/S0) / (sigma * sqrt(T))
        double logMoneyness = Math.Abs(Math.Log(b / s0));
        double denom = sigmaAtm * Math.Sqrt(t);
        double d = logMoneyness / Math.Max(1e-4, denom);
        // Approx normal CDF Phi(d)
        double phiD = 0.5 * (1.0 + Math.Tanh(d / Math.Sqrt(2.0)));
        double hitProbPercent = Math.Clamp(2.0 * (1.0 - phiD) * 100.0, 0.5, 99.5);

        // Local stochastic leverage factor a(S, t) ~ sigma_LV / sqrt(E[v|S])
        // Skew adjustment from 25-delta Risk Reversal (RR) and Butterfly (BF)
        double leverageFactorA = 1.0 + (telemetry.RiskReversal25DeltaPercent * 0.6) + 
                                       (telemetry.Butterfly25DeltaPercent * 0.4) + 
                                       (telemetry.StochasticVolOfVolXi * 0.4);
        leverageFactorA = Math.Clamp(leverageFactorA, 0.1, 5.0);

        // Option pricing in pips (1 pip = 0.0001 in EURUSD)
        double intrinsicValuePips = Math.Max(0.0, (s0 - (s0 * 0.98))) * 10000.0;
        double survivalProb = 1.0 - (hitProbPercent / 100.0);
        double optionPricePips = Math.Clamp((intrinsicValuePips * 0.65 + 120.0 * sigmaAtm) * survivalProb * leverageFactorA, 0.0, 2500.0);
        double vanillaSmileDeltaPips = optionPricePips * (telemetry.Butterfly25DeltaPercent * 0.04);

        bool isLeverageCalibrated = leverageFactorA >= 0.25 && leverageFactorA <= 2.50;
        bool isArbitrageFree = telemetry.AtTheMoneyImpliedVolatilityPercent > 1.5 && telemetry.Butterfly25DeltaPercent >= 0.05;
        bool isPinningContained = distToBarrierPercent >= 0.85 || telemetry.StochasticVolOfVolXi <= 0.80;

        if (!isLeverageCalibrated)
        {
            regimes.Add("LEVERAGE_FUNCTION_DIVERGENCE_INSTABILITY");
            mitigations.Add("Regularize LSV Fokker-Planck PDE grid boundary conditions and apply kernel smoothing to the leverage surface.");
        }

        if (!isArbitrageFree)
        {
            regimes.Add("LOCAL_VOLATILITY_SURFACE_CALENDAR_ARBITRAGE");
            mitigations.Add("Re-interpolate total implied variance smile with SVI (Stochastic Volatility Inspired) parametrization to eliminate negative local variance.");
        }

        if (!isPinningContained)
        {
            regimes.Add("BARRIER_PINNING_GAP_SLIPPAGE_ALERT");
            mitigations.Add("Implement dynamic delta-gamma barrier window widening and delta-hedge using micro-lot futures to absorb discrete jump risk.");
        }

        bool isReliable = isLeverageCalibrated && isArbitrageFree && isPinningContained;

        return new FxLsvBarrierEvaluation
        {
            DealContractId = telemetry.DealContractId,
            CalculatedBarrierOptionPricePips = Math.Round(optionPricePips, 1),
            LocalStochasticLeverageFactorA = Math.Round(leverageFactorA, 3),
            BarrierHittingProbabilityPercent = Math.Round(hitProbPercent, 2),
            VanillaSmilePriceDeltaPips = Math.Round(vanillaSmileDeltaPips, 2),
            IsLeverageFunctionCalibrated = isLeverageCalibrated,
            IsLocalVarianceArbitrageFree = isArbitrageFree,
            IsBarrierPinningRiskContained = isPinningContained,
            IsLsvPricingReliable = isReliable,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
