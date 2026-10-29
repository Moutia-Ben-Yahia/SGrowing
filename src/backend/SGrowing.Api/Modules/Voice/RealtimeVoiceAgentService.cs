using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice;

public record RichVoiceTurnResult(
    string AgentResponse,
    double SentimentScore,
    List<string> BuyingSignals
);

public class RealtimeVoiceAgentService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<RealtimeVoiceAgentService>? _logger;
    private readonly VoiceAiStreamService _streamService;

    public RealtimeVoiceAgentService(
        SGrowingDbContext db,
        ILogger<RealtimeVoiceAgentService>? logger = null,
        VoiceAiStreamService? streamService = null)
    {
        _db = db;
        _logger = logger;
        _streamService = streamService ?? new VoiceAiStreamService(null, db, null, null);
    }

    public async Task<RichVoiceTurnResult> ProcessRichVoiceTurnAsync(
        Guid tenantId,
        Guid sessionId,
        string callerSpeechTranscript,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.VoiceCallSessions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Voice call session {sessionId} not found for tenant {tenantId}.");
        }

        var speech = (callerSpeechTranscript ?? string.Empty).Trim();
        var sentiment = _streamService.CalculateSentimentScore(speech);
        var signals = _streamService.ExtractBuyingSignals(speech);

        var agentResponse = await ProcessVoiceTurnAsync(tenantId, sessionId, speech, cancellationToken);

        return new RichVoiceTurnResult(agentResponse, sentiment, signals);
    }

    public async Task<string> ProcessVoiceTurnAsync(
        Guid tenantId,
        Guid sessionId,
        string callerSpeechTranscript,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.VoiceCallSessions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Voice call session {sessionId} not found for tenant {tenantId}.");
        }

        var speech = (callerSpeechTranscript ?? string.Empty).Trim();
        var normalized = speech.ToLowerInvariant();

        // Voice reasoning loop: semantic analysis for concise, voice-optimized output
        string agentResponse;

        if (normalized.Contains("price") || normalized.Contains("pricing") || normalized.Contains("cost") || normalized.Contains("tier") || normalized.Contains("plan"))
        {
            agentResponse = "Our plans start at forty-nine dollars a month for the Starter tier, and two hundred forty-nine dollars for the Growth tier. Would you like me to book a demo?";
        }
        else if (normalized.Contains("book") || normalized.Contains("demo") || normalized.Contains("schedule") || normalized.Contains("appointment") || normalized.Contains("meeting"))
        {
            agentResponse = "I would be glad to schedule a demo with our team. We have openings tomorrow at two PM or Thursday morning. Which one works better for you?";
        }
        else if (normalized.Contains("feature") || normalized.Contains("what do you do") || normalized.Contains("sdr") || normalized.Contains("outbound") || normalized.Contains("whatsapp"))
        {
            agentResponse = "SGrowing provides an autonomous AI growth platform with omnichannel messaging, automated outbound cadences, and CRM integration.";
        }
        else if (normalized.Contains("support") || normalized.Contains("agent") || normalized.Contains("human") || normalized.Contains("help") || normalized.Contains("transfer"))
        {
            agentResponse = "I understand. I am transferring you to a senior specialist right away. Please hold the line.";
        }
        else if (normalized.Contains("hello") || normalized.Contains("hi") || normalized.Contains("hey") || normalized.Contains("good morning") || normalized.Contains("good afternoon"))
        {
            agentResponse = "Hello! Thank you for calling SGrowing. How can I assist you today?";
        }
        else
        {
            agentResponse = "Thank you for sharing that. SGrowing can help streamline your sales pipelines and customer messaging. Could you tell me more about what your team needs?";
        }

        // Accumulate turn in transcript
        var turnLog = $"Caller: {speech}\nAgent: {agentResponse}";
        session.AppendTranscript(turnLog);

        await _db.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("Processed voice turn for session {SessionId}. Response generated.", sessionId);

        return agentResponse;
    }
}
