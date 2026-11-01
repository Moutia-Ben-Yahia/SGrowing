using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI.Firewall;

public record PromptCanaryToken(
    string TokenId,
    string SessionId,
    string PlainHexSignature,
    string ZeroWidthEncodedToken,
    DateTime CreatedAtUtc
);

public record CanarySecurityIncident(
    Guid Id,
    string SessionId,
    string IncidentType,
    string Severity,
    string? TokenId,
    string Description,
    DateTime DetectedAtUtc
);

public record CanaryScanResult(
    bool IsExfiltrationDetected,
    string? DetectedTokenId,
    string? IncidentType,
    bool SessionLocked,
    string Message
);

public interface ICryptographicPromptCanaryService
{
    PromptCanaryToken GenerateCanary(string sessionId, string? tenantId = null);
    string InjectCanary(string systemPromptTemplate, string sessionId);
    CanaryScanResult ScanResponse(string sessionId, string responseText);
    IAsyncEnumerable<string> ScanStreamAsync(string sessionId, IAsyncEnumerable<string> chunkStream, CancellationToken ct = default);
    bool IsSessionLocked(string sessionId);
    bool LockSession(string sessionId, string reason = "PromptExfiltrationAttempt");
    bool UnlockSession(string sessionId);
    IReadOnlyList<CanarySecurityIncident> GetIncidents(string? sessionId = null);
    void ClearIncidents();
}

public class CryptographicPromptCanaryService : ICryptographicPromptCanaryService
{
    private readonly ILogger<CryptographicPromptCanaryService>? _logger;
    private readonly byte[] _hmacSecretKey;

    // Zero-width alphabet (quaternary 2-bit mapping)
    private const char Zw0 = '\u200B'; // Zero-Width Space       (00)
    private const char Zw1 = '\u200C'; // Zero-Width Non-Joiner   (01)
    private const char Zw2 = '\u200D'; // Zero-Width Joiner       (10)
    private const char Zw3 = '\uFEFF'; // Zero-Width No-Break / BOM (11)

    // Sentinel boundaries for zero-width framing
    private static readonly string SentinelPrefix = new([Zw3, Zw0, Zw1, Zw2]);
    private static readonly string SentinelSuffix = new([Zw2, Zw1, Zw0, Zw3]);

    // Active tokens per session
    private readonly ConcurrentDictionary<string, PromptCanaryToken> _sessionTokens = new();
    private readonly ConcurrentDictionary<string, DateTime> _lockedSessions = new();
    private readonly ConcurrentBag<CanarySecurityIncident> _incidents = new();

    public CryptographicPromptCanaryService(
        ILogger<CryptographicPromptCanaryService>? logger = null,
        byte[]? hmacSecretKey = null)
    {
        _logger = logger;
        if (hmacSecretKey is { Length: >= 16 })
        {
            _hmacSecretKey = [.. hmacSecretKey];
        }
        else
        {
            _hmacSecretKey = new byte[32];
            RandomNumberGenerator.Fill(_hmacSecretKey);
        }
    }

    /// <summary>
    /// Generates a session-specific HMAC-SHA256 canary token encoded in zero-width characters.
    /// </summary>
    public PromptCanaryToken GenerateCanary(string sessionId, string? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var tokenId = Guid.NewGuid().ToString("N");
        var payload = $"{tenantId ?? "default"}:{sessionId}:{tokenId}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(_hmacSecretKey);
        var hash = hmac.ComputeHash(payloadBytes);

        // Truncate to 16 bytes (128 bits = 64 zero-width chars)
        var truncatedHash = hash[..16];
        var hexSignature = Convert.ToHexString(truncatedHash);

        var zeroWidthCore = EncodeToZeroWidth(truncatedHash);
        var zeroWidthToken = $"{SentinelPrefix}{zeroWidthCore}{SentinelSuffix}";

        var canary = new PromptCanaryToken(
            TokenId: tokenId,
            SessionId: sessionId,
            PlainHexSignature: hexSignature,
            ZeroWidthEncodedToken: zeroWidthToken,
            CreatedAtUtc: DateTime.UtcNow
        );

        _sessionTokens[sessionId] = canary;
        return canary;
    }

    /// <summary>
    /// Injects the generated canary token invisibly into a system prompt template.
    /// </summary>
    public string InjectCanary(string systemPromptTemplate, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var canary = GenerateCanary(sessionId);

        if (string.IsNullOrEmpty(systemPromptTemplate))
        {
            return canary.ZeroWidthEncodedToken;
        }

        // Place canary invisibly near the start of the prompt
        var newlineIndex = systemPromptTemplate.IndexOf('\n');
        if (newlineIndex >= 0)
        {
            return systemPromptTemplate.Insert(newlineIndex, canary.ZeroWidthEncodedToken);
        }

        return $"{systemPromptTemplate}{canary.ZeroWidthEncodedToken}";
    }

    /// <summary>
    /// Scans a full model response for the presence of the session's canary token.
    /// </summary>
    public CanaryScanResult ScanResponse(string sessionId, string responseText)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return new CanaryScanResult(false, null, null, false, "SessionId required.");
        }

        if (IsSessionLocked(sessionId))
        {
            return new CanaryScanResult(
                IsExfiltrationDetected: true,
                DetectedTokenId: null,
                IncidentType: "PromptExfiltrationAttempt",
                SessionLocked: true,
                Message: "Session is locked due to prior exfiltration attempt."
            );
        }

        if (string.IsNullOrEmpty(responseText))
        {
            return new CanaryScanResult(false, null, null, false, "Safe - empty response.");
        }

        // Check against session token
        if (_sessionTokens.TryGetValue(sessionId, out var canary))
        {
            if (responseText.Contains(canary.ZeroWidthEncodedToken, StringComparison.Ordinal))
            {
                return TriggerExfiltrationIncident(sessionId, canary.TokenId, "Exact zero-width canary token detected in LLM output.");
            }
        }

        // Also check if any active canary token or zero-width sentinel pattern was exfiltrated
        foreach (var (sessId, tok) in _sessionTokens)
        {
            if (responseText.Contains(tok.ZeroWidthEncodedToken, StringComparison.Ordinal))
            {
                return TriggerExfiltrationIncident(sessionId, tok.TokenId, $"Zero-width canary token of session '{sessId}' leaked in output.");
            }
        }

        return new CanaryScanResult(
            IsExfiltrationDetected: false,
            DetectedTokenId: null,
            IncidentType: null,
            SessionLocked: false,
            Message: "Safe - no prompt canary exfiltration detected."
        );
    }

    /// <summary>
    /// Scans a streaming LLM response in real-time. Aborts stream immediately if canary is detected.
    /// </summary>
    public async IAsyncEnumerable<string> ScanStreamAsync(
        string sessionId,
        IAsyncEnumerable<string> chunkStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (IsSessionLocked(sessionId))
        {
            _logger?.LogWarning("Refusing to stream output for locked session '{SessionId}'.", sessionId);
            yield break;
        }

        _sessionTokens.TryGetValue(sessionId, out var canary);
        var targetToken = canary?.ZeroWidthEncodedToken;

        var buffer = new StringBuilder();
        const int maxLookback = 120; // Enough to hold sentinel prefix + 64 zero-width chars + suffix

        await foreach (var chunk in chunkStream.WithCancellation(ct))
        {
            if (IsSessionLocked(sessionId))
            {
                yield break;
            }

            buffer.Append(chunk);
            var currentBuffer = buffer.ToString();

            // Check if canary token is present
            bool detected = false;
            string? detectedTokenId = canary?.TokenId;

            if (targetToken != null && currentBuffer.Contains(targetToken, StringComparison.Ordinal))
            {
                detected = true;
            }
            else
            {
                // Also scan for any active canary
                foreach (var (_, activeToken) in _sessionTokens)
                {
                    if (currentBuffer.Contains(activeToken.ZeroWidthEncodedToken, StringComparison.Ordinal))
                    {
                        detected = true;
                        detectedTokenId = activeToken.TokenId;
                        break;
                    }
                }
            }

            if (detected)
            {
                TriggerExfiltrationIncident(sessionId, detectedTokenId, "Real-time streaming abort: prompt canary detected in chunk stream.");
                yield break;
            }

            // If buffer is safely longer than lookback window, yield confirmed safe prefix
            if (buffer.Length > maxLookback)
            {
                var safeLength = buffer.Length - maxLookback;
                var safeOutput = buffer.ToString(0, safeLength);
                buffer.Remove(0, safeLength);
                yield return safeOutput;
            }
        }

        // Flush remaining buffer if not empty and safe
        if (buffer.Length > 0 && !IsSessionLocked(sessionId))
        {
            yield return buffer.ToString();
        }
    }

    public bool IsSessionLocked(string sessionId) =>
        _lockedSessions.ContainsKey(sessionId);

    public bool LockSession(string sessionId, string reason = "PromptExfiltrationAttempt")
    {
        var locked = _lockedSessions.TryAdd(sessionId, DateTime.UtcNow);
        if (locked)
        {
            _logger?.LogCritical("CRITICAL SECURITY: Session '{SessionId}' locked. Reason: {Reason}", sessionId, reason);
        }
        return locked;
    }

    public bool UnlockSession(string sessionId) =>
        _lockedSessions.TryRemove(sessionId, out _);

    public IReadOnlyList<CanarySecurityIncident> GetIncidents(string? sessionId = null)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return [.. _incidents.OrderByDescending(i => i.DetectedAtUtc)];
        }
        return [.. _incidents.Where(i => i.SessionId == sessionId).OrderByDescending(i => i.DetectedAtUtc)];
    }

    public void ClearIncidents()
    {
        _incidents.Clear();
        _sessionTokens.Clear();
        _lockedSessions.Clear();
    }

    private CanaryScanResult TriggerExfiltrationIncident(string sessionId, string? tokenId, string description)
    {
        LockSession(sessionId, "PromptExfiltrationAttempt");

        var incident = new CanarySecurityIncident(
            Id: Guid.NewGuid(),
            SessionId: sessionId,
            IncidentType: "PromptExfiltrationAttempt",
            Severity: "Critical",
            TokenId: tokenId,
            Description: description,
            DetectedAtUtc: DateTime.UtcNow
        );

        _incidents.Add(incident);

        _logger?.LogCritical(
            "CRITICAL SECURITY ALERT: [PromptExfiltrationAttempt] detected for Session '{SessionId}'. Token: '{TokenId}'. Stream aborted & session locked.",
            sessionId, tokenId);

        return new CanaryScanResult(
            IsExfiltrationDetected: true,
            DetectedTokenId: tokenId,
            IncidentType: "PromptExfiltrationAttempt",
            SessionLocked: true,
            Message: "CRITICAL: Prompt exfiltration attempt detected. Stream aborted and session locked."
        );
    }

    private static string EncodeToZeroWidth(byte[] data)
    {
        var sb = new StringBuilder(data.Length * 4);
        foreach (var b in data)
        {
            // 8 bits -> 4 pairs of 2 bits (bits 6-7, 4-5, 2-3, 0-1)
            sb.Append(Map2BitsToZw((b >> 6) & 0x03));
            sb.Append(Map2BitsToZw((b >> 4) & 0x03));
            sb.Append(Map2BitsToZw((b >> 2) & 0x03));
            sb.Append(Map2BitsToZw(b & 0x03));
        }
        return sb.ToString();
    }

    private static char Map2BitsToZw(int twoBits) => twoBits switch
    {
        0 => Zw0,
        1 => Zw1,
        2 => Zw2,
        3 => Zw3,
        _ => Zw0
    };
}
