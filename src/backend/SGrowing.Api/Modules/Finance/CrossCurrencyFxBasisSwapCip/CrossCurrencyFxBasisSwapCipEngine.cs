using System;

namespace SGrowing.Api.Modules.Finance.CrossCurrencyFxBasisSwapCip;

public sealed class CrossCurrencyFxBasisSwapCipEngine : ICrossCurrencyFxBasisSwapCipEngine
{
    public CrossCurrencyFxBasisAnalysisResult AnalyzeCipDeviation(CrossCurrencyFxBasisTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.CurrencyPair))
        {
            throw new ArgumentException("Currency pair must be provided.", nameof(telemetry));
        }

        if (telemetry.SpotRate <= 0 || telemetry.ForwardRate <= 0 || telemetry.TenorYears <= 0)
        {
            throw new ArgumentException("Rates and tenor must be strictly positive.", nameof(telemetry));
        }

        var tau = telemetry.TenorYears;
        var rBase = telemetry.BaseCurrencyRatePercent / 100.0;
        var rQuote = telemetry.QuoteCurrencyRatePercent / 100.0;

        // Theoretical CIP Forward: F_CIP = S * (1 + r_quote * tau) / (1 + r_base * tau)
        var cipForward = telemetry.SpotRate * ((1.0 + (rQuote * tau)) / (1.0 + (rBase * tau)));

        // Basis spread in bps: x_basis = ((F - F_CIP) / S) * 10000 / tau
        var basisSpreadBps = ((telemetry.ForwardRate - cipForward) / telemetry.SpotRate) * (10000.0 / tau);

        // Net arbitrage margin after balance sheet regulatory / leverage charges
        var capitalCost = Math.Max(0.0, telemetry.BankBalanceSheetCapitalCostBps);
        var netSpreadBps = Math.Abs(basisSpreadBps) - capitalCost;

        string regime;
        string guidance;
        bool isProfitable = netSpreadBps >= 8.0 && Math.Abs(basisSpreadBps) >= 20.0;

        if (Math.Abs(basisSpreadBps) >= 65.0)
        {
            regime = "SEVERE_GLOBAL_DOLLAR_LIQUIDITY_SQUEEZE_REGIME";
            guidance = $"Acute cross-currency basis dislocation ({basisSpreadBps:F1} bps). Global USD funding stress is extreme. Tap Fed central bank liquidity swap line to circumvent broken cash-futures basis.";
            isProfitable = true;
        }
        else if (netSpreadBps > 15.0)
        {
            regime = "PROFITABLE_POST_REGULATION_CIP_ARBITRAGE_OPPORTUNITY";
            guidance = $"Persistent CIP deviation ({basisSpreadBps:F1} bps) comfortably exceeds SLR capital costs ({capitalCost:F1} bps). Net margin is {netSpreadBps:F1} bps; deploy balance sheet arbitrage.";
        }
        else if (Math.Abs(basisSpreadBps) <= 5.0)
        {
            regime = "FRICTIONLESS_CLASSICAL_CIP_EQUILIBRIUM_REGIME";
            guidance = $"Covered interest parity holds within frictionless bounds ({basisSpreadBps:F1} bps). No cross-currency basis arbitrage available.";
            isProfitable = false;
        }
        else if (netSpreadBps <= 0.0)
        {
            regime = "BALANCE_SHEET_CONSTRAINED_UNPROFITABLE_ARBITRAGE";
            guidance = $"Basis spread ({basisSpreadBps:F1} bps) is consumed by SLR regulatory capital charges ({capitalCost:F1} bps). Arbitrage trade yields negative net ROIC.";
            isProfitable = false;
        }
        else
        {
            regime = "MODERATE_CROSS_CURRENCY_BASIS_WIDENING";
            guidance = $"Moderate basis spread ({basisSpreadBps:F1} bps). Net spread after capital charges is {netSpreadBps:F1} bps. Execute if repo collateral rates are favorable.";
        }

        return new CrossCurrencyFxBasisAnalysisResult(
            telemetry.CurrencyPair,
            Math.Round(cipForward, 5),
            Math.Round(basisSpreadBps, 2),
            Math.Round(netSpreadBps, 2),
            regime,
            guidance,
            isProfitable
        );
    }
}
