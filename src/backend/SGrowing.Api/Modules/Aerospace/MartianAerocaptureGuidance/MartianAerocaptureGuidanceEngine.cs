using System;

namespace SGrowing.Api.Modules.Aerospace.MartianAerocaptureGuidance;

public interface IMartianAerocaptureGuidanceEngine
{
    MartianAerocaptureAnalysisResult AnalyzeAerocaptureTrajectory(MartianAerocaptureTelemetry telemetry);
}

public sealed class MartianAerocaptureGuidanceEngine : IMartianAerocaptureGuidanceEngine
{
    private const double MarsGravitationalParameter = 4.282837e13; // m^3 / s^2
    private const double MarsRadiusKm = 3389.5;

    public MartianAerocaptureAnalysisResult AnalyzeAerocaptureTrajectory(MartianAerocaptureTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.SpacecraftMassKg <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Spacecraft mass must be strictly positive.");
        }

        double dragArea = Math.Max(0.1, telemetry.AerodynamicReferenceAreaM2 * telemetry.DragCoefficientCd);
        double ballisticCoefficient = telemetry.SpacecraftMassKg / dragArea;

        // Lift-vector bank modulation: cos(bank) > 0 -> lift up (less drag), cos(bank) < 0 -> lift down (more drag)
        double bankRad = telemetry.CurrentBankAngleDegrees * (Math.PI / 180.0);
        double liftModulation = Math.Cos(bankRad);

        // Approximate integrated velocity dissipation across Martian atmospheric pass
        double dustFactor = Math.Clamp(telemetry.AtmosphericDustFactor, 0.0, 1.5);
        double deltaV = 1100.0 * (140.0 / ballisticCoefficient) * (1.0 + (0.4 * dustFactor)) * (1.0 - (0.20 * liftModulation));
        double projectedExitVelocity = Math.Max(2500.0, telemetry.InertialVelocityMPerSec - deltaV);

        // Orbit calculation from exit state (assuming atmospheric exit altitude around 120 km)
        double rExitMeters = (MarsRadiusKm + 120.0) * 1000.0;
        double specificOrbitalEnergy = (Math.Pow(projectedExitVelocity, 2.0) / 2.0) - (MarsGravitationalParameter / rExitMeters);

        double projectedApoapsisKm;
        if (specificOrbitalEnergy >= 0.0)
        {
            projectedApoapsisKm = 99999.0; // Hyperbolic escape
        }
        else
        {
            double semiMajorAxisMeters = -MarsGravitationalParameter / (2.0 * specificOrbitalEnergy);
            double rApoapsisMeters = (2.0 * semiMajorAxisMeters) - rExitMeters;
            projectedApoapsisKm = (rApoapsisMeters / 1000.0) - MarsRadiusKm;
        }

        string status;
        string guidanceCommand;
        bool isCaptureGuaranteed;

        if (projectedExitVelocity >= 4950.0 || projectedApoapsisKm > (telemetry.TargetApoapsisAltitudeKm + 2500.0))
        {
            status = "CORRIDOR_UNDERSHOOT_SKIP_RISK";
            guidanceCommand = "ROLL_BANK_ANGLE_TO_LIFT_DOWN_FULL_DIVE";
            isCaptureGuaranteed = false;
        }
        else if (telemetry.MeasuredDecelerationG > 4.5 || projectedApoapsisKm < 1500.0)
        {
            status = "PEAK_DECELERATION_G_LOAD_WARNING";
            guidanceCommand = "ROLL_BANK_ANGLE_TO_LIFT_UP_PULL_OUT";
            isCaptureGuaranteed = true;
        }
        else
        {
            status = "AEROCAPTURE_TRAJECTORY_NOMINAL";
            guidanceCommand = "MAINTAIN_EQUILIBRIUM_GLIDE_BANK_MODULATION";
            isCaptureGuaranteed = true;
        }

        return new MartianAerocaptureAnalysisResult(
            telemetry.SpacecraftId,
            Math.Round(ballisticCoefficient, 2),
            Math.Round(projectedExitVelocity, 1),
            Math.Round(projectedApoapsisKm, 1),
            status,
            guidanceCommand,
            isCaptureGuaranteed,
            DateTimeOffset.UtcNow);
    }
}
