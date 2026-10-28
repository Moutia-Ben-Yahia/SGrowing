using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Auth;

public record RevokeTokenRequest(string Jti, int? TtlSeconds = 86400, string? Reason = null);

public interface ITokenRevocationService
{
    Task RevokeTokenAsync(string jti, TimeSpan? ttl = null, string? reason = null, CancellationToken ct = default);
    Task<bool> IsTokenRevokedAsync(string jti, CancellationToken ct = default);
    Task<int> GetRevokedCountAsync(CancellationToken ct = default);
}

public class TokenRevocationService : ITokenRevocationService
{
    private readonly IDistributedCache? _cache;
    private readonly ILogger<TokenRevocationService> _logger;
    private static readonly ConcurrentDictionary<string, DateTime> InMemoryBlocklist = new();

    public TokenRevocationService(ILogger<TokenRevocationService> logger, IDistributedCache? cache = null)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task RevokeTokenAsync(string jti, TimeSpan? ttl = null, string? reason = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
            throw new ArgumentException("JTI cannot be empty.", nameof(jti));

        var effectiveTtl = ttl ?? TimeSpan.FromDays(1);
        var expiry = DateTime.UtcNow.Add(effectiveTtl);

        InMemoryBlocklist[jti] = expiry;

        if (_cache != null)
        {
            try
            {
                var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = effectiveTtl };
                await _cache.SetStringAsync($"revoked:jti:{jti}", reason ?? "revoked", options, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist token revocation for JTI {Jti} in distributed cache. Fallback active.", jti);
            }
        }

        _logger.LogInformation("Token JTI {Jti} successfully added to revocation blocklist until {Expiry}. Reason: {Reason}",
            jti, expiry, reason ?? "Unspecified");
    }

    public async Task<bool> IsTokenRevokedAsync(string jti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;

        // 1. Check in-memory store
        if (InMemoryBlocklist.TryGetValue(jti, out var expiry))
        {
            if (DateTime.UtcNow <= expiry)
                return true;

            InMemoryBlocklist.TryRemove(jti, out _);
        }

        // 2. Check distributed cache
        if (_cache != null)
        {
            try
            {
                var cached = await _cache.GetStringAsync($"revoked:jti:{jti}", ct);
                if (!string.IsNullOrEmpty(cached))
                    return true;
            }
            catch
            {
                // Fallback to memory
            }
        }

        return false;
    }

    public Task<int> GetRevokedCountAsync(CancellationToken ct = default)
    {
        // Clean expired
        var now = DateTime.UtcNow;
        foreach (var kv in InMemoryBlocklist)
        {
            if (kv.Value < now)
            {
                InMemoryBlocklist.TryRemove(kv.Key, out _);
            }
        }

        return Task.FromResult(InMemoryBlocklist.Count);
    }
}
