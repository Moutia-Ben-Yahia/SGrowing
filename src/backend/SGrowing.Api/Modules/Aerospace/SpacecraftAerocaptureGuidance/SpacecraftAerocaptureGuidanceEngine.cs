namespace SGrowing.Api.Modules.Aerospace.SpacecraftAerocaptureGuidance;

public interface ISpacecraftAerocaptureGuidanceEngine
{
    AerocaptureGuidanceDecision ComputeGuidance(AerocaptureGuidanceRequest request);
}

public class SpacecraftAerocaptureGuidanceEngine : ISpacecraftAerocaptureGuidanceEngine
{
    public AerocaptureGuidanceDecision ComputeGuidance(AerocaptureGuidanceRequest request)
    {
        var state = request.State;
        var atmo = request.Atmosphere;
        var limits = request.CorridorLimits;

        // Current atmospheric density: rho(h) = rho0 * exp(-h / H)
        double altKm = Math.Max(0.0, state.AltitudeKm);
        double density = atmo.SurfaceDensityKgPerM3 * Math.Exp(-altKm / Math.Max(1.0, atmo.ScaleHeightKm));

        // Aerodynamic stagnation heat flux approximation: q_dot ~ k * sqrt(rho) * v^3
        double vKmS = Math.Max(1.0, state.VelocityKmPerSec);
        double peakHeatFlux = 1.75e-4 * Math.Sqrt(Math.Max(1e-8, density)) * Math.Pow(vKmS * 1000.0, 3.0) / 10000.0; // W/cm^2

        // Aerodynamic deceleration G-load: a_drag = (0.5 * rho * v^2) / ballistic_coeff
        // Approx ballistic coeff beta ~ 150 kg/m2
        double dynamicPressureKPa = 0.5 * density * (vKmS * 1000.0) * (vKmS * 1000.0) / 1000.0;
        double decelerationGs = (dynamicPressureKPa * 1000.0 / 150.0) / 9.80665;

        // Predictor-Corrector apoapsis projection based on flight-path angle gamma and bank angle sigma:
        // Steeper gamma (more negative) penetrates deeper into atmosphere, shedding more energy, resulting in lower apoapsis
        // Lift vector down (sigma ~ 180) holds vehicle down in denser air; Lift vector up (sigma ~ 0) pulls vehicle up to skip out earlier
        double gammaRad = state.FlightPathAngleDegrees * Math.PI / 180.0;
        double currentBankRad = state.CurrentBankAngleDegrees * Math.PI / 180.0;

        // Energy dissipation factor: Delta V_loss
        double baseLoss = (Math.Abs(gammaRad) - 0.05) * 1.5;
        double densityTerm = density * 20.0;
        double liftVectorEffect = -0.05 * Math.Cos(currentBankRad);
        double energyLossFraction = Math.Clamp(baseLoss + densityTerm + liftVectorEffect, 0.02, 0.75);
        double exitVelocityKmS = vKmS * (1.0 - energyLossFraction);

        // Circular orbit velocity: v_circ = sqrt(mu / (R + h_exit))
        double exitRadiusKm = atmo.PlanetaryRadiusKm + 125.0; // nominal atmospheric interface
        double vCircKmS = Math.Sqrt(atmo.GravitationalParamMuKm3S2 / exitRadiusKm);

        // Vis-viva projected apoapsis altitude:
        // r_a = (2 * mu * r_p) / (2 * mu - r_p * v_p^2) - R
        double specificEnergy = (0.5 * exitVelocityKmS * exitVelocityKmS) - (atmo.GravitationalParamMuKm3S2 / exitRadiusKm);
        double projectedApoapsisKm;

        if (specificEnergy >= 0)
        {
            projectedApoapsisKm = 99999.0; // Hyperbolic escape (skipped out!)
        }
        else
        {
            double semiMajorAxisKm = -atmo.GravitationalParamMuKm3S2 / (2.0 * specificEnergy);
            double periapsisKm = exitRadiusKm * 0.98;
            projectedApoapsisKm = (2.0 * semiMajorAxisKm - periapsisKm) - atmo.PlanetaryRadiusKm;
            projectedApoapsisKm = Math.Max(150.0, projectedApoapsisKm);
        }

        // Closed-loop bank angle correction to hit target apoapsis
        double apoapsisErrorKm = projectedApoapsisKm - limits.TargetApoapsisKm;
        double recommendedBankAngle;
        string guidanceMode;
        string directive;

        if (apoapsisErrorKm > 250.0)
        {
            // Apoapsis too high (skipping out): roll lift-vector DOWN (towards 140-160 deg) to dive deeper and shed energy
            recommendedBankAngle = 145.0;
            guidanceMode = "LIFT_DOWN_DIVE_PREVENT";
            directive = "ROLL_LIFT_DOWN: Projected apoapsis too high (escape risk). Roll lift-vector to 145 deg to increase atmospheric energy dissipation.";
        }
        else if (apoapsisErrorKm < -250.0)
        {
            // Apoapsis too low (diving too deep): roll lift-vector UP (towards 25-45 deg) to pull out
            recommendedBankAngle = 35.0;
            guidanceMode = "LIFT_UP_SKIP_PREVENT";
            directive = "ROLL_LIFT_UP: Projected apoapsis too low (over-deceleration risk). Roll lift-vector to 35 deg to ascend toward upper corridor boundary.";
        }
        else
        {
            // Nominal corridor tracking: bank reversal to balance cross-range while maintaining vertical lift component
            recommendedBankAngle = state.CurrentBankAngleDegrees >= 0 ? 65.0 : -65.0;
            guidanceMode = "BANK_REVERSAL_NOMINAL";
            directive = $"CORRIDOR_CONVERGED: Apoapsis within +/- 250 km of target. Maintain {recommendedBankAngle:F0} deg bank angle for cross-range nulling.";
        }

        bool corridorSatisfied = Math.Abs(apoapsisErrorKm) <= 500.0 &&
                                 peakHeatFlux <= limits.MaxAllowableHeatFluxWattsPerCm2 &&
                                 decelerationGs <= limits.MaxAllowableDecelerationGs;

        return new AerocaptureGuidanceDecision(
            VehicleId: state.VehicleId,
            TargetBody: atmo.TargetBody,
            RecommendedBankAngleDegrees: Math.Round(recommendedBankAngle, 1),
            ProjectedApoapsisKm: Math.Round(projectedApoapsisKm, 1),
            PeakHeatFluxWattsPerCm2: Math.Round(peakHeatFlux, 1),
            PeakDecelerationGs: Math.Round(decelerationGs, 2),
            CorridorSatisfied: corridorSatisfied,
            GuidanceMode: guidanceMode,
            ManeuverDirective: directive
        );
    }
}
