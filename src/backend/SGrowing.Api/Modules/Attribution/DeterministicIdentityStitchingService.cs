using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Attribution;

public record StitchIdentityEventRequest(
    Guid TenantId,
    string AnonymousVisitorId,
    Guid? KnownContactId = null,
    string? Email = null,
    string? Phone = null,
    string? FirstName = null,
    string? LastName = null,
    string? Company = null,
    string IdentificationSource = "Login",
    string? DeviceFingerprint = null,
    string? ClientIpAddress = null,
    string? ClientUserAgent = null,
    Dictionary<string, object?>? CustomMetadata = null,
    DateTime? EventTimeUtc = null
);

public record StitchIdentityResult(
    bool Success,
    Guid CanonicalContactId,
    string AnonymousVisitorId,
    string? Email,
    string? Phone,
    string? PrimaryName,
    int RetroactiveTouchpointsUpdated,
    IReadOnlyList<string> MergedDeviceFingerprints,
    IReadOnlyList<string> MergedIpAddresses,
    IReadOnlyList<string> MergedUserAgents,
    DateTime StitchedAtUtc,
    string Message
);

public record RecordAnonymousTouchpointRequest(
    Guid TenantId,
    string AnonymousVisitorId,
    string Channel,
    string CampaignName,
    TouchpointStage Stage = TouchpointStage.FirstDiscovery,
    decimal Cost = 0m,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? UtmCampaign = null,
    Guid? DealId = null,
    DateTime? OccurredAtUtc = null
);

public record UnifiedIdentityProfileDto(
    Guid ContactId,
    Guid TenantId,
    string PrimaryName,
    string? PrimaryEmail,
    string? PrimaryPhone,
    IReadOnlyList<string> AnonymousVisitorIds,
    IReadOnlyList<string> DeviceFingerprints,
    IReadOnlyList<string> IpAddresses,
    IReadOnlyList<string> UserAgents,
    IReadOnlyList<string> IdentificationSources,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    int TotalTouchpointsCount
);

public interface IDeterministicIdentityStitchingService
{
    string GenerateVisitorId();
    bool IsValidVisitorId(string? visitorId);
    Task<AttributionTouchpoint> RecordAnonymousTouchpointAsync(RecordAnonymousTouchpointRequest request, CancellationToken ct = default);
    Task<StitchIdentityResult> StitchIdentityAsync(StitchIdentityEventRequest request, CancellationToken ct = default);
    Task<UnifiedIdentityProfileDto?> GetUnifiedProfileAsync(Guid tenantId, Guid contactId, CancellationToken ct = default);
    Task<UnifiedIdentityProfileDto?> GetProfileByVisitorIdAsync(Guid tenantId, string visitorId, CancellationToken ct = default);
    Task<IReadOnlyList<UnifiedIdentityProfileDto>> GetAllStitchedProfilesAsync(Guid tenantId, CancellationToken ct = default);
}

public class DeterministicIdentityStitchingService : IDeterministicIdentityStitchingService
{
    private readonly SGrowingDbContext? _db;
    private readonly ILogger<DeterministicIdentityStitchingService>? _logger;

    // In-memory identity registry for fast multi-tenant graph lookup & standalone testing
    private readonly ConcurrentDictionary<string, Guid> _visitorToContactMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, IdentityGraphNode> _identityProfiles = new();
    private readonly ConcurrentDictionary<string, List<AttributionTouchpoint>> _anonymousTouchpointStore = new(StringComparer.OrdinalIgnoreCase);

    public DeterministicIdentityStitchingService(
        SGrowingDbContext? db = null,
        ILogger<DeterministicIdentityStitchingService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public string GenerateVisitorId()
    {
        var randomBytes = new byte[16];
        RandomNumberGenerator.Fill(randomBytes);
        return $"_sg_vid_{Convert.ToHexStringLower(randomBytes)}";
    }

    public bool IsValidVisitorId(string? visitorId)
    {
        if (string.IsNullOrWhiteSpace(visitorId))
            return false;

        return visitorId.StartsWith("_sg_vid_", StringComparison.OrdinalIgnoreCase) && visitorId.Length >= 16;
    }

    public async Task<AttributionTouchpoint> RecordAnonymousTouchpointAsync(
        RecordAnonymousTouchpointRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var visitorId = string.IsNullOrWhiteSpace(request.AnonymousVisitorId)
            ? GenerateVisitorId()
            : request.AnonymousVisitorId.Trim();

        // Determine if this visitor is already stitched to a known contact
        Guid contactId;
        if (_visitorToContactMap.TryGetValue(visitorId, out var mappedContactId))
        {
            contactId = mappedContactId;
        }
        else
        {
            // Deterministic temporary UUID derived from visitorId for DB FK integrity
            contactId = GenerateDeterministicGuid(visitorId);
        }

        var touchpoint = AttributionTouchpoint.Create(
            tenantId: request.TenantId,
            contactId: contactId,
            channel: request.Channel,
            campaignName: request.CampaignName,
            stage: request.Stage,
            dealId: request.DealId,
            cost: request.Cost,
            utmSource: request.UtmSource,
            utmMedium: request.UtmMedium,
            utmCampaign: request.UtmCampaign
        );

        if (request.OccurredAtUtc.HasValue)
        {
            touchpoint.OccurredAtUtc = request.OccurredAtUtc.Value;
        }

        // Store in memory for immediate access
        _anonymousTouchpointStore.AddOrUpdate(
            visitorId,
            [touchpoint],
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(touchpoint);
                }
                return list;
            });

        // If DbContext is available, persist touchpoint
        if (_db != null)
        {
            await _db.AttributionTouchpoints.AddAsync(touchpoint, ct);
            await _db.SaveChangesAsync(ct);
        }

        _logger?.LogInformation(
            "Recorded touchpoint {TouchpointId} for anonymous visitor {VisitorId} (channel: {Channel}, cost: {Cost})",
            touchpoint.Id, visitorId, touchpoint.Channel, touchpoint.Cost);

        return touchpoint;
    }

    public async Task<StitchIdentityResult> StitchIdentityAsync(
        StitchIdentityEventRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AnonymousVisitorId))
            throw new ArgumentException("Anonymous visitor ID (_sg_vid) is required for identity stitching.", nameof(request));

        var visitorId = request.AnonymousVisitorId.Trim();
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedPhone = NormalizePhone(request.Phone);
        var now = request.EventTimeUtc ?? DateTime.UtcNow;

        Guid canonicalContactId = Guid.Empty;
        UnifiedCustomerProfile? canonicalProfile = null;
        int retroactiveTouchpointsCount = 0;

        // 1. Resolve canonical contact via DbContext if available
        if (_db != null)
        {
            // A. Check by known contact ID
            if (request.KnownContactId.HasValue && request.KnownContactId.Value != Guid.Empty)
            {
                canonicalProfile = await _db.UnifiedCustomerProfiles
                    .FirstOrDefaultAsync(p => p.TenantId == request.TenantId && p.Id == request.KnownContactId.Value, ct);
            }

            // B. Check by normalized phone
            if (canonicalProfile == null && !string.IsNullOrWhiteSpace(normalizedPhone))
            {
                canonicalProfile = await _db.UnifiedCustomerProfiles
                    .FirstOrDefaultAsync(p => p.TenantId == request.TenantId && p.PrimaryPhone == normalizedPhone, ct);

                if (canonicalProfile == null)
                {
                    var phoneLink = await _db.CustomerIdentityLinks
                        .FirstOrDefaultAsync(l => l.TenantId == request.TenantId &&
                                                  l.IdentityType == CustomerChannelIdentityType.PhoneE164 &&
                                                  l.IdentityValue == normalizedPhone, ct);
                    if (phoneLink != null)
                    {
                        canonicalProfile = await _db.UnifiedCustomerProfiles
                            .FirstOrDefaultAsync(p => p.TenantId == request.TenantId && p.Id == phoneLink.UnifiedCustomerId, ct);
                    }
                }
            }

            // C. Check by normalized email
            if (canonicalProfile == null && !string.IsNullOrWhiteSpace(normalizedEmail))
            {
                canonicalProfile = await _db.UnifiedCustomerProfiles
                    .FirstOrDefaultAsync(p => p.TenantId == request.TenantId && p.PrimaryEmail == normalizedEmail, ct);

                if (canonicalProfile == null)
                {
                    var emailLink = await _db.CustomerIdentityLinks
                        .FirstOrDefaultAsync(l => l.TenantId == request.TenantId &&
                                                  l.IdentityType == CustomerChannelIdentityType.Email &&
                                                  l.IdentityValue == normalizedEmail, ct);
                    if (emailLink != null)
                    {
                        canonicalProfile = await _db.UnifiedCustomerProfiles
                            .FirstOrDefaultAsync(p => p.TenantId == request.TenantId && p.Id == emailLink.UnifiedCustomerId, ct);
                    }
                }
            }

            // D. Check by existing cookie link
            if (canonicalProfile == null)
            {
                var cookieLink = await _db.CustomerIdentityLinks
                    .FirstOrDefaultAsync(l => l.TenantId == request.TenantId &&
                                              l.IdentityType == CustomerChannelIdentityType.WebCookieId &&
                                              l.IdentityValue == visitorId, ct);
                if (cookieLink != null)
                {
                    canonicalProfile = await _db.UnifiedCustomerProfiles
                        .FirstOrDefaultAsync(p => p.TenantId == request.TenantId && p.Id == cookieLink.UnifiedCustomerId, ct);
                }
            }

            // E. If profile still not found, create new UnifiedCustomerProfile
            if (canonicalProfile == null)
            {
                var resolvedName = ResolveDisplayName(request.FirstName, request.LastName, normalizedEmail, normalizedPhone);
                canonicalProfile = new UnifiedCustomerProfile
                {
                    Id = request.KnownContactId ?? Guid.NewGuid(),
                    TenantId = request.TenantId,
                    PrimaryName = resolvedName,
                    PrimaryEmail = string.IsNullOrEmpty(normalizedEmail) ? null : normalizedEmail,
                    PrimaryPhone = normalizedPhone ?? string.Empty,
                    Company = request.Company?.Trim(),
                    LifecycleStage = DetermineLifecycleStage(request.IdentificationSource),
                    TotalOrdersCount = string.Equals(request.IdentificationSource, "Purchase", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                    TotalSpent = 0m,
                    DeliverySuccessRate = 100.0,
                    CreatedAtUtc = now,
                    LastSeenAtUtc = now
                };

                await _db.UnifiedCustomerProfiles.AddAsync(canonicalProfile, ct);
            }
            else
            {
                // Update profile attributes if new ones provided
                if (!string.IsNullOrWhiteSpace(request.FirstName) || !string.IsNullOrWhiteSpace(request.LastName))
                {
                    var fullName = $"{request.FirstName?.Trim()} {request.LastName?.Trim()}".Trim();
                    if (!string.IsNullOrWhiteSpace(fullName) && (canonicalProfile.PrimaryName == "Unified Customer" || canonicalProfile.PrimaryName.StartsWith("+")))
                    {
                        canonicalProfile.PrimaryName = fullName;
                    }
                }
                if (string.IsNullOrWhiteSpace(canonicalProfile.PrimaryEmail) && !string.IsNullOrWhiteSpace(normalizedEmail))
                {
                    canonicalProfile.PrimaryEmail = normalizedEmail;
                }
                if (string.IsNullOrWhiteSpace(canonicalProfile.PrimaryPhone) && !string.IsNullOrWhiteSpace(normalizedPhone))
                {
                    canonicalProfile.PrimaryPhone = normalizedPhone;
                }
                if (string.IsNullOrWhiteSpace(canonicalProfile.Company) && !string.IsNullOrWhiteSpace(request.Company))
                {
                    canonicalProfile.Company = request.Company.Trim();
                }

                canonicalProfile.LastSeenAtUtc = now;
            }

            canonicalContactId = canonicalProfile.Id;

            // Link cookie identity
            var existingCookieLink = await _db.CustomerIdentityLinks
                .AnyAsync(l => l.TenantId == request.TenantId &&
                               l.UnifiedCustomerId == canonicalContactId &&
                               l.IdentityType == CustomerChannelIdentityType.WebCookieId &&
                               l.IdentityValue == visitorId, ct);

            if (!existingCookieLink)
            {
                await _db.CustomerIdentityLinks.AddAsync(new CustomerIdentityLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = request.TenantId,
                    UnifiedCustomerId = canonicalContactId,
                    IdentityType = CustomerChannelIdentityType.WebCookieId,
                    IdentityValue = visitorId,
                    LinkedAtUtc = now
                }, ct);
            }

            // Link Phone & Email if present
            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                var phoneExists = await _db.CustomerIdentityLinks
                    .AnyAsync(l => l.TenantId == request.TenantId &&
                                   l.UnifiedCustomerId == canonicalContactId &&
                                   l.IdentityType == CustomerChannelIdentityType.PhoneE164 &&
                                   l.IdentityValue == normalizedPhone, ct);
                if (!phoneExists)
                {
                    await _db.CustomerIdentityLinks.AddAsync(new CustomerIdentityLink
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId,
                        UnifiedCustomerId = canonicalContactId,
                        IdentityType = CustomerChannelIdentityType.PhoneE164,
                        IdentityValue = normalizedPhone,
                        LinkedAtUtc = now
                    }, ct);
                }
            }

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                var emailExists = await _db.CustomerIdentityLinks
                    .AnyAsync(l => l.TenantId == request.TenantId &&
                                   l.UnifiedCustomerId == canonicalContactId &&
                                   l.IdentityType == CustomerChannelIdentityType.Email &&
                                   l.IdentityValue == normalizedEmail, ct);
                if (!emailExists)
                {
                    await _db.CustomerIdentityLinks.AddAsync(new CustomerIdentityLink
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId,
                        UnifiedCustomerId = canonicalContactId,
                        IdentityType = CustomerChannelIdentityType.Email,
                        IdentityValue = normalizedEmail,
                        LinkedAtUtc = now
                    }, ct);
                }
            }

            // Retroactively update touchpoints in database
            var anonTempGuid = GenerateDeterministicGuid(visitorId);
            var touchpointsToUpdate = await _db.AttributionTouchpoints
                .Where(t => t.TenantId == request.TenantId && t.ContactId == anonTempGuid)
                .ToListAsync(ct);

            foreach (var tp in touchpointsToUpdate)
            {
                tp.ContactId = canonicalContactId;
                retroactiveTouchpointsCount++;
            }

            // Record timeline event for identity stitching
            var timelinePayload = JsonSerializer.Serialize(new
            {
                visitorId,
                identificationSource = request.IdentificationSource,
                deviceFingerprint = request.DeviceFingerprint,
                ipAddress = request.ClientIpAddress,
                userAgent = request.ClientUserAgent,
                retroactiveTouchpointsCount
            });

            await _db.CustomerTimelineEvents.AddAsync(new CustomerTimelineEvent
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                UnifiedCustomerId = canonicalContactId,
                EventType = "IdentityStitched",
                Summary = $"Deterministic identity stitched anonymous visitor {visitorId} via {request.IdentificationSource}",
                PayloadJson = timelinePayload,
                OccurredAtUtc = now
            }, ct);

            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // In-Memory Fallback for lightweight / test environments
            canonicalContactId = request.KnownContactId ??
                                 _visitorToContactMap.GetValueOrDefault(visitorId, GenerateDeterministicGuid(visitorId));
        }

        // 2. Unify In-Memory Identity Graph Node
        _visitorToContactMap[visitorId] = canonicalContactId;

        var node = _identityProfiles.GetOrAdd(canonicalContactId, id => new IdentityGraphNode
        {
            ContactId = id,
            TenantId = request.TenantId,
            PrimaryName = ResolveDisplayName(request.FirstName, request.LastName, normalizedEmail, normalizedPhone),
            PrimaryEmail = normalizedEmail,
            PrimaryPhone = normalizedPhone,
            FirstSeenAtUtc = now,
            LastSeenAtUtc = now
        });

        lock (node)
        {
            node.AnonymousVisitorIds.Add(visitorId);

            if (!string.IsNullOrWhiteSpace(request.DeviceFingerprint))
                node.DeviceFingerprints.Add(request.DeviceFingerprint.Trim());

            if (!string.IsNullOrWhiteSpace(request.ClientIpAddress))
                node.IpAddresses.Add(request.ClientIpAddress.Trim());

            if (!string.IsNullOrWhiteSpace(request.ClientUserAgent))
                node.UserAgents.Add(request.ClientUserAgent.Trim());

            if (!string.IsNullOrWhiteSpace(request.IdentificationSource))
                node.IdentificationSources.Add(request.IdentificationSource.Trim());

            if (!string.IsNullOrWhiteSpace(normalizedEmail) && string.IsNullOrEmpty(node.PrimaryEmail))
                node.PrimaryEmail = normalizedEmail;

            if (!string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrEmpty(node.PrimaryPhone))
                node.PrimaryPhone = normalizedPhone;

            if (!string.IsNullOrWhiteSpace(request.FirstName) || !string.IsNullOrWhiteSpace(request.LastName))
            {
                var fullName = $"{request.FirstName?.Trim()} {request.LastName?.Trim()}".Trim();
                if (!string.IsNullOrWhiteSpace(fullName))
                    node.PrimaryName = fullName;
            }

            node.LastSeenAtUtc = now;
        }

        // 3. Retroactively update in-memory touchpoint store
        if (_anonymousTouchpointStore.TryGetValue(visitorId, out var memTouchpoints))
        {
            lock (memTouchpoints)
            {
                foreach (var tp in memTouchpoints)
                {
                    if (tp.ContactId != canonicalContactId)
                    {
                        tp.ContactId = canonicalContactId;
                        if (_db == null)
                        {
                            retroactiveTouchpointsCount++;
                        }
                    }
                }
            }
        }

        var result = new StitchIdentityResult(
            Success: true,
            CanonicalContactId: canonicalContactId,
            AnonymousVisitorId: visitorId,
            Email: node.PrimaryEmail,
            Phone: node.PrimaryPhone,
            PrimaryName: node.PrimaryName,
            RetroactiveTouchpointsUpdated: retroactiveTouchpointsCount,
            MergedDeviceFingerprints: [.. node.DeviceFingerprints],
            MergedIpAddresses: [.. node.IpAddresses],
            MergedUserAgents: [.. node.UserAgents],
            StitchedAtUtc: now,
            Message: $"Successfully stitched anonymous visitor '{visitorId}' to canonical contact '{canonicalContactId}' with {retroactiveTouchpointsCount} retroactive touchpoints linked."
        );

        _logger?.LogInformation(
            "Stitched identity for visitor {VisitorId} -> Contact {ContactId} (Retroactive touchpoints: {Count})",
            visitorId, canonicalContactId, retroactiveTouchpointsCount);

        return result;
    }

    public async Task<UnifiedIdentityProfileDto?> GetUnifiedProfileAsync(
        Guid tenantId,
        Guid contactId,
        CancellationToken ct = default)
    {
        if (_identityProfiles.TryGetValue(contactId, out var node))
        {
            int touchpointCount = 0;
            if (_db != null)
            {
                touchpointCount = await _db.AttributionTouchpoints
                    .CountAsync(t => t.TenantId == tenantId && t.ContactId == contactId, ct);
            }
            else
            {
                touchpointCount = _anonymousTouchpointStore.Values
                    .SelectMany(l => l)
                    .Count(t => t.ContactId == contactId);
            }

            return new UnifiedIdentityProfileDto(
                ContactId: node.ContactId,
                TenantId: node.TenantId,
                PrimaryName: node.PrimaryName,
                PrimaryEmail: node.PrimaryEmail,
                PrimaryPhone: node.PrimaryPhone,
                AnonymousVisitorIds: [.. node.AnonymousVisitorIds],
                DeviceFingerprints: [.. node.DeviceFingerprints],
                IpAddresses: [.. node.IpAddresses],
                UserAgents: [.. node.UserAgents],
                IdentificationSources: [.. node.IdentificationSources],
                FirstSeenAtUtc: node.FirstSeenAtUtc,
                LastSeenAtUtc: node.LastSeenAtUtc,
                TotalTouchpointsCount: touchpointCount
            );
        }

        if (_db != null)
        {
            var profile = await _db.UnifiedCustomerProfiles
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == contactId, ct);
            if (profile == null) return null;

            var cookieLinks = await _db.CustomerIdentityLinks
                .Where(l => l.TenantId == tenantId && l.UnifiedCustomerId == contactId && l.IdentityType == CustomerChannelIdentityType.WebCookieId)
                .Select(l => l.IdentityValue)
                .ToListAsync(ct);

            var touchpointCount = await _db.AttributionTouchpoints
                .CountAsync(t => t.TenantId == tenantId && t.ContactId == contactId, ct);

            return new UnifiedIdentityProfileDto(
                ContactId: profile.Id,
                TenantId: profile.TenantId,
                PrimaryName: profile.PrimaryName,
                PrimaryEmail: profile.PrimaryEmail,
                PrimaryPhone: profile.PrimaryPhone,
                AnonymousVisitorIds: cookieLinks,
                DeviceFingerprints: [],
                IpAddresses: [],
                UserAgents: [],
                IdentificationSources: ["Database"],
                FirstSeenAtUtc: profile.CreatedAtUtc,
                LastSeenAtUtc: profile.LastSeenAtUtc,
                TotalTouchpointsCount: touchpointCount
            );
        }

        return null;
    }

    public async Task<UnifiedIdentityProfileDto?> GetProfileByVisitorIdAsync(
        Guid tenantId,
        string visitorId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(visitorId))
            return null;

        var cleanVisitorId = visitorId.Trim();

        if (_visitorToContactMap.TryGetValue(cleanVisitorId, out var contactId))
        {
            return await GetUnifiedProfileAsync(tenantId, contactId, ct);
        }

        if (_db != null)
        {
            var link = await _db.CustomerIdentityLinks
                .FirstOrDefaultAsync(l => l.TenantId == tenantId &&
                                          l.IdentityType == CustomerChannelIdentityType.WebCookieId &&
                                          l.IdentityValue == cleanVisitorId, ct);

            if (link != null)
            {
                return await GetUnifiedProfileAsync(tenantId, link.UnifiedCustomerId, ct);
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<UnifiedIdentityProfileDto>> GetAllStitchedProfilesAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var results = new List<UnifiedIdentityProfileDto>();

        foreach (var node in _identityProfiles.Values.Where(n => n.TenantId == tenantId))
        {
            var dto = await GetUnifiedProfileAsync(tenantId, node.ContactId, ct);
            if (dto != null)
            {
                results.Add(dto);
            }
        }

        return results;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var trimmed = phone.Trim();
        var digitsOnly = Regex.Replace(trimmed, @"\D", "");
        if (string.IsNullOrEmpty(digitsOnly))
            return null;

        return trimmed.StartsWith('+') ? "+" + digitsOnly : "+" + digitsOnly;
    }

    private static string ResolveDisplayName(string? first, string? last, string? email, string? phone)
    {
        var name = $"{first?.Trim()} {last?.Trim()}".Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        if (!string.IsNullOrWhiteSpace(email))
            return email.Trim();

        if (!string.IsNullOrWhiteSpace(phone))
            return phone.Trim();

        return "Unified Customer";
    }

    private static CustomerLifecycleStage DetermineLifecycleStage(string source)
    {
        return source.ToLowerInvariant() switch
        {
            "purchase" or "phoneorder" => CustomerLifecycleStage.Customer,
            "formfill" or "login" or "whatsappchat" => CustomerLifecycleStage.Prospect,
            _ => CustomerLifecycleStage.Lead
        };
    }

    private static Guid GenerateDeterministicGuid(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    private sealed class IdentityGraphNode
    {
        public required Guid ContactId { get; init; }
        public required Guid TenantId { get; init; }
        public string PrimaryName { get; set; } = string.Empty;
        public string? PrimaryEmail { get; set; }
        public string? PrimaryPhone { get; set; }
        public HashSet<string> AnonymousVisitorIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DeviceFingerprints { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> IpAddresses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> UserAgents { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> IdentificationSources { get; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime FirstSeenAtUtc { get; set; }
        public DateTime LastSeenAtUtc { get; set; }
    }
}
