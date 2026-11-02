namespace SGrowing.Api.Modules.Quantum.StrontiumOpticalLatticeClock;

public sealed record StrontiumClockTelemetry(
    string ClockSystemId,
    double TrappedSr87AtomCount,
    double LatticeLaserWavelengthNanometers,
    double ClockLaserLinewidthHertz,
    double MeasuredFractionalFrequencyOffset,
    double BlackbodyRadiationTemperatureKelvin,
    double GeodeticElevationMeters,
    double FrequencyCombRepetitionRateMegaHertz,
    bool CarrierEnvelopeOffsetLocked
);

public sealed record StrontiumClockAnalysisResult(
    string ClockSystemId,
    double AllanDeviationInstabilityAtOneSecond,
    double BlackbodyRadiationFractionalShift,
    double GravitationalRedshiftCorrection,
    string MetrologyPrecisionRegime,
    string LaserFrequencySynthesisAction,
    bool IsClockMetrologicallyValid
);

public interface IStrontiumOpticalLatticeClockEngine
{
    StrontiumClockAnalysisResult AnalyzeOpticalAtomicClock(StrontiumClockTelemetry telemetry);
}
