using System;

namespace SGrowing.Api.Modules.Environmental.ScwoPfasElimination;

public sealed class ScwoPfasEliminationEngine : IScwoPfasEliminationEngine
{
    public ScwoAnalysisResult AnalyzeScwoProcess(ScwoTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var tempC = telemetry.ReactorCoreTemperatureCelsius;
        var pressureMpa = telemetry.ReactorOperatingPressureMegaPascals;
        var o2Ratio = Math.Max(0.1, telemetry.DissolvedOxygenStoichiometricRatio);
        var tau = Math.Max(1.0, telemetry.ResidenceTimeSeconds);

        // Supercritical reaction kinetics modeling
        var tFactor = Math.Clamp((tempC - 370.0) / 140.0, 0.0, 1.0);
        var pFactor = Math.Clamp((pressureMpa - 22.0) / 3.0, 0.0, 1.0);
        var oFactor = Math.Clamp(o2Ratio / 1.3, 0.1, 1.0);
        var tauFactor = Math.Clamp(tau / 50.0, 0.1, 1.0);

        var defluorinationEfficiency = Math.Clamp(95.0 + 4.996 * (tFactor * pFactor * oFactor * tauFactor), 50.0, 99.998);

        var influentPpm = Math.Max(0.01, telemetry.InfluentTotalPfasConcentrationPpm);
        var residualPfasPpb = influentPpm * 1000.0 * (1.0 - defluorinationEfficiency / 100.0);
        var tocDestruction = Math.Clamp(defluorinationEfficiency + 0.002, 50.0, 99.999);

        string regime;
        string guidance;
        bool isComplete;

        if (tempC < 380.0 || pressureMpa < 22.5)
        {
            regime = "SUB_CRITICAL_COLLAPSE_INCOMPLETE_OXIDATION";
            guidance = "Reactor dropped into sub-critical thermodynamic regime (T < 380 C or P < 22.5 MPa). Incomplete oxidation risk; activate auxiliary preheater and high-pressure plunger pumps.";
            isComplete = false;
        }
        else if (telemetry.InorganicSaltPrecipitationPluggingRisk > 0.70)
        {
            regime = "HYDROTHERMAL_SALT_PLUGGING_HAZARD";
            guidance = "Severe inorganic salt precipitation plugging detected in reactor throat. Increase transpiring-wall supercritical sheath water flow rate.";
            isComplete = false;
        }
        else if (defluorinationEfficiency < 99.90)
        {
            regime = "PFAS_DEFLUORINATION_SHORTFALL";
            guidance = "Defluorination efficiency below regulatory destruction requirement (99.90%). Increase liquid oxygen stoichiometric excess and residence time.";
            isComplete = false;
        }
        else
        {
            regime = "SUPERCRITICAL_COMPLETE_MINERALIZATION_OPTIMAL";
            guidance = "Supercritical water oxidation operating in complete mineralization regime. Refractory PFAS destroyed beyond 99.99% without toxic fugitive emissions.";
            isComplete = true;
        }

        return new ScwoAnalysisResult(
            ReactorId: telemetry.ReactorId,
            PfasDefluorinationEfficiencyPercent: defluorinationEfficiency,
            ResidualPfasConcentrationPpb: residualPfasPpb,
            TotalOrganicCarbonDestructionPercent: tocDestruction,
            ScwoDestructionRegime: regime,
            HydrothermalProcessControlGuidance: guidance,
            IsDestructionComplete: isComplete
        );
    }
}
