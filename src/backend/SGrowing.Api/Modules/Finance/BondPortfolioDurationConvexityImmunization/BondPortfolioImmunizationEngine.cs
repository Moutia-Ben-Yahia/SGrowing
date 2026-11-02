using System;

namespace SGrowing.Api.Modules.Finance.BondPortfolioDurationConvexityImmunization;

public interface IBondPortfolioImmunizationEngine
{
    BondPortfolioImmunizationAnalysisResult AnalyzeImmunization(BondPortfolioImmunizationTelemetry telemetry);
}

public sealed class BondPortfolioImmunizationEngine : IBondPortfolioImmunizationEngine
{
    private const double MaxAllowableDurationGapYears = 0.15; // |D_A - D_L| <= 0.15 years
    private const double MinFundingRatioPercent = 100.0; // PV(A) >= PV(L)

    public BondPortfolioImmunizationAnalysisResult AnalyzeImmunization(BondPortfolioImmunizationTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.PortfolioPresentValueUsd <= 0.0 || telemetry.LiabilityPresentValueUsd <= 0.0)
        {
            throw new ArgumentException("Portfolio and liability present values must be positive.", nameof(telemetry));
        }

        // 1. Present Value Funding Ratio: PV(A) / PV(L) * 100%
        double fundingRatio = (telemetry.PortfolioPresentValueUsd / telemetry.LiabilityPresentValueUsd) * 100.0;
        fundingRatio = Math.Round(fundingRatio, 2);

        // 2. Duration Mismatch Gap: D_A - D_L
        double durationGap = telemetry.PortfolioMacaulayDurationYears - telemetry.LiabilityMacaulayDurationYears;
        durationGap = Math.Round(durationGap, 3);

        // 3. Convexity Surplus: C_A - C_L
        double convexitySurplus = telemetry.PortfolioConvexityYears2 - telemetry.LiabilityConvexityYears2;
        convexitySurplus = Math.Round(convexitySurplus, 2);

        // 4. Taylor Series Second-Order Value Changes under Simulated Rate Shift:
        // Delta P / P approx - (D / (1+y)) * Delta y + 0.5 * C * (Delta y)^2
        // Modified duration D_mod = D / (1+y)
        double deltaY = (telemetry.SimulatedParallelRateShiftBps / 10000.0);
        double yFactor = 1.0 + Math.Max(0.001, telemetry.CurrentYieldCurveRate);

        double assetDMod = telemetry.PortfolioMacaulayDurationYears / yFactor;
        double liabDMod = telemetry.LiabilityMacaulayDurationYears / yFactor;

        double assetPctChange = -assetDMod * deltaY + 0.5 * telemetry.PortfolioConvexityYears2 * Math.Pow(deltaY, 2.0);
        double liabPctChange = -liabDMod * deltaY + 0.5 * telemetry.LiabilityConvexityYears2 * Math.Pow(deltaY, 2.0);

        double assetDeltaUsd = telemetry.PortfolioPresentValueUsd * assetPctChange;
        double liabDeltaUsd = telemetry.LiabilityPresentValueUsd * liabPctChange;
        double netSurplusDeltaUsd = assetDeltaUsd - liabDeltaUsd;

        assetDeltaUsd = Math.Round(assetDeltaUsd, 2);
        liabDeltaUsd = Math.Round(liabDeltaUsd, 2);
        netSurplusDeltaUsd = Math.Round(netSurplusDeltaUsd, 2);

        // 5. Validation Checks (Redington's 3 conditions):
        bool isFundingSurplus = fundingRatio >= MinFundingRatioPercent;
        bool isDurationMatched = Math.Abs(durationGap) <= MaxAllowableDurationGapYears;
        bool isConvexityDominant = convexitySurplus > 0.0;
        bool isFullyImmunized = isFundingSurplus && isDurationMatched && isConvexityDominant;

        // 6. Regime & Guidance:
        string regime;
        string guidance;

        if (!isFundingSurplus)
        {
            regime = "UNDERFUNDED_LIABILITY_DEFICIT_HAZARD";
            guidance = $"Portfolio is underfunded (funding ratio {fundingRatio:F1}% < 100%). Inject capital or reallocate into high-yielding credit spreads while strictly managing tracking error.";
        }
        else if (!isDurationMatched)
        {
            string direction = durationGap > 0 ? "long (over-exposed to rate rises)" : "short (under-exposed to rate drops)";
            regime = "DURATION_MISMATCH_FIRST_ORDER_RATE_RISK";
            guidance = $"Duration gap ({durationGap:F2} years) exceeds tolerance ({MaxAllowableDurationGapYears:F2} years). Portfolio is {direction}. Execute interest rate swaps (IRS) or Treasury futures overlay to neutralize duration gap.";
        }
        else if (!isConvexityDominant)
        {
            regime = "CONVEXITY_DEFICIT_RATE_SHOCK_VULNERABILITY";
            guidance = $"Asset convexity ({telemetry.PortfolioConvexityYears2:F1}) is inferior to liability convexity ({telemetry.LiabilityConvexityYears2:F1}). Severe rate swings will erode funding surplus. Rebalance barbell allocations into long-dated zero-coupon bonds to restore positive convexity dominance.";
        }
        else
        {
            regime = "REDINGTON_IMMUNIZED_CONVEXITY_DOMINANT";
            guidance = "Fixed income portfolio fully satisfies Redington's immunization conditions: funding surplus intact, duration gap eliminated, and positive convexity surplus guarantees liability protection under rate shocks.";
        }

        return new BondPortfolioImmunizationAnalysisResult
        {
            PortfolioId = telemetry.PortfolioId,
            PresentValueFundingRatioPercent = fundingRatio,
            DurationMismatchGapYears = durationGap,
            ConvexitySurplusYears2 = convexitySurplus,
            SimulatedAssetValueChangeUsd = assetDeltaUsd,
            SimulatedLiabilityValueChangeUsd = liabDeltaUsd,
            SimulatedNetSurplusChangeUsd = netSurplusDeltaUsd,
            IsFundingSurplusPresent = isFundingSurplus,
            IsDurationMatched = isDurationMatched,
            IsConvexityDominant = isConvexityDominant,
            IsRedingtonImmunizationAchieved = isFullyImmunized,
            ImmunizationStatusRegime = regime,
            PortfolioRebalancingGuidance = guidance
        };
    }
}
