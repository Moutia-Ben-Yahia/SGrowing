using System;

namespace SGrowing.Api.Modules.Aerospace.NuclearThermalPropulsionPebbleBed;

public sealed class NuclearThermalPropulsionPebbleBedEngine : INuclearThermalPropulsionPebbleBedEngine
{
    private const double UniversalGasConstant = 8.314462618; // J / (mol * K)
    private const double G0StandardGravity = 9.80665; // m/s^2

    public NuclearThermalAnalysisResult AnalyzeReactorPropulsion(NuclearThermalTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var tCoreK = Math.Max(500.0, telemetry.PeakFuelPebbleCenterlineTemperatureKelvin);
        var tLimitK = Math.Max(tCoreK, telemetry.FuelPebbleMeltingLimitTemperatureKelvin);
        var thermalMarginK = tLimitK - tCoreK;

        var mDotKgPerSec = Math.Max(0.5, telemetry.HydrogenPropellantMassFlowRateKgPerSec);
        var alpha = Math.Clamp(telemetry.HydrogenDissociationFractionAlpha, 0.0, 0.60);

        // Mean exhaust molecular weight: M_bar = 2.016 / (1 + alpha) [g/mol]
        var meanMolecularWeightGPerMol = 2.016 / (1.0 + alpha);
        var meanMolecularWeightKgPerMol = meanMolecularWeightGPerMol * 1e-3;

        // Effective ratio of specific heats for high-T H2/H mixture
        var gamma = 1.30;
        var gammaFactor = (2.0 * gamma) / (gamma - 1.0);

        // Ideal exhaust velocity: v_e = sqrt(gammaFactor * (R * T_core / M_bar) * (1 - (Pe/Pc)^((gamma-1)/gamma)))
        // For vacuum expansion with area ratio > 200: pressure recovery term ~ 0.96
        var idealVeMps = Math.Sqrt(gammaFactor * (UniversalGasConstant * tCoreK / meanMolecularWeightKgPerMol) * 0.96);
        var nozzleEfficiency = 0.95;
        var effectiveVeMps = idealVeMps * nozzleEfficiency;

        // Specific impulse I_sp = v_e / g_0 [seconds]
        var specificImpulseSeconds = effectiveVeMps / G0StandardGravity;

        // Thrust F = m_dot * v_e [kN]
        var thrustKn = (mDotKgPerSec * effectiveVeMps) / 1000.0;

        // Ergun pressure drop across pebble bed: Delta P ~ f(porosity, m_dot) [MPa]
        var porosity = Math.Clamp(telemetry.PebbleBedPoreFractionPorosity, 0.25, 0.60);
        var porosityFactor = Math.Pow(0.40 / porosity, 3.0);
        var bedDeltaPMpa = 0.15 * Math.Pow(mDotKgPerSec / 20.0, 1.75) * porosityFactor;

        string regime;
        string guidance;
        bool isSafe;

        if (thermalMarginK < 200.0)
        {
            regime = "REACTOR_CORE_MELTDOWN_CRITICAL_HAZARD";
            guidance = "Fuel pebble centerline thermal margin below 200 K. Imminent TRISO ceramic containment degradation and fuel kernel melting. Scram reactor safety rods and increase LH2 coolant flow.";
            isSafe = false;
        }
        else if (bedDeltaPMpa > 1.8)
        {
            regime = "PEBBLE_BED_EXCESSIVE_PRESSURE_DROP_CHOKING";
            guidance = "Pebble-bed hydraulic pressure drop exceeds 1.8 MPa. Turbopump discharge limit breached; fluid friction choking core throughput. Adjust propellant turbopump bypass valve.";
            isSafe = false;
        }
        else if (specificImpulseSeconds < 850.0)
        {
            regime = "LOW_SPECIFIC_IMPULSE_THERMAL_DEFICIT";
            guidance = "Specific impulse below 850 s requirement. Insufficient core thermal dissociation or low chamber temperature. Ramp reactor thermal power to target 2700 K.";
            isSafe = false;
        }
        else
        {
            regime = "OPTIMAL_HIGH_ISP_NUCLEAR_THERMAL_PROPULSION";
            guidance = "High-temperature pebble-bed reactor operating with robust fuel thermal margins. Molecular hydrogen thermal dissociation achieving ultra-high specific impulse (> 950 s).";
            isSafe = true;
        }

        return new NuclearThermalAnalysisResult(
            ReactorCoreId: telemetry.ReactorCoreId,
            CalculatedSpecificImpulseSeconds: specificImpulseSeconds,
            GeneratedThrustKiloNewtons: thrustKn,
            MeanExhaustMolecularWeightGramsPerMole: meanMolecularWeightGPerMol,
            FuelCenterlineThermalMarginKelvin: thermalMarginK,
            PebbleBedPressureDropMegaPascals: bedDeltaPMpa,
            ReactorThermalRegime: regime,
            PropulsionReactorGuidance: guidance,
            IsReactorOperatingSafe: isSafe
        );
    }
}
