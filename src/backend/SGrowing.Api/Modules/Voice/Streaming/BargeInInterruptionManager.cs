using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice.Streaming;

public record BargeInDetectedEventArgs(
    Guid SessionId,
    DateTime InterruptedAtUtc,
    double TriggeringRmsEnergy,
    double ContinuousSpeechDurationMs,
    int SavedAudioMs,
    int FlushedChunksCount,
    string Reason
);

public record BargeInTelemetry(
    Guid Id,
    Guid SessionId,
    DateTime InterruptedAtUtc,
    double InterruptionRmsEnergy,
    double ContinuousSpeechDurationMs,
    int SavedAudioMs,
    int FlushedChunksCount,
    string Reason
);

public record BargeInEvaluationResult(
    bool InterruptionTriggered,
    double CurrentRms,
    double ContinuousSpeechDurationMs,
    StreamingPipelineState PreviousState,
    StreamingPipelineState NewState,
    int SavedAudioMs,
    int FlushedChunksCount,
    string? Message = null
);

public class BargeInSessionTracker
{
    private readonly Lock _sync = new();
    private readonly List<BargeInTelemetry> _history = [];

    public Guid SessionId { get; init; }
    public StreamingPipelineState State { get; set; } = StreamingPipelineState.Listening;
    public double ContinuousSpeechMs { get; set; }
    public DateTime? SpeechStartUtc { get; set; }
    public int InterruptionCount { get; set; }

    public BargeInSessionTracker(Guid sessionId)
    {
        SessionId = sessionId;
    }

    public void RecordTelemetry(BargeInTelemetry item)
    {
        lock (_sync)
        {
            _history.Add(item);
        }
    }

    public IReadOnlyList<BargeInTelemetry> GetHistory()
    {
        lock (_sync)
        {
            return _history.ToArray();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            ContinuousSpeechMs = 0;
            SpeechStartUtc = null;
        }
    }
}

public interface IBargeInInterruptionManager
{
    event EventHandler<BargeInDetectedEventArgs>? BargeInDetected;

    double RmsThreshold { get; set; }
    double SustainedDurationThresholdMs { get; set; }

    BargeInEvaluationResult ProcessIncomingAudioEnergy(
        Guid sessionId,
        double rmsEnergy,
        int frameDurationMs = 20,
        StreamingPipelineState? currentStateOverride = null);

    BargeInEvaluationResult TriggerExplicitBargeIn(
        Guid sessionId,
        string reason = "ExplicitBargeInSignal");

    void SetSessionState(Guid sessionId, StreamingPipelineState newState);
    StreamingPipelineState GetSessionState(Guid sessionId);
    IReadOnlyList<BargeInTelemetry> GetTelemetryHistory(Guid sessionId);
    void ResetSession(Guid sessionId);
}

public class BargeInInterruptionManager : IBargeInInterruptionManager
{
    private readonly ILowLatencyAudioStreamingEngine _streamingEngine;
    private readonly ILogger<BargeInInterruptionManager>? _logger;
    private readonly ConcurrentDictionary<Guid, BargeInSessionTracker> _sessions = new();

    public event EventHandler<BargeInDetectedEventArgs>? BargeInDetected;

    public double RmsThreshold { get; set; } = 400.0;
    public double SustainedDurationThresholdMs { get; set; } = 140.0; // 140ms sustained user audio threshold

    public BargeInInterruptionManager(
        ILowLatencyAudioStreamingEngine streamingEngine,
        ILogger<BargeInInterruptionManager>? logger = null)
    {
        _streamingEngine = streamingEngine;
        _logger = logger;
    }

    private BargeInSessionTracker GetOrCreateSession(Guid sessionId)
    {
        return _sessions.GetOrAdd(sessionId, id => new BargeInSessionTracker(id));
    }

    public void SetSessionState(Guid sessionId, StreamingPipelineState newState)
    {
        var tracker = GetOrCreateSession(sessionId);
        tracker.State = newState;
        if (newState != StreamingPipelineState.AgentSpeaking)
        {
            tracker.Reset();
        }
    }

    public StreamingPipelineState GetSessionState(Guid sessionId)
    {
        return GetOrCreateSession(sessionId).State;
    }

    public BargeInEvaluationResult ProcessIncomingAudioEnergy(
        Guid sessionId,
        double rmsEnergy,
        int frameDurationMs = 20,
        StreamingPipelineState? currentStateOverride = null)
    {
        var tracker = GetOrCreateSession(sessionId);

        if (currentStateOverride.HasValue)
        {
            tracker.State = currentStateOverride.Value;
        }

        var currentState = tracker.State;

        // Barge-in is only triggered when the agent is currently speaking
        if (currentState != StreamingPipelineState.AgentSpeaking)
        {
            tracker.Reset();
            return new BargeInEvaluationResult(
                InterruptionTriggered: false,
                CurrentRms: rmsEnergy,
                ContinuousSpeechDurationMs: 0,
                PreviousState: currentState,
                NewState: currentState,
                SavedAudioMs: 0,
                FlushedChunksCount: 0,
                Message: $"Audio processed during state '{currentState}', barge-in monitor inactive."
            );
        }

        if (rmsEnergy >= RmsThreshold)
        {
            tracker.SpeechStartUtc ??= DateTime.UtcNow;
            tracker.ContinuousSpeechMs += frameDurationMs;

            if (tracker.ContinuousSpeechMs >= SustainedDurationThresholdMs)
            {
                // Condition met (> 140ms of sustained speech energy above threshold while agent speaking)
                var collisionTimestamp = DateTime.UtcNow;
                var (flushedChunks, savedMs) = _streamingEngine.FlushOutboundQueue(sessionId);

                var prev = tracker.State;
                tracker.State = StreamingPipelineState.Listening;
                tracker.InterruptionCount++;

                var telemetry = new BargeInTelemetry(
                    Id: Guid.NewGuid(),
                    SessionId: sessionId,
                    InterruptedAtUtc: collisionTimestamp,
                    InterruptionRmsEnergy: rmsEnergy,
                    ContinuousSpeechDurationMs: tracker.ContinuousSpeechMs,
                    SavedAudioMs: savedMs,
                    FlushedChunksCount: flushedChunks,
                    Reason: "AcousticEnergyThresholdSurpassed"
                );

                tracker.RecordTelemetry(telemetry);
                tracker.Reset();

                var eventArgs = new BargeInDetectedEventArgs(
                    SessionId: sessionId,
                    InterruptedAtUtc: collisionTimestamp,
                    TriggeringRmsEnergy: rmsEnergy,
                    ContinuousSpeechDurationMs: telemetry.ContinuousSpeechDurationMs,
                    SavedAudioMs: savedMs,
                    FlushedChunksCount: flushedChunks,
                    Reason: telemetry.Reason
                );

                try
                {
                    BargeInDetected?.Invoke(this, eventArgs);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing BargeInDetected event subscribers for session {SessionId}.", sessionId);
                }

                _logger?.LogInformation(
                    "Barge-in DETECTED on session {SessionId}. Continuous speech: {Ms}ms. Outbound flushed: {Chunks} chunks, {SavedMs}ms. State changed from {Prev} to {New}.",
                    sessionId, telemetry.ContinuousSpeechDurationMs, flushedChunks, savedMs, prev, tracker.State);

                return new BargeInEvaluationResult(
                    InterruptionTriggered: true,
                    CurrentRms: rmsEnergy,
                    ContinuousSpeechDurationMs: telemetry.ContinuousSpeechDurationMs,
                    PreviousState: prev,
                    NewState: tracker.State,
                    SavedAudioMs: savedMs,
                    FlushedChunksCount: flushedChunks,
                    Message: "Barge-in threshold exceeded; agent speech truncated and output flushed."
                );
            }

            return new BargeInEvaluationResult(
                InterruptionTriggered: false,
                CurrentRms: rmsEnergy,
                ContinuousSpeechDurationMs: tracker.ContinuousSpeechMs,
                PreviousState: currentState,
                NewState: currentState,
                SavedAudioMs: 0,
                FlushedChunksCount: 0,
                Message: $"Speech energy detected ({tracker.ContinuousSpeechMs}ms / {SustainedDurationThresholdMs}ms required)."
            );
        }

        // Energy dropped below threshold - reset continuous duration counter
        tracker.Reset();
        return new BargeInEvaluationResult(
            InterruptionTriggered: false,
            CurrentRms: rmsEnergy,
            ContinuousSpeechDurationMs: 0,
            PreviousState: currentState,
            NewState: currentState,
            SavedAudioMs: 0,
            FlushedChunksCount: 0,
            Message: "Energy below barge-in threshold."
        );
    }

    public BargeInEvaluationResult TriggerExplicitBargeIn(
        Guid sessionId,
        string reason = "ExplicitBargeInSignal")
    {
        var tracker = GetOrCreateSession(sessionId);
        var prev = tracker.State;
        var collisionTimestamp = DateTime.UtcNow;

        var (flushedChunks, savedMs) = _streamingEngine.FlushOutboundQueue(sessionId);
        tracker.State = StreamingPipelineState.Listening;
        tracker.InterruptionCount++;

        var telemetry = new BargeInTelemetry(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            InterruptedAtUtc: collisionTimestamp,
            InterruptionRmsEnergy: 0.0,
            ContinuousSpeechDurationMs: tracker.ContinuousSpeechMs,
            SavedAudioMs: savedMs,
            FlushedChunksCount: flushedChunks,
            Reason: reason
        );

        tracker.RecordTelemetry(telemetry);
        tracker.Reset();

        var eventArgs = new BargeInDetectedEventArgs(
            SessionId: sessionId,
            InterruptedAtUtc: collisionTimestamp,
            TriggeringRmsEnergy: 0.0,
            ContinuousSpeechDurationMs: 0,
            SavedAudioMs: savedMs,
            FlushedChunksCount: flushedChunks,
            Reason: reason
        );

        try
        {
            BargeInDetected?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error firing explicit BargeInDetected event for session {SessionId}.", sessionId);
        }

        return new BargeInEvaluationResult(
            InterruptionTriggered: true,
            CurrentRms: 0.0,
            ContinuousSpeechDurationMs: 0,
            PreviousState: prev,
            NewState: tracker.State,
            SavedAudioMs: savedMs,
            FlushedChunksCount: flushedChunks,
            Message: $"Explicit barge-in triggered: {reason}."
        );
    }

    public IReadOnlyList<BargeInTelemetry> GetTelemetryHistory(Guid sessionId)
    {
        return GetOrCreateSession(sessionId).GetHistory();
    }

    public void ResetSession(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var tracker))
        {
            tracker.Reset();
            tracker.State = StreamingPipelineState.Listening;
        }
    }
}
