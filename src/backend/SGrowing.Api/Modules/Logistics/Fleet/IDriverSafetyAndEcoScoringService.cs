namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// Evaluates driver performance, telematics kinetics, eco-driving metrics, and courier green fuel bonus eligibility.
/// </summary>
public interface IDriverSafetyAndEcoScoringService
{
    /// <summary>
    /// Analyzes a time-window of raw OBD2 telemetry packets for a driver and generates an eco and safety score.
    /// </summary>
    DriverEcoScore EvaluateDriverSession(string driverId, IEnumerable<Obd2TelemetryPacket> telemetryWindow);

    /// <summary>
    /// Retrieves the most recently computed score record for a driver.
    /// </summary>
    DriverEcoScore? GetDriverScore(string driverId);
}
