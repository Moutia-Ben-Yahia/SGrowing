using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;

/// <summary>
/// Service contract for the cryptographically verified cross-micro-app event broker.
/// </summary>
public interface IFederatedCrossAppEventBus
{
    EventPublishResult Publish(CrossAppEvent appEvent);
    Task<EventPublishResult> PublishAsync(CrossAppEvent appEvent, CancellationToken cancellationToken = default);
    string GenerateSignature(string sourceAppId, string targetAppId, string eventType, string payloadJson, string nonce, DateTimeOffset timestampUtc);
    IDisposable Subscribe(string eventType, Func<CrossAppEvent, Task> handler);
    IReadOnlyList<CrossAppEvent> GetEventHistory(string? eventType = null, int limit = 50);
    EventBusMetrics GetMetrics();
    void ClearHistory();
}

/// <summary>
/// Thread-safe in-memory event bus broker for cross-sub-app communication with
/// HMAC-SHA256 nonces, origin signatures, and replay attack prevention.
/// </summary>
public sealed class FederatedCrossAppEventBus : IFederatedCrossAppEventBus
{
    private readonly byte[] _hmacKey;
    private readonly int _maxClockSkewSeconds;
    private readonly Lock _lock = new();

    private readonly ConcurrentDictionary<string, DateTimeOffset> _seenNonces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<SubscriptionEntry>> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CrossAppEvent> _history = [];
    private const int MaxHistorySize = 500;

    private long _totalPublished;
    private long _totalDelivered;
    private long _totalReplayBlocked;
    private long _totalTamperedBlocked;
    private long _totalExpiredBlocked;

    public const string DefaultSecretKey = "sgrowing-mfe-crossapp-hmac-sha256-secret-token-key-2026!";
    public const int DefaultMaxClockSkewSeconds = 300;

    public FederatedCrossAppEventBus(string? secretKey = null, int maxClockSkewSeconds = DefaultMaxClockSkewSeconds)
    {
        _hmacKey = Encoding.UTF8.GetBytes(secretKey ?? DefaultSecretKey);
        _maxClockSkewSeconds = Math.Max(10, maxClockSkewSeconds);
    }

    public string GenerateSignature(
        string sourceAppId,
        string targetAppId,
        string eventType,
        string payloadJson,
        string nonce,
        DateTimeOffset timestampUtc)
    {
        var canonical = BuildCanonicalPayload(sourceAppId, targetAppId, eventType, payloadJson, nonce, timestampUtc);
        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }

    public EventPublishResult Publish(CrossAppEvent appEvent)
    {
        return PublishAsync(appEvent).GetAwaiter().GetResult();
    }

    public async Task<EventPublishResult> PublishAsync(CrossAppEvent appEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        if (string.IsNullOrWhiteSpace(appEvent.SourceAppId) ||
            string.IsNullOrWhiteSpace(appEvent.EventType) ||
            string.IsNullOrWhiteSpace(appEvent.Nonce))
        {
            return new EventPublishResult(
                IsSuccess: false,
                EventId: appEvent.EventId,
                DeliveredCount: 0,
                ErrorCode: "INVALID_EVENT_FORMAT",
                ErrorMessage: "SourceAppId, EventType, and Nonce are mandatory fields.");
        }

        // 1. Validate Clock Skew
        var now = DateTimeOffset.UtcNow;
        var skewSeconds = Math.Abs((now - appEvent.TimestampUtc).TotalSeconds);
        if (skewSeconds > _maxClockSkewSeconds)
        {
            Interlocked.Increment(ref _totalExpiredBlocked);
            return new EventPublishResult(
                IsSuccess: false,
                EventId: appEvent.EventId,
                DeliveredCount: 0,
                ErrorCode: "TIMESTAMP_EXPIRED",
                ErrorMessage: $"Event timestamp skew ({skewSeconds:F1}s) exceeds allowed tolerance ({_maxClockSkewSeconds}s).");
        }

        // 2. Validate Cryptographic HMAC-SHA256 Signature
        if (string.IsNullOrWhiteSpace(appEvent.Signature))
        {
            Interlocked.Increment(ref _totalTamperedBlocked);
            return new EventPublishResult(
                IsSuccess: false,
                EventId: appEvent.EventId,
                DeliveredCount: 0,
                ErrorCode: "TAMPERED_SIGNATURE",
                ErrorMessage: "Cryptographic signature is missing.");
        }

        var expectedSignature = GenerateSignature(
            appEvent.SourceAppId,
            appEvent.TargetAppId,
            appEvent.EventType,
            appEvent.PayloadJson,
            appEvent.Nonce,
            appEvent.TimestampUtc);

        var signatureBytes = Encoding.UTF8.GetBytes(appEvent.Signature.Trim().ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

        if (signatureBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(signatureBytes, expectedBytes))
        {
            Interlocked.Increment(ref _totalTamperedBlocked);
            return new EventPublishResult(
                IsSuccess: false,
                EventId: appEvent.EventId,
                DeliveredCount: 0,
                ErrorCode: "TAMPERED_SIGNATURE",
                ErrorMessage: "HMAC-SHA256 signature mismatch. Message was tampered with or unauthorized.");
        }

        // 3. Validate Nonce Uniqueness (Replay Attack Prevention)
        if (!_seenNonces.TryAdd(appEvent.Nonce, now))
        {
            Interlocked.Increment(ref _totalReplayBlocked);
            return new EventPublishResult(
                IsSuccess: false,
                EventId: appEvent.EventId,
                DeliveredCount: 0,
                ErrorCode: "REPLAY_ATTACK_DETECTED",
                ErrorMessage: $"Nonce '{appEvent.Nonce}' has already been processed. Replay attack blocked.");
        }

        // Clean expired nonces occasionally
        CleanupExpiredNonces(now);

        // 4. Resolve Matching Subscribers (Exact channel + Wildcard "*")
        List<Func<CrossAppEvent, Task>> handlersToInvoke;
        lock (_lock)
        {
            handlersToInvoke = [];
            if (_subscriptions.TryGetValue(appEvent.EventType, out var exactList))
            {
                handlersToInvoke.AddRange(exactList.Select(e => e.Handler));
            }
            if (_subscriptions.TryGetValue("*", out var wildcardList))
            {
                handlersToInvoke.AddRange(wildcardList.Select(e => e.Handler));
            }

            // Record to history
            _history.Add(appEvent);
            if (_history.Count > MaxHistorySize)
            {
                _history.RemoveAt(0);
            }
        }

        Interlocked.Increment(ref _totalPublished);

        // 5. Deliver to Subscribers
        var delivered = 0;
        foreach (var handler in handlersToInvoke)
        {
            try
            {
                await handler(appEvent);
                delivered++;
            }
            catch
            {
                // Resilient broker: subscriber failures do not abort delivery to other subscribers
            }
        }

        Interlocked.Add(ref _totalDelivered, delivered);

        return new EventPublishResult(
            IsSuccess: true,
            EventId: appEvent.EventId,
            DeliveredCount: delivered);
    }

    public IDisposable Subscribe(string eventType, Func<CrossAppEvent, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(handler);

        var subscriptionId = Guid.NewGuid();
        var entry = new SubscriptionEntry(subscriptionId, handler);

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out var list))
            {
                list = [];
                _subscriptions[eventType] = list;
            }

            list.Add(entry);
        }

        return new SubscriptionToken(() =>
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(eventType, out var list))
                {
                    list.RemoveAll(s => s.Id == subscriptionId);
                    if (list.Count == 0)
                    {
                        _subscriptions.Remove(eventType);
                    }
                }
            }
        });
    }

    public IReadOnlyList<CrossAppEvent> GetEventHistory(string? eventType = null, int limit = 50)
    {
        lock (_lock)
        {
            var query = _history.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(eventType))
            {
                query = query.Where(e => string.Equals(e.EventType, eventType, StringComparison.OrdinalIgnoreCase));
            }

            return query.TakeLast(Math.Max(1, limit)).ToList();
        }
    }

    public EventBusMetrics GetMetrics()
    {
        lock (_lock)
        {
            var activeSubs = _subscriptions.Values.Sum(list => list.Count);
            return new EventBusMetrics(
                TotalEventsPublished: Interlocked.Read(ref _totalPublished),
                TotalEventsDelivered: Interlocked.Read(ref _totalDelivered),
                TotalReplayAttacksBlocked: Interlocked.Read(ref _totalReplayBlocked),
                TotalTamperedSignaturesBlocked: Interlocked.Read(ref _totalTamperedBlocked),
                TotalExpiredTimestampsBlocked: Interlocked.Read(ref _totalExpiredBlocked),
                ActiveSubscriptionsCount: activeSubs);
        }
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
            _seenNonces.Clear();
        }
    }

    private static string BuildCanonicalPayload(
        string sourceAppId,
        string targetAppId,
        string eventType,
        string payloadJson,
        string nonce,
        DateTimeOffset timestampUtc)
    {
        return $"{sourceAppId}|{targetAppId}|{eventType}|{payloadJson}|{nonce}|{timestampUtc.ToUnixTimeMilliseconds()}";
    }

    private void CleanupExpiredNonces(DateTimeOffset now)
    {
        if (_seenNonces.Count < 500) return;

        var threshold = now.AddSeconds(-(_maxClockSkewSeconds * 2));
        foreach (var (nonce, ts) in _seenNonces)
        {
            if (ts < threshold)
            {
                _seenNonces.TryRemove(nonce, out _);
            }
        }
    }

    private sealed record SubscriptionEntry(Guid Id, Func<CrossAppEvent, Task> Handler);

    private sealed class SubscriptionToken(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}
