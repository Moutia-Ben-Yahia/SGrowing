using System;

namespace SGrowing.Api.Modules.Manufacturing.MbeRheedGrowthKinetics;

public sealed class MbeRheedGrowthKineticsEngine : IMbeRheedGrowthKineticsEngine
{
    public MbeGrowthAnalysisResult AnalyzeEpitaxialGrowth(MbeGrowthTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var fluxIII = Math.Max(1e-12, telemetry.GroupIIIFluxTorr);
        var fluxV = Math.Max(1e-12, telemetry.GroupVFluxTorr);
        var fluxRatio = Math.Round(fluxV / fluxIII, 2);

        // GaAs(001) monolayer thickness = a0 / 2 = 0.283 nm
        const double monolayerThicknessNm = 0.283;
        var period = Math.Max(0.1, telemetry.RheedOscillationPeriodSeconds);
        var growthRate = Math.Round(monolayerThicknessNm / period, 3);

        // Roughness estimate from specular RHEED intensity dampening (Angstroms)
        var intensity = Math.Clamp(telemetry.RheedSpecularIntensityNormalized, 0.05, 1.0);
        var roughnessAngstroms = Math.Round(0.50 + 3.8 * (1.0 - intensity), 2);

        string mode;
        string command;
        bool isUniform;

        if (telemetry.GrowthTemperatureCelsius > 640.0 || fluxRatio < 5.0)
        {
            mode = "ARSENIC_DESORPTION_SURFACE_ROUGHENING";
            command = "Arsenic overpressure insufficient for substrate temperature. Escalate Group V cracker valve and decrease substrate heater.";
            isUniform = false;
        }
        else if (intensity < 0.40 || telemetry.SurfaceAdatomDiffusionLengthNanometers < 10.0)
        {
            mode = "STRANSKI_KRASTANOV_3D_ISLANDING";
            command = "Transition to 3D quantum islanding detected. Close Group III shutter for growth interrupt surface recovery anneal.";
            isUniform = false;
        }
        else if (telemetry.SurfaceAdatomDiffusionLengthNanometers > 60.0 && period > 6.0)
        {
            mode = "STEP_FLOW_EQUILIBRIUM";
            command = "Surface adatoms reaching terrace steps. True step-flow epitaxy established with atomic terrace step advancement.";
            isUniform = true;
        }
        else
        {
            mode = "FRANK_VAN_DER_MERWE_2D_OPTIMAL";
            command = "Persistent RHEED intensity oscillations confirm strict 2D layer-by-layer growth. Quantum well heterointerface abruptness preserved.";
            isUniform = true;
        }

        return new MbeGrowthAnalysisResult(
            ChamberId: telemetry.ChamberId,
            FluxRatioVtoIII: fluxRatio,
            MonolayerGrowthRateNanometersPerSecond: growthRate,
            InterfacialRoughnessEstimateAngstroms: roughnessAngstroms,
            EpitaxialGrowthMode: mode,
            EffusionCellShutterCommand: command,
            IsCrystalStructureUniform: isUniform
        );
    }
}
