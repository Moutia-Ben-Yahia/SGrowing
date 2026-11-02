using System;

namespace SGrowing.Api.Modules.Aerospace.HypersonicScramjetIsolatorShockTrain;

public sealed class HypersonicScramjetIsolatorShockTrainEngine : IHypersonicScramjetIsolatorShockTrainEngine
{
    public HypersonicScramjetAnalysisResult AnalyzeIsolatorShockTrain(HypersonicScramjetTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.ScramjetEngineId))
        {
            throw new ArgumentException("Scramjet engine ID must be provided.", nameof(telemetry));
        }

        if (telemetry.IsolatorHydraulicDiameterMeters <= 0 || telemetry.IsolatorLengthMeters <= 0 ||
            telemetry.IsolatorInletStaticPressureKilopascals <= 0 || telemetry.FanningFrictionFactor <= 0)
        {
            throw new ArgumentException("Dimensions, inlet pressure, and friction factor must be positive.", nameof(telemetry));
        }

        var backpressureRatio = telemetry.CombustorBackpressureKilopascals / telemetry.IsolatorInletStaticPressureKilopascals;
        var pDiff = Math.Max(0.0, backpressureRatio - 1.0);

        // Waltrup-Billig shock train length correlation:
        // L_st = D * (1 / sqrt(f)) * [1.05 * (P_out/P_in - 1) + 0.075 * (P_out/P_in - 1)^2]
        var invSqrtF = 1.0 / Math.Sqrt(telemetry.FanningFrictionFactor);
        var shockTrainLength = telemetry.IsolatorHydraulicDiameterMeters * invSqrtF * ((1.05 * pDiff) + (0.075 * pDiff * pDiff));

        var unstartMarginMeters = telemetry.IsolatorLengthMeters - shockTrainLength;
        var unstartMarginPercent = (unstartMarginMeters / telemetry.IsolatorLengthMeters) * 100.0;

        string regime;
        string guidance;
        bool isStable = unstartMarginPercent >= 20.0 &&
                        telemetry.IsolatorInletMachNumber >= 1.5 &&
                        pDiff > 0.2;

        if (telemetry.CombustorBackpressureKilopascals <= telemetry.IsolatorInletStaticPressureKilopascals)
        {
            regime = "ZERO_BACKPRESSURE_DEFLAGRATION_BLOWOUT";
            guidance = "No adverse pressure gradient detected (P_out <= P_in). Combustion flameout occurred; ramp up fuel staging injector flow.";
            isStable = false;
        }
        else if (unstartMarginMeters <= 0.0)
        {
            regime = "CATASTROPHIC_INLET_UNSTART_SHOCK_DISGORGEMENT_HAZARD";
            guidance = $"Shock train length ({shockTrainLength:F2} m) exceeds isolator duct length ({telemetry.IsolatorLengthMeters:F2} m). Shock disgorging into supersonic inlet cowl; immediately throttle back combustor fuel flow!";
            isStable = false;
        }
        else if (unstartMarginPercent < 15.0)
        {
            regime = "HIGH_UNSTART_RISK_SHOCK_TRAIN_APPROACHING_INLET_THROAT";
            guidance = $"Shock train margin is dangerously narrow ({unstartMarginPercent:F1}% remaining, {unstartMarginMeters:F2} m). Boundary layer separation imminent; trim fuel equivalence ratio.";
            isStable = false;
        }
        else if (unstartMarginPercent >= 35.0 && telemetry.IsolatorInletMachNumber >= 1.8)
        {
            regime = "STABLE_SUPERSONIC_ISOLATOR_SHOCK_CONTAINED_REGIME";
            guidance = $"Shock train stably contained within isolator ({shockTrainLength:F2} m vs {telemetry.IsolatorLengthMeters:F2} m, margin = {unstartMarginPercent:F1}%). High combustion pressure recovery without inlet interaction.";
        }
        else
        {
            regime = "MARGINAL_SWBLI_EQUILIBRIUM_REGIME";
            guidance = $"Shock train equilibrium maintained. Backpressure ratio is {backpressureRatio:F2}. Continue monitoring wall static pressure transducers.";
        }

        return new HypersonicScramjetAnalysisResult(
            telemetry.ScramjetEngineId,
            Math.Round(backpressureRatio, 2),
            Math.Round(shockTrainLength, 3),
            Math.Round(unstartMarginMeters, 3),
            Math.Round(unstartMarginPercent, 1),
            regime,
            guidance,
            isStable
        );
    }
}
