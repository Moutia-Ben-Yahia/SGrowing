namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// Leaky Integrate-and-Fire (LIF) Spiking Neural Network (SNN) anomaly detector for micro-payload conveyor systems.
/// </summary>
public interface ISpikingNeuralNetworkAnomalyDetector
{
    /// <summary>
    /// Processes an incoming DVS event through the LIF neuron array.
    /// Returns true if the corresponding neuron fired an output spike.
    /// </summary>
    bool ProcessEvent(DvsEvent evt);

    /// <summary>
    /// Processes a batch of events through the LIF neuron array and returns the IDs of neurons that spiked.
    /// </summary>
    IReadOnlyList<int> ProcessEventBatch(IReadOnlyList<DvsEvent> events);

    /// <summary>
    /// Analyzes conveyor behavior using SNN spike patterns, optical flow, and physical telemetry.
    /// </summary>
    NeuromorphicInspectionResult AnalyzeConveyor(
        string conveyorId,
        IReadOnlyList<DvsEvent> events,
        double opticalSpeedMetersPerSec,
        ConveyorBeltTelemetry? telemetry = null);

    /// <summary>
    /// Retrieves current state of all LIF neurons in the grid.
    /// </summary>
    IReadOnlyList<SpikeNeuronState> GetNeuronStates();

    /// <summary>
    /// Retrieves current state of a specific LIF neuron.
    /// </summary>
    SpikeNeuronState? GetNeuronState(int neuronId);

    /// <summary>
    /// Retrieves all currently active anomaly alarms.
    /// </summary>
    IReadOnlyList<NeuromorphicAlarmRecord> GetActiveAlarms();

    /// <summary>
    /// Clears an active alarm by its ID.
    /// </summary>
    bool ClearAlarm(string alarmId);

    /// <summary>
    /// Resets all neurons, spike statistics, and active alarms.
    /// </summary>
    void Reset();
}
