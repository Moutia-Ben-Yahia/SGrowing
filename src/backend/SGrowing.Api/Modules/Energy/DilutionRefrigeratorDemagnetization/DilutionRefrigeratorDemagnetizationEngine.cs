using System;

namespace SGrowing.Api.Modules.Energy.DilutionRefrigeratorDemagnetization;

public sealed class DilutionRefrigeratorDemagnetizationEngine : IDilutionRefrigeratorDemagnetizationEngine
{
    public DilutionRefrigeratorAnalysisResult AnalyzeSubKelvinRefrigeration(DilutionRefrigeratorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        // Dilution cooling power: Q_dot = n_dot_3 * (84 * T_mix^2 - 12 * T_inlet^2) in Watts
        // In counter-flow heat exchangers, incoming He3 is pre-cooled near T_mix (T_inlet ~ 0.012 * T_still)
        var tMix = Math.Max(0.001, telemetry.MixingChamberTempKelvin);
        var tInlet = Math.Max(tMix, telemetry.StillTempKelvin * 0.012);
        var qDilutionWatts = Math.Max(0.0, telemetry.He3CirculationFlowRateMolPerSec * (84.0 * Math.Pow(tMix, 2) - 12.0 * Math.Pow(tInlet, 2)));
        var qDilutionMicroWatts = Math.Round(qDilutionWatts * 1_000_000.0, 3);

        var loadMicroWatts = Math.Max(0.05, telemetry.SuperconductingQubitThermalLoadMicroWatts);
        var coolingMargin = Math.Round(qDilutionMicroWatts / loadMicroWatts, 3);

        // ADR Demagnetization stage: T_f = T_i * sqrt((B_f^2 + b^2) / (B_i^2 + b^2))
        const double internalDipoleFieldB = 0.025; // Tesla
        var initialField = Math.Max(internalDipoleFieldB, telemetry.AdrMagneticFieldTesla);
        // Demagnetization drops magnetic field to near zero
        var adrFinalTemp = Math.Max(0.0008, telemetry.AdrPillTemperatureKelvin * (internalDipoleFieldB / initialField));
        
        // Entropy transfer deltaS (J/K)
        var entropyTransfer = Math.Round(0.85 * 8.314 * Math.Log(2.0) * (1.0 - (adrFinalTemp / Math.Max(0.001, telemetry.AdrPillTemperatureKelvin))), 4);
        var effectiveTemp = Math.Round(Math.Min(tMix, adrFinalTemp), 5);

        string regime;
        string action;
        bool isStable;

        if (telemetry.MixingChamberTempKelvin > 0.080)
        {
            regime = "THERMAL_RUNAWAY_QUENCH_WARNING";
            action = "Mixing chamber exceeded 80 mK. Ramp He3 circulation pump speed and engage 1K pot thermal dump.";
            isStable = false;
        }
        else if (coolingMargin < 1.20 || telemetry.MixingChamberTempKelvin > 0.025)
        {
            regime = "CIRCULATION_FLOW_THROTTLED";
            action = "Dilution cooling margin tight. Increase He3 condensing pressure and optimize still heater power.";
            isStable = false;
        }
        else if (telemetry.AdrMagneticFieldTesla > 0.5 && !telemetry.ThermalSwitchEngaged)
        {
            regime = "ADR_DEMAGNETIZATION_SWEEP";
            action = "Sub-kelvin ADR active demagnetization sweep in progress. Superconducting switch decoupled.";
            isStable = true;
        }
        else
        {
            regime = "QUANTUM_COHERENCE_OPTIMAL";
            action = "Thermal noise below 15 mK quantum threshold. Cryogenic phase coherence preserved.";
            isStable = true;
        }

        return new DilutionRefrigeratorAnalysisResult(
            CryostatId: telemetry.CryostatId,
            DilutionCoolingPowerMicroWatts: qDilutionMicroWatts,
            CoolingMarginRatio: coolingMargin,
            AdrDemagnetizationEntropyTransferJoulesPerKelvin: entropyTransfer,
            EffectiveColdFingerTempKelvin: effectiveTemp,
            OperatingRegime: regime,
            RecommendedAction: action,
            IsThermalStable: isStable
        );
    }
}
