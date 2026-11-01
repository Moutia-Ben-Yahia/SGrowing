namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// Event polarity for Dynamic Vision Sensors (DVS).
/// </summary>
public enum EventPolarity
{
    /// <summary>
    /// Decrease in log-intensity (-1).
    /// </summary>
    Off = -1,

    /// <summary>
    /// Increase in log-intensity (+1).
    /// </summary>
    On = 1
}

/// <summary>
/// Types of conveyor anomalies detectable by edge neuromorphic vision.
/// </summary>
public enum ConveyorAnomalyType
{
    None = 0,
    ParcelJam = 1,
    BeltSlipTear = 2,
    PackagingRupture = 3,
    BarcodeMotionSmear = 4,
    HighSpeedOverhang = 5
}

/// <summary>
/// Severity classification of detected neuromorphic anomalies.
/// </summary>
public enum AnomalySeverity
{
    Normal = 0,
    Warning = 1,
    CriticalEStop = 2
}

/// <summary>
/// Microsecond-precision asynchronous event emitted by a Dynamic Vision Sensor (DVS).
/// </summary>
/// <param name="X">Pixel X coordinate.</param>
/// <param name="Y">Pixel Y coordinate.</param>
/// <param name="TimestampMicroseconds">Timestamp in microseconds (monotonic stream).</param>
/// <param name="Polarity">Polarity change (On = +1, Off = -1).</param>
public readonly record struct DvsEvent(
    int X,
    int Y,
    long TimestampMicroseconds,
    EventPolarity Polarity);

/// <summary>
/// Point on a 2D continuous exponential decay time surface.
/// </summary>
/// <param name="X">Pixel X coordinate.</param>
/// <param name="Y">Pixel Y coordinate.</param>
/// <param name="Intensity">Normalized time-surface value (0.0 to 1.0).</param>
/// <param name="VelocityX">Estimated optical flow velocity X (m/s).</param>
/// <param name="VelocityY">Estimated optical flow velocity Y (m/s).</param>
public readonly record struct TimeSurfacePoint(
    int X,
    int Y,
    double Intensity,
    double VelocityX,
    double VelocityY);

/// <summary>
/// Internal state of a Leaky Integrate-and-Fire (LIF) spiking neuron.
/// </summary>
/// <param name="NeuronId">Unique identifier of the neuron in the grid.</param>
/// <param name="X">Spatial X receptive field center.</param>
/// <param name="Y">Spatial Y receptive field center.</param>
/// <param name="MembranePotential">Current membrane potential Vi(t).</param>
/// <param name="LastSpikeTimeMicroseconds">Timestamp of last emitted spike.</param>
/// <param name="IsInRefractory">True if currently within refractory period.</param>
/// <param name="TotalSpikes">Lifetime spike count of this neuron.</param>
public readonly record struct SpikeNeuronState(
    int NeuronId,
    int X,
    int Y,
    double MembranePotential,
    long LastSpikeTimeMicroseconds,
    bool IsInRefractory,
    int TotalSpikes);

/// <summary>
/// Telemetry from micro-payload conveyor belt PLC/motor controller.
/// </summary>
/// <param name="ConveyorId">Conveyor system identifier.</param>
/// <param name="BeltSpeedMetersPerSec">Belt linear encoder speed in meters/second.</param>
/// <param name="VibrationLevelG">Accelerometer vibration level in Gs.</param>
/// <param name="MotorCurrentAmps">Motor current draw in Amperes.</param>
/// <param name="TimestampMicroseconds">Telemetry timestamp in microseconds.</param>
public record ConveyorBeltTelemetry(
    string ConveyorId,
    double BeltSpeedMetersPerSec,
    double VibrationLevelG,
    double MotorCurrentAmps,
    long TimestampMicroseconds);

/// <summary>
/// Comprehensive inspection result from neuromorphic SNN anomaly detection.
/// </summary>
/// <param name="AnomalyDetected">True if an anomaly was identified.</param>
/// <param name="AnomalyType">Classified anomaly type.</param>
/// <param name="Severity">Severity rating.</param>
/// <param name="Confidence">Detection confidence (0.0 to 1.0).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="DetectionTimestampMicroseconds">Timestamp when anomaly was detected.</param>
/// <param name="SustainedJamDurationMs">Duration of sustained jam in milliseconds.</param>
/// <param name="EmergencyStopTriggered">True if emergency stop was triggered.</param>
/// <param name="HotspotEvents">Key DVS events driving the anomaly detection.</param>
/// <param name="EstimatedOpticalSpeedMetersPerSec">Calculated optical flow speed.</param>
public record NeuromorphicInspectionResult(
    bool AnomalyDetected,
    ConveyorAnomalyType AnomalyType,
    AnomalySeverity Severity,
    double Confidence,
    string Description,
    long DetectionTimestampMicroseconds,
    double SustainedJamDurationMs,
    bool EmergencyStopTriggered,
    IReadOnlyList<DvsEvent> HotspotEvents,
    double EstimatedOpticalSpeedMetersPerSec);

/// <summary>
/// Request to ingest microsecond DVS event stream batch.
/// </summary>
public record DvsEventIngestRequest(
    string StreamId,
    string ConveyorId,
    IReadOnlyList<DvsEvent> Events);

/// <summary>
/// Response for DVS event stream batch ingestion.
/// </summary>
public record DvsEventIngestResponse(
    bool Success,
    int IngestedCount,
    int FilteredCount,
    double EventRateEventsPerMs,
    double PolarBalanceRatio,
    string Message);

/// <summary>
/// Request to analyze conveyor events with optional telemetry.
/// </summary>
public record ConveyorAnalysisRequest(
    string ConveyorId,
    IReadOnlyList<DvsEvent> Events,
    ConveyorBeltTelemetry? Telemetry = null);

/// <summary>
/// Response from conveyor neuromorphic analysis.
/// </summary>
public record ConveyorAnalysisResponse(
    bool Success,
    NeuromorphicInspectionResult Result,
    string Message);

/// <summary>
/// Global neuromorphic vision processing metrics.
/// </summary>
public record NeuromorphicMetricsResponse(
    string StreamId,
    long TotalEventsProcessed,
    double CurrentRateEventsPerMs,
    double PolarBalanceRatio,
    int ActiveSpikeClusters,
    int TotalAnomaliesDetected,
    int TotalEStopsTriggered);

/// <summary>
/// Active alarm record for conveyor anomaly interceptor.
/// </summary>
public record NeuromorphicAlarmRecord(
    string AlarmId,
    string ConveyorId,
    ConveyorAnomalyType AnomalyType,
    AnomalySeverity Severity,
    string Message,
    long TimestampMicroseconds,
    bool IsActive);

/// <summary>
/// Response listing active alarms.
/// </summary>
public record ActiveAlarmsResponse(
    IReadOnlyList<NeuromorphicAlarmRecord> Alarms);

/// <summary>
/// Configuration parameters for Leaky Integrate-and-Fire (LIF) spiking neural network.
/// </summary>
public record LifNeuronConfig(
    double MembraneTimeConstantUs = 10_000.0,
    double RestingPotential = 0.0,
    double ThresholdPotential = 1.0,
    double ResetPotential = 0.0,
    long RefractoryPeriodUs = 2_000,
    double SynapticWeight = 0.25,
    int GridDimension = 16,
    double AnomalySigmaThreshold = 3.5,
    double CriticalJamDurationMs = 20.0);

/// <summary>
/// Configuration parameters for Asynchronous Time Surface Filter.
/// </summary>
public record TimeSurfaceConfig(
    int Width = 128,
    int Height = 128,
    double DecayTimeConstantUs = 10_000.0,
    double MetersPerPixel = 0.001);
