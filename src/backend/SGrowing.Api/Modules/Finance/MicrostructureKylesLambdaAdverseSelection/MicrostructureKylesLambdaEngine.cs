using System;

namespace SGrowing.Api.Modules.Finance.MicrostructureKylesLambdaAdverseSelection;

public interface IMicrostructureKylesLambdaEngine
{
    MicrostructureKylesLambdaAnalysisResult AnalyzeMicrostructure(MicrostructureKylesLambdaTelemetry telemetry);
}

public sealed class MicrostructureKylesLambdaEngine : IMicrostructureKylesLambdaEngine
{
    private const double CriticalLambdaSpikeMultiplier = 2.5; // lambda >= 2.5x baseline signals illiquidity shock
    private const double InfoDominanceThreshold = 0.60; // rho_info >= 0.60 permanent price impact
    private const double CriticalVpinToxicityThreshold = 0.65; // VPIN >= 0.65 impending toxicity cascade

    public MicrostructureKylesLambdaAnalysisResult AnalyzeMicrostructure(MicrostructureKylesLambdaTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.VarianceOrderFlow <= 0.0)
        {
            throw new ArgumentException("Order flow variance must be positive.", nameof(telemetry));
        }

        // 1. Kyle's Lambda Price Impact: lambda = Cov(Delta P, Q) / Var(Q) ($/share)
        double kylesLambda = telemetry.CovariancePriceChangeOrderFlow / telemetry.VarianceOrderFlow;

        // 2. Lambda Ratio relative to baseline
        double baselineLambda = Math.Max(1e-9, telemetry.RollingBaselineLambda);
        double lambdaRatio = kylesLambda / baselineLambda;

        // 3. Hasbrouck Information Fraction: rho_info
        double hasbrouckFraction = Math.Clamp(telemetry.HasbrouckPermanentVarianceFraction, 0.0, 1.0);

        // 4. VPIN Toxicity
        double vpin = Math.Clamp(telemetry.VolumeSynchronizedToxicityVpin, 0.0, 1.0);

        // 5. Recommended Spread Widening Multiplier
        // Multiplier scales with lambda ratio and toxicity
        double wideningMultiplier = 1.0;
        if (lambdaRatio >= CriticalLambdaSpikeMultiplier || vpin >= CriticalVpinToxicityThreshold)
        {
            wideningMultiplier = 1.0 + 0.6 * Math.Max(0.0, lambdaRatio - 1.0) + 1.5 * Math.Max(0.0, vpin - 0.5);
        }
        wideningMultiplier = Math.Round(Math.Clamp(wideningMultiplier, 1.0, 8.0), 2);

        // 6. Validation Checks
        bool isLambdaSpike = lambdaRatio >= CriticalLambdaSpikeMultiplier;
        bool isInfoDominant = hasbrouckFraction >= InfoDominanceThreshold;
        bool isVpinCritical = vpin >= CriticalVpinToxicityThreshold;

        // 7. Regime & Execution Defense
        string regime;
        string defense;

        if (isVpinCritical && isLambdaSpike)
        {
            regime = "TOXIC_INFORMED_CASCADE_LIQUIDITY_RUN";
            defense = $"Both Kyle's lambda spike ({lambdaRatio:F2}x baseline) and VPIN ({vpin:F2}) indicate severe informed toxic selling. Immediately widen market quotes by {wideningMultiplier:F1}x, cancel passive bids, and skew limit orders away from the book pressure side.";
        }
        else if (isVpinCritical)
        {
            regime = "HIGH_VPIN_ORDER_TOXICITY_WARNING";
            defense = $"VPIN ({vpin:F2}) exceeded critical threshold ({CriticalVpinToxicityThreshold:F2}). Elevate quoting spread by {wideningMultiplier:F1}x and throttle quoting size to avoid adverse selection fills.";
        }
        else if (isLambdaSpike)
        {
            regime = "SUDDEN_PRICE_IMPACT_ILLIQUIDITY_SPIKE";
            defense = $"Kyle's lambda ({kylesLambda:E3} $/share, {lambdaRatio:F2}x baseline) indicates severe temporary order book thinning. Apply algorithmic execution TWAP pacing to minimize permanent footprint.";
        }
        else if (isInfoDominant)
        {
            regime = "PERMANENT_INFORMATION_DOMINATED_FLOW";
            defense = $"Hasbrouck permanent variance fraction ({hasbrouckFraction * 100.0:F1}%) confirms trades reflect fundamental information rather than transitory noise. Maintain moderate spread and track order flow momentum.";
        }
        else
        {
            regime = "BALANCED_NOISE_TRADER_LIQUIDITY_HARVEST";
            defense = "Market microstructure optimal: minimal adverse selection, low VPIN toxicity, stable Kyle's lambda, and high liquidity replenishment rate.";
        }

        return new MicrostructureKylesLambdaAnalysisResult
        {
            Ticker = telemetry.Ticker,
            KylesLambdaPriceImpactPerShare = Math.Round(kylesLambda, 8),
            LambdaRatioToRollingBaseline = Math.Round(lambdaRatio, 2),
            HasbrouckPermanentInformationFraction = Math.Round(hasbrouckFraction, 3),
            VolumeSynchronizedToxicityVpin = Math.Round(vpin, 3),
            RecommendedSpreadWideningMultiplier = wideningMultiplier,
            IsLambdaSpikeDetected = isLambdaSpike,
            IsInformationDominantRegime = isInfoDominant,
            IsToxicityCritical = isVpinCritical,
            MarketMicrostructureRegime = regime,
            ExecutionDefenseProtocol = defense
        };
    }
}
