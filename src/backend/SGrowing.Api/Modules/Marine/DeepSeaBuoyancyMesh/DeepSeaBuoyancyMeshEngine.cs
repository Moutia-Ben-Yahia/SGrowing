namespace SGrowing.Api.Modules.Marine.DeepSeaBuoyancyMesh;

public interface IDeepSeaBuoyancyMeshEngine
{
    BuoyancyControlPlan EvaluateBuoyancy(DeepSeaBuoyancyRequest request);
}

public class DeepSeaBuoyancyMeshEngine : IDeepSeaBuoyancyMeshEngine
{
    private const double Gravity = 9.80665;
    private const double FoamBulkModulusBar = 35000.0; // 35 kbar bulk modulus for glass microsphere syntactic foam

    public BuoyancyControlPlan EvaluateBuoyancy(DeepSeaBuoyancyRequest request)
    {
        var tel = request.Telemetry;
        var depth = Math.Clamp(tel.CurrentDepthMeters, 0.0, 11500.0);

        // Hydrostatic pressure in bar: ~1 bar per 10 meters depth
        double pressureBar = (depth * 1.025 * Gravity) / 100.0;

        // In-situ seawater density via compressed equation of state
        // rho(z) = 1025 * (1 + 4.5e-5 * pressureBar)
        double densityKgM3 = 1025.0 * (1.0 + (4.5e-5 * pressureBar));

        // Syntactic foam volume under compression: V(P) = V0 * (1 - P / K)
        double foamCompressionFraction = Math.Min(0.06, pressureBar / FoamBulkModulusBar);
        double compressedFoamVolumeM3 = tel.SyntacticFoamVolumeM3 * (1.0 - foamCompressionFraction);

        // Total vehicle displaced volume in m^3
        // Includes hull volume (approx dry mass / 1800), compressed foam, and VBE oil bladder
        double titaniumHullVolumeM3 = (tel.SubmersibleDryMassKg * 0.45) / 4500.0; // Titanium grade 5 sphere
        double vbeVolumeM3 = tel.CurrentVbeDisplacementLiters / 1000.0;
        double totalDisplacedVolumeM3 = compressedFoamVolumeM3 + titaniumHullVolumeM3 + vbeVolumeM3;

        // Buoyancy force F_b = rho * V * g; Gravity force F_g = m * g
        double buoyantForceN = densityKgM3 * totalDisplacedVolumeM3 * Gravity;
        double weightForceN = tel.SubmersibleDryMassKg * Gravity;
        double netForceN = buoyantForceN - weightForceN;

        // VBE adjustment needed to achieve target vertical velocity or neutral trim (F_net = 0)
        // Delta V = -netForce / (rho * g) in m^3 -> convert to Liters
        double requiredVolumeDeltaM3 = -netForceN / (densityKgM3 * Gravity);
        double recommendedVbeDeltaLiters = requiredVolumeDeltaM3 * 1000.0;

        string trimState;
        string thrusterCommand;

        if (Math.Abs(netForceN) <= 250.0)
        {
            trimState = "NEUTRAL_HOVER";
            thrusterCommand = "THRUSTER_MINIMAL_TRIM: Neutral equilibrium reached. Maintain station-keeping.";
        }
        else if (netForceN < -250.0)
        {
            trimState = "HEAVY_SINKING";
            thrusterCommand = recommendedVbeDeltaLiters > 0
                ? $"PUMP_VBE_EXPAND: Pump {Math.Min(25.0, recommendedVbeDeltaLiters):F1} L hydraulic oil into external bladder to halt descent."
                : "VERTICAL_THRUST_UP: Engage vertical thrusters at 40% forward power.";
        }
        else
        {
            trimState = "POSITIVE_BUOYANT_RISING";
            thrusterCommand = recommendedVbeDeltaLiters < 0
                ? $"VENT_VBE_RETRACT: Vent {Math.Min(25.0, -recommendedVbeDeltaLiters):F1} L oil into internal reservoir for controlled descent."
                : "VERTICAL_THRUST_DOWN: Engage down-thrusters.";
        }

        double velocity = Math.Abs(tel.VerticalVelocityMps) > 0.05 ? Math.Abs(tel.VerticalVelocityMps) : 0.5;
        double timeMinutes = (request.UsblAcousticRangeMeters / velocity) / 60.0;

        return new BuoyancyControlPlan(
            VehicleId: tel.VehicleId,
            HydrostaticPressureBar: Math.Round(pressureBar, 1),
            InSituSeawaterDensityKgPerM3: Math.Round(densityKgM3, 1),
            NetBuoyancyForceNewtons: Math.Round(netForceN, 1),
            RecommendedVbeAdjustmentLiters: Math.Round(recommendedVbeDeltaLiters, 2),
            BuoyancyTrimState: trimState,
            ThrusterCommand: thrusterCommand,
            EstimatedTimeToTargetDepthMinutes: Math.Round(timeMinutes, 1)
        );
    }
}
