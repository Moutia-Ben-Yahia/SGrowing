using System;

namespace SGrowing.Api.Modules.Aerospace.HypersonicSkipGlideTrajectory;

public interface IHypersonicSkipGlideTrajectoryEngine
{
    HypersonicSkipGlideResult AnalyzeTrajectoryEnvelope(HypersonicSkipGlideTelemetry telemetry);
}

public sealed class HypersonicSkipGlideTrajectoryEngine : IHypersonicSkipGlideTrajectoryEngine
{
    private const double StefanBoltzmannConstant = 5.670374419e-8;
    private const double NoseEmissivity = 0.85;
    private const double StandardGravity = 9.80665;

    public HypersonicSkipGlideResult AnalyzeTrajectoryEnvelope(HypersonicSkipGlideTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.VehicleMassKg <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Vehicle mass must be strictly positive.");
        }

        double noseRadius = Math.Max(0.01, telemetry.NoseRadiusMeters);
        double density = Math.Max(1e-9, telemetry.AtmosphericDensityKgPerM3);
        double velocity = Math.Max(100.0, telemetry.VelocityMPerSec);

        // Chapman convective heat flux: q_dot = 1.83e-4 * sqrt(rho / Rn) * v^3 (W/m^2)
        double heatFluxWattsPerM2 = 1.83e-4 * Math.Sqrt(density / noseRadius) * Math.Pow(velocity, 3.0);
        double heatFluxMegawattsPerM2 = heatFluxWattsPerM2 / 1_000_000.0;

        // Radiative equilibrium surface temperature: T_w = (q_dot / (eps * sigma))^(1/4)
        double tempKelvin = Math.Pow(heatFluxWattsPerM2 / (NoseEmissivity * StefanBoltzmannConstant), 0.25);
        double calculatedTempCelsius = tempKelvin - 273.15;
        double effectiveTempCelsius = Math.Max(calculatedTempCelsius, telemetry.TpsNoseCapTemperatureCelsius);

        // Aerodynamics: dynamic pressure, lift, drag, load factor
        double dynamicPressure = 0.5 * density * Math.Pow(velocity, 2.0);
        double refArea = Math.Max(0.1, telemetry.ReferenceAreaM2);
        double liftForceNewtons = dynamicPressure * refArea * telemetry.AerodynamicLiftCoefficient;
        double dragCoefficient = Math.Max(0.01, telemetry.AerodynamicDragCoefficient);
        double liftToDragRatio = telemetry.AerodynamicLiftCoefficient / dragCoefficient;

        double normalLoadFactorG = liftForceNewtons / (telemetry.VehicleMassKg * StandardGravity);

        string regime;
        string guidance;
        bool isSafe;

        if (effectiveTempCelsius > 1900.0 || heatFluxMegawattsPerM2 > 4.5)
        {
            regime = "TPS_THERMAL_SOAK_EXCEEDED";
            guidance = "EXECUTE_HIGH_ALTITUDE_PULL_UP_AND_EXTEND_SKIP_APOGEE";
            isSafe = false;
        }
        else if (normalLoadFactorG > 7.5)
        {
            regime = "PULL_UP_G_FORCE_WARNING";
            guidance = "TRIM_ELEVONS_TO_REDUCE_ANGLE_OF_ATTACK_3_DEGREES";
            isSafe = false;
        }
        else
        {
            regime = "NOMINAL_SKIP_GLIDE_TRAJECTORY";
            guidance = "MAINTAIN_MAX_LIFT_OVER_DRAG_CRUISE_ATTITUDE";
            isSafe = true;
        }

        return new HypersonicSkipGlideResult(
            telemetry.VehicleId,
            Math.Round(heatFluxMegawattsPerM2, 3),
            Math.Round(effectiveTempCelsius, 1),
            Math.Round(normalLoadFactorG, 2),
            Math.Round(liftToDragRatio, 2),
            regime,
            guidance,
            isSafe,
            DateTimeOffset.UtcNow);
    }
}
