using System;

namespace SGrowing.Api.Modules.Transportation.HyperloopKantrowitzTubeAerodynamics;

public sealed class HyperloopKantrowitzTubeAerodynamicsEngine : IHyperloopKantrowitzTubeAerodynamicsEngine
{
    public HyperloopTubeAnalysisResult AnalyzeTubeAerodynamics(HyperloopTubeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        const double gamma = 1.4;
        const double gasConstantR = 287.05;

        var tempK = Math.Max(100.0, telemetry.TubeTemperatureKelvin);
        var speedOfSound = Math.Sqrt(gamma * gasConstantR * tempK);
        var podVelocity = Math.Max(0.0, telemetry.PodVelocityMetersPerSecond);
        var podMach = podVelocity / speedOfSound;

        var aTube = Math.Max(1.0, telemetry.TubeCrossSectionalAreaSquareMeters);
        var aPod = Math.Clamp(telemetry.PodFrontalAreaSquareMeters, 0.1, aTube * 0.95);
        var blockageRatio = aPod / aTube;

        // Effective blockage ratio with nose compressor suction
        var effBlockage = blockageRatio;
        if (telemetry.CompressorBypassActive)
        {
            var massFlow = Math.Max(0.0, telemetry.CompressorMassFlowRateKgPerSec);
            var reliefFactor = Math.Clamp(1.0 - 0.035 * massFlow, 0.35, 1.0);
            effBlockage *= reliefFactor;
        }

        // Kantrowitz Mach limit estimation for choked flow
        var bypassFraction = Math.Clamp(1.0 - effBlockage, 0.05, 0.95);
        var kantrowitzMach = Math.Pow(bypassFraction / (1.0 + 0.25 * effBlockage), 1.25) * 0.98;

        // Air density in low pressure tube (ideal gas: rho = P / (R * T))
        var pressurePa = Math.Max(10.0, telemetry.TubeStaticPressurePascals);
        var airDensity = pressurePa / (gasConstantR * tempK);

        // Dynamic pressure & baseline drag force (N)
        var qDyn = 0.5 * airDensity * Math.Pow(podVelocity, 2);
        var cd = Math.Max(0.05, telemetry.PodDragCoefficient);
        var baseDragNewtons = qDyn * cd * aPod;

        // Choking piston drag amplification
        var chokingDelta = Math.Max(0.0, podMach - kantrowitzMach);
        double chokingMultiplier;
        if (chokingDelta > 0.0)
        {
            chokingMultiplier = telemetry.CompressorBypassActive
                ? 1.0 + 2.5 * chokingDelta
                : 1.0 + 14.0 * chokingDelta;
        }
        else
        {
            chokingMultiplier = 1.0;
        }

        var totalDragKiloNewtons = (baseDragNewtons * chokingMultiplier) / 1000.0;

        string regime;
        string guidance;
        bool isStable;

        if (podMach > kantrowitzMach && !telemetry.CompressorBypassActive)
        {
            regime = "KANTROWITZ_CHOKING_PISTON_SHOCK_WAVE";
            guidance = "Flow around capsule choked at sonic bypass threshold. Extreme bow shock and piston drag force surge. Deploy nose compressor ingestion and trim vehicle velocity.";
            isStable = false;
        }
        else if (telemetry.TubeStaticPressurePascals > 1500.0)
        {
            regime = "TUBE_VACUUM_DEPRESSURIZATION_DEGRADATION";
            guidance = "Tube vacuum compromised (> 1500 Pa). High gas density multiplies viscous skin friction and aerodynamic thermal heating. Engage auxiliary turbomolecular vacuum pump banks.";
            isStable = false;
        }
        else if (totalDragKiloNewtons > 3.0)
        {
            regime = "HIGH_AERODYNAMIC_DRAG_MARGIN_WARNING";
            guidance = "Aerodynamic drag exceeds propulsion thrust vector margin (3.0 kN). Modulate linear synchronous motor (LSM) acceleration profile.";
            isStable = false;
        }
        else
        {
            regime = "SUB_CHOKING_OPTIMAL_BYPASS_AERODYNAMICS";
            guidance = "Capsule operating safely below Kantrowitz choking boundary with active boundary layer bypass. Aerodynamic drag within cruising envelope.";
            isStable = true;
        }

        return new HyperloopTubeAnalysisResult(
            PodId: telemetry.PodId,
            PodMachNumber: podMach,
            KantrowitzMachLimit: kantrowitzMach,
            BlockageRatio: blockageRatio,
            AerodynamicPistonDragKiloNewtons: totalDragKiloNewtons,
            KantrowitzChokingRegime: regime,
            AerodynamicControlGuidance: guidance,
            IsAerodynamicallyStable: isStable
        );
    }
}
