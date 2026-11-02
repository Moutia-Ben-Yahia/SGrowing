namespace SGrowing.Api.Modules.Finance.AmmLossVersusRebalancing;

public class AmmLossVersusRebalancingEngine
{
    private const double HoursPerYear = 8760.0;

    public AmmLvrAnalysisResult EvaluatePoolLvr(AmmLvrTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.PoolId))
        {
            throw new ArgumentException("Pool ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.TotalValueLockedUsd <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Total value locked must be positive.");
        }

        if (telemetry.ObservationWindowHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Observation window must be positive.");
        }

        if (telemetry.CurrentAnnualizedVolatility < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Volatility cannot be negative.");
        }

        // Time fraction in years
        double timeFraction = telemetry.ObservationWindowHours / HoursPerYear;

        // Continuous-time LVR estimation:
        // LVR = (sigma^2 / 8) * TVL * t
        double sigma = telemetry.CurrentAnnualizedVolatility;
        double lvrFraction = (Math.Pow(sigma, 2) / 8.0) * timeFraction;
        decimal estimatedLvrUsd = Math.Round(telemetry.TotalValueLockedUsd * (decimal)lvrFraction, 2);

        // Accumulated fee revenue:
        // Fee = Volume * (BaseSwapFeeBps / 10000)
        decimal feeRate = (decimal)telemetry.BaseSwapFeeBps / 10000.0m;
        decimal feeRevenueUsd = Math.Round(telemetry.CumulativeTradingVolumeUsd * feeRate, 2);

        // Net LP PnL = Fee Revenue - LVR Drain
        decimal netLpPnL = feeRevenueUsd - estimatedLvrUsd;
        bool isBleedExceedingFees = netLpPnL < 0m;

        // Dynamic Fee calculation: scale with volatility and toxic flow fraction
        double volMultiplier = Math.Max(1.0, sigma / 0.50);
        double toxicMultiplier = 1.0 + (Math.Clamp(telemetry.ToxicOrderFlowFraction, 0.0, 1.0) * 1.5);
        int optimalFeeBps = (int)Math.Round(telemetry.BaseSwapFeeBps * volMultiplier * toxicMultiplier);
        optimalFeeBps = Math.Clamp(optimalFeeBps, telemetry.BaseSwapFeeBps, 150); // bounded to 1.5%

        string state;
        string action;
        string summary;

        if (isBleedExceedingFees && telemetry.ToxicOrderFlowFraction >= 0.35)
        {
            state = "CRITICAL_TOXIC_ARBITRAGE_DRAIN";
            action = "WIDEN_CONCENTRATED_TICKS_AND_HIKE_FEE";
            summary = $"CRITICAL LVR BLEED: Arbitrageurs extracted ${estimatedLvrUsd:N2} against only ${feeRevenueUsd:N2} fee revenue (Net LP Loss: -${Math.Abs(netLpPnL):N2}). Toxic flow fraction {telemetry.ToxicOrderFlowFraction:P1}. Hike fee to {optimalFeeBps} bps and widen tick range.";
        }
        else if (isBleedExceedingFees)
        {
            state = "LVR_EQUILIBRIUM_NEUTRAL";
            action = "ACTIVATE_DYNAMIC_VOLATILITY_SURCHARGE";
            summary = $"MODERATE LVR BLEED: Realized volatility ({sigma:P1}) outpaced base swap fees (${feeRevenueUsd:N2} fees vs ${estimatedLvrUsd:N2} LVR). Activate dynamic fee surcharge to {optimalFeeBps} bps.";
        }
        else
        {
            state = "PROFITABLE_LP_CONFINEMENT";
            action = "MAINTAIN_BASE_FEE_TIER";
            summary = $"PROFITABLE LIQUIDITY: Organic volume generated ${feeRevenueUsd:N2} fees exceeding LVR extraction (${estimatedLvrUsd:N2}). Net LP surplus: +${netLpPnL:N2}.";
        }

        return new AmmLvrAnalysisResult(
            telemetry.PoolId,
            estimatedLvrUsd,
            feeRevenueUsd,
            netLpPnL,
            optimalFeeBps,
            isBleedExceedingFees,
            state,
            action,
            summary);
    }
}
