using System;

namespace SGrowing.Api.Modules.BioTech.MicrofluidicLnpMrnaEncapsulation;

public sealed class MicrofluidicLnpMrnaEncapsulationEngine : IMicrofluidicLnpMrnaEncapsulationEngine
{
    public LnpFormulationAnalysisResult AnalyzeLnpFormulation(LnpFormulationTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var qEth = Math.Max(0.01, telemetry.EthanolLipidFlowRateMlPerMin);
        var qAq = Math.Max(0.01, telemetry.AqueousMrnaFlowRateMlPerMin);
        var frr = Math.Round(qAq / qEth, 2);
        var tfr = Math.Round(qAq + qEth, 2);

        // Quality score: composite of EE%, PDI, and size deviation from 75 nm target
        var eeFraction = Math.Clamp(telemetry.MeasuredEncapsulationEfficiencyPercent / 100.0, 0.0, 1.0);
        var pdiFactor = Math.Max(0.0, 1.0 - Math.Clamp(telemetry.MeasuredPolydispersityIndex, 0.0, 1.0));
        var sizeDeviationFactor = Math.Exp(-Math.Abs(telemetry.MeasuredHydrodynamicDiameterNanometers - 75.0) / 45.0);
        var qualityScore = Math.Round(Math.Clamp(eeFraction * pdiFactor * sizeDeviationFactor, 0.0, 1.0), 3);

        string regime;
        string action;
        bool isAcceptable;

        if (telemetry.MicrochannelDifferentialPressureBar > 4.2)
        {
            regime = "MICROCHANNEL_CLOGGING_PRESSURE_SPIKE";
            action = "Microfluidic chip backpressure exceeds 4.2 bar. Stop syringe pumps, initiate automated ethanol back-flush, and replace mixer cartridge.";
            isAcceptable = false;
        }
        else if (telemetry.MeasuredPolydispersityIndex > 0.15 || telemetry.MeasuredHydrodynamicDiameterNanometers > 115.0)
        {
            regime = "HIGH_PDI_AGGREGATION_REJECT";
            action = "Polydispersity index exceeds 0.15. Increase total flow rate to accelerate convective chaotic mixing before self-assembly coalescence.";
            isAcceptable = false;
        }
        else if (telemetry.IonizableNitrogenToMrnaPhosphateRatio < 4.5 || telemetry.MeasuredEncapsulationEfficiencyPercent < 88.0)
        {
            regime = "SUBOPTIMAL_NP_RATIO_LOW_ENCAPSULATION";
            action = "Encapsulation efficiency below 88% or N/P ratio below 4.5. Titrate ionizable lipid stock concentration to ensure full mRNA electrostatic condensation.";
            isAcceptable = false;
        }
        else
        {
            regime = "MONODISPERSE_NANO_ENCAPSULATION_OPTIMAL";
            action = "Monodisperse LNP assembly verified (PDI < 0.12, EE > 90%). Proceed directly to inline tangential flow filtration (TFF) ethanol removal.";
            isAcceptable = true;
        }

        return new LnpFormulationAnalysisResult(
            FormulationBatchId: telemetry.FormulationBatchId,
            FlowRateRatioAqueousToEthanol: frr,
            TotalFlowRateMlPerMin: tfr,
            NanoparticleQualityScore: qualityScore,
            FormulationRegime: regime,
            FormulationPumpAction: action,
            IsBatchClinicallyAcceptable: isAcceptable
        );
    }
}
