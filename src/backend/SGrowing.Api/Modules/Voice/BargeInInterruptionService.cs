using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Voice;

public class WarmTransferResult
{
    public bool Success { get; set; }
    public string TargetSipUri { get; set; } = string.Empty;
    public string TwiMlXml { get; set; } = string.Empty;
    public Dictionary<string, string> ContextHeaders { get; set; } = new();
}

public class BargeInInterruptionService
{
    private readonly ILogger<BargeInInterruptionService>? _logger;

    public BargeInInterruptionService(ILogger<BargeInInterruptionService>? logger = null)
    {
        _logger = logger;
    }

    public string CreateClearBufferCommand(string streamSid)
    {
        return $"{{\"event\":\"clear\",\"streamSid\":\"{streamSid}\"}}";
    }

    public string CreateMarkCommand(string streamSid, string markName)
    {
        return $"{{\"event\":\"mark\",\"streamSid\":\"{streamSid}\",\"mark\":{{\"name\":\"{markName}\"}}}}";
    }

    public string CreateMediaChunkCommand(string streamSid, string payloadBase64)
    {
        return $"{{\"event\":\"media\",\"streamSid\":\"{streamSid}\",\"media\":{{\"payload\":\"{payloadBase64}\"}}}}";
    }

    public WarmTransferResult InitiateWarmSipTransfer(
        StreamingVoiceCallSession session,
        string targetSipUri,
        string reason,
        string conversationSummary)
    {
        session.TriggerWarmTransfer(targetSipUri, reason);

        var headers = new Dictionary<string, string>
        {
            ["X-Session-Id"] = session.Id.ToString(),
            ["X-Call-Sid"] = session.CallSid,
            ["X-Transfer-Reason"] = reason,
            ["X-Interruption-Count"] = session.InterruptionCount.ToString(),
            ["X-Conversation-Summary"] = Uri.EscapeDataString(conversationSummary)
        };

        var headerQueryString = string.Join("&amp;", headers.Select(h => $"{h.Key}={h.Value}"));
        var destinationWithHeaders = $"{targetSipUri}?{headerQueryString}";

        var twiMl = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Response>\n  <Say voice=\"Polly.Joanna-Neural\">Transferring your call to a senior specialist now. Please hold.</Say>\n  <Dial>\n    <Sip>{destinationWithHeaders}</Sip>\n  </Dial>\n</Response>";

        _logger?.LogInformation("Warm SIP Transfer initiated for session {SessionId} to {SipUri}. Reason: {Reason}",
            session.Id, targetSipUri, reason);

        return new WarmTransferResult
        {
            Success = true,
            TargetSipUri = targetSipUri,
            TwiMlXml = twiMl,
            ContextHeaders = headers
        };
    }
}
