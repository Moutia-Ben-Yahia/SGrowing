namespace SGrowing.Api.Modules.Energy.LiquidMetalBlanketHartmannFlow;

public class LiquidMetalBlanketHartmannFlowEngine
{
    private const double LiquidMetalConductivitySigma = 7.5e5; // S/m for Pb-17Li
    private const double LiquidMetalViscosityMu = 1.5e-3;      // Pa*s for Pb-17Li

    public LiquidMetalMhdAnalysisResult AnalyzeBlanketChannel(LiquidMetalMhdTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.BlanketModuleId))
        {
            throw new ArgumentException("Blanket module ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.ToroidalMagneticFieldTesla <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Magnetic field must be positive.");
        }

        if (telemetry.ChannelHalfWidthMeters <= 0 || telemetry.ChannelLengthMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Channel dimensions must be positive.");
        }

        if (telemetry.LiquidMetalFlowVelocityMPerSec <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Flow velocity must be positive.");
        }

        if (telemetry.MaxAllowableStructuralPressureDropMpa <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Allowable pressure drop must be positive.");
        }

        // Hartmann Number: Ha = B * a * sqrt(sigma / mu)
        double sqrtSigmaOverMu = Math.Sqrt(LiquidMetalConductivitySigma / LiquidMetalViscosityMu);
        double hartmannNumber = telemetry.ToroidalMagneticFieldTesla * telemetry.ChannelHalfWidthMeters * sqrtSigmaOverMu;

        // Hartmann layer thickness: delta_H = a / Ha [microns]
        double deltaHMicrons = (telemetry.ChannelHalfWidthMeters / Math.Max(hartmannNumber, 1.0)) * 1e6;

        // Theoretical MHD Pressure Drop in thin conducting wall duct:
        // Delta_P_MHD = sigma * u_0 * B^2 * L * (c_w / (1 + c_w)) [Pa -> MPa]
        double cwFactor = telemetry.WallConductanceRatio / (1.0 + telemetry.WallConductanceRatio);
        double bSquared = Math.Pow(telemetry.ToroidalMagneticFieldTesla, 2);
        double predictedPressureDropMpa = (LiquidMetalConductivitySigma * telemetry.LiquidMetalFlowVelocityMPerSec * bSquared * telemetry.ChannelLengthMeters * cwFactor) * 1e-6;

        double safetyMarginPercent = ((telemetry.MaxAllowableStructuralPressureDropMpa - predictedPressureDropMpa) / telemetry.MaxAllowableStructuralPressureDropMpa) * 100.0;
        bool isPressureLimitExceeded = predictedPressureDropMpa >= telemetry.MaxAllowableStructuralPressureDropMpa || safetyMarginPercent <= 0.0;
        bool isTpbDegraded = telemetry.TritiumPermeationReductionFactor < 50.0;

        string status;
        string recommendation;
        string summary;

        if (isPressureLimitExceeded)
        {
            status = "CRITICAL_MHD_OVERPRESSURE_TRIP";
            recommendation = "EMERGENCY_FLOW_BYPASS_AND_FIELD_RAMP_COORDINATION";
            summary = $"CRITICAL MHD HAZARD: Lorentz drag induced {predictedPressureDropMpa:F2} MPa pressure drop, exceeding {telemetry.MaxAllowableStructuralPressureDropMpa:F2} MPa structural limit (margin {safetyMarginPercent:F1}%). High Hartmann flattening (Ha={hartmannNumber:F0}, delta_H={deltaHMicrons:F2} µm). Emergency bypass engaged.";
        }
        else if (safetyMarginPercent < 20.0)
        {
            status = "MHD_PRESSURE_THROTTLING_WARNING";
            recommendation = "THROTTLE_LIQUID_METAL_PUMP_SPEED_25_PERCENT";
            summary = $"WARNING: Elevated MHD pressure drop ({predictedPressureDropMpa:F2} MPa, margin {safetyMarginPercent:F1}%). Throttle liquid Pb-17Li flow velocity by 25% to preserve structural duct integrity.";
        }
        else
        {
            status = "NOMINAL_MHD_BLANKET_FLOW";
            recommendation = "MAINTAIN_NOMINAL_BREEDING_FLOW";
            summary = $"Nominal liquid metal breeding flow: Ha={hartmannNumber:F0}, pressure drop {predictedPressureDropMpa:F2} MPa with safe {safetyMarginPercent:F1}% margin. Tritium permeation barrier PRF={telemetry.TritiumPermeationReductionFactor:F0}.";
        }

        return new LiquidMetalMhdAnalysisResult(
            telemetry.BlanketModuleId,
            Math.Round(hartmannNumber, 1),
            Math.Round(deltaHMicrons, 2),
            Math.Round(predictedPressureDropMpa, 3),
            Math.Round(safetyMarginPercent, 1),
            isPressureLimitExceeded,
            isTpbDegraded,
            status,
            recommendation,
            summary);
    }
}
