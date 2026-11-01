using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Attribution;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public record RawCustomerPii(
    string? Email = null,
    string? Phone = null,
    string? FirstName = null,
    string? LastName = null,
    string? City = null,
    string? State = null,
    string? Zip = null,
    string? Country = null,
    string? ClientIpAddress = null,
    string? ClientUserAgent = null,
    string? Fbc = null,
    string? Fbp = null,
    string? Gclid = null,
    string? Ttclid = null
);

public record NormalizedCapiPii(
    string HashedEmail,
    string HashedPhone,
    string HashedFirstName,
    string HashedLastName,
    string HashedCity,
    string HashedState,
    string HashedZip,
    string HashedCountry,
    string? RawNormalizedCity,
    string? RawNormalizedState,
    string? RawNormalizedZip,
    string? RawNormalizedCountry,
    string? ClientIpAddress,
    string? ClientUserAgent,
    string? Fbc,
    string? Fbp,
    string? Gclid,
    string? Ttclid
);

public record CapiConversionPayload(
    Guid TenantId,
    string EventName,
    string EventId,
    DateTime EventTimeUtc,
    decimal? Value = null,
    string Currency = "USD",
    string? OrderId = null,
    string? EventSourceUrl = null,
    Dictionary<string, object?>? CustomData = null
);

public record CapiDispatchEventRequest(
    Guid TenantId,
    string EventName,
    string? EventId = null,
    decimal? Value = null,
    string Currency = "USD",
    string? OrderId = null,
    RawCustomerPii? Pii = null,
    List<CapiPlatform>? TargetPlatforms = null,
    string? TestEventCode = null,
    string? EventSourceUrl = null,
    Dictionary<string, object?>? CustomData = null
);

public record PlatformDispatchOutcome(
    CapiPlatform Platform,
    bool Success,
    int StatusCode,
    int AttemptCount,
    long LatencyMs,
    string PayloadJson,
    string? ResponseJson = null,
    string? ErrorMessage = null,
    CircuitState CircuitState = CircuitState.Closed
);

public record CapiFanoutResult(
    string EventId,
    Guid TenantId,
    string EventName,
    bool AllSucceeded,
    IReadOnlyList<PlatformDispatchOutcome> PlatformOutcomes,
    DateTime DispatchedAtUtc
);

public interface IServerSideCapiFanoutService
{
    // Normalization & SHA-256 Hashing
    string ComputeSha256(string? input);
    string NormalizeAndHashEmail(string? email);
    string NormalizeAndHashPhone(string? phone, string defaultCallingCode = "1");
    string NormalizeAndHashName(string? name);
    string NormalizeAndHashCity(string? city);
    string NormalizeAndHashState(string? state);
    string NormalizeAndHashZip(string? zip);
    string NormalizeAndHashCountry(string? countryIso2);
    NormalizedCapiPii NormalizePii(RawCustomerPii? raw);

    // Platform Payload Formatter
    string FormatMetaPayload(CapiConversionPayload conversion, NormalizedCapiPii pii, string? testEventCode = null);
    string FormatGoogleEnhancedConversionsPayload(CapiConversionPayload conversion, NormalizedCapiPii pii);
    string FormatTikTokEventsPayload(CapiConversionPayload conversion, NormalizedCapiPii pii);

    // Resilient Dispatch & Circuit Breakers
    Task<CapiFanoutResult> DispatchAsync(CapiDispatchEventRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CapiFanoutResult>> BatchDispatchAsync(IEnumerable<CapiDispatchEventRequest> requests, int maxDegreeOfParallelism = 10, CancellationToken ct = default);
    CircuitState GetCircuitBreakerState(CapiPlatform platform);
    void ResetCircuitBreaker(CapiPlatform platform);
    void TripCircuitBreakerForTesting(CapiPlatform platform);
}

public class ServerSideCapiFanoutService : IServerSideCapiFanoutService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerSideCapiFanoutService>? _logger;

    private readonly ConcurrentDictionary<CapiPlatform, CircuitBreakerTracker> _circuitBreakers = new();
    private readonly int _consecutiveFailureThreshold = 5;
    private readonly TimeSpan _circuitResetTimeout = TimeSpan.FromSeconds(30);

    public ServerSideCapiFanoutService(
        HttpClient? httpClient = null,
        ILogger<ServerSideCapiFanoutService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;

        foreach (var platform in Enum.GetValues<CapiPlatform>())
        {
            _circuitBreakers[platform] = new CircuitBreakerTracker();
        }
    }

    #region Normalization & Hashing

    public string ComputeSha256(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    public string NormalizeAndHashEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var normalized = email.Trim().ToLowerInvariant();
        return ComputeSha256(normalized);
    }

    public string NormalizeAndHashPhone(string? phone, string defaultCallingCode = "1")
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var trimmed = phone.Trim();
        var hadPlus = trimmed.StartsWith('+');
        var digitsOnly = Regex.Replace(trimmed, @"\D", "");
        if (string.IsNullOrEmpty(digitsOnly))
            return string.Empty;

        string canonical;
        if (hadPlus)
        {
            canonical = digitsOnly;
        }
        else
        {
            var cleanCallingCode = Regex.Replace(defaultCallingCode ?? "1", @"\D", "");
            canonical = digitsOnly.StartsWith(cleanCallingCode, StringComparison.Ordinal)
                ? digitsOnly
                : cleanCallingCode + digitsOnly;
        }

        return ComputeSha256(canonical);
    }

    public string NormalizeAndHashName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var lower = name.Trim().ToLowerInvariant();
        var lettersOnly = Regex.Replace(lower, @"[^\p{L}]", "");
        var target = string.IsNullOrEmpty(lettersOnly) ? lower : lettersOnly;

        return ComputeSha256(target);
    }

    public string NormalizeAndHashCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
            return string.Empty;

        var lower = city.Trim().ToLowerInvariant();
        var lettersOnly = Regex.Replace(lower, @"[^\p{L}]", "");
        var target = string.IsNullOrEmpty(lettersOnly) ? lower : lettersOnly;

        return ComputeSha256(target);
    }

    public string NormalizeAndHashState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return string.Empty;

        var normalized = state.Trim().ToLowerInvariant();
        return ComputeSha256(normalized);
    }

    public string NormalizeAndHashZip(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
            return string.Empty;

        var normalized = Regex.Replace(zip.Trim().ToLowerInvariant(), @"\s+", "");
        return ComputeSha256(normalized);
    }

    public string NormalizeAndHashCountry(string? countryIso2)
    {
        if (string.IsNullOrWhiteSpace(countryIso2))
            return string.Empty;

        var normalized = countryIso2.Trim().ToLowerInvariant();
        if (normalized.Length > 2)
            normalized = normalized[..2];

        return ComputeSha256(normalized);
    }

    public NormalizedCapiPii NormalizePii(RawCustomerPii? raw)
    {
        if (raw == null)
        {
            return new NormalizedCapiPii(
                HashedEmail: string.Empty,
                HashedPhone: string.Empty,
                HashedFirstName: string.Empty,
                HashedLastName: string.Empty,
                HashedCity: string.Empty,
                HashedState: string.Empty,
                HashedZip: string.Empty,
                HashedCountry: string.Empty,
                RawNormalizedCity: null,
                RawNormalizedState: null,
                RawNormalizedZip: null,
                RawNormalizedCountry: null,
                ClientIpAddress: null,
                ClientUserAgent: null,
                Fbc: null,
                Fbp: null,
                Gclid: null,
                Ttclid: null
            );
        }

        var rawCity = !string.IsNullOrWhiteSpace(raw.City) ? raw.City.Trim().ToLowerInvariant() : null;
        var rawState = !string.IsNullOrWhiteSpace(raw.State) ? raw.State.Trim().ToLowerInvariant() : null;
        var rawZip = !string.IsNullOrWhiteSpace(raw.Zip) ? Regex.Replace(raw.Zip.Trim().ToLowerInvariant(), @"\s+", "") : null;
        var rawCountry = !string.IsNullOrWhiteSpace(raw.Country) ? (raw.Country.Trim().ToLowerInvariant().Length > 2 ? raw.Country.Trim().ToLowerInvariant()[..2] : raw.Country.Trim().ToLowerInvariant()) : null;

        return new NormalizedCapiPii(
            HashedEmail: NormalizeAndHashEmail(raw.Email),
            HashedPhone: NormalizeAndHashPhone(raw.Phone),
            HashedFirstName: NormalizeAndHashName(raw.FirstName),
            HashedLastName: NormalizeAndHashName(raw.LastName),
            HashedCity: NormalizeAndHashCity(raw.City),
            HashedState: NormalizeAndHashState(raw.State),
            HashedZip: NormalizeAndHashZip(raw.Zip),
            HashedCountry: NormalizeAndHashCountry(raw.Country),
            RawNormalizedCity: rawCity,
            RawNormalizedState: rawState,
            RawNormalizedZip: rawZip,
            RawNormalizedCountry: rawCountry,
            ClientIpAddress: raw.ClientIpAddress?.Trim(),
            ClientUserAgent: raw.ClientUserAgent?.Trim(),
            Fbc: raw.Fbc?.Trim(),
            Fbp: raw.Fbp?.Trim(),
            Gclid: raw.Gclid?.Trim(),
            Ttclid: raw.Ttclid?.Trim()
        );
    }

    #endregion

    #region Platform Payload Formatters

    public string FormatMetaPayload(CapiConversionPayload conversion, NormalizedCapiPii pii, string? testEventCode = null)
    {
        var userData = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(pii.HashedEmail))
            userData["em"] = new[] { pii.HashedEmail };

        if (!string.IsNullOrEmpty(pii.HashedPhone))
            userData["ph"] = new[] { pii.HashedPhone };

        if (!string.IsNullOrEmpty(pii.HashedFirstName))
            userData["fn"] = new[] { pii.HashedFirstName };

        if (!string.IsNullOrEmpty(pii.HashedLastName))
            userData["ln"] = new[] { pii.HashedLastName };

        if (!string.IsNullOrEmpty(pii.HashedCity))
            userData["ct"] = new[] { pii.HashedCity };

        if (!string.IsNullOrEmpty(pii.HashedState))
            userData["st"] = new[] { pii.HashedState };

        if (!string.IsNullOrEmpty(pii.HashedZip))
            userData["zp"] = new[] { pii.HashedZip };

        if (!string.IsNullOrEmpty(pii.HashedCountry))
            userData["country"] = new[] { pii.HashedCountry };

        if (!string.IsNullOrEmpty(pii.ClientIpAddress))
            userData["client_ip_address"] = pii.ClientIpAddress;

        if (!string.IsNullOrEmpty(pii.ClientUserAgent))
            userData["client_user_agent"] = pii.ClientUserAgent;

        if (!string.IsNullOrEmpty(pii.Fbc))
            userData["fbc"] = pii.Fbc;

        if (!string.IsNullOrEmpty(pii.Fbp))
            userData["fbp"] = pii.Fbp;

        var customData = new Dictionary<string, object?>();
        if (conversion.Value.HasValue)
            customData["value"] = conversion.Value.Value;

        if (!string.IsNullOrEmpty(conversion.Currency))
            customData["currency"] = conversion.Currency;

        if (!string.IsNullOrEmpty(conversion.OrderId))
            customData["order_id"] = conversion.OrderId;

        if (conversion.CustomData != null)
        {
            foreach (var (k, v) in conversion.CustomData)
            {
                customData[k] = v;
            }
        }

        var eventObj = new Dictionary<string, object?>
        {
            ["event_name"] = conversion.EventName,
            ["event_time"] = new DateTimeOffset(conversion.EventTimeUtc).ToUnixTimeSeconds(),
            ["event_id"] = conversion.EventId,
            ["action_source"] = "website",
            ["user_data"] = userData,
            ["custom_data"] = customData
        };

        if (!string.IsNullOrEmpty(conversion.EventSourceUrl))
        {
            eventObj["event_source_url"] = conversion.EventSourceUrl;
        }

        var root = new Dictionary<string, object?>
        {
            ["data"] = new[] { eventObj }
        };

        if (!string.IsNullOrWhiteSpace(testEventCode))
        {
            root["test_event_code"] = testEventCode.Trim();
        }

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = false });
    }

    public string FormatGoogleEnhancedConversionsPayload(CapiConversionPayload conversion, NormalizedCapiPii pii)
    {
        var userIdentifiers = new List<Dictionary<string, object?>>();

        if (!string.IsNullOrEmpty(pii.HashedEmail))
        {
            userIdentifiers.Add(new Dictionary<string, object?>
            {
                ["hashed_email"] = pii.HashedEmail
            });
        }

        if (!string.IsNullOrEmpty(pii.HashedPhone))
        {
            userIdentifiers.Add(new Dictionary<string, object?>
            {
                ["hashed_phone_number"] = pii.HashedPhone
            });
        }

        var addressInfo = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(pii.HashedFirstName)) addressInfo["hashed_first_name"] = pii.HashedFirstName;
        if (!string.IsNullOrEmpty(pii.HashedLastName)) addressInfo["hashed_last_name"] = pii.HashedLastName;
        if (!string.IsNullOrEmpty(pii.RawNormalizedCity)) addressInfo["city"] = pii.RawNormalizedCity;
        if (!string.IsNullOrEmpty(pii.RawNormalizedState)) addressInfo["state"] = pii.RawNormalizedState;
        if (!string.IsNullOrEmpty(pii.RawNormalizedZip)) addressInfo["postal_code"] = pii.RawNormalizedZip;
        if (!string.IsNullOrEmpty(pii.RawNormalizedCountry)) addressInfo["country_code"] = pii.RawNormalizedCountry.ToUpperInvariant();

        if (addressInfo.Count > 0)
        {
            userIdentifiers.Add(new Dictionary<string, object?>
            {
                ["address_info"] = addressInfo
            });
        }

        var conversionObj = new Dictionary<string, object?>
        {
            ["conversion_action"] = $"customers/{conversion.TenantId:N}/conversionActions/{conversion.EventName}",
            ["conversion_date_time"] = conversion.EventTimeUtc.ToString("yyyy-MM-dd HH:mm:ss+00:00"),
            ["order_id"] = conversion.OrderId ?? conversion.EventId,
            ["user_identifiers"] = userIdentifiers
        };

        if (conversion.Value.HasValue)
            conversionObj["conversion_value"] = conversion.Value.Value;

        if (!string.IsNullOrEmpty(conversion.Currency))
            conversionObj["currency_code"] = conversion.Currency;

        if (!string.IsNullOrEmpty(pii.Gclid))
            conversionObj["gclid"] = pii.Gclid;

        var root = new Dictionary<string, object?>
        {
            ["conversions"] = new[] { conversionObj },
            ["partial_failure"] = true
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = false });
    }

    public string FormatTikTokEventsPayload(CapiConversionPayload conversion, NormalizedCapiPii pii)
    {
        var userObj = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(pii.HashedEmail))
            userObj["email"] = pii.HashedEmail;

        if (!string.IsNullOrEmpty(pii.HashedPhone))
            userObj["phone_number"] = pii.HashedPhone;

        if (!string.IsNullOrEmpty(pii.Ttclid))
            userObj["ttclid"] = pii.Ttclid;

        if (!string.IsNullOrEmpty(pii.ClientIpAddress))
            userObj["ip"] = pii.ClientIpAddress;

        if (!string.IsNullOrEmpty(pii.ClientUserAgent))
            userObj["user_agent"] = pii.ClientUserAgent;

        var properties = new Dictionary<string, object?>();
        if (conversion.Value.HasValue)
            properties["value"] = conversion.Value.Value;

        if (!string.IsNullOrEmpty(conversion.Currency))
            properties["currency"] = conversion.Currency;

        if (!string.IsNullOrEmpty(conversion.OrderId))
            properties["order_id"] = conversion.OrderId;

        var eventObj = new Dictionary<string, object?>
        {
            ["event"] = conversion.EventName,
            ["event_time"] = new DateTimeOffset(conversion.EventTimeUtc).ToUnixTimeSeconds(),
            ["event_id"] = conversion.EventId,
            ["user"] = userObj,
            ["properties"] = properties
        };

        var root = new Dictionary<string, object?>
        {
            ["event_source"] = "web",
            ["event_source_id"] = conversion.TenantId.ToString(),
            ["data"] = new[] { eventObj }
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = false });
    }

    #endregion

    #region Resilient Dispatch & Circuit Breakers

    public CircuitState GetCircuitBreakerState(CapiPlatform platform)
    {
        var tracker = _circuitBreakers.GetOrAdd(platform, _ => new CircuitBreakerTracker());
        lock (tracker)
        {
            if (tracker.State == CircuitState.Open && DateTime.UtcNow - tracker.LastTrippedAtUtc > _circuitResetTimeout)
            {
                tracker.State = CircuitState.HalfOpen;
            }
            return tracker.State;
        }
    }

    public void ResetCircuitBreaker(CapiPlatform platform)
    {
        var tracker = _circuitBreakers.GetOrAdd(platform, _ => new CircuitBreakerTracker());
        lock (tracker)
        {
            tracker.State = CircuitState.Closed;
            tracker.ConsecutiveFailures = 0;
            tracker.LastTrippedAtUtc = DateTime.MinValue;
        }
    }

    public void TripCircuitBreakerForTesting(CapiPlatform platform)
    {
        var tracker = _circuitBreakers.GetOrAdd(platform, _ => new CircuitBreakerTracker());
        lock (tracker)
        {
            tracker.State = CircuitState.Open;
            tracker.ConsecutiveFailures = _consecutiveFailureThreshold;
            tracker.LastTrippedAtUtc = DateTime.UtcNow;
        }
    }

    public async Task<CapiFanoutResult> DispatchAsync(CapiDispatchEventRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eventId = string.IsNullOrWhiteSpace(request.EventId)
            ? $"sg_ev_{Guid.NewGuid():N}"
            : request.EventId.Trim();

        var normalizedPii = NormalizePii(request.Pii);
        var conversion = new CapiConversionPayload(
            TenantId: request.TenantId,
            EventName: request.EventName,
            EventId: eventId,
            EventTimeUtc: DateTime.UtcNow,
            Value: request.Value,
            Currency: request.Currency ?? "USD",
            OrderId: request.OrderId,
            EventSourceUrl: request.EventSourceUrl,
            CustomData: request.CustomData
        );

        var targetPlatforms = request.TargetPlatforms != null && request.TargetPlatforms.Count > 0
            ? request.TargetPlatforms.Distinct().ToList()
            : [CapiPlatform.Meta, CapiPlatform.Google, CapiPlatform.TikTok];

        var tasks = targetPlatforms.Select(platform => DispatchToPlatformAsync(platform, conversion, normalizedPii, request.TestEventCode, ct));
        var outcomes = await Task.WhenAll(tasks);

        var allSucceeded = outcomes.All(o => o.Success);

        return new CapiFanoutResult(
            EventId: eventId,
            TenantId: request.TenantId,
            EventName: request.EventName,
            AllSucceeded: allSucceeded,
            PlatformOutcomes: outcomes,
            DispatchedAtUtc: DateTime.UtcNow
        );
    }

    public async Task<IReadOnlyList<CapiFanoutResult>> BatchDispatchAsync(
        IEnumerable<CapiDispatchEventRequest> requests,
        int maxDegreeOfParallelism = 10,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        using var throttler = new SemaphoreSlim(Math.Max(1, maxDegreeOfParallelism));
        var tasks = requests.Select(async req =>
        {
            await throttler.WaitAsync(ct);
            try
            {
                return await DispatchAsync(req, ct);
            }
            finally
            {
                throttler.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private async Task<PlatformDispatchOutcome> DispatchToPlatformAsync(
        CapiPlatform platform,
        CapiConversionPayload conversion,
        NormalizedCapiPii pii,
        string? testEventCode,
        CancellationToken ct)
    {
        var circuitState = GetCircuitBreakerState(platform);
        if (circuitState == CircuitState.Open)
        {
            return new PlatformDispatchOutcome(
                Platform: platform,
                Success: false,
                StatusCode: (int)HttpStatusCode.ServiceUnavailable,
                AttemptCount: 0,
                LatencyMs: 0,
                PayloadJson: string.Empty,
                ErrorMessage: $"Circuit breaker for platform '{platform}' is OPEN. Fast-failing dispatch.",
                CircuitState: CircuitState.Open
            );
        }

        string payloadJson = platform switch
        {
            CapiPlatform.Meta => FormatMetaPayload(conversion, pii, testEventCode),
            CapiPlatform.Google => FormatGoogleEnhancedConversionsPayload(conversion, pii),
            CapiPlatform.TikTok => FormatTikTokEventsPayload(conversion, pii),
            _ => "{}"
        };

        var endpointUrl = GetPlatformEndpoint(platform, conversion.TenantId);
        int attempts = 0;
        const int maxAttempts = 3;
        var sw = Stopwatch.StartNew();

        while (attempts < maxAttempts)
        {
            attempts++;
            try
            {
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(httpReq, ct);
                var statusCode = (int)response.StatusCode;
                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    RecordSuccess(platform);
                    sw.Stop();
                    return new PlatformDispatchOutcome(
                        Platform: platform,
                        Success: true,
                        StatusCode: statusCode,
                        AttemptCount: attempts,
                        LatencyMs: sw.ElapsedMilliseconds,
                        PayloadJson: payloadJson,
                        ResponseJson: responseBody,
                        CircuitState: CircuitState.Closed
                    );
                }

                // If non-transient client error (e.g. 400 Bad Request, 401 Unauthorized), do not retry
                if (statusCode >= 400 && statusCode < 500 && statusCode != 429)
                {
                    RecordFailure(platform);
                    sw.Stop();
                    return new PlatformDispatchOutcome(
                        Platform: platform,
                        Success: false,
                        StatusCode: statusCode,
                        AttemptCount: attempts,
                        LatencyMs: sw.ElapsedMilliseconds,
                        PayloadJson: payloadJson,
                        ResponseJson: responseBody,
                        ErrorMessage: $"Platform returned client error HTTP {statusCode}: {responseBody}",
                        CircuitState: GetCircuitBreakerState(platform)
                    );
                }

                // Retryable error (429 Too Many Requests or 5xx Server Error)
                if (attempts >= maxAttempts)
                {
                    RecordFailure(platform);
                    sw.Stop();
                    return new PlatformDispatchOutcome(
                        Platform: platform,
                        Success: false,
                        StatusCode: statusCode,
                        AttemptCount: attempts,
                        LatencyMs: sw.ElapsedMilliseconds,
                        PayloadJson: payloadJson,
                        ResponseJson: responseBody,
                        ErrorMessage: $"Max retry attempts ({maxAttempts}) exhausted. HTTP {statusCode}: {responseBody}",
                        CircuitState: GetCircuitBreakerState(platform)
                    );
                }

                // Exponential backoff delay
                var delayMs = (int)(Math.Pow(2, attempts) * 50);
                await Task.Delay(delayMs, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempts >= maxAttempts)
                {
                    RecordFailure(platform);
                    sw.Stop();
                    return new PlatformDispatchOutcome(
                        Platform: platform,
                        Success: false,
                        StatusCode: (int)HttpStatusCode.InternalServerError,
                        AttemptCount: attempts,
                        LatencyMs: sw.ElapsedMilliseconds,
                        PayloadJson: payloadJson,
                        ErrorMessage: $"Network exception dispatching to {platform}: {ex.Message}",
                        CircuitState: GetCircuitBreakerState(platform)
                    );
                }

                var delayMs = (int)(Math.Pow(2, attempts) * 50);
                await Task.Delay(delayMs, ct);
            }
        }

        sw.Stop();
        return new PlatformDispatchOutcome(
            Platform: platform,
            Success: false,
            StatusCode: (int)HttpStatusCode.InternalServerError,
            AttemptCount: attempts,
            LatencyMs: sw.ElapsedMilliseconds,
            PayloadJson: payloadJson,
            ErrorMessage: "Unknown dispatch termination.",
            CircuitState: GetCircuitBreakerState(platform)
        );
    }

    private void RecordSuccess(CapiPlatform platform)
    {
        var tracker = _circuitBreakers.GetOrAdd(platform, _ => new CircuitBreakerTracker());
        lock (tracker)
        {
            tracker.ConsecutiveFailures = 0;
            tracker.State = CircuitState.Closed;
        }
    }

    private void RecordFailure(CapiPlatform platform)
    {
        var tracker = _circuitBreakers.GetOrAdd(platform, _ => new CircuitBreakerTracker());
        lock (tracker)
        {
            tracker.ConsecutiveFailures++;
            if (tracker.ConsecutiveFailures >= _consecutiveFailureThreshold)
            {
                tracker.State = CircuitState.Open;
                tracker.LastTrippedAtUtc = DateTime.UtcNow;
                _logger?.LogWarning("Circuit breaker TRIPPED to OPEN for CAPI platform {Platform} after {Count} consecutive failures", platform, tracker.ConsecutiveFailures);
            }
        }
    }

    private static string GetPlatformEndpoint(CapiPlatform platform, Guid tenantId)
    {
        return platform switch
        {
            CapiPlatform.Meta => $"https://graph.facebook.com/v19.0/{tenantId:N}/events",
            CapiPlatform.Google => $"https://googleads.googleapis.com/v16/customers/{tenantId:N}:uploadConversionAdjustments",
            CapiPlatform.TikTok => "https://business-api.tiktok.com/open_api/v1.3/event/track/",
            _ => "https://localhost/api/capi/mock"
        };
    }

    #endregion

    private sealed class CircuitBreakerTracker
    {
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int ConsecutiveFailures { get; set; } = 0;
        public DateTime LastTrippedAtUtc { get; set; } = DateTime.MinValue;
    }
}
