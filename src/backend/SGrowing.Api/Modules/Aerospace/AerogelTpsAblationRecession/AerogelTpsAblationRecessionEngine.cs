using System;

namespace SGrowing.Api.Modules.Aerospace.AerogelTpsAblationRecession;

public sealed class AerogelTpsAblationRecessionEngine : IAerogelTpsAblationRecessionEngine
{
    public AerogelTpsAnalysisResult AnalyzeTpsAblationRecession(AerogelTpsTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var density = Math.Max(50.0, telemetry.AerogelBulkDensityKgPerCubicMeter);
        var heatOfAblation = Math.Max(5.0, telemetry.HeatOfAblationMegaJoulesPerKg);
        var thickness = Math.Max(5.0, telemetry.AerogelLayerThicknessMillimeters);

        // Surface recession rate: s_dot = 1000 * q_stag / (rho * H_abl) [mm/s]
        var recessionRate = 1000.0 * Math.Max(0.0, telemetry.StagnationHeatFluxMegawattsPerSquareMeter) / (density * heatOfAblation);

        // Projected bondline temperature based on heat soak
        var machFactor = 1.0 + 0.015 * Math.Max(1.0, telemetry.FlightMachNumber);
        var heatSoakRise = (telemetry.StagnationHeatFluxMegawattsPerSquareMeter * 190.0 / thickness) * machFactor;
        var projectedBondlineTemp = telemetry.SubstructureBondlineTemperatureKelvin + heatSoakRise;

        // Thermal shock stress ratio
        var qNorm = telemetry.StagnationHeatFluxMegawattsPerSquareMeter / 12.0;
        var qDynNorm = telemetry.DynamicPressureKiloPascals / 60.0;
        var aoaFactor = 1.0 + 0.012 * Math.Max(0.0, telemetry.AngleOfAttackDegrees);
        var thermalShockStress = Math.Max(0.0, qNorm * qDynNorm * aoaFactor);

        string regime;
        string action;
        bool isNominal;

        if (telemetry.StagnationHeatFluxMegawattsPerSquareMeter > 14.0 || recessionRate > 1.85)
        {
            regime = "HYPERSONIC_SUPERCRITICAL_ABLATION_RECESSION";
            action = "Critical aerodynamic heating exceeds aerogel sublimation enthalpy limit. Increase re-entry lift-to-drag ratio (L/D) or pull-up angle of attack.";
            isNominal = false;
        }
        else if (projectedBondlineTemp > 525.0 || telemetry.SubstructureBondlineTemperatureKelvin > 490.0)
        {
            regime = "BONDLINE_ADHESIVE_THERMAL_EXCURSION";
            action = "CFRP substructure adhesive bondline approaching glass transition limit (525 K). Modulate aerodynamic roll-reversal maneuvers.";
            isNominal = false;
        }
        else if (thermalShockStress > 0.82)
        {
            regime = "AEROGEL_MICROCRACKING_THERMAL_SHOCK";
            action = "Steep thermal gradients generate surface microcracking risk. Pre-condition nose stagnation curvature and bank angle.";
            isNominal = false;
        }
        else
        {
            regime = "NOMINAL_QUASI_STEADY_REENTRY_ABLATION";
            action = "Aerogel thermal protection system ablation recession and internal temperature gradients nominal. Substructure integrity assured.";
            isNominal = true;
        }

        return new AerogelTpsAnalysisResult(
            VehicleId: telemetry.VehicleId,
            SurfaceRecessionRateMillimetersPerSec: recessionRate,
            ProjectedBondlinePeakTemperatureKelvin: projectedBondlineTemp,
            ThermalShockStressRatio: thermalShockStress,
            AblationRegime: regime,
            ThermalControlAction: action,
            IsTpsNominal: isNominal
        );
    }
}
