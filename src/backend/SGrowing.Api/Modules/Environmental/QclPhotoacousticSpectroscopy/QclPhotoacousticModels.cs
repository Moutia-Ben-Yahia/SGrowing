namespace SGrowing.Api.Modules.Environmental.QclPhotoacousticSpectroscopy;

public record QclPhotoacousticTelemetry(
    string SensorId,
    string TargetGasSpecies,
    double LaserWavelengthMicrons,
    double LaserOpticalPowerMilliWatts,
    double QepasResonatorQFactor,
    double Demodulated2fHarmonicSignalMicroVolts,
    double CellAmbientPressureAtm,
    double CellTemperatureCelsius,
    double BaselineNoiseMicroVolts);

public record QclPhotoacousticAnalysisResult(
    string SensorId,
    string TargetGasSpecies,
    double SignalToNoiseRatio,
    double CalculatedConcentrationPpb,
    bool IsGasLeakSurgeDetected,
    string SensorOperationalStatus,
    string LaserTuningCorrection,
    string DiagnosticSummary);
