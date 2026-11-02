namespace SGrowing.Api.Modules.Energy.HtsRebcoCableDelamination;

public class HtsRebcoCableDelaminationEngine
{
    private const double QuenchVoltageThresholdMv = 100.0;
    private const double QuenchDurationThresholdMs = 15.0;

    public HtsRebcoCableAnalysisResult AnalyzeCable(HtsRebcoCableTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.OperatingCurrentAmps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Operating current must be positive.");
        }

        if (telemetry.NumberOfTapesInConductor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Number of tapes must be greater than zero.");
        }

        if (telemetry.TapeWidthMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Tape width must be positive.");
        }

        if (telemetry.CriticalInterfacialYieldStrengthMpa <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Critical yield strength must be positive.");
        }

        // Current per tape
        double currentPerTape = telemetry.OperatingCurrentAmps / telemetry.NumberOfTapesInConductor;

        // Lorentz force per unit length: F_L = I_tape * B * sin(theta) [N/m]
        double thetaRad = telemetry.MagneticFieldAngleDegrees * Math.PI / 180.0;
        double sinTheta = Math.Abs(Math.Sin(thetaRad));
        double lorentzForcePerMeter = currentPerTape * telemetry.PeakMagneticFieldTesla * sinTheta;

        // Transverse peel stress in MPa: F_L / w_tape [N / (m * m)] * 1e-6 = N / (mm * m * 1000)
        // w_tape in mm -> w_tape * 1e-3 m. Stress [Pa] = F_L / (w_tape * 1e-3). Stress [MPa] = Stress [Pa] / 1e6 = F_L / (w_tape * 1000.0).
        double lorentzStressMpa = lorentzForcePerMeter / (telemetry.TapeWidthMm * 1000.0);

        // Delamination safety margin
        double safetyMarginPercent = ((telemetry.CriticalInterfacialYieldStrengthMpa - lorentzStressMpa) / telemetry.CriticalInterfacialYieldStrengthMpa) * 100.0;

        bool isDelaminationRisk = safetyMarginPercent < 25.0;
        bool isQuenchRunaway = telemetry.MeasuredTapVoltageMilliVolts >= QuenchVoltageThresholdMv && telemetry.VoltageGrowthDurationMs >= QuenchDurationThresholdMs;

        string operatingState;
        string recommendedAction;
        string summary;

        if (isQuenchRunaway)
        {
            operatingState = "CRITICAL_QUENCH_TRIP";
            recommendedAction = "FIRE_FAST_EXTRACTION_DUMP_RESISTOR";
            summary = $"Quench runaway detected! Tap voltage reached {telemetry.MeasuredTapVoltageMilliVolts:F1} mV for {telemetry.VoltageGrowthDurationMs:F1} ms. Dump circuit must be triggered to avoid localized thermal burnout.";
        }
        else if (safetyMarginPercent < 10.0)
        {
            operatingState = "IMMINENT_MECHANICAL_FAILURE";
            recommendedAction = "EMERGENCY_MAGNETIC_RAMP_DOWN";
            summary = $"Severe delamination risk! Transverse Lorentz peel stress {lorentzStressMpa:F2} MPa exceeds safety threshold (margin {safetyMarginPercent:F1}%). Immediate ramp-down of power supply required.";
        }
        else if (isDelaminationRisk)
        {
            operatingState = "DELAMINATION_WARNING";
            recommendedAction = "CURRENT_THROTTLING_RECOMMENDED";
            summary = $"Moderate mechanical stress: Lorentz peel stress {lorentzStressMpa:F2} MPa leaves only {safetyMarginPercent:F1}% safety margin. Recommend throttling transport current by 20%.";
        }
        else
        {
            operatingState = "NOMINAL_SUPERCONDUCTING";
            recommendedAction = "CONTINUE_SUPERCONDUCTING_OPERATION";
            summary = $"Cable operating nominally in superconducting state. Lorentz stress {lorentzStressMpa:F2} MPa with robust {safetyMarginPercent:F1}% margin against delamination.";
        }

        return new HtsRebcoCableAnalysisResult(
            telemetry.CableId,
            Math.Round(lorentzStressMpa, 3),
            Math.Round(safetyMarginPercent, 2),
            isDelaminationRisk,
            isQuenchRunaway,
            operatingState,
            recommendedAction,
            summary);
    }
}
