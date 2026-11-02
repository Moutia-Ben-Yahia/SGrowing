using System;

namespace SGrowing.Api.Modules.Energy.SupercriticalThermosiphonGeothermal;

public sealed class SupercriticalThermosiphonGeothermalEngine : ISupercriticalThermosiphonGeothermalEngine
{
    public SupercriticalThermosiphonAnalysisResult AnalyzeThermosiphonState(SupercriticalThermosiphonTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var netDriveBar = Math.Round(telemetry.ThermosiphonBuoyancyHeadBar - telemetry.WellboreFrictionLossBar, 2);

        // Carnot maximum exergy: eta = (1 - T_cold / T_hot) * 0.68
        var tHotKelvin = 273.15 + Math.Max(50.0, telemetry.BottomHoleTemperatureCelsius);
        var tColdKelvin = 273.15 + Math.Max(5.0, telemetry.SurfaceInjectionTemperatureCelsius);
        var carnotLimit = 1.0 - (tColdKelvin / tHotKelvin);
        var exergyEffPercent = Math.Round(Math.Clamp(carnotLimit * 0.68 * 100.0, 5.0, 60.0), 1);

        // Parasitic pumping power saved (MW)
        const double pumpEfficiency = 0.78;
        const double averageFluidDensity = 820.0; // kg/m3
        var frictionPa = telemetry.WellboreFrictionLossBar * 100_000.0;
        var volumeFlowM3PerSec = telemetry.WorkingFluidMassFlowRateKgPerSec / averageFluidDensity;
        var savedPowerMw = Math.Round((volumeFlowM3PerSec * frictionPa) / (pumpEfficiency * 1_000_000.0), 3);

        string regime;
        string command;
        bool isStable;

        if (netDriveBar <= 0.0)
        {
            regime = "FRICTION_CHOKING_STAGNATION_HAZARD";
            command = "Wellbore friction exceeds natural buoyancy head. Start auxiliary surface kick-pump and open downhole bypass choke.";
            isStable = false;
        }
        else if (netDriveBar < 5.0 && !telemetry.SurfaceTurbineThrottleValveActive)
        {
            regime = "DENSITY_WAVE_OSCILLATION_RISK";
            command = "Net drive pressure low. Modulate surface turbine throttle valve to stabilize supercritical sCO2 density-wave resonance.";
            isStable = false;
        }
        else if (telemetry.BottomHoleTemperatureCelsius < 220.0 || telemetry.ThermalPowerExtractedMegawatts < 8.0)
        {
            regime = "THERMAL_DEPLETION_DEGRADED";
            command = "Downhole fracture reservoir exhibiting thermal depletion. Throttle mass flow rate by 20% to allow thermal conduction recovery.";
            isStable = false;
        }
        else
        {
            regime = "SUPERCRITICAL_THERMOSIPHON_SELF_DRIVEN";
            command = "Thermosiphon natural buoyancy drive robust (>5 bar margin). Self-sustained closed-loop supercritical heat pipe confirmed.";
            isStable = true;
        }

        return new SupercriticalThermosiphonAnalysisResult(
            WellboreId: telemetry.WellboreId,
            NetBuoyancyDrivePressureBar: netDriveBar,
            ThermodynamicExergyEfficiencyPercent: exergyEffPercent,
            ParasiticPumpingPowerSavedMegawatts: savedPowerMw,
            ThermosiphonRegime: regime,
            WellheadControlCommand: command,
            IsThermosiphonCirculationStable: isStable
        );
    }
}
