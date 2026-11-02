namespace SGrowing.Api.Modules.BioTech.MicrofluidicLnpMrnaEncapsulation;

public sealed record LnpFormulationTelemetry(
    string FormulationBatchId,
    double AqueousMrnaFlowRateMlPerMin,
    double EthanolLipidFlowRateMlPerMin,
    double IonizableNitrogenToMrnaPhosphateRatio,
    double MeasuredHydrodynamicDiameterNanometers,
    double MeasuredPolydispersityIndex,
    double MicrochannelDifferentialPressureBar,
    double MeasuredEncapsulationEfficiencyPercent,
    bool InlineTangentialFlowFiltrationActive
);

public sealed record LnpFormulationAnalysisResult(
    string FormulationBatchId,
    double FlowRateRatioAqueousToEthanol,
    double TotalFlowRateMlPerMin,
    double NanoparticleQualityScore,
    string FormulationRegime,
    string FormulationPumpAction,
    bool IsBatchClinicallyAcceptable
);

public interface IMicrofluidicLnpMrnaEncapsulationEngine
{
    LnpFormulationAnalysisResult AnalyzeLnpFormulation(LnpFormulationTelemetry telemetry);
}
