using System;

namespace SGrowing.Api.Modules.Energy.OceanThermalEnergyConversion;

public sealed class OceanThermalEnergyConversionEngine : IOceanThermalEnergyConversionEngine
{
    private const double SeawaterDensityKgPerM3 = 1025.0;
    private const double SeawaterSpecificHeatKjPerKgK = 4.0; // kJ / (kg * K)
    private const double G0 = 9.80665; // m/s^2

    public OtecAnalysisResult AnalyzeOtecPlant(OtecTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var tWarmC = telemetry.SurfaceSeawaterTemperatureCelsius;
        var tColdC = telemetry.DeepSeawaterTemperatureCelsius;
        var deltaTC = Math.Max(0.0, tWarmC - tColdC);

        var vDotWarm = Math.Max(1.0, telemetry.WarmSeawaterVolumetricFlowRateM3PerSec);
        var vDotCold = Math.Max(1.0, telemetry.ColdSeawaterVolumetricFlowRateM3PerSec);

        // Theoretical Carnot efficiency: eta_carnot = DeltaT / T_warm (Kelvin)
        var tWarmK = tWarmC + 273.15;
        var carnotEfficiency = deltaTC / Math.Max(270.0, tWarmK);

        // Realistic Rankine cycle with ammonia: ~42% of Carnot efficiency
        var rankineCycleEfficiency = carnotEfficiency * 0.42;

        // Thermal heat absorbed from warm seawater (assumed 3.2 C drop across evaporator)
        var evaporatorDeltaT = Math.Min(3.5, deltaTC * 0.16);
        var qInThermalKw = vDotWarm * SeawaterDensityKgPerM3 * SeawaterSpecificHeatKjPerKgK * evaporatorDeltaT;
        var qInThermalMw = qInThermalKw / 1000.0;

        // Gross turbine power output
        var etaTurbine = Math.Clamp(telemetry.TurbineGeneratorIsentropicEfficiencyPercent / 100.0, 0.40, 0.95);
        var grossPowerMw = qInThermalMw * rankineCycleEfficiency * etaTurbine;

        // Parasitic pumping power: cold water pipe friction (Darcy-Weisbach) + warm water intake
        var pipeLength = Math.Max(100.0, telemetry.DeepSeawaterPipeLengthMeters);
        var pipeDiam = Math.Max(0.5, telemetry.DeepSeawaterPipeDiameterMeters);
        var pipeArea = (Math.PI * Math.Pow(pipeDiam, 2.0)) / 4.0;
        var coldVelocity = vDotCold / pipeArea;

        var frictionFactor = 0.016;
        var frictionHeadM = frictionFactor * (pipeLength / pipeDiam) * (Math.Pow(coldVelocity, 2.0) / (2.0 * G0));
        var densityGradientHeadM = 1.4; // Seawater density difference over 1000m
        var totalColdHeadM = frictionHeadM + densityGradientHeadM;

        var etaPump = Math.Clamp(telemetry.SeawaterPumpingStationMechanicalEfficiencyPercent / 100.0, 0.40, 0.95);
        var pColdPumpMw = (SeawaterDensityKgPerM3 * G0 * vDotCold * totalColdHeadM) / (etaPump * 1e6);

        // Warm water pumping power (intake head ~2.5m)
        var pWarmPumpMw = (SeawaterDensityKgPerM3 * G0 * vDotWarm * 2.5) / (etaPump * 1e6);
        var totalParasiticMw = pColdPumpMw + pWarmPumpMw;

        // Net electric power output
        var netPowerMw = grossPowerMw - totalParasiticMw;
        var netThermalEfficiencyPercent = qInThermalMw > 0.0
            ? Math.Clamp((netPowerMw / qInThermalMw) * 100.0, -10.0, 10.0)
            : 0.0;

        string regime;
        string guidance;
        bool isNetPositive;

        if (deltaTC < 18.0)
        {
            regime = "INSUFFICIENT_THERMAL_GRADIENT_SHUTDOWN";
            guidance = "Ocean surface-to-deep temperature differential below 18 C threshold. Carnot potential insufficient for thermodynamic cycle breakeven. Suspend turbine operation.";
            isNetPositive = false;
        }
        else if (totalParasiticMw >= grossPowerMw)
        {
            regime = "PARASITIC_PUMPING_LOSS_DEFICIT";
            guidance = "Deep seawater hydraulic friction and intake pumping exceeds gross turbine power. Negative net electricity balance. Reduce cold water volumetric flow rate or enlarge pipe diameter.";
            isNetPositive = false;
        }
        else if (netPowerMw < 1.0)
        {
            regime = "MARGINAL_NET_POWER_WARNING";
            guidance = "Net power export below 1.0 MW capacity margin. Clean evaporator plate heat exchangers to eliminate marine biofouling and decrease terminal temperature difference.";
            isNetPositive = false;
        }
        else
        {
            regime = "OPTIMAL_BASELOAD_OCEAN_THERMAL_POWER_REGIME";
            guidance = "Continuous baseload 24/7 marine solar thermal power generation confirmed. Positive net export power balance with sustainable upwelling dilution.";
            isNetPositive = true;
        }

        return new OtecAnalysisResult(
            PlantId: telemetry.PlantId,
            OceanTemperatureDifferentialDeltaTCelsius: deltaTC,
            GrossTurbineElectricPowerMegaWatts: grossPowerMw,
            ParasiticSeawaterPumpingPowerMegaWatts: totalParasiticMw,
            NetExportElectricPowerMegaWatts: netPowerMw,
            NetThermalCycleEfficiencyPercent: netThermalEfficiencyPercent,
            OtecOperatingRegime: regime,
            PlantThermodynamicOptimizationGuidance: guidance,
            IsOtecNetPositive: isNetPositive
        );
    }
}
