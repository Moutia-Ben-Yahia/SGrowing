using System;

namespace SGrowing.Api.Modules.Aerospace.MhdBoundaryLayerFlowControl;

public sealed class MhdBoundaryLayerFlowControlEngine : IMhdBoundaryLayerFlowControlEngine
{
    public MhdBoundaryLayerAnalysisResult AnalyzeMhdBoundaryLayer(MhdBoundaryLayerTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var mach = Math.Max(1.0, telemetry.FreeStreamMachNumber);
        var altKm = Math.Clamp(telemetry.FlightAltitudeKilometers, 10.0, 80.0);
        var bTesla = Math.Max(0.1, telemetry.AppliedMagneticFieldTesla);
        var sigma = Math.Max(0.5, telemetry.BoundaryLayerPlasmaElectricalConductivitySiemensPerM);
        var lengthM = Math.Max(0.2, telemetry.FlowInteractionLengthMeters);
        var loadFactorK = Math.Clamp(telemetry.MhdGeneratorLoadFactorK, 0.0, 1.0);

        // Approximate speed of sound and atmospheric density at altitude
        var speedOfSoundMps = 300.0;
        var velocityMps = mach * speedOfSoundMps;
        var densityKgPerM3 = 1.225 * Math.Exp(-altKm / 7.2);

        // Stuart Number (MHD Interaction Parameter): S = (sigma * B^2 * L) / (rho * u)
        var momentumFlux = densityKgPerM3 * velocityMps;
        var stuartNumber = (sigma * Math.Pow(bTesla, 2.0) * lengthM) / Math.Max(0.01, momentumFlux);

        // Interaction volume (assuming 0.15 m^2 effective channel area)
        var channelVolumeM3 = 0.15 * lengthM;

        // Skin friction drag reduction: electromagnetic body force modifying wall shear stress
        var dragReductionPercent = Math.Clamp(stuartNumber * 1.85 * (1.0 - loadFactorK * 0.5), 1.0, 35.0);

        // Extracted electrical power: P = sigma * u^2 * B^2 * K * (1 - K) * Vol [Watts -> kW]
        var extractedPowerWatts = sigma * Math.Pow(velocityMps, 2.0) * Math.Pow(bTesla, 2.0) * loadFactorK * (1.0 - loadFactorK) * channelVolumeM3;
        var extractedPowerKw = extractedPowerWatts / 1000.0;

        // Induced Lorentz force: F_L = sigma * u * B^2 * (1 - K) * Vol [N -> kN]
        var lorentzForceNewtons = sigma * velocityMps * Math.Pow(bTesla, 2.0) * (1.0 - loadFactorK) * channelVolumeM3;
        var lorentzForceKn = lorentzForceNewtons / 1000.0;

        string regime;
        string guidance;
        bool isEffective;

        if (stuartNumber > 25.0)
        {
            regime = "HYPERSONIC_MHD_INLET_CHOKING_SURGE";
            guidance = "MHD interaction parameter Stuart number exceeds 25. Strong Lorentz body force creates upstream bow shock detachment and scramjet inlet unstart. Decrease magnetic field strength.";
            isEffective = false;
        }
        else if (stuartNumber < 0.8)
        {
            regime = "WEAK_MHD_IONIZATION_INSUFFICIENT_COUPLING";
            guidance = "Stuart number below 0.8. Plasma electrical conductivity insufficient for significant boundary layer control. Fire seed electron-beam or localized laser pre-ionizers.";
            isEffective = false;
        }
        else if (telemetry.WallSurfaceHeatFluxMegaWattsPerM2 > 4.0)
        {
            regime = "THERMAL_ABLATATIVE_LIMIT_WARNING";
            guidance = "Wall surface heat flux exceeds 4.0 MW/m^2 thermal limit. Increase load factor K to extract more kinetic energy into electrical storage, cooling the boundary layer.";
            isEffective = false;
        }
        else
        {
            regime = "OPTIMAL_HYPERSONIC_MHD_FLOW_CONTROL";
            guidance = "Hypersonic MHD interaction parameter and power extraction in equilibrium. Skin friction drag suppressed and onboard electrical power harvested without inlet unstart.";
            isEffective = true;
        }

        return new MhdBoundaryLayerAnalysisResult(
            HypersonicVehicleId: telemetry.HypersonicVehicleId,
            MhdInteractionParameterStuartNumber: stuartNumber,
            SkinFrictionDragReductionPercent: dragReductionPercent,
            ExtractedMhdElectricPowerKiloWatts: extractedPowerKw,
            InducedLorentzDecelerationForceKiloNewtons: lorentzForceKn,
            MhdFlowRegime: regime,
            BoundaryLayerControlGuidance: guidance,
            IsMhdControlEffective: isEffective
        );
    }
}
