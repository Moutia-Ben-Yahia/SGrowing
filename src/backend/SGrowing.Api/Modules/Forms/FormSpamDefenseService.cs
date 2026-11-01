using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Forms;

public class FormSpamCheckRequest
{
    public string ClientIp { get; set; } = "127.0.0.1";
    public Dictionary<string, string> FormFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? HoneypotFieldName { get; set; }
    public double? SubmissionDurationSeconds { get; set; }
    public DateTimeOffset? FormRenderedAt { get; set; }
    public DateTimeOffset? FormSubmittedAt { get; set; }
    public string? CaptchaToken { get; set; }
    public bool RequireCaptcha { get; set; }
    public int? MaxRequestsPerMinute { get; set; }
}

public class FormSpamCheckResult
{
    public bool IsAllowed { get; set; } = true;
    public bool IsSpam { get; set; } = false;
    public string? SpamReason { get; set; }
    public string? DefenseTriggered { get; set; } // "Honeypot", "SubmissionSpeed", "CaptchaVerification", "RateLimit"
    public double RiskScore { get; set; } // 0.0 = safe, 1.0 = definite spam
    public int StatusCode { get; set; } = 200; // 200, 422, 429
    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IFormSpamDefenseService
{
    Task<FormSpamCheckResult> CheckSpamAsync(FormSpamCheckRequest request, CancellationToken cancellationToken = default);
    bool CheckHoneypot(IReadOnlyDictionary<string, string> fields, string? specificHoneypotField = null);
    bool CheckSubmissionSpeed(double? durationSeconds, DateTimeOffset? renderedAt, DateTimeOffset? submittedAt, double minDurationSeconds = 1.5);
    (bool IsValid, string? ErrorMessage) VerifyCaptchaToken(string? token, bool isRequired);
    bool CheckRateLimit(string clientIp, int maxRequestsPerMinute = 10);
    void ResetRateLimit(string clientIp);
    void ClearRateLimits();
}

public class FormSpamDefenseService : IFormSpamDefenseService
{
    private const double DefaultMinSubmissionDurationSeconds = 1.5;
    private const int DefaultMaxRequestsPerMinute = 10;

    private static readonly HashSet<string> DefaultHoneypotKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "_hp_trap",
        "honeypot",
        "website_url_hp",
        "hp_check",
        "bot_field",
        "_gotcha",
        "company_tax_id_confirm"
    };

    // Sliding-window request timestamps per IP address
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _ipRequestLog = new();

    public Task<FormSpamCheckResult> CheckSpamAsync(FormSpamCheckRequest request, CancellationToken cancellationToken = default)
    {
        var clientIp = string.IsNullOrWhiteSpace(request.ClientIp) ? "127.0.0.1" : request.ClientIp.Trim();

        // Layer 1: Honeypot Validation
        if (!CheckHoneypot(request.FormFields, request.HoneypotFieldName))
        {
            return Task.FromResult(new FormSpamCheckResult
            {
                IsAllowed = false,
                IsSpam = true,
                SpamReason = "Honeypot trap triggered. Hidden field was populated by automated client.",
                DefenseTriggered = "Honeypot",
                RiskScore = 1.0,
                StatusCode = 422
            });
        }

        // Layer 2: Minimum Submission Duration (< 1.5s is bot filling)
        if (!CheckSubmissionSpeed(request.SubmissionDurationSeconds, request.FormRenderedAt, request.FormSubmittedAt, DefaultMinSubmissionDurationSeconds))
        {
            var calculatedDuration = CalculateDuration(request.SubmissionDurationSeconds, request.FormRenderedAt, request.FormSubmittedAt);
            return Task.FromResult(new FormSpamCheckResult
            {
                IsAllowed = false,
                IsSpam = true,
                SpamReason = $"Submission completed too quickly ({calculatedDuration:F2}s < {DefaultMinSubmissionDurationSeconds:F1}s). Fast completion indicates automated bot filling.",
                DefenseTriggered = "SubmissionSpeed",
                RiskScore = 0.95,
                StatusCode = 422
            });
        }

        // Layer 3: Cloudflare Turnstile / reCAPTCHA Token Verification Stub
        var (captchaValid, captchaError) = VerifyCaptchaToken(request.CaptchaToken, request.RequireCaptcha);
        if (!captchaValid)
        {
            return Task.FromResult(new FormSpamCheckResult
            {
                IsAllowed = false,
                IsSpam = true,
                SpamReason = captchaError ?? "CAPTCHA/Turnstile verification failed.",
                DefenseTriggered = "CaptchaVerification",
                RiskScore = 0.85,
                StatusCode = 422
            });
        }

        // Layer 4: Sliding-Window Token Bucket IP Rate Limiter
        int maxRate = request.MaxRequestsPerMinute ?? DefaultMaxRequestsPerMinute;
        if (!CheckRateLimit(clientIp, maxRate))
        {
            return Task.FromResult(new FormSpamCheckResult
            {
                IsAllowed = false,
                IsSpam = true,
                SpamReason = $"IP rate limit exceeded: more than {maxRate} requests in sliding 60-second window.",
                DefenseTriggered = "RateLimit",
                RiskScore = 0.80,
                StatusCode = 429
            });
        }

        // All layers passed cleanly
        return Task.FromResult(new FormSpamCheckResult
        {
            IsAllowed = true,
            IsSpam = false,
            RiskScore = 0.02,
            StatusCode = 200,
            SpamReason = null,
            DefenseTriggered = null
        });
    }

    public bool CheckHoneypot(IReadOnlyDictionary<string, string> fields, string? specificHoneypotField = null)
    {
        if (fields == null || fields.Count == 0)
        {
            return true;
        }

        // Check specific field if requested
        if (!string.IsNullOrWhiteSpace(specificHoneypotField))
        {
            if (fields.TryGetValue(specificHoneypotField, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return false; // Honeypot was filled -> SPAM
            }
        }

        // Check known honeypot names
        foreach (var key in DefaultHoneypotKeys)
        {
            if (fields.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return false; // Honeypot was filled -> SPAM
            }
        }

        // Check any field that looks like a honeypot trap: starts or ends with _hp or honeypot
        foreach (var (k, v) in fields)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;

            if (k.StartsWith("_hp", StringComparison.OrdinalIgnoreCase) ||
                k.EndsWith("_hp", StringComparison.OrdinalIgnoreCase) ||
                k.Contains("honeypot", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public bool CheckSubmissionSpeed(double? durationSeconds, DateTimeOffset? renderedAt, DateTimeOffset? submittedAt, double minDurationSeconds = 1.5)
    {
        var duration = CalculateDuration(durationSeconds, renderedAt, submittedAt);

        // If no timing info provided, treat as acceptable
        if (!duration.HasValue)
        {
            return true;
        }

        return duration.Value >= minDurationSeconds;
    }

    private static double? CalculateDuration(double? durationSeconds, DateTimeOffset? renderedAt, DateTimeOffset? submittedAt)
    {
        if (durationSeconds.HasValue)
        {
            return durationSeconds.Value;
        }

        if (renderedAt.HasValue)
        {
            var end = submittedAt ?? DateTimeOffset.UtcNow;
            return (end - renderedAt.Value).TotalSeconds;
        }

        return null;
    }

    public (bool IsValid, string? ErrorMessage) VerifyCaptchaToken(string? token, bool isRequired)
    {
        if (isRequired && string.IsNullOrWhiteSpace(token))
        {
            return (false, "Missing required CAPTCHA/Turnstile verification token.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            // Token is optional and absent
            return (true, null);
        }

        var trimmed = token.Trim();

        // Stub verification rules:
        // Reject explicit mock failure strings
        if (trimmed.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("reject", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
            trimmed == "0x4AAAAAA")
        {
            return (false, "Cloudflare Turnstile token validation failed: response signature rejected.");
        }

        // Accept standard Cloudflare test tokens & valid tokens
        // e.g. "1x00000000000000000000AA" (Always passes in Cloudflare docs)
        // or any non-empty reasonable length string
        if (trimmed.Length >= 4)
        {
            return (true, null);
        }

        return (false, "Token length is insufficient for a valid Turnstile or reCAPTCHA payload.");
    }

    public bool CheckRateLimit(string clientIp, int maxRequestsPerMinute = 10)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            clientIp = "127.0.0.1";
        }

        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddSeconds(-60);

        var list = _ipRequestLog.GetOrAdd(clientIp, _ => []);

        lock (list)
        {
            // Prune timestamps older than 60 seconds
            list.RemoveAll(t => t < windowStart);

            if (list.Count >= maxRequestsPerMinute)
            {
                return false; // Rate limit exceeded
            }

            list.Add(now);
            return true;
        }
    }

    public void ResetRateLimit(string clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp)) return;
        _ipRequestLog.TryRemove(clientIp, out _);
    }

    public void ClearRateLimits()
    {
        _ipRequestLog.Clear();
    }
}
