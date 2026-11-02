namespace SGrowing.Api.Modules.Energy.CryoFusionPelletExtruder;

public interface ICryoFusionPelletExtrusionEngine
{
    PelletExtrusionAssessment EvaluateExtrusion(PelletExtrusionEvaluationRequest request);
}

public class CryoFusionPelletExtrusionEngine : ICryoFusionPelletExtrusionEngine
{
    private const double DTIceDensityGramsPerCm3 = 0.25; // Cryogenic solid 50-50 D-T ice

    public PelletExtrusionAssessment EvaluateExtrusion(PelletExtrusionEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var lim = request.Limits;

        // 1. Single pellet volume and mass in grams:
        // V = pi * r^2 * h in cm^3
        double radiusCm = (tel.PelletDiameterMm / 10.0) / 2.0;
        double lengthCm = tel.PelletLengthMm / 10.0;
        double pelletVolumeCm3 = Math.PI * Math.Pow(radiusCm, 2.0) * lengthCm;
        double singlePelletMassGrams = pelletVolumeCm3 * DTIceDensityGramsPerCm3;

        // Total fueling rate in g/s = mass * frequency
        double fuelingRateGramsPerSec = singlePelletMassGrams * Math.Max(1.0, tel.InjectionFrequencyHertz);

        // 2. Neutral Gas Shielding (NGS) ablation penetration depth past magnetic separatrix:
        // lambda ~ k * (v / 1000)^0.44 * (r_p)^1.33 in meters
        double velocityFactor = Math.Pow(Math.Max(100.0, tel.GasGunMuzzleVelocityMPerSec) / 1000.0, 0.44);
        double radiusFactor = Math.Pow(tel.PelletDiameterMm / 2.0, 1.33) * 0.15;
        double penetrationDepthMeters = Math.Clamp(velocityFactor * radiusFactor, 0.05, 0.85);

        // 3. Operational risks evaluation:
        bool thermalSublimationRisk = tel.IceBarrelTemperatureKelvin >= lim.MaxAllowableBarrelTemperatureKelvin;
        bool mechanicalJamRisk = tel.ExtrusionPressureMegaPascals >= lim.MaxAllowableExtrusionPressureMpa;

        string regime;
        string directive;

        if (thermalSublimationRisk)
        {
            regime = "BARREL_THERMAL_SUBLIMATION_DANGER";
            directive = $"BOOST_LIQUID_HELIUM_COOLING: Barrel temperature ({tel.IceBarrelTemperatureKelvin:F1} K) exceeds {lim.MaxAllowableBarrelTemperatureKelvin:F1} K limit near D2 triple point. Increase supercritical helium cooling loop flow by 40% to prevent cryogenic ice liquefaction.";
        }
        else if (mechanicalJamRisk)
        {
            regime = "EXTRUSION_NOZZLE_JAM_CRITICAL";
            directive = $"THROTTLE_EXTRUSION_DRIVE: Extrusion hydraulic pressure ({tel.ExtrusionPressureMegaPascals:F1} MPa) exceeds {lim.MaxAllowableExtrusionPressureMpa:F1} MPa nozzle yield limit. Reduce extrusion ram velocity by 35% to prevent solid ice plug compaction.";
        }
        else
        {
            regime = "STEADY_CRYOGENIC_FUELING";
            directive = $"NOMINAL_PACING: Cryogenic D-T ice extrusion stable. Muzzle velocity ({tel.GasGunMuzzleVelocityMPerSec:F0} m/s) delivers {penetrationDepthMeters:F2}m separatrix penetration depth at {tel.InjectionFrequencyHertz:F0} Hz ELM pacing cadence.";
        }

        return new PelletExtrusionAssessment(
            ExtruderId: tel.ExtruderId,
            DeuteriumTritiumFuelingRateGramsPerSec: Math.Round(fuelingRateGramsPerSec, 4),
            EstimatedSeparatrixPenetrationDepthMeters: Math.Round(penetrationDepthMeters, 3),
            CryogenicThermalSublimationRiskDetected: thermalSublimationRisk,
            MechanicalJammingRiskDetected: mechanicalJamRisk,
            ExtruderOperationalRegime: regime,
            RecommendedCryostatControlDirective: directive
        );
    }
}
