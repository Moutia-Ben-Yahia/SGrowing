using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice.Streaming;

public enum AmdClassification
{
    Unknown = 0,
    Human = 1,
    Machine = 2,
    BeepToneDetected = 3,
    Silence = 4
}

public enum AmdDetectionMode
{
    None = 0,
    FrequencyAnalysis = 1,
    TemporalSpeechAnalysis = 2,
    FastToneHeuristic = 3
}

public enum AmdAction
{
    ContinueListening = 0,
    TriggerConversationalGreeting = 1,
    DropVoicemailAndTerminate = 2
}

public record AmdDecisionResult(
    Guid SessionId,
    AmdClassification Classification,
    AmdDetectionMode Mode,
    AmdAction Action,
    double Confidence,
    double DetectedToneHz,
    double SpeechDurationMs,
    double SilenceDurationMs,
    string? VoicemailTemplateDropped,
    bool ShouldTerminateCall,
    string Reasoning,
    string SpokenPrompt
);

public record AmdEvaluationRequest(
    Guid SessionId,
    string? AudioBase64 = null,
    AudioEncodingFormat Encoding = AudioEncodingFormat.Pcm16_16kHz,
    double ElapsedCallTimeMs = 0,
    double ContinuousSpeechMs = 0,
    double TrailingSilenceMs = 0,
    string? CustomerName = null,
    string? OrderNumber = null
);

public class AmdCallTracker
{
    public Guid SessionId { get; init; }
    public double TotalElapsedMs { get; set; }
    public double TotalSpeechDurationMs { get; set; }
    public double CurrentContinuousSpeechMs { get; set; }
    public double CurrentTrailingSilenceMs { get; set; }
    public bool BeepDetected { get; set; }
    public double DetectedBeepFrequencyHz { get; set; }
    public AmdClassification FinalClassification { get; set; } = AmdClassification.Unknown;
    public bool VoicemailDropped { get; set; }

    public AmdCallTracker(Guid sessionId)
    {
        SessionId = sessionId;
    }
}

public interface INeuralAnsweringMachineDetector
{
    AmdDecisionResult EvaluateAudioBuffer(AmdEvaluationRequest request);

    AmdDecisionResult EvaluatePcmSamples(
        Guid sessionId,
        ReadOnlySpan<short> samples,
        int sampleRate,
        double elapsedCallTimeMs,
        double continuousSpeechMs,
        double trailingSilenceMs,
        string? customerName = null,
        string? orderNumber = null);

    double CalculateGoertzelPower(ReadOnlySpan<short> samples, int sampleRate, double targetFrequencyHz);
    bool DetectToneFrequency(ReadOnlySpan<short> samples, int sampleRate, out double detectedToneHz, out double toneStrength);

    AmdDecisionResult DropVoicemailAndTerminate(
        Guid sessionId,
        string customerName,
        string orderNumber,
        string? customTemplate = null);
}

public class NeuralAnsweringMachineDetector : INeuralAnsweringMachineDetector
{
    private readonly ILogger<NeuralAnsweringMachineDetector>? _logger;
    private readonly ConcurrentDictionary<Guid, AmdCallTracker> _trackers = new();

    public const double BeepDetectionWindowMs = 2500.0;
    public const double LongMonologueThresholdMs = 3500.0; // > 3.5s monologue without pause -> answering machine
    public const double HumanMaxSpeechGreetingMs = 2000.0; // Short phrase <= 2.0s
    public const double HumanMinSilencePauseMs = 600.0;    // followed by > 600ms conversational pause -> human

    public NeuralAnsweringMachineDetector(ILogger<NeuralAnsweringMachineDetector>? logger = null)
    {
        _logger = logger;
    }

    private AmdCallTracker GetOrCreateTracker(Guid sessionId)
    {
        return _trackers.GetOrAdd(sessionId, id => new AmdCallTracker(id));
    }

    public AmdDecisionResult EvaluateAudioBuffer(AmdEvaluationRequest request)
    {
        ReadOnlySpan<short> samples = [];
        int sampleRate = AudioFrameConstants.GetSampleRate(request.Encoding);

        if (!string.IsNullOrWhiteSpace(request.AudioBase64))
        {
            try
            {
                byte[] raw = Convert.FromBase64String(request.AudioBase64);
                if (request.Encoding == AudioEncodingFormat.G711u)
                {
                    short[] decoded = new short[raw.Length];
                    for (int i = 0; i < raw.Length; i++)
                    {
                        decoded[i] = LowLatencyAudioStreamingEngine.MuLawToPcm16Sample(raw[i]);
                    }
                    samples = decoded;
                    sampleRate = 8000;
                }
                else
                {
                    int sampleCount = raw.Length / 2;
                    short[] pcm = new short[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        pcm[i] = BitConverter.ToInt16(raw.AsSpan(i * 2, 2));
                    }
                    samples = pcm;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to decode base64 audio for AMD evaluation on session {SessionId}.", request.SessionId);
            }
        }

        return EvaluatePcmSamples(
            sessionId: request.SessionId,
            samples: samples,
            sampleRate: sampleRate,
            elapsedCallTimeMs: request.ElapsedCallTimeMs,
            continuousSpeechMs: request.ContinuousSpeechMs,
            trailingSilenceMs: request.TrailingSilenceMs,
            customerName: request.CustomerName,
            orderNumber: request.OrderNumber
        );
    }

    public AmdDecisionResult EvaluatePcmSamples(
        Guid sessionId,
        ReadOnlySpan<short> samples,
        int sampleRate,
        double elapsedCallTimeMs,
        double continuousSpeechMs,
        double trailingSilenceMs,
        string? customerName = null,
        string? orderNumber = null)
    {
        var tracker = GetOrCreateTracker(sessionId);
        tracker.TotalElapsedMs = Math.Max(tracker.TotalElapsedMs, elapsedCallTimeMs);
        tracker.CurrentContinuousSpeechMs = continuousSpeechMs;
        tracker.CurrentTrailingSilenceMs = trailingSilenceMs;

        string custName = string.IsNullOrWhiteSpace(customerName) ? "Valued Customer" : customerName;
        string ordNum = string.IsNullOrWhiteSpace(orderNumber) ? "your pending order" : $"order #{orderNumber}";

        // Mode 1: Frequency Analysis (Detecting 1000Hz or 440Hz voicemail beep tones within connect window)
        if (samples.Length > 0 && DetectToneFrequency(samples, sampleRate, out double detectedTone, out double toneStrength))
        {
            tracker.BeepDetected = true;
            tracker.DetectedBeepFrequencyHz = detectedTone;
            tracker.FinalClassification = AmdClassification.BeepToneDetected;

            _logger?.LogInformation(
                "AMD Frequency Detection: Beep tone of {Freq:F1}Hz (Strength: {Strength:F2}) detected on session {SessionId}.",
                detectedTone, toneStrength, sessionId);

            return DropVoicemailAndTerminate(sessionId, custName, ordNum);
        }

        // Mode 2: Temporal Speech Duration Analysis
        // 2A: Long monologue without pause (>3.5s) -> Answering Machine Greeting
        if (continuousSpeechMs >= LongMonologueThresholdMs)
        {
            tracker.FinalClassification = AmdClassification.Machine;
            _logger?.LogInformation(
                "AMD Temporal Detection: Long monologue greeting ({Ms:F0}ms >= {Threshold}ms) detected on session {SessionId} -> Answering Machine.",
                continuousSpeechMs, LongMonologueThresholdMs, sessionId);

            return DropVoicemailAndTerminate(sessionId, custName, ordNum);
        }

        // 2B: Short greeting followed by conversational pause -> Live Human
        if (continuousSpeechMs > 200.0 && continuousSpeechMs <= HumanMaxSpeechGreetingMs && trailingSilenceMs >= HumanMinSilencePauseMs)
        {
            tracker.FinalClassification = AmdClassification.Human;
            string humanGreeting = $"Hi {custName}! This is Sarah from SGrowing customer care regarding {ordNum}. Am I speaking with {custName}?";

            _logger?.LogInformation(
                "AMD Temporal Detection: Short greeting ({SpeechMs:F0}ms) followed by conversational silence ({SilenceMs:F0}ms) detected on session {SessionId} -> Live Human.",
                continuousSpeechMs, trailingSilenceMs, sessionId);

            return new AmdDecisionResult(
                SessionId: sessionId,
                Classification: AmdClassification.Human,
                Mode: AmdDetectionMode.TemporalSpeechAnalysis,
                Action: AmdAction.TriggerConversationalGreeting,
                Confidence: 0.94,
                DetectedToneHz: 0.0,
                SpeechDurationMs: continuousSpeechMs,
                SilenceDurationMs: trailingSilenceMs,
                VoicemailTemplateDropped: null,
                ShouldTerminateCall: false,
                Reasoning: $"Short greeting ({continuousSpeechMs:F0}ms) followed by natural conversational pause ({trailingSilenceMs:F0}ms) indicates live human receiver.",
                SpokenPrompt: humanGreeting
            );
        }

        // In-progress or silence
        return new AmdDecisionResult(
            SessionId: sessionId,
            Classification: AmdClassification.Unknown,
            Mode: AmdDetectionMode.None,
            Action: AmdAction.ContinueListening,
            Confidence: 0.50,
            DetectedToneHz: 0.0,
            SpeechDurationMs: continuousSpeechMs,
            SilenceDurationMs: trailingSilenceMs,
            VoicemailTemplateDropped: null,
            ShouldTerminateCall: false,
            Reasoning: "Awaiting sufficient temporal speech pattern or beep tone frequency confirmation.",
            SpokenPrompt: string.Empty
        );
    }

    public bool DetectToneFrequency(ReadOnlySpan<short> samples, int sampleRate, out double detectedToneHz, out double toneStrength)
    {
        detectedToneHz = 0.0;
        toneStrength = 0.0;

        if (samples.Length < 80 || sampleRate <= 0) return false;

        // Calculate total broadband energy
        double totalBroadbandPower = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            double val = samples[i];
            totalBroadbandPower += val * val;
        }

        if (totalBroadbandPower < 1000.0)
        {
            // Floor noise, insufficient power
            return false;
        }

        // Test standard frequencies: 1000Hz (Voicemail Beep) and 440Hz (Call progress / dial tone component)
        double[] targetFreqs = [1000.0, 440.0];

        foreach (var freq in targetFreqs)
        {
            double tonePower = CalculateGoertzelPower(samples, sampleRate, freq);
            // Normalized Goertzel spectral concentration: for a pure single-frequency tone,
            // (2.0 * tonePower) / (N * broadbandPower) approaches ~1.0, while for broadband noise it is ~ 2/N (< 0.05).
            double ratio = (2.0 * tonePower) / (samples.Length * totalBroadbandPower);

            if (ratio > 0.35)
            {
                detectedToneHz = freq;
                toneStrength = ratio;
                return true;
            }
        }

        return false;
    }

    public double CalculateGoertzelPower(ReadOnlySpan<short> samples, int sampleRate, double targetFrequencyHz)
    {
        if (samples.IsEmpty || sampleRate <= 0) return 0.0;

        int n = samples.Length;
        double omega = (2.0 * Math.PI * targetFrequencyHz) / sampleRate;
        double coeff = 2.0 * Math.Cos(omega);

        double s0 = 0.0;
        double s1 = 0.0;
        double s2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            s0 = samples[i] + (coeff * s1) - s2;
            s2 = s1;
            s1 = s0;
        }

        double power = (s1 * s1) + (s2 * s2) - (coeff * s1 * s2);
        return Math.Max(0.0, power);
    }

    public AmdDecisionResult DropVoicemailAndTerminate(
        Guid sessionId,
        string customerName,
        string orderNumber,
        string? customTemplate = null)
    {
        var tracker = GetOrCreateTracker(sessionId);
        tracker.VoicemailDropped = true;

        string template = customTemplate ??
            $"Hello {customerName}, this is an automated dispatch notification from SGrowing regarding {orderNumber}. Your delivery is scheduled and our courier will arrive shortly. Please have exact cash ready or click the tracking link in your SMS. Have a great day!";

        _logger?.LogInformation("Voicemail dropped for session {SessionId}. Call marked for termination.", sessionId);

        return new AmdDecisionResult(
            SessionId: sessionId,
            Classification: tracker.FinalClassification == AmdClassification.BeepToneDetected ? AmdClassification.BeepToneDetected : AmdClassification.Machine,
            Mode: tracker.BeepDetected ? AmdDetectionMode.FrequencyAnalysis : AmdDetectionMode.TemporalSpeechAnalysis,
            Action: AmdAction.DropVoicemailAndTerminate,
            Confidence: 0.98,
            DetectedToneHz: tracker.DetectedBeepFrequencyHz,
            SpeechDurationMs: tracker.CurrentContinuousSpeechMs,
            SilenceDurationMs: tracker.CurrentTrailingSilenceMs,
            VoicemailTemplateDropped: template,
            ShouldTerminateCall: true,
            Reasoning: tracker.BeepDetected
                ? $"Voicemail beep tone ({tracker.DetectedBeepFrequencyHz:F0}Hz) detected; dropped personalized voicemail template and scheduled call termination."
                : $"Long greeting monologue ({tracker.CurrentContinuousSpeechMs:F0}ms) detected answering machine; dropped voicemail template and terminated.",
            SpokenPrompt: template
        );
    }
}
