using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Messaging;

public interface IEventDeduplicationService
{
    Task<bool> TryAcquireAsync(Guid tenantId, string eventId, string? payload = null, TimeSpan? ttl = null, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid tenantId, string eventId, CancellationToken ct = default);
    Task<bool> IsProcessedAsync(Guid tenantId, string eventId, CancellationToken ct = default);
}

public class EventDeduplicationService : IEventDeduplicationService
{
    private class DeduplicationEntry
    {
        public string EventId { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    private static readonly ConcurrentDictionary<string, DeduplicationEntry> _cache = new();

    public Task<bool> TryAcquireAsync(Guid tenantId, string eventId, string? payload = null, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        CleanupExpiredEntries();

        string key = $"{tenantId}:{eventId.Trim()}";
        string hash = ComputeSha256(payload ?? string.Empty);
        var expiry = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromHours(24));

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                if (existing.ExpiresAtUtc > DateTime.UtcNow)
                {
                    // Already in flight or already completed
                    return Task.FromResult(false);
                }
            }

            _cache[key] = new DeduplicationEntry
            {
                EventId = eventId,
                PayloadHash = hash,
                IsCompleted = false,
                ExpiresAtUtc = expiry
            };

            return Task.FromResult(true);
        }
    }

    public Task MarkProcessedAsync(Guid tenantId, string eventId, CancellationToken ct = default)
    {
        string key = $"{tenantId}:{eventId.Trim()}";
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                existing.IsCompleted = true;
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsProcessedAsync(Guid tenantId, string eventId, CancellationToken ct = default)
    {
        CleanupExpiredEntries();
        string key = $"{tenantId}:{eventId.Trim()}";
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                if (existing.ExpiresAtUtc > DateTime.UtcNow && existing.IsCompleted)
                {
                    return Task.FromResult(true);
                }
            }
            return Task.FromResult(false);
        }
    }

    private static void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAtUtc <= now)
            {
                _cache.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static string ComputeSha256(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
