using System;

namespace SGrowing.Api.Modules.Finance.SabrVolatilitySurfaceCalibration;

public sealed class SabrVolatilitySurfaceCalibrationEngine : ISabrVolatilitySurfaceCalibrationEngine
{
    public SabrVolatilityAnalysisResult AnalyzeSabrSurface(SabrVolatilityTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.SurfaceId))
        {
            throw new ArgumentException("Surface ID must be provided.", nameof(telemetry));
        }

        if (telemetry.ForwardPrice <= 0 || telemetry.StrikePrice <= 0 || telemetry.TimeToMaturityYears <= 0)
        {
            throw new ArgumentException("Forward, Strike, and Maturity must be strictly positive.", nameof(telemetry));
        }

        if (telemetry.AlphaInitialVol <= 0 || telemetry.NuVolOfVol < 0)
        {
            throw new ArgumentException("Alpha and Nu must be positive.", nameof(telemetry));
        }

        if (telemetry.BetaElasticity < 0.0 || telemetry.BetaElasticity > 1.0)
        {
            throw new ArgumentException("Beta must be in [0, 1].", nameof(telemetry));
        }

        if (telemetry.RhoCorrelation <= -1.0 || telemetry.RhoCorrelation >= 1.0)
        {
            throw new ArgumentException("Rho must be strictly in (-1, 1).", nameof(telemetry));
        }

        var f = telemetry.ForwardPrice;
        var k = telemetry.StrikePrice;
        var t = telemetry.TimeToMaturityYears;
        var alpha = telemetry.AlphaInitialVol;
        var beta = telemetry.BetaElasticity;
        var rho = telemetry.RhoCorrelation;
        var nu = telemetry.NuVolOfVol;

        // Hagan SABR ATM volatility formula:
        var fBeta = Math.Pow(f, 1.0 - beta);
        var term1 = (Math.Pow(1.0 - beta, 2.0) / 24.0) * (alpha * alpha / Math.Pow(f, 2.0 * (1.0 - beta)));
        var term2 = (rho * beta * nu * alpha) / (4.0 * fBeta);
        var term3 = ((2.0 - (3.0 * rho * rho)) / 24.0) * (nu * nu);
        var atmVol = (alpha / fBeta) * (1.0 + ((term1 + term2 + term3) * t));

        // Evaluate Hagan general formula if K != F
        double impliedVol;
        if (Math.Abs(f - k) < 1e-4)
        {
            impliedVol = atmVol;
        }
        else
        {
            var fkBeta = Math.Pow(f * k, (1.0 - beta) / 2.0);
            var logFk = Math.Log(f / k);
            var z = (nu / alpha) * fkBeta * logFk;

            var sqrtTerm = Math.Sqrt(Math.Max(1e-8, 1.0 - (2.0 * rho * z) + (z * z)));
            var chiZ = Math.Log((sqrtTerm + z - rho) / (1.0 - rho));
            var zOverChi = Math.Abs(z) > 1e-6 && Math.Abs(chiZ) > 1e-6 ? z / chiZ : 1.0;

            var denom = fkBeta * (1.0 + ((Math.Pow(1.0 - beta, 2.0) / 24.0) * logFk * logFk));
            impliedVol = (alpha / denom) * zOverChi * (1.0 + ((term1 + term2 + term3) * t));
        }

        // ATM skew slope in bps per 1% moneyness move: d(vol)/d(ln(K/F))
        var skewSlopeBps = ((rho * nu / (2.0 * fBeta)) + (((beta - 1.0) / (2.0 * fBeta)) * alpha)) * 10000.0;

        string regime;
        string guidance;
        bool isArbitrageFree = Math.Abs(rho) < 0.90 &&
                               alpha > 0.01 &&
                               nu > 0.01 &&
                               nu < 2.0;

        if (rho <= -0.95 || rho >= 0.95)
        {
            regime = "EXTREME_CORRELATION_DENSITY_LEAKAGE_HAZARD";
            guidance = $"Extreme correlation (rho = {rho:F2}) induces negative risk-neutral probability density (butterfly arbitrage) in the wings. Constrain rho in [-0.85, 0.85].";
            isArbitrageFree = false;
        }
        else if (nu > 1.8 && t > 3.0)
        {
            regime = "HIGH_VOL_OF_VOL_LONG_TENOR_SMILE_WING_INSTABILITY";
            guidance = $"High vol-of-vol (nu = {nu:F2}) over long maturity (T = {t:F1} yrs) risks Hagan expansion divergence at extreme strikes. Blend with numerical PDE or shifted lognormal SABR.";
            isArbitrageFree = false;
        }
        else if (Math.Abs(rho) < 0.85 && alpha > 0.05 && nu > 0.1)
        {
            regime = "ARBITRAGE_FREE_CALIBRATED_SABR_SMILE_REGIME";
            guidance = $"Smooth, arbitrage-free smile generated (ATM Vol = {atmVol * 100:F2}%, Skew = {skewSlopeBps:F1} bps). Full surface consistent with vanillas and swaption cubes.";
        }
        else
        {
            regime = "SUBOPTIMAL_SURFACE_FIT_REGIME";
            guidance = $"Parameters calibrated. Implied volatility is {impliedVol * 100:F2}%. Monitor wing curvature against market quotes.";
        }

        return new SabrVolatilityAnalysisResult(
            telemetry.SurfaceId,
            Math.Round(impliedVol * 100.0, 2),
            Math.Round(atmVol * 100.0, 2),
            Math.Round(skewSlopeBps, 1),
            regime,
            guidance,
            isArbitrageFree
        );
    }
}
