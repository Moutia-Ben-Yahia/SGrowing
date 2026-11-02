namespace SGrowing.Api.Modules.Energy.VanadiumRedoxFlowBatteryBalancing;

public sealed record VanadiumFlowBatteryTelemetry(
    string FlowBatteryStackId,
    double NegalyteV2ConcentrationMolar, // [V2+]: e.g. 1.25 M
    double NegalyteV3ConcentrationMolar, // [V3+]: e.g. 0.35 M
    double PosalyteV4ConcentrationMolar, // [VO2+]: e.g. 0.38 M
    double PosalyteV5ConcentrationMolar, // [VO2+]: e.g. 1.22 M
    double ElectrolyteTemperatureCelsius, // T: e.g. 32.0 C
    double PumpElectrolyteFlowRateLitersPerMinute, // e.g. 45.0 L/min
    double StackOpenCircuitVoltageVolts // V_ocv: e.g. 1.48 V
);

public sealed record VanadiumFlowBatteryAnalysisResult(
    string FlowBatteryStackId,
    double NegalyteSocPercent,
    double PosalyteSocPercent,
    double SocImbalanceDeltaPercent,
    double TotalVanadiumConcentrationMismatchMolar,
    string VrfbOperatingRegime,
    string RebalancingControlGuidance,
    bool IsElectrolyteBalanced
);

public interface IVanadiumRedoxFlowBatteryBalancingEngine
{
    VanadiumFlowBatteryAnalysisResult AnalyzeElectrolyteBalance(VanadiumFlowBatteryTelemetry telemetry);
}
