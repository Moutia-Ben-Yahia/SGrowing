using System;

namespace SGrowing.Api.Modules.Finance.StatisticalArbitrageOrnsteinUhlenbeck;

public sealed class StatisticalArbitrageOrnsteinUhlenbeckEngine : IStatisticalArbitrageOrnsteinUhlenbeckEngine
{
    public StatisticalArbitrageAnalysisResult AnalyzePairMeanReversion(StatisticalArbitrageTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.PairSymbol))
        {
            throw new ArgumentException("Pair symbol must be provided.", nameof(telemetry));
        }

        if (telemetry.VolatilitySigma <= 0 || telemetry.AugmentedDickeyFullerPValue < 0)
        {
            throw new ArgumentException("Volatility and p-values must be positive.", nameof(telemetry));
        }

        var theta = telemetry.MeanReversionSpeedTheta;
        var halfLifeDays = theta > 0.0 ? Math.Log(2.0) / theta : 999.0;

        var equilibriumStdDev = theta > 0.0
            ? telemetry.VolatilitySigma / Math.Sqrt(2.0 * theta)
            : telemetry.VolatilitySigma;

        var zScore = (telemetry.CurrentSpreadResidual - telemetry.LongTermMeanMu) / Math.Max(1e-6, equilibriumStdDev);

        string regime;
        string guidance;
        bool isValid = telemetry.AugmentedDickeyFullerPValue <= 0.05 &&
                       theta > 0.02 &&
                       Math.Abs(zScore) < 3.8;

        if (telemetry.AugmentedDickeyFullerPValue > 0.10 || theta <= 0.0)
        {
            regime = "BROKEN_COINTEGRATION_RANDOM_WALK_REGIME";
            guidance = $"ADF p-value ({telemetry.AugmentedDickeyFullerPValue:F3} > 0.10) or non-positive theta ({theta:F4}) indicates non-stationarity. Pair cointegration relationship has disintegrated; do not trade.";
            isValid = false;
        }
        else if (Math.Abs(zScore) >= 3.8)
        {
            regime = "STRUCTURAL_BREAK_STOP_LOSS_DIVERGENCE_HAZARD";
            guidance = $"Extreme divergence (Z = {zScore:F2} sigma). High probability of unmodeled fundamental corporate action or regime shift. Trigger emergency stop-loss and unwind.";
            isValid = false;
        }
        else if (Math.Abs(zScore) >= 2.0 && halfLifeDays <= 30.0)
        {
            regime = "ACTIVE_STAT_ARB_MEAN_REVERSION_ENTRY_OPPORTUNITY";
            var action = zScore > 0 ? "Short spread (Sell A, Buy B)" : "Long spread (Buy A, Sell B)";
            guidance = $"Statistically significant divergence (Z = {zScore:F2}) with rapid half-life ({halfLifeDays:F1} days). {action} scaled to hedge ratio beta = {telemetry.CointegrationHedgeRatioBeta:F3}.";
        }
        else if (Math.Abs(zScore) <= 0.5)
        {
            regime = "EQUILIBRIUM_MEAN_REVERTED_EXIT_STATE";
            guidance = $"Spread has mean-reverted within equilibrium band (|Z| = {Math.Abs(zScore):F2} <= 0.5 sigma). Take profits and close outstanding pair positions.";
        }
        else
        {
            regime = "IN_POSITION_CONVERGING_HOLD_REGIME";
            guidance = $"Spread residual (Z = {zScore:F2}) is converging toward mean. Maintain current market-neutral pair inventory.";
        }

        return new StatisticalArbitrageAnalysisResult(
            telemetry.PairSymbol,
            Math.Round(halfLifeDays, 2),
            Math.Round(zScore, 3),
            Math.Round(equilibriumStdDev, 4),
            regime,
            guidance,
            isValid
        );
    }
}
