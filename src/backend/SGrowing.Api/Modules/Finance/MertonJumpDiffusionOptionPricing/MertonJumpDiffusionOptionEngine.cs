using System;

namespace SGrowing.Api.Modules.Finance.MertonJumpDiffusionOptionPricing;

public interface IMertonJumpDiffusionOptionEngine
{
    MertonJumpDiffusionAnalysisResult PriceOptionWithJumps(MertonJumpDiffusionTelemetry telemetry);
}

public sealed class MertonJumpDiffusionOptionEngine : IMertonJumpDiffusionOptionEngine
{
    private const double JumpDominanceThreshold = 0.35; // Jump variance >= 35% of total variance
    private const int MaxPoissonTerms = 35; // Convergence up to 35 terms is accurate to 10^-8

    public MertonJumpDiffusionAnalysisResult PriceOptionWithJumps(MertonJumpDiffusionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.UnderlyingPrice <= 0.0 || telemetry.StrikePrice <= 0.0 || telemetry.TimeToExpiryYears <= 0.0)
        {
            throw new ArgumentException("Underlying price, strike, and time to expiry must be positive.", nameof(telemetry));
        }

        double s = telemetry.UnderlyingPrice;
        double kStrike = telemetry.StrikePrice;
        double t = telemetry.TimeToExpiryYears;
        double r = telemetry.RiskFreeRate;
        double sigma = Math.Max(0.01, telemetry.ContinuousVolatility);
        double lambdaJ = Math.Max(0.0, telemetry.JumpIntensityLambda);
        double muJ = telemetry.MeanJumpSizeLogMuJ;
        double sigmaJ = Math.Max(0.001, telemetry.JumpVolatilitySigmaJ);
        bool isCall = telemetry.IsCallOption;

        // 1. Jump parameters:
        // k = exp(mu_J + 0.5 * sigma_J^2) - 1
        double kJumpMean = Math.Exp(muJ + 0.5 * Math.Pow(sigmaJ, 2.0)) - 1.0;
        double lambdaPrime = lambdaJ * (1.0 + kJumpMean);

        // 2. Variance decomposition:
        // sigma_total^2 = sigma^2 + lambda_J * (mu_J^2 + sigma_J^2)
        double jumpVariance = lambdaJ * (Math.Pow(muJ, 2.0) + Math.Pow(sigmaJ, 2.0));
        double totalVariance = Math.Pow(sigma, 2.0) + jumpVariance;
        double totalEffectiveVol = Math.Sqrt(totalVariance);
        double jumpVarianceFraction = totalVariance > 0.0 ? jumpVariance / totalVariance : 0.0;

        // 3. Merton Jump-Diffusion Series Summation:
        double mertonPrice = 0.0;
        double lambdaPrimeT = lambdaPrime * t;
        double poissonFactor = Math.Exp(-lambdaPrimeT);

        for (int n = 0; n <= MaxPoissonTerms; n++)
        {
            if (n > 0)
            {
                poissonFactor *= (lambdaPrimeT / n);
            }

            if (poissonFactor < 1e-12 && n > 5)
            {
                break;
            }

            double sigmaN = Math.Sqrt(Math.Pow(sigma, 2.0) + (n * Math.Pow(sigmaJ, 2.0) / t));
            double rN = r - lambdaJ * kJumpMean + (n * Math.Log(1.0 + kJumpMean) / t);

            double bsTerm = CalculateBlackScholes(s, kStrike, t, sigmaN, rN, isCall);
            mertonPrice += poissonFactor * bsTerm;
        }

        // 4. Baseline Black-Scholes Price (Continuous diffusion only):
        double bsContinuousPrice = CalculateBlackScholes(s, kStrike, t, sigma, r, isCall);

        // 5. Jump Risk Premium Spread:
        double jumpPremiumSpread = mertonPrice - bsContinuousPrice;

        // 6. Validation Checks:
        bool isJumpDominant = jumpVarianceFraction >= JumpDominanceThreshold;
        bool isSkewSignificant = Math.Abs(jumpPremiumSpread) >= 0.015 * s;

        // 7. Regime & Guidance:
        string regime;
        string guidance;

        if (isJumpDominant && muJ < -0.05)
        {
            regime = "ASYMMETRIC_NEGATIVE_CRASH_DISCONTINUITY";
            guidance = $"Negative jump expectation (mu_J = {muJ * 100.0:F1}%) and high jump variance ({jumpVarianceFraction * 100.0:F1}%) produce extreme OTM put implied volatility skew. Long gamma dynamic hedging requires discrete jump gap buffers and tail put overlays.";
        }
        else if (isJumpDominant)
        {
            regime = "HIGH_JUMP_INTENSITY_EVENT_VOLATILITY";
            guidance = $"Jump intensity (lambda_J = {lambdaJ:F2}/year) accounts for {jumpVarianceFraction * 100.0:F1}% of variance. Continuous delta hedging will fail during discontinuous announcements; structure risk-defined defined-loss spreads (e.g. butterflies or vertical collars).";
        }
        else if (isSkewSignificant)
        {
            regime = "MODERATE_EARNINGS_SURPRISE_SMILE_SKEW";
            guidance = $"Option price reflects {jumpPremiumSpread:+$0.00;-$0.00} jump premium over continuous Black-Scholes. Calibrate implied vol smiles using Merton jump parameters rather than flat implied vol.";
        }
        else
        {
            regime = "GAUSSIAN_DIFFUSION_CONTINUOUS_DOMINATED";
            guidance = "Merton pricing converges closely to standard continuous Black-Scholes: minimal jump variance, stable geometric Brownian motion, and continuous delta hedging remains highly effective.";
        }

        return new MertonJumpDiffusionAnalysisResult
        {
            OptionSymbol = telemetry.OptionSymbol,
            MertonTheoreticalPrice = Math.Round(mertonPrice, 4),
            BlackScholesContinuousPrice = Math.Round(bsContinuousPrice, 4),
            JumpRiskPremiumSpread = Math.Round(jumpPremiumSpread, 4),
            JumpVarianceFractionOfTotal = Math.Round(jumpVarianceFraction, 3),
            TotalEffectiveVolatility = Math.Round(totalEffectiveVol, 4),
            IsJumpRiskDominant = isJumpDominant,
            IsSignificantPreEarningsSmileSkew = isSkewSignificant,
            OptionValuationRegime = regime,
            HedgingAndVolatilityGuidance = guidance
        };
    }

    private static double CalculateBlackScholes(double s, double k, double t, double sigma, double r, bool isCall)
    {
        double d1 = (Math.Log(s / k) + (r + 0.5 * Math.Pow(sigma, 2.0)) * t) / (sigma * Math.Sqrt(t));
        double d2 = d1 - sigma * Math.Sqrt(t);

        if (isCall)
        {
            return s * NormalCdf(d1) - k * Math.Exp(-r * t) * NormalCdf(d2);
        }
        else
        {
            return k * Math.Exp(-r * t) * NormalCdf(-d2) - s * NormalCdf(-d1);
        }
    }

    private static double NormalCdf(double x)
    {
        // Abramowitz & Stegun approximation 7.1.26 (error < 1.5e-7)
        double p = 0.2316419;
        double b1 = 0.319381530;
        double b2 = -0.356563782;
        double b3 = 1.781477937;
        double b4 = -1.821255978;
        double b5 = 1.330274429;

        double absX = Math.Abs(x);
        double t = 1.0 / (1.0 + p * absX);
        double pdf = (1.0 / Math.Sqrt(2.0 * Math.PI)) * Math.Exp(-0.5 * x * x);
        double poly = ((((b5 * t + b4) * t + b3) * t + b2) * t + b1) * t;
        double cdf = 1.0 - pdf * poly;

        return x >= 0.0 ? cdf : 1.0 - cdf;
    }
}
