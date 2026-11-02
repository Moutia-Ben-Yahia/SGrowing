using System;

namespace SGrowing.Api.Modules.Energy.LiquidLithiumCpsDivertor;

public sealed class LiquidLithiumCpsDivertorEngine : ILiquidLithiumCpsDivertorEngine
{
    public LiquidLithiumDivertorAnalysisResult AnalyzeLiquidMetalDivertor(LiquidLithiumDivertorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        // Capillary pressure head vs MHD Hartmann resistance safety margin
        var mhdResistanceKPa = Math.Max(2.0, telemetry.TransverseMagneticFieldTesla * 4.2);
        var capillaryMargin = Math.Round(telemetry.CapillaryReplenishmentPressureKPa / mhdResistanceKPa, 2);

        // Self-regulating vapor shielding attenuation
        double attenuationRatio;
        if (telemetry.LithiumSurfaceTemperatureCelsius > 500.0 && telemetry.PeakIncidentPlasmaHeatFluxMwPerM2 > 10.0)
        {
            var tempOverhead = Math.Clamp((telemetry.LithiumSurfaceTemperatureCelsius - 500.0) / 220.0, 0.0, 1.0);
            attenuationRatio = Math.Round(0.25 + (0.60 * tempOverhead), 3);
        }
        else
        {
            attenuationRatio = 0.08;
        }

        var incidentHeatFlux = Math.Max(0.5, telemetry.PeakIncidentPlasmaHeatFluxMwPerM2);
        var netHeatFlux = Math.Round(incidentHeatFlux * (1.0 - attenuationRatio), 2);

        string regime;
        string command;
        bool isProtected;

        if (telemetry.CorePlasmaEffectiveChargeZeff > 1.35 || telemetry.LithiumSurfaceTemperatureCelsius > 720.0)
        {
            regime = "CORE_PLASMA_LITHIUM_CONTAMINATION_ALERT";
            command = "Core plasma effective charge Zeff exceeds 1.35. Throttle divertor gas puffing and step magnetic strike-point sweep frequency.";
            isProtected = false;
        }
        else if (capillaryMargin < 1.0 || !telemetry.ThermoelectricMhdReplenishmentPumpActive)
        {
            regime = "CAPILLARY_DRYOUT_MELTDOWN_HAZARD";
            command = "Capillary replenishment head below MHD drag threshold. Activate thermoelectric liquid lithium replenishment pump immediately.";
            isProtected = false;
        }
        else if (incidentHeatFlux > 12.0)
        {
            regime = "VAPOR_SHIELDING_REGULATED_OPTIMAL";
            command = "Self-regulating lithium vapor cloud formed. Radiative dissipation reduced peak divertor heat load below 5 MW/m2.";
            isProtected = true;
        }
        else
        {
            regime = "SUB_VAPORIZATION_CONVECTIVE_STABLE";
            command = "Incident heat flux within conductive-convective liquid lithium limit. Minimal vapor shielding active.";
            isProtected = true;
        }

        return new LiquidLithiumDivertorAnalysisResult(
            DivertorCassetteId: telemetry.DivertorCassetteId,
            NetTargetHeatFluxMwPerM2: netHeatFlux,
            VaporShieldingAttenuationRatio: attenuationRatio,
            CapillaryToMhdHeadSafetyMargin: capillaryMargin,
            DivertorProtectionRegime: regime,
            DivertorCoolingCommand: command,
            IsDivertorTargetProtected: isProtected
        );
    }
}
