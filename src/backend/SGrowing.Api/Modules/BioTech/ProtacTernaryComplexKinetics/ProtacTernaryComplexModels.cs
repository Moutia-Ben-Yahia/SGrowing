namespace SGrowing.Api.Modules.BioTech.ProtacTernaryComplexKinetics;

public sealed record ProtacTernaryComplexTelemetry(
    string ProtacCandidateId,
    string TargetProtein, // e.g. "BRD4", "KRAS"
    string E3Ligase, // e.g. "VHL", "CRBN"
    double BinaryKdPoiNanomolar, // K_D(POI): e.g. 120.0 nM
    double BinaryKdE3Nanomolar, // K_D(E3): e.g. 80.0 nM
    double TernaryKdPoiNanomolar, // K_D(POI, ternary): e.g. 15.0 nM
    double MeasuredDc50Nanomolar, // DC_50: e.g. 12.5 nM
    double MaximalDegradationPercent, // D_max: e.g. 94.0%
    double CellularHookConcentrationMicromolar // e.g. 2.5 uM
);

public sealed record ProtacTernaryComplexAnalysisResult(
    string ProtacCandidateId,
    double CooperativityFactorAlpha,
    double OptimalTernaryConcentrationNanomolar,
    string ProtacEfficacyRegime,
    string MedicinalChemistryGuidance,
    bool IsProtacLeadViable
);

public interface IProtacTernaryComplexKineticsEngine
{
    ProtacTernaryComplexAnalysisResult AnalyzeProtacKinetics(ProtacTernaryComplexTelemetry telemetry);
}
