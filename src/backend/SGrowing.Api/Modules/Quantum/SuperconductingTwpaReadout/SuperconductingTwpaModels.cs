namespace SGrowing.Api.Modules.Quantum.SuperconductingTwpaReadout;

public sealed record SuperconductingTwpaTelemetry(
    string AmplifierId,
    double CenterFrequencyGigahertz, // f_0: e.g. 6.0 GHz
    double UsableBandwidthGigahertz, // Delta f: e.g. 3.5 GHz
    double AveragePowerGainDecibels, // G: e.g. 21.5 dB (target >= 20 dB)
    double GainRippleDecibels, // e.g. 1.8 dB (target <= 3.0 dB)
    double InputOneDbCompressionPowerDbm, // P_-1dB: e.g. -98.0 dBm
    double AddedNoisePhotons, // n_add: e.g. 0.65 photons (SQL = 0.5)
    double PumpPowerDbm // e.g. -55.0 dBm
);

public sealed record SuperconductingTwpaAnalysisResult(
    string AmplifierId,
    double AddedNoiseRelativeToSql,
    int MultiplexedQubitReadoutCapacity,
    double DynamicRangeDecibels,
    string TwpaOperatingRegime,
    string MicrowaveEngineeringGuidance,
    bool IsQuantumLimitedFidelityAchieved
);

public interface ISuperconductingTwpaReadoutEngine
{
    SuperconductingTwpaAnalysisResult AnalyzeTwpaPerformance(SuperconductingTwpaTelemetry telemetry);
}
