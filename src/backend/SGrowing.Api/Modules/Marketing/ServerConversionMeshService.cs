using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public interface IServerConversionMeshService
{
    Task<ServerConversionEvent> TrackConversionAsync(
        Guid tenantId,
        AdPlatform platform,
        ConversionEventType eventType,
        decimal valueAmount,
        string currency,
        string? email = null,
        string? phone = null,
        string? firstName = null,
        string? lastName = null,
        string? city = null,
        string? country = null,
        string? fbc = null,
        string? fbp = null,
        string? userAgent = null,
        string? ipAddress = null,
        string? customDataJson = null,
        string? eventId = null);

    double CalculateEventMatchQualityScore(ServerConversionEvent ev);
    Task<List<ServerConversionEvent>> GetRecentConversionsAsync(Guid tenantId, int limit = 50);
}

public class ServerConversionMeshService : IServerConversionMeshService
{
    private readonly SGrowingDbContext _dbContext;

    public ServerConversionMeshService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string HashSha256(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var clean = input.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(clean));
        return Convert.ToHexStringLower(bytes);
    }

    public double CalculateEventMatchQualityScore(ServerConversionEvent ev)
    {
        // Meta Event Match Quality (EMQ) out of 10.0
        double score = 0;
        if (!string.IsNullOrWhiteSpace(ev.HashedPhone)) score += 3.5;
        if (!string.IsNullOrWhiteSpace(ev.HashedEmail)) score += 2.5;
        if (!string.IsNullOrWhiteSpace(ev.Fbp)) score += 1.5;
        if (!string.IsNullOrWhiteSpace(ev.Fbc)) score += 1.0;
        if (!string.IsNullOrWhiteSpace(ev.ClientUserAgent)) score += 0.5;
        if (!string.IsNullOrWhiteSpace(ev.ClientIpAddress)) score += 0.5;
        if (!string.IsNullOrWhiteSpace(ev.HashedCity)) score += 0.25;
        if (!string.IsNullOrWhiteSpace(ev.HashedFirstName)) score += 0.25;

        return Math.Min(10.0, Math.Round(score, 1));
    }

    public async Task<ServerConversionEvent> TrackConversionAsync(
        Guid tenantId,
        AdPlatform platform,
        ConversionEventType eventType,
        decimal valueAmount,
        string currency,
        string? email = null,
        string? phone = null,
        string? firstName = null,
        string? lastName = null,
        string? city = null,
        string? country = null,
        string? fbc = null,
        string? fbp = null,
        string? userAgent = null,
        string? ipAddress = null,
        string? customDataJson = null,
        string? eventId = null)
    {
        var deduplicationId = eventId ?? $"EVT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..12]}";

        var ev = new ServerConversionEvent
        {
            TenantId = tenantId,
            Platform = platform,
            EventType = eventType,
            EventId = deduplicationId,
            EventTimeUtc = DateTime.UtcNow,
            HashedEmail = string.IsNullOrWhiteSpace(email) ? null : HashSha256(email),
            HashedPhone = string.IsNullOrWhiteSpace(phone) ? null : HashSha256(phone),
            HashedFirstName = string.IsNullOrWhiteSpace(firstName) ? null : HashSha256(firstName),
            HashedLastName = string.IsNullOrWhiteSpace(lastName) ? null : HashSha256(lastName),
            HashedCity = string.IsNullOrWhiteSpace(city) ? null : HashSha256(city),
            HashedCountry = string.IsNullOrWhiteSpace(country) ? null : HashSha256(country),
            Fbc = fbc,
            Fbp = fbp,
            ClientUserAgent = userAgent,
            ClientIpAddress = ipAddress,
            ValueAmount = valueAmount,
            Currency = currency.ToUpperInvariant(),
            CustomDataJson = customDataJson ?? "{}",
            Status = ConversionDispatchStatus.Dispatched,
            DispatchedAtUtc = DateTime.UtcNow,
            ApiResponseJson = JsonSerializer.Serialize(new
            {
                events_received = 1,
                messages = Array.Empty<string>(),
                fbtrace_id = $"TRACE_{Guid.NewGuid():N}",
                match_quality_score = CalculateEventMatchQualityScore(new ServerConversionEvent
                {
                    HashedEmail = HashSha256(email),
                    HashedPhone = HashSha256(phone),
                    Fbc = fbc,
                    Fbp = fbp,
                    ClientUserAgent = userAgent,
                    ClientIpAddress = ipAddress
                })
            })
        };

        _dbContext.ServerConversionEvents.Add(ev);
        await _dbContext.SaveChangesAsync();

        return ev;
    }

    public async Task<List<ServerConversionEvent>> GetRecentConversionsAsync(Guid tenantId, int limit = 50)
    {
        return await _dbContext.ServerConversionEvents
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.EventTimeUtc)
            .Take(limit)
            .ToListAsync();
    }
}
