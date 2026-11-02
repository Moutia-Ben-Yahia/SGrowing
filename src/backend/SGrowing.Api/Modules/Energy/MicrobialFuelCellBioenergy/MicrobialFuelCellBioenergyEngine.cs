using System;

namespace SGrowing.Api.Modules.Energy.MicrobialFuelCellBioenergy;

public sealed class MicrobialFuelCellBioenergyEngine : IMicrobialFuelCellBioenergyEngine
{
    private const double FaradayCoulombsPerMgCod = 12.06; // F * b / M_O2 (96485 * 4 / 32000)

    public MicrobialFuelCellAnalysisResult AnalyzeBioenergyProcess(MicrobialFuelCellTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var infCod = Math.Max(1.0, telemetry.InfluentCodMilligramsPerLiter);
        var effCod = Math.Clamp(telemetry.EffluentCodMilligramsPerLiter, 0.0, infCod);
        var deltaCod = infCod - effCod;

        var codRemovalPercent = Math.Clamp((deltaCod / infCod) * 100.0, 0.0, 100.0);

        var hrtHours = Math.Max(0.5, telemetry.HydraulicRetentionTimeHours);
        var currentMa = Math.Max(0.1, telemetry.MeasuredOperatingCurrentMilliAmperes);

        // Coulombic Efficiency: recovered charge vs theoretical substrate charge
        var totalCoulombsRecovered = (currentMa * 1e-3) * (hrtHours * 3600.0);
        var theoreticalCoulombs = FaradayCoulombsPerMgCod * (deltaCod * 0.05 + 1.0);
        var coulombicEfficiency = Math.Clamp((totalCoulombsRecovered / theoreticalCoulombs) * 100.0, 0.5, 95.0);

        // Power density calculations
        var ocv = Math.Clamp(telemetry.OpenCircuitVoltageVolts, 0.1, 1.5);
        var rInt = Math.Max(1.0, telemetry.TotalInternalResistanceOhms);
        var anodeAreaM2 = Math.Max(0.01, telemetry.AnodeElectrodeSurfaceAreaM2);

        // Theoretical maximum power at impedance matched load: P_max = OCV^2 / (4 * R_int) [Watts]
        var pMaxWatts = Math.Pow(ocv, 2.0) / (4.0 * rInt);
        var pMaxMilliWatts = pMaxWatts * 1000.0;
        var pMaxDensityMilliWattsPerM2 = pMaxMilliWatts / anodeAreaM2;

        // Operating voltage under load: V_cell = max(0, OCV - I * R_int)
        var operatingVoltageVolts = Math.Max(0.0, ocv - ((currentMa * 1e-3) * rInt));
        var operatingPowerMw = operatingVoltageVolts * currentMa;

        string regime;
        string guidance;
        bool isNominal;

        if (telemetry.AnodeBiofilmThicknessMicrometers > 110.0)
        {
            regime = "BIOFILM_DIFFUSION_LIMITATION_CLOGGING";
            guidance = "Anode exoelectrogenic biofilm thickness exceeds 110 um causing severe substrate diffusion limitation and dead core biomass. Apply periodic hydrodynamic shearing pulse.";
            isNominal = false;
        }
        else if (rInt > 60.0)
        {
            regime = "EXCESSIVE_INTERNAL_RESISTANCE_OHMIC_LOSS";
            guidance = "Total internal resistance exceeds 60 Ohms. High ohmic and charge transfer losses; backwash proton exchange membrane (PEM) and decrease inter-electrode spacing.";
            isNominal = false;
        }
        else if (codRemovalPercent < 60.0)
        {
            regime = "COD_REMEDIATION_SHORTFALL_WARNING";
            guidance = "Wastewater COD removal efficiency dropped below 60%. Increase hydraulic retention time or augment anode chamber with active Geobacter inoculum.";
            isNominal = false;
        }
        else
        {
            regime = "OPTIMAL_EXOELECTROGENIC_BIOENERGY_GENERATION";
            guidance = "Anode exoelectrogenesis and extracellular electron transfer balanced. Substrate COD remediation and renewable bioelectricity generation optimal.";
            isNominal = true;
        }

        return new MicrobialFuelCellAnalysisResult(
            ReactorId: telemetry.ReactorId,
            CodRemovalEfficiencyPercent: codRemovalPercent,
            CoulombicEfficiencyPercent: coulombicEfficiency,
            MaximumPowerPointDensityMilliWattsPerM2: pMaxDensityMilliWattsPerM2,
            OperatingPowerOutputMilliWatts: operatingPowerMw,
            BioenergyRegime: regime,
            BioreactorOptimizationGuidance: guidance,
            IsBioenergyNominal: isNominal
        );
    }
}
