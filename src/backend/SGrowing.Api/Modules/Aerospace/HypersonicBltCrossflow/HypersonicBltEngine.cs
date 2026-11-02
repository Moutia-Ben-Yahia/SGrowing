namespace SGrowing.Api.Modules.Aerospace.HypersonicBltCrossflow;

public class HypersonicBltEngine
{
    public HypersonicBltAnalysisResult EvaluateBoundaryLayer(HypersonicBltTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.MachNumber < 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Mach number must be at least supersonic (>= 1.0).");
        }

        if (telemetry.ThermalProtectionTileLimitKwM2 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Thermal protection limit must be positive.");
        }

        if (telemetry.SurfaceLaminarHeatFluxKwM2 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Surface laminar heat flux must be positive.");
        }

        string flowRegime;
        double turbulentMultiplier;

        bool isFullyTurbulent = telemetry.PressureFluctuationRmsRatio >= 0.12 ||
                                (telemetry.CrossflowReynoldsNumber >= 350.0 && telemetry.PressureFluctuationRmsRatio >= 0.08);

        bool isTransitionOnset = telemetry.PressureFluctuationRmsRatio >= 0.05 ||
                                 telemetry.CrossflowReynoldsNumber >= 220.0;

        if (isFullyTurbulent)
        {
            flowRegime = "FULLY_DEVELOPED_TURBULENT";
            turbulentMultiplier = Math.Clamp(4.0 + (telemetry.MachNumber - 6.0) * 0.2, 3.5, 6.5);
        }
        else if (isTransitionOnset)
        {
            flowRegime = "TRANSITION_ONSET";
            turbulentMultiplier = 1.0 + Math.Min(2.5, (telemetry.PressureFluctuationRmsRatio / 0.12) * 2.5);
        }
        else
        {
            flowRegime = "LAMINAR_FLOW";
            turbulentMultiplier = 1.0;
        }

        double predictedHeatFlux = telemetry.SurfaceLaminarHeatFluxKwM2 * turbulentMultiplier;
        double safetyMarginPercent = ((telemetry.ThermalProtectionTileLimitKwM2 - predictedHeatFlux) / telemetry.ThermalProtectionTileLimitKwM2) * 100.0;
        bool isOverheatRisk = safetyMarginPercent < 15.0;

        string activeFlowControl;
        string advisory;

        if (isOverheatRisk || isFullyTurbulent)
        {
            activeFlowControl = "DEPLOY_MICRO_VORTEX_GENERATORS_AND_COOLING";
            advisory = $"CRITICAL AEROTHERMAL ALERT: Heat flux surged to {predictedHeatFlux:F1} kW/m² ({turbulentMultiplier:F2}x laminar) with only {safetyMarginPercent:F1}% tile margin. Micro-vortex generators and active leading-edge transpiration cooling engaged.";
        }
        else if (isTransitionOnset)
        {
            activeFlowControl = "ACTIVATE_BOUNDARY_LAYER_SUCTION";
            advisory = $"TRANSITION WARNING: Boundary layer instability detected at {telemetry.AcousticSensorFrequencyKhz:F0} kHz (Mack 2nd mode / crossflow Rcf={telemetry.CrossflowReynoldsNumber:F0}). Active suction panels activated to delay turbulence.";
        }
        else
        {
            activeFlowControl = "NONE_MAINTAIN_CRUISE";
            advisory = $"NOMINAL LAMINAR FLOW: Acoustic pressure fluctuation {telemetry.PressureFluctuationRmsRatio:P1} within laminar envelope. Robust {safetyMarginPercent:F1}% thermal margin.";
        }

        return new HypersonicBltAnalysisResult(
            telemetry.VehicleId,
            flowRegime,
            Math.Round(turbulentMultiplier, 2),
            Math.Round(predictedHeatFlux, 1),
            Math.Round(safetyMarginPercent, 1),
            isOverheatRisk,
            activeFlowControl,
            advisory);
    }
}
