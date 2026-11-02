namespace SGrowing.Api.Modules.Aerospace.ScramjetIsolatorShockTrain;

public class ScramjetIsolatorShockTrainEngine
{
    public ScramjetIsolatorAnalysisResult AnalyzeShockTrain(ScramjetIsolatorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.EngineId))
        {
            throw new ArgumentException("Engine ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.IsolatorInletMachNumber <= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Isolator inlet Mach number must be supersonic (> 1.0).");
        }

        if (telemetry.IsolatorDuctHeightMm <= 0 || telemetry.IsolatorDuctLengthMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Isolator dimensions must be positive.");
        }

        if (telemetry.InletStaticPressureKpa <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Inlet static pressure must be positive.");
        }

        // Pressure rise ratio across combustor
        double pressureRiseRatio = Math.Max(0.0, (telemetry.CombustorBackpressureKpa - telemetry.InletStaticPressureKpa) / telemetry.InletStaticPressureKpa);

        // Waltrup-Billig shock train length calculation (calibrated formulation):
        // S = 1.2 * H * sqrt(Delta_P / P_inlet) * sqrt(M_1^2 - 1) / (1 + theta / H)
        double machFactor = Math.Sqrt(Math.Pow(telemetry.IsolatorInletMachNumber, 2) - 1.0);
        double blRatio = telemetry.BoundaryLayerMomentumThicknessMm / telemetry.IsolatorDuctHeightMm;
        double shockTrainLength = 1.2 * telemetry.IsolatorDuctHeightMm * Math.Sqrt(pressureRiseRatio) * machFactor / (1.0 + blRatio);

        // Position from isolator entrance: Distance = L_isolator - S
        double leadingEdgePosition = telemetry.IsolatorDuctLengthMm - shockTrainLength;
        double unstartMarginPercent = (leadingEdgePosition / telemetry.IsolatorDuctLengthMm) * 100.0;

        bool isUnstartImminent = unstartMarginPercent < 15.0 || shockTrainLength >= telemetry.IsolatorDuctLengthMm;

        string regime;
        string actuation;
        string advisory;

        if (isUnstartImminent)
        {
            regime = "CRITICAL_INLET_UNSTART_SURGE";
            actuation = "RAPID_FUEL_CUT_AND_COWL_FLAP_DUMP";
            advisory = $"CRITICAL UNSTART HAZARD: Combustor backpressure forced shock train to {shockTrainLength:F1} mm, leaving only {unstartMarginPercent:F1}% margin from inlet lip. Commanding 40% fuel cut and cowl bleed dump within <5 ms to prevent unstart.";
        }
        else if (unstartMarginPercent < 35.0)
        {
            regime = "ISOLATOR_SHOCK_CONFINEMENT";
            actuation = "ACTIVATE_BOUNDARY_LAYER_BLEED";
            advisory = $"WARNING: Elevated shock train upstream penetration ({shockTrainLength:F1} mm, margin {unstartMarginPercent:F1}%). High-pressure boundary layer bleed valves opened to fix shock foot position.";
        }
        else
        {
            regime = "SUPERCONDUCTING_RAMJET_NOMINAL";
            actuation = "MAINTAIN_CRUISE_FUEL_FLOW";
            advisory = $"Nominal supersonic isolator flow: Shock train contained at {shockTrainLength:F1} mm with safe {unstartMarginPercent:F1}% unstart margin.";
        }

        return new ScramjetIsolatorAnalysisResult(
            telemetry.EngineId,
            Math.Round(shockTrainLength, 1),
            Math.Round(leadingEdgePosition, 1),
            Math.Round(unstartMarginPercent, 1),
            isUnstartImminent,
            regime,
            actuation,
            advisory);
    }
}
