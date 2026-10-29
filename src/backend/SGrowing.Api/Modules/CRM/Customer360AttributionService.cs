using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM;

#region DTOs and Contracts

public record StitchIdentityRequest(
    Guid TenantId,
    string? CookieId = null,            // e.g. "_sg_vid_94a8b1c7"
    string? Phone = null,               // e.g. "+21698123456"
    string? Email = null,               // e.g. "customer@example.com"
    string? WhatsAppJid = null,         // e.g. "21698123456@s.whatsapp.net"
    string? SocialHandle = null,        // e.g. "@samir_ecom"
    CustomerChannelIdentityType? SocialType = null, // InstagramHandle or TikTokHandle
    string? Name = null,
    string? Company = null,
    string? PerformedBy = "System"
);

public record LinkedChannelBadgeDto(
    CustomerChannelIdentityType IdentityType,
    string ChannelName,
    string IdentityValue,
    DateTime LinkedAtUtc,
    string Icon
);

public record IdentityStitchResultDto(
    bool Success,
    Guid UnifiedCustomerId,
    string PrimaryName,
    string PrimaryPhone,
    string? PrimaryEmail,
    CustomerLifecycleStage LifecycleStage,
    bool WasMerged,
    Guid? MergedFromCustomerId,
    List<LinkedChannelBadgeDto> LinkedChannels,
    string Message
);

public record Customer360ListItemDto(
    Guid Id,
    Guid TenantId,
    string PrimaryName,
    string PrimaryPhone,
    string? PrimaryEmail,
    string? Company,
    CustomerLifecycleStage LifecycleStage,
    int TotalOrdersCount,
    decimal TotalSpent,
    double DeliverySuccessRate,
    string ChurnRiskLevel, // Low, Medium, High, Critical
    int ChurnRiskScore,    // 0 - 100
    int LinkedChannelsCount,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc
);

public record Customer360OverviewDto(
    Guid Id,
    Guid TenantId,
    string PrimaryName,
    string PrimaryPhone,
    string? PrimaryEmail,
    string? Company,
    CustomerLifecycleStage LifecycleStage,
    decimal LifetimeValue,
    int TotalOrdersCount,
    decimal TotalSpent,
    double DeliverySuccessRate,
    string ChurnRiskLevel,
    int ChurnRiskScore,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    int TotalInteractionsCount,
    int TotalTouchpointsCount,
    List<LinkedChannelBadgeDto> LinkedChannels,
    List<CustomerTimelineEventDto> RecentTimeline
);

public record CustomerTimelineEventDto(
    Guid Id,
    Guid UnifiedCustomerId,
    string EventType,
    string Channel,
    string Summary,
    string Icon,
    string Severity, // info, success, warning, accent
    string PayloadJson,
    DateTime OccurredAtUtc
);

public record RecordCustomerEventRequest(
    Guid TenantId,
    Guid? UnifiedCustomerId = null,
    string? CookieId = null,
    string? Phone = null,
    string? Email = null,
    string EventType = "CustomerInteraction",
    string Summary = "",
    string PayloadJson = "{}",
    DateTime? OccurredAtUtc = null
);

public record RecordTouchpointRequest(
    Guid TenantId,
    Guid ContactId,
    string Channel,
    string CampaignName,
    TouchpointStage Stage = TouchpointStage.FirstDiscovery,
    Guid? DealId = null,
    decimal Cost = 0m,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? UtmCampaign = null,
    DateTime? OccurredAtUtc = null
);

public record AttributedTouchpointShareDto(
    Guid TouchpointId,
    string Channel,
    string CampaignName,
    TouchpointStage Stage,
    decimal WeightPercent,
    decimal AttributedRevenue,
    decimal Cost,
    DateTime OccurredAtUtc
);

public record AttributionModelResultDto(
    AttributionModelType ModelType,
    string ModelName,
    string Description,
    List<AttributedTouchpointShareDto> Shares,
    Dictionary<string, decimal> ChannelRevenueBreakdown,
    Dictionary<string, decimal> CampaignRevenueBreakdown
);

public record AttributionComparisonDto(
    Guid TenantId,
    Guid? CustomerId,
    decimal TotalRevenue,
    int TotalTouchpointsCount,
    AttributionModelResultDto FirstTouch,
    AttributionModelResultDto LastTouch,
    AttributionModelResultDto Linear,
    AttributionModelResultDto UShaped,
    AttributionModelResultDto TimeDecay
);

public record CampaignRoasBreakdownDto(
    string CampaignName,
    string Channel,
    decimal TotalCost,
    decimal AttributedRevenue,
    decimal TrueRoas,
    decimal Cac,
    int CustomerCount,
    int TouchpointCount
);

#endregion

public interface ICustomer360AttributionService
{
    // Search & List
    Task<List<Customer360ListItemDto>> SearchCustomersAsync(Guid tenantId, string? query = null, CustomerLifecycleStage? stage = null, int page = 1, int pageSize = 20, CancellationToken ct = default);

    // Overview
    Task<Customer360OverviewDto> GetCustomer360OverviewAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);

    // Timeline Stream
    Task<List<CustomerTimelineEventDto>> GetCustomerTimelineAsync(Guid tenantId, Guid customerId, string? channel = null, string? eventType = null, bool chronological = false, CancellationToken ct = default);

    // Identity Stitching & Resolution
    Task<IdentityStitchResultDto> StitchIdentityAsync(StitchIdentityRequest req, CancellationToken ct = default);

    Task<UnifiedCustomerProfile> MergeCustomerProfilesAsync(Guid tenantId, Guid primaryCustomerId, Guid duplicateCustomerId, string performedBy = "System", CancellationToken ct = default);

    // Events & Touchpoints
    Task<CustomerTimelineEventDto> RecordEventAsync(RecordCustomerEventRequest req, CancellationToken ct = default);

    Task<AttributionTouchpoint> RecordTouchpointAsync(RecordTouchpointRequest req, CancellationToken ct = default);

    // Attribution Engine
    Task<AttributionComparisonDto> CompareAttributionModelsAsync(Guid tenantId, Guid? customerId = null, Guid? dealId = null, CancellationToken ct = default);

    Task<List<CampaignRoasBreakdownDto>> GetCampaignRoasBreakdownAsync(Guid tenantId, AttributionModelType model = AttributionModelType.TimeDecay, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);
}

public class Customer360AttributionService : ICustomer360AttributionService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<Customer360AttributionService>? _logger;

    public Customer360AttributionService(SGrowingDbContext db, ILogger<Customer360AttributionService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    #region Normalization Helpers

    public static string NormalizePhoneToE164(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var trimmed = phone.Trim();
        var digits = Regex.Replace(trimmed, @"[^\d+]", "");

        if (digits.StartsWith("+"))
            return digits;

        // North Africa & Middle East prefixes
        if (digits.StartsWith("216") || digits.StartsWith("213") || digits.StartsWith("212") || digits.StartsWith("20"))
            return "+" + digits;

        // Tunisia local 8 digits (e.g. 98123456 or 50123456)
        if (digits.Length == 8)
            return "+216" + digits;

        // Egypt local 11 digits starting with 01
        if (digits.Length == 11 && digits.StartsWith("01"))
            return "+20" + digits.Substring(1);

        // Morocco local 10 digits starting with 06/07
        if (digits.Length == 10 && (digits.StartsWith("06") || digits.StartsWith("07")))
            return "+212" + digits.Substring(1);

        // Algeria local 10 digits starting with 05/06/07
        if (digits.Length == 10 && (digits.StartsWith("05") || digits.StartsWith("06") || digits.StartsWith("07")))
            return "+213" + digits.Substring(1);

        return "+" + digits;
    }

    public static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
    }

    public static string NormalizeSocialHandle(string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
            return string.Empty;

        var trimmed = handle.Trim();
        return trimmed.StartsWith("@") ? trimmed.ToLowerInvariant() : "@" + trimmed.ToLowerInvariant();
    }

    public static (string Level, int Score) CalculateChurnRisk(UnifiedCustomerProfile profile)
    {
        int score = 10; // Baseline

        var daysSinceLastSeen = (DateTime.UtcNow - profile.LastSeenAtUtc).TotalDays;
        if (daysSinceLastSeen > 90)
            score += 45;
        else if (daysSinceLastSeen > 45)
            score += 25;
        else if (daysSinceLastSeen > 20)
            score += 10;

        if (profile.DeliverySuccessRate < 60.0)
            score += 35;
        else if (profile.DeliverySuccessRate < 80.0)
            score += 15;

        if (profile.LifecycleStage == CustomerLifecycleStage.ChurnRisk)
            score += 30;
        else if (profile.LifecycleStage == CustomerLifecycleStage.Blacklisted)
            score = 100;
        else if (profile.LifecycleStage == CustomerLifecycleStage.Vip)
            score = Math.Max(5, score - 20);

        score = Math.Clamp(score, 0, 100);

        string level = score switch
        {
            >= 75 => "Critical",
            >= 50 => "High",
            >= 25 => "Medium",
            _ => "Low"
        };

        return (level, score);
    }

    public static (string Channel, string Icon, string Severity) CategorizeEvent(string eventType)
    {
        var lower = eventType.ToLowerInvariant();

        if (lower.Contains("order") || lower.Contains("purchase") || lower.Contains("checkout") || lower.Contains("dealwon"))
            return ("Order", "shopping-cart", "success");

        if (lower.Contains("call") || lower.Contains("dialer") || lower.Contains("voice") || lower.Contains("amd"))
            return ("Voice", "phone", "accent");

        if (lower.Contains("whatsapp"))
            return ("WhatsApp", "message-circle", "info");

        if (lower.Contains("instagram") || lower.Contains("tiktok") || lower.Contains("social") || lower.Contains("dm") || lower.Contains("comment"))
            return ("Social", "share-2", "accent");

        if (lower.Contains("email"))
            return ("Email", "mail", "info");

        if (lower.Contains("proposal") || lower.Contains("quote") || lower.Contains("signing"))
            return ("Proposal", "file-text", "warning");

        if (lower.Contains("support") || lower.Contains("ticket") || lower.Contains("csat") || lower.Contains("rma"))
            return ("Support", "headphones", "warning");

        if (lower.Contains("stitch") || lower.Contains("merge") || lower.Contains("link") || lower.Contains("identity"))
            return ("Identity", "link", "accent");

        return ("Web", "activity", "info");
    }

    public static string GetChannelIcon(CustomerChannelIdentityType type) => type switch
    {
        CustomerChannelIdentityType.PhoneE164 => "phone",
        CustomerChannelIdentityType.Email => "mail",
        CustomerChannelIdentityType.WhatsAppId => "message-circle",
        CustomerChannelIdentityType.InstagramHandle => "instagram",
        CustomerChannelIdentityType.TikTokHandle => "video",
        CustomerChannelIdentityType.FacebookPsid => "facebook",
        CustomerChannelIdentityType.TelegramId => "send",
        CustomerChannelIdentityType.WebCookieId => "globe",
        _ => "user"
    };

    public static string GetChannelDisplayName(CustomerChannelIdentityType type) => type switch
    {
        CustomerChannelIdentityType.PhoneE164 => "Phone (E.164)",
        CustomerChannelIdentityType.Email => "Email",
        CustomerChannelIdentityType.WhatsAppId => "WhatsApp",
        CustomerChannelIdentityType.InstagramHandle => "Instagram",
        CustomerChannelIdentityType.TikTokHandle => "TikTok",
        CustomerChannelIdentityType.FacebookPsid => "Facebook Messenger",
        CustomerChannelIdentityType.TelegramId => "Telegram",
        CustomerChannelIdentityType.WebCookieId => "Web Cookie (_sg_vid)",
        _ => type.ToString()
    };

    #endregion

    #region Search & Overview

    public async Task<List<Customer360ListItemDto>> SearchCustomersAsync(
        Guid tenantId,
        string? query = null,
        CustomerLifecycleStage? stage = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var q = _db.UnifiedCustomerProfiles.Where(c => c.TenantId == tenantId);

        if (stage.HasValue)
        {
            q = q.Where(c => c.LifecycleStage == stage.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var clean = query.Trim().ToLower();
            q = q.Where(c =>
                c.PrimaryName.ToLower().Contains(clean) ||
                c.PrimaryPhone.Contains(clean) ||
                (c.PrimaryEmail != null && c.PrimaryEmail.ToLower().Contains(clean)) ||
                (c.Company != null && c.Company.ToLower().Contains(clean)));
        }

        var profiles = await q
            .OrderByDescending(c => c.LastSeenAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var customerIds = profiles.Select(p => p.Id).ToList();

        var linkCounts = await _db.CustomerIdentityLinks
            .Where(l => l.TenantId == tenantId && customerIds.Contains(l.UnifiedCustomerId))
            .GroupBy(l => l.UnifiedCustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Count, ct);

        return profiles.Select(p =>
        {
            var (riskLevel, riskScore) = CalculateChurnRisk(p);
            var channelsCount = linkCounts.GetValueOrDefault(p.Id, 0);

            return new Customer360ListItemDto(
                p.Id,
                p.TenantId,
                p.PrimaryName,
                p.PrimaryPhone,
                p.PrimaryEmail,
                p.Company,
                p.LifecycleStage,
                p.TotalOrdersCount,
                p.TotalSpent,
                p.DeliverySuccessRate,
                riskLevel,
                riskScore,
                channelsCount,
                p.CreatedAtUtc,
                p.LastSeenAtUtc
            );
        }).ToList();
    }

    public async Task<Customer360OverviewDto> GetCustomer360OverviewAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        var profile = await _db.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == customerId, ct)
            ?? throw new KeyNotFoundException($"Customer {customerId} not found for tenant {tenantId}.");

        var links = await _db.CustomerIdentityLinks
            .Where(l => l.TenantId == tenantId && l.UnifiedCustomerId == customerId)
            .OrderBy(l => l.LinkedAtUtc)
            .ToListAsync(ct);

        var badges = links.Select(l => new LinkedChannelBadgeDto(
            l.IdentityType,
            GetChannelDisplayName(l.IdentityType),
            l.IdentityValue,
            l.LinkedAtUtc,
            GetChannelIcon(l.IdentityType)
        )).ToList();

        var timelineEvents = await _db.CustomerTimelineEvents
            .Where(t => t.TenantId == tenantId && t.UnifiedCustomerId == customerId)
            .OrderByDescending(t => t.OccurredAtUtc)
            .Take(25)
            .ToListAsync(ct);

        var recentTimeline = timelineEvents.Select(e =>
        {
            var (channel, icon, severity) = CategorizeEvent(e.EventType);
            return new CustomerTimelineEventDto(
                e.Id,
                e.UnifiedCustomerId,
                e.EventType,
                channel,
                e.Summary,
                icon,
                severity,
                e.PayloadJson,
                e.OccurredAtUtc
            );
        }).ToList();

        var totalInteractions = await _db.CustomerTimelineEvents
            .CountAsync(t => t.TenantId == tenantId && t.UnifiedCustomerId == customerId, ct);

        var totalTouchpoints = await _db.AttributionTouchpoints
            .CountAsync(t => t.TenantId == tenantId && t.ContactId == customerId, ct);

        var (riskLevel, riskScore) = CalculateChurnRisk(profile);

        return new Customer360OverviewDto(
            profile.Id,
            profile.TenantId,
            profile.PrimaryName,
            profile.PrimaryPhone,
            profile.PrimaryEmail,
            profile.Company,
            profile.LifecycleStage,
            profile.TotalSpent,
            profile.TotalOrdersCount,
            profile.TotalSpent,
            profile.DeliverySuccessRate,
            riskLevel,
            riskScore,
            profile.CreatedAtUtc,
            profile.LastSeenAtUtc,
            totalInteractions,
            totalTouchpoints,
            badges,
            recentTimeline
        );
    }

    #endregion

    #region Master Chronological Interaction Stream

    public async Task<List<CustomerTimelineEventDto>> GetCustomerTimelineAsync(
        Guid tenantId,
        Guid customerId,
        string? channel = null,
        string? eventType = null,
        bool chronological = false,
        CancellationToken ct = default)
    {
        var query = _db.CustomerTimelineEvents
            .Where(t => t.TenantId == tenantId && t.UnifiedCustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var cleanEventType = eventType.Trim().ToLowerInvariant();
            query = query.Where(t => t.EventType.ToLower().Contains(cleanEventType));
        }

        query = chronological
            ? query.OrderBy(t => t.OccurredAtUtc)
            : query.OrderByDescending(t => t.OccurredAtUtc);

        var events = await query.ToListAsync(ct);

        var dtoList = events.Select(e =>
        {
            var (catChannel, icon, severity) = CategorizeEvent(e.EventType);
            return new CustomerTimelineEventDto(
                e.Id,
                e.UnifiedCustomerId,
                e.EventType,
                catChannel,
                e.Summary,
                icon,
                severity,
                e.PayloadJson,
                e.OccurredAtUtc
            );
        }).ToList();

        if (!string.IsNullOrWhiteSpace(channel))
        {
            var cleanChan = channel.Trim().ToLowerInvariant();
            dtoList = dtoList.Where(d => d.Channel.ToLowerInvariant() == cleanChan).ToList();
        }

        return dtoList;
    }

    public async Task<CustomerTimelineEventDto> RecordEventAsync(RecordCustomerEventRequest req, CancellationToken ct = default)
    {
        Guid targetCustomerId;

        if (req.UnifiedCustomerId.HasValue && req.UnifiedCustomerId.Value != Guid.Empty)
        {
            targetCustomerId = req.UnifiedCustomerId.Value;
        }
        else
        {
            // Resolve customer by cookie, phone, or email
            var stitched = await StitchIdentityAsync(new StitchIdentityRequest(
                TenantId: req.TenantId,
                CookieId: req.CookieId,
                Phone: req.Phone,
                Email: req.Email,
                Name: "Anonymous Visitor",
                PerformedBy: "EventIngestion"
            ), ct);

            targetCustomerId = stitched.UnifiedCustomerId;
        }

        var timelineEvent = new CustomerTimelineEvent
        {
            Id = Guid.NewGuid(),
            TenantId = req.TenantId,
            UnifiedCustomerId = targetCustomerId,
            EventType = string.IsNullOrWhiteSpace(req.EventType) ? "CustomerInteraction" : req.EventType.Trim(),
            Summary = string.IsNullOrWhiteSpace(req.Summary) ? "Event recorded" : req.Summary.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(req.PayloadJson) ? "{}" : req.PayloadJson,
            OccurredAtUtc = req.OccurredAtUtc ?? DateTime.UtcNow
        };

        await _db.CustomerTimelineEvents.AddAsync(timelineEvent, ct);

        // Update LastSeenAtUtc on profile
        var profile = await _db.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == req.TenantId && c.Id == targetCustomerId, ct);

        if (profile != null)
        {
            if (timelineEvent.OccurredAtUtc > profile.LastSeenAtUtc)
            {
                profile.LastSeenAtUtc = timelineEvent.OccurredAtUtc;
            }
        }

        await _db.SaveChangesAsync(ct);

        var (channel, icon, severity) = CategorizeEvent(timelineEvent.EventType);
        return new CustomerTimelineEventDto(
            timelineEvent.Id,
            timelineEvent.UnifiedCustomerId,
            timelineEvent.EventType,
            channel,
            timelineEvent.Summary,
            icon,
            severity,
            timelineEvent.PayloadJson,
            timelineEvent.OccurredAtUtc
        );
    }

    #endregion

    #region Deterministic Identity Resolution & Merging

    public async Task<IdentityStitchResultDto> StitchIdentityAsync(StitchIdentityRequest req, CancellationToken ct = default)
    {
        var tenantId = req.TenantId;
        var normalizedPhone = !string.IsNullOrWhiteSpace(req.Phone) ? NormalizePhoneToE164(req.Phone) : null;
        var normalizedEmail = !string.IsNullOrWhiteSpace(req.Email) ? NormalizeEmail(req.Email) : null;
        var cleanCookieId = !string.IsNullOrWhiteSpace(req.CookieId) ? req.CookieId.Trim() : null;
        var cleanWhatsApp = !string.IsNullOrWhiteSpace(req.WhatsAppJid) ? req.WhatsAppJid.Trim() : null;
        var cleanSocial = !string.IsNullOrWhiteSpace(req.SocialHandle) ? NormalizeSocialHandle(req.SocialHandle) : null;

        // 1. Locate potential matching profiles
        UnifiedCustomerProfile? phoneProfile = null;
        UnifiedCustomerProfile? emailProfile = null;
        UnifiedCustomerProfile? cookieProfile = null;
        UnifiedCustomerProfile? whatsappProfile = null;
        UnifiedCustomerProfile? socialProfile = null;

        // Search by PrimaryPhone or IdentityLink Phone
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            phoneProfile = await _db.UnifiedCustomerProfiles
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PrimaryPhone == normalizedPhone, ct);

            if (phoneProfile == null)
            {
                var link = await _db.CustomerIdentityLinks
                    .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.IdentityType == CustomerChannelIdentityType.PhoneE164 && l.IdentityValue == normalizedPhone, ct);
                if (link != null)
                {
                    phoneProfile = await _db.UnifiedCustomerProfiles.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == link.UnifiedCustomerId, ct);
                }
            }
        }

        // Search by PrimaryEmail or IdentityLink Email
        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            emailProfile = await _db.UnifiedCustomerProfiles
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PrimaryEmail == normalizedEmail, ct);

            if (emailProfile == null)
            {
                var link = await _db.CustomerIdentityLinks
                    .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.IdentityType == CustomerChannelIdentityType.Email && l.IdentityValue == normalizedEmail, ct);
                if (link != null)
                {
                    emailProfile = await _db.UnifiedCustomerProfiles.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == link.UnifiedCustomerId, ct);
                }
            }
        }

        // Search by CookieId
        if (!string.IsNullOrWhiteSpace(cleanCookieId))
        {
            var cookieLink = await _db.CustomerIdentityLinks
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.IdentityType == CustomerChannelIdentityType.WebCookieId && l.IdentityValue == cleanCookieId, ct);
            if (cookieLink != null)
            {
                cookieProfile = await _db.UnifiedCustomerProfiles.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == cookieLink.UnifiedCustomerId, ct);
            }
        }

        // Search by WhatsApp
        if (!string.IsNullOrWhiteSpace(cleanWhatsApp))
        {
            var waLink = await _db.CustomerIdentityLinks
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.IdentityType == CustomerChannelIdentityType.WhatsAppId && l.IdentityValue == cleanWhatsApp, ct);
            if (waLink != null)
            {
                whatsappProfile = await _db.UnifiedCustomerProfiles.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == waLink.UnifiedCustomerId, ct);
            }
        }

        // Search by Social Handle
        if (!string.IsNullOrWhiteSpace(cleanSocial))
        {
            var sType = req.SocialType ?? CustomerChannelIdentityType.InstagramHandle;
            var socLink = await _db.CustomerIdentityLinks
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && (l.IdentityType == sType || l.IdentityType == CustomerChannelIdentityType.InstagramHandle || l.IdentityType == CustomerChannelIdentityType.TikTokHandle) && l.IdentityValue == cleanSocial, ct);
            if (socLink != null)
            {
                socialProfile = await _db.UnifiedCustomerProfiles.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == socLink.UnifiedCustomerId, ct);
            }
        }

        // Prioritize identified master profile over anonymous session
        var candidateIdentifiedProfiles = new[] { phoneProfile, emailProfile, whatsappProfile, socialProfile }
            .Where(p => p != null)
            .DistinctBy(p => p!.Id)
            .ToList();

        UnifiedCustomerProfile masterProfile;
        bool wasMerged = false;
        Guid? mergedFromId = null;

        if (candidateIdentifiedProfiles.Count > 0)
        {
            masterProfile = candidateIdentifiedProfiles.First()!;

            // If multiple different identified profiles matched (e.g. phone matched one, email matched another)
            for (int i = 1; i < candidateIdentifiedProfiles.Count; i++)
            {
                var other = candidateIdentifiedProfiles[i]!;
                if (other.Id != masterProfile.Id)
                {
                    masterProfile = await MergeCustomerProfilesAsync(tenantId, masterProfile.Id, other.Id, req.PerformedBy ?? "StitchAutoMerge", ct);
                    wasMerged = true;
                    mergedFromId = other.Id;
                }
            }

            // If there is also an anonymous/separate cookie profile that is distinct from masterProfile
            if (cookieProfile != null && cookieProfile.Id != masterProfile.Id)
            {
                masterProfile = await MergeCustomerProfilesAsync(tenantId, masterProfile.Id, cookieProfile.Id, req.PerformedBy ?? "StitchCookieMerge", ct);
                wasMerged = true;
                mergedFromId = cookieProfile.Id;
            }
        }
        else if (cookieProfile != null)
        {
            // Anonymous cookie profile promoted to known profile
            masterProfile = cookieProfile;
            if (!string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(masterProfile.PrimaryPhone))
                masterProfile.PrimaryPhone = normalizedPhone;
            if (!string.IsNullOrWhiteSpace(normalizedEmail) && string.IsNullOrWhiteSpace(masterProfile.PrimaryEmail))
                masterProfile.PrimaryEmail = normalizedEmail;
            if (!string.IsNullOrWhiteSpace(req.Name) && (string.IsNullOrWhiteSpace(masterProfile.PrimaryName) || masterProfile.PrimaryName == "Anonymous Visitor"))
                masterProfile.PrimaryName = req.Name.Trim();
        }
        else
        {
            // Brand new profile
            masterProfile = new UnifiedCustomerProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PrimaryName = !string.IsNullOrWhiteSpace(req.Name) ? req.Name.Trim() : (!string.IsNullOrWhiteSpace(normalizedPhone) ? normalizedPhone : (!string.IsNullOrWhiteSpace(normalizedEmail) ? normalizedEmail : "Unified Customer")),
                PrimaryPhone = normalizedPhone ?? string.Empty,
                PrimaryEmail = normalizedEmail,
                Company = req.Company?.Trim(),
                LifecycleStage = CustomerLifecycleStage.Lead,
                TotalOrdersCount = 0,
                TotalSpent = 0m,
                DeliverySuccessRate = 100.0,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };

            await _db.UnifiedCustomerProfiles.AddAsync(masterProfile, ct);
            await _db.SaveChangesAsync(ct);
        }

        // Update profile fields if newly supplied
        if (!string.IsNullOrWhiteSpace(req.Name) && (string.IsNullOrWhiteSpace(masterProfile.PrimaryName) || masterProfile.PrimaryName.StartsWith("+") || masterProfile.PrimaryName == "Unified Customer" || masterProfile.PrimaryName == "Anonymous Visitor"))
        {
            masterProfile.PrimaryName = req.Name.Trim();
        }
        if (!string.IsNullOrWhiteSpace(normalizedEmail) && string.IsNullOrWhiteSpace(masterProfile.PrimaryEmail))
        {
            masterProfile.PrimaryEmail = normalizedEmail;
        }
        if (!string.IsNullOrWhiteSpace(normalizedPhone) && string.IsNullOrWhiteSpace(masterProfile.PrimaryPhone))
        {
            masterProfile.PrimaryPhone = normalizedPhone;
        }
        if (!string.IsNullOrWhiteSpace(req.Company) && string.IsNullOrWhiteSpace(masterProfile.Company))
        {
            masterProfile.Company = req.Company.Trim();
        }
        masterProfile.LastSeenAtUtc = DateTime.UtcNow;

        // Link all presented identifiers deterministically
        var identifiersToLink = new List<(CustomerChannelIdentityType Type, string Value)>();

        if (!string.IsNullOrWhiteSpace(normalizedPhone))
            identifiersToLink.Add((CustomerChannelIdentityType.PhoneE164, normalizedPhone));

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
            identifiersToLink.Add((CustomerChannelIdentityType.Email, normalizedEmail));

        if (!string.IsNullOrWhiteSpace(cleanCookieId))
            identifiersToLink.Add((CustomerChannelIdentityType.WebCookieId, cleanCookieId));

        if (!string.IsNullOrWhiteSpace(cleanWhatsApp))
            identifiersToLink.Add((CustomerChannelIdentityType.WhatsAppId, cleanWhatsApp));

        if (!string.IsNullOrWhiteSpace(cleanSocial))
        {
            var sType = req.SocialType ?? CustomerChannelIdentityType.InstagramHandle;
            identifiersToLink.Add((sType, cleanSocial));
        }

        int newlyLinkedCount = 0;
        foreach (var (type, val) in identifiersToLink)
        {
            var exists = await _db.CustomerIdentityLinks
                .AnyAsync(l => l.TenantId == tenantId && l.UnifiedCustomerId == masterProfile.Id && l.IdentityType == type && l.IdentityValue == val, ct);

            if (!exists)
            {
                var newLink = new CustomerIdentityLink
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UnifiedCustomerId = masterProfile.Id,
                    IdentityType = type,
                    IdentityValue = val,
                    LinkedAtUtc = DateTime.UtcNow
                };
                await _db.CustomerIdentityLinks.AddAsync(newLink, ct);
                newlyLinkedCount++;
            }
        }

        // Record Stitch Timeline Event
        var stitchEvent = new CustomerTimelineEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UnifiedCustomerId = masterProfile.Id,
            EventType = wasMerged ? "ProfileMerged" : "IdentityStitched",
            Summary = wasMerged
                ? $"Unified identity merge: stitched into master profile {masterProfile.PrimaryName} ({masterProfile.PrimaryPhone}). Merged duplicate {mergedFromId}."
                : $"Identity stitched: linked {newlyLinkedCount} new channel identifier(s).",
            PayloadJson = JsonSerializer.Serialize(new
            {
                req.Phone,
                req.Email,
                req.CookieId,
                req.WhatsAppJid,
                req.SocialHandle,
                wasMerged,
                mergedFromId
            }),
            OccurredAtUtc = DateTime.UtcNow
        };
        await _db.CustomerTimelineEvents.AddAsync(stitchEvent, ct);

        await _db.SaveChangesAsync(ct);

        // Fetch all linked channels for badge response
        var allLinks = await _db.CustomerIdentityLinks
            .Where(l => l.TenantId == tenantId && l.UnifiedCustomerId == masterProfile.Id)
            .OrderBy(l => l.LinkedAtUtc)
            .ToListAsync(ct);

        var badges = allLinks.Select(l => new LinkedChannelBadgeDto(
            l.IdentityType,
            GetChannelDisplayName(l.IdentityType),
            l.IdentityValue,
            l.LinkedAtUtc,
            GetChannelIcon(l.IdentityType)
        )).ToList();

        return new IdentityStitchResultDto(
            Success: true,
            UnifiedCustomerId: masterProfile.Id,
            PrimaryName: masterProfile.PrimaryName,
            PrimaryPhone: masterProfile.PrimaryPhone,
            PrimaryEmail: masterProfile.PrimaryEmail,
            LifecycleStage: masterProfile.LifecycleStage,
            WasMerged: wasMerged,
            MergedFromCustomerId: mergedFromId,
            LinkedChannels: badges,
            Message: wasMerged
                ? $"Successfully merged profile {mergedFromId} into master profile {masterProfile.Id}"
                : $"Stitched {newlyLinkedCount} new channel identities to customer {masterProfile.Id}"
        );
    }

    public async Task<UnifiedCustomerProfile> MergeCustomerProfilesAsync(
        Guid tenantId,
        Guid primaryCustomerId,
        Guid duplicateCustomerId,
        string performedBy = "System",
        CancellationToken ct = default)
    {
        if (primaryCustomerId == duplicateCustomerId)
        {
            return await _db.UnifiedCustomerProfiles
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == primaryCustomerId, ct)
                ?? throw new KeyNotFoundException($"Customer {primaryCustomerId} not found.");
        }

        var primary = await _db.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == primaryCustomerId, ct)
            ?? throw new KeyNotFoundException($"Primary customer {primaryCustomerId} not found.");

        var duplicate = await _db.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == duplicateCustomerId, ct)
            ?? throw new KeyNotFoundException($"Duplicate customer {duplicateCustomerId} not found.");

        // 1. Move identity links
        var dupLinks = await _db.CustomerIdentityLinks
            .Where(l => l.TenantId == tenantId && l.UnifiedCustomerId == duplicateCustomerId)
            .ToListAsync(ct);

        foreach (var link in dupLinks)
        {
            var alreadyInPrimary = await _db.CustomerIdentityLinks
                .AnyAsync(l => l.TenantId == tenantId && l.UnifiedCustomerId == primaryCustomerId && l.IdentityType == link.IdentityType && l.IdentityValue == link.IdentityValue, ct);

            if (!alreadyInPrimary)
            {
                link.UnifiedCustomerId = primaryCustomerId;
            }
            else
            {
                _db.CustomerIdentityLinks.Remove(link);
            }
        }

        // 2. Move timeline events
        var dupTimeline = await _db.CustomerTimelineEvents
            .Where(t => t.TenantId == tenantId && t.UnifiedCustomerId == duplicateCustomerId)
            .ToListAsync(ct);

        foreach (var ev in dupTimeline)
        {
            ev.UnifiedCustomerId = primaryCustomerId;
        }

        // 3. Move attribution touchpoints
        var dupTouchpoints = await _db.AttributionTouchpoints
            .Where(t => t.TenantId == tenantId && t.ContactId == duplicateCustomerId)
            .ToListAsync(ct);

        foreach (var tp in dupTouchpoints)
        {
            tp.ContactId = primaryCustomerId;
        }

        // 4. Aggregate metrics
        primary.TotalOrdersCount += duplicate.TotalOrdersCount;
        primary.TotalSpent += duplicate.TotalSpent;
        primary.DeliverySuccessRate = Math.Round((primary.DeliverySuccessRate + duplicate.DeliverySuccessRate) / 2.0, 1);

        if (duplicate.LastSeenAtUtc > primary.LastSeenAtUtc)
            primary.LastSeenAtUtc = duplicate.LastSeenAtUtc;

        if (string.IsNullOrWhiteSpace(primary.PrimaryEmail) && !string.IsNullOrWhiteSpace(duplicate.PrimaryEmail))
            primary.PrimaryEmail = duplicate.PrimaryEmail;

        if (string.IsNullOrWhiteSpace(primary.Company) && !string.IsNullOrWhiteSpace(duplicate.Company))
            primary.Company = duplicate.Company;

        // 5. Remove duplicate profile
        _db.UnifiedCustomerProfiles.Remove(duplicate);

        // 6. Record merge event
        var mergeEvent = new CustomerTimelineEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UnifiedCustomerId = primaryCustomerId,
            EventType = "ProfileMerged",
            Summary = $"Merged duplicate profile {duplicateCustomerId} ({duplicate.PrimaryName}) performed by {performedBy}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                primaryId = primaryCustomerId,
                duplicateId = duplicateCustomerId,
                performedBy,
                timestamp = DateTime.UtcNow
            }),
            OccurredAtUtc = DateTime.UtcNow
        };
        await _db.CustomerTimelineEvents.AddAsync(mergeEvent, ct);

        await _db.SaveChangesAsync(ct);
        return primary;
    }

    #endregion

    #region Touchpoints & Attribution Engine

    public async Task<AttributionTouchpoint> RecordTouchpointAsync(RecordTouchpointRequest req, CancellationToken ct = default)
    {
        var touchpoint = new AttributionTouchpoint
        {
            Id = Guid.NewGuid(),
            TenantId = req.TenantId,
            ContactId = req.ContactId,
            DealId = req.DealId,
            Channel = string.IsNullOrWhiteSpace(req.Channel) ? "Direct" : req.Channel.Trim(),
            CampaignName = string.IsNullOrWhiteSpace(req.CampaignName) ? "DefaultCampaign" : req.CampaignName.Trim(),
            Stage = req.Stage,
            Cost = Math.Max(0m, req.Cost),
            UtmSource = req.UtmSource?.Trim(),
            UtmMedium = req.UtmMedium?.Trim(),
            UtmCampaign = req.UtmCampaign?.Trim(),
            OccurredAtUtc = req.OccurredAtUtc ?? DateTime.UtcNow
        };

        await _db.AttributionTouchpoints.AddAsync(touchpoint, ct);

        // Also record a timeline event for this touchpoint
        var timelineEvent = new CustomerTimelineEvent
        {
            Id = Guid.NewGuid(),
            TenantId = req.TenantId,
            UnifiedCustomerId = req.ContactId,
            EventType = $"Touchpoint_{req.Stage}",
            Summary = $"Attribution touchpoint recorded via {touchpoint.Channel} ({touchpoint.CampaignName}) - Cost: ${touchpoint.Cost:N2}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                touchpoint.Channel,
                touchpoint.CampaignName,
                touchpoint.Stage,
                touchpoint.Cost,
                touchpoint.UtmSource,
                touchpoint.UtmCampaign
            }),
            OccurredAtUtc = touchpoint.OccurredAtUtc
        };
        await _db.CustomerTimelineEvents.AddAsync(timelineEvent, ct);

        await _db.SaveChangesAsync(ct);
        return touchpoint;
    }

    public async Task<AttributionComparisonDto> CompareAttributionModelsAsync(
        Guid tenantId,
        Guid? customerId = null,
        Guid? dealId = null,
        CancellationToken ct = default)
    {
        // 1. Fetch touchpoints
        var q = _db.AttributionTouchpoints.Where(t => t.TenantId == tenantId);

        if (customerId.HasValue && customerId.Value != Guid.Empty)
        {
            q = q.Where(t => t.ContactId == customerId.Value);
        }

        if (dealId.HasValue && dealId.Value != Guid.Empty)
        {
            q = q.Where(t => t.DealId == dealId.Value);
        }

        var touchpoints = await q
            .OrderBy(t => t.OccurredAtUtc)
            .ToListAsync(ct);

        // Determine total revenue to attribute
        decimal totalRevenue = 0m;
        if (dealId.HasValue && dealId.Value != Guid.Empty)
        {
            var deal = await _db.Deals.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == dealId.Value, ct);
            if (deal != null) totalRevenue = deal.Amount;
        }

        if (totalRevenue == 0m && customerId.HasValue && customerId.Value != Guid.Empty)
        {
            var profile = await _db.UnifiedCustomerProfiles.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == customerId.Value, ct);
            if (profile != null) totalRevenue = profile.TotalSpent;
        }

        // If total revenue is still 0 (e.g. prospective deal or synthetic comparison), fallback to standard base or deal amounts
        if (totalRevenue == 0m)
        {
            var totalDealsAmount = await _db.Deals
                .Where(d => d.TenantId == tenantId)
                .SumAsync(d => (decimal?)d.Amount, ct) ?? 0m;

            totalRevenue = totalDealsAmount > 0 ? totalDealsAmount : 1000m;
        }

        // 2. Compute 5 models
        var firstTouch = ComputeFirstTouch(touchpoints, totalRevenue);
        var lastTouch = ComputeLastTouch(touchpoints, totalRevenue);
        var linear = ComputeLinear(touchpoints, totalRevenue);
        var uShaped = ComputeUShaped(touchpoints, totalRevenue);
        var timeDecay = ComputeTimeDecay(touchpoints, totalRevenue);

        return new AttributionComparisonDto(
            TenantId: tenantId,
            CustomerId: customerId,
            TotalRevenue: totalRevenue,
            TotalTouchpointsCount: touchpoints.Count,
            FirstTouch: firstTouch,
            LastTouch: lastTouch,
            Linear: linear,
            UShaped: uShaped,
            TimeDecay: timeDecay
        );
    }

    public async Task<List<CampaignRoasBreakdownDto>> GetCampaignRoasBreakdownAsync(
        Guid tenantId,
        AttributionModelType model = AttributionModelType.TimeDecay,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        var q = _db.AttributionTouchpoints.Where(t => t.TenantId == tenantId);

        if (fromUtc.HasValue) q = q.Where(t => t.OccurredAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) q = q.Where(t => t.OccurredAtUtc <= toUtc.Value);

        var touchpoints = await q.OrderBy(t => t.OccurredAtUtc).ToListAsync(ct);

        if (touchpoints.Count == 0)
        {
            return new List<CampaignRoasBreakdownDto>();
        }

        // Group touchpoints by contact to run customer-level attribution
        var contactGroups = touchpoints.GroupBy(t => t.ContactId).ToList();

        // Get total spend per contact from UnifiedCustomerProfile
        var contactIds = contactGroups.Select(g => g.Key).ToList();
        var profiles = await _db.UnifiedCustomerProfiles
            .Where(p => p.TenantId == tenantId && contactIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.TotalSpent, ct);

        // Map to store campaign aggregates
        var campaignStats = new Dictionary<string, (string Channel, decimal TotalCost, decimal AttributedRevenue, HashSet<Guid> Customers, int TouchpointCount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in contactGroups)
        {
            var contactTps = group.OrderBy(t => t.OccurredAtUtc).ToList();
            var customerSpend = profiles.GetValueOrDefault(group.Key, 0m);

            if (customerSpend == 0m)
            {
                // Fallback estimate if customer hasn't purchased yet
                customerSpend = 250m;
            }

            AttributionModelResultDto modelResult = model switch
            {
                AttributionModelType.FirstTouch => ComputeFirstTouch(contactTps, customerSpend),
                AttributionModelType.LastTouch => ComputeLastTouch(contactTps, customerSpend),
                AttributionModelType.Linear => ComputeLinear(contactTps, customerSpend),
                AttributionModelType.UShaped => ComputeUShaped(contactTps, customerSpend),
                _ => ComputeTimeDecay(contactTps, customerSpend)
            };

            foreach (var share in modelResult.Shares)
            {
                var campKey = string.IsNullOrWhiteSpace(share.CampaignName) ? "Direct / None" : share.CampaignName;
                if (!campaignStats.TryGetValue(campKey, out var current))
                {
                    current = (share.Channel, 0m, 0m, new HashSet<Guid>(), 0);
                }

                current.TotalCost += share.Cost;
                current.AttributedRevenue += share.AttributedRevenue;
                current.Customers.Add(group.Key);
                current.TouchpointCount++;
                campaignStats[campKey] = current;
            }
        }

        return campaignStats.Select(kvp =>
        {
            var camp = kvp.Key;
            var data = kvp.Value;
            var custCount = data.Customers.Count;
            var roas = data.TotalCost > 0 ? Math.Round(data.AttributedRevenue / data.TotalCost, 2) : (data.AttributedRevenue > 0 ? 99.9m : 0m);
            var cac = custCount > 0 ? Math.Round(data.TotalCost / custCount, 2) : 0m;

            return new CampaignRoasBreakdownDto(
                CampaignName: camp,
                Channel: data.Channel,
                TotalCost: Math.Round(data.TotalCost, 2),
                AttributedRevenue: Math.Round(data.AttributedRevenue, 2),
                TrueRoas: roas,
                Cac: cac,
                CustomerCount: custCount,
                TouchpointCount: data.TouchpointCount
            );
        }).OrderByDescending(c => c.AttributedRevenue).ToList();
    }

    #endregion

    #region Model Calculation Math

    // 1. First-Touch (100% to initial touchpoint)
    public static AttributionModelResultDto ComputeFirstTouch(List<AttributionTouchpoint> touchpoints, decimal totalRevenue)
    {
        var shares = new List<AttributedTouchpointShareDto>();
        var channelRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var campaignRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (touchpoints.Count == 0)
        {
            return new AttributionModelResultDto(
                AttributionModelType.FirstTouch,
                "First-Touch",
                "100% credit to the initial top-of-funnel discovery touchpoint.",
                shares, channelRev, campaignRev);
        }

        var first = touchpoints.First();
        shares.Add(new AttributedTouchpointShareDto(
            first.Id,
            first.Channel,
            first.CampaignName,
            first.Stage,
            100m,
            totalRevenue,
            first.Cost,
            first.OccurredAtUtc
        ));

        channelRev[first.Channel] = totalRevenue;
        campaignRev[first.CampaignName] = totalRevenue;

        // Zero share for other touches
        foreach (var tp in touchpoints.Skip(1))
        {
            shares.Add(new AttributedTouchpointShareDto(
                tp.Id,
                tp.Channel,
                tp.CampaignName,
                tp.Stage,
                0m,
                0m,
                tp.Cost,
                tp.OccurredAtUtc
            ));
        }

        return new AttributionModelResultDto(
            AttributionModelType.FirstTouch,
            "First-Touch",
            "100% credit to the initial top-of-funnel discovery touchpoint.",
            shares, channelRev, campaignRev);
    }

    // 2. Last-Touch (100% to final touchpoint prior to conversion)
    public static AttributionModelResultDto ComputeLastTouch(List<AttributionTouchpoint> touchpoints, decimal totalRevenue)
    {
        var shares = new List<AttributedTouchpointShareDto>();
        var channelRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var campaignRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (touchpoints.Count == 0)
        {
            return new AttributionModelResultDto(
                AttributionModelType.LastTouch,
                "Last-Touch",
                "100% credit to the final touchpoint prior to deal conversion.",
                shares, channelRev, campaignRev);
        }

        var last = touchpoints.Last();

        foreach (var tp in touchpoints.Take(touchpoints.Count - 1))
        {
            shares.Add(new AttributedTouchpointShareDto(
                tp.Id,
                tp.Channel,
                tp.CampaignName,
                tp.Stage,
                0m,
                0m,
                tp.Cost,
                tp.OccurredAtUtc
            ));
        }

        shares.Add(new AttributedTouchpointShareDto(
            last.Id,
            last.Channel,
            last.CampaignName,
            last.Stage,
            100m,
            totalRevenue,
            last.Cost,
            last.OccurredAtUtc
        ));

        channelRev[last.Channel] = totalRevenue;
        campaignRev[last.CampaignName] = totalRevenue;

        return new AttributionModelResultDto(
            AttributionModelType.LastTouch,
            "Last-Touch",
            "100% credit to the final touchpoint prior to deal conversion.",
            shares, channelRev, campaignRev);
    }

    // 3. Linear (Equal 1/N distribution across all touchpoints)
    public static AttributionModelResultDto ComputeLinear(List<AttributionTouchpoint> touchpoints, decimal totalRevenue)
    {
        var shares = new List<AttributedTouchpointShareDto>();
        var channelRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var campaignRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (touchpoints.Count == 0)
        {
            return new AttributionModelResultDto(
                AttributionModelType.Linear,
                "Linear",
                "Equal 1/N distribution across all customer journey touchpoints.",
                shares, channelRev, campaignRev);
        }

        int count = touchpoints.Count;
        decimal weightPercent = Math.Round(100m / count, 2);
        decimal perTouchRevenue = Math.Round(totalRevenue / count, 2);

        // Adjust rounding on the last element to guarantee exact sum
        decimal allocatedSum = 0m;

        for (int i = 0; i < count; i++)
        {
            var tp = touchpoints[i];
            decimal rev = (i == count - 1) ? (totalRevenue - allocatedSum) : perTouchRevenue;
            allocatedSum += rev;

            shares.Add(new AttributedTouchpointShareDto(
                tp.Id,
                tp.Channel,
                tp.CampaignName,
                tp.Stage,
                weightPercent,
                rev,
                tp.Cost,
                tp.OccurredAtUtc
            ));

            channelRev[tp.Channel] = channelRev.GetValueOrDefault(tp.Channel, 0m) + rev;
            campaignRev[tp.CampaignName] = campaignRev.GetValueOrDefault(tp.CampaignName, 0m) + rev;
        }

        return new AttributionModelResultDto(
            AttributionModelType.Linear,
            "Linear",
            "Equal 1/N distribution across all customer journey touchpoints.",
            shares, channelRev, campaignRev);
    }

    // 4. U-Shaped (40% First-Touch, 40% Lead/Conversion, 20% distributed across intermediate)
    public static AttributionModelResultDto ComputeUShaped(List<AttributionTouchpoint> touchpoints, decimal totalRevenue)
    {
        var shares = new List<AttributedTouchpointShareDto>();
        var channelRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var campaignRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (touchpoints.Count == 0)
        {
            return new AttributionModelResultDto(
                AttributionModelType.UShaped,
                "U-Shaped",
                "40% First-Touch, 40% Lead/Conversion Touch, and 20% distributed across intermediate touches.",
                shares, channelRev, campaignRev);
        }

        if (touchpoints.Count == 1)
        {
            var single = touchpoints[0];
            shares.Add(new AttributedTouchpointShareDto(
                single.Id, single.Channel, single.CampaignName, single.Stage, 100m, totalRevenue, single.Cost, single.OccurredAtUtc));
            channelRev[single.Channel] = totalRevenue;
            campaignRev[single.CampaignName] = totalRevenue;
        }
        else if (touchpoints.Count == 2)
        {
            var first = touchpoints[0];
            var second = touchpoints[1];
            decimal halfRev = Math.Round(totalRevenue * 0.50m, 2);
            decimal remainderRev = totalRevenue - halfRev;

            shares.Add(new AttributedTouchpointShareDto(
                first.Id, first.Channel, first.CampaignName, first.Stage, 50m, halfRev, first.Cost, first.OccurredAtUtc));
            shares.Add(new AttributedTouchpointShareDto(
                second.Id, second.Channel, second.CampaignName, second.Stage, 50m, remainderRev, second.Cost, second.OccurredAtUtc));

            channelRev[first.Channel] = channelRev.GetValueOrDefault(first.Channel, 0m) + halfRev;
            campaignRev[first.CampaignName] = campaignRev.GetValueOrDefault(first.CampaignName, 0m) + halfRev;

            channelRev[second.Channel] = channelRev.GetValueOrDefault(second.Channel, 0m) + remainderRev;
            campaignRev[second.CampaignName] = campaignRev.GetValueOrDefault(second.CampaignName, 0m) + remainderRev;
        }
        else
        {
            // 3+ touchpoints: 40% First, 40% Last, 20% split among intermediate
            var first = touchpoints.First();
            var last = touchpoints.Last();
            var middle = touchpoints.Skip(1).Take(touchpoints.Count - 2).ToList();

            decimal firstRev = Math.Round(totalRevenue * 0.40m, 2);
            decimal lastRev = Math.Round(totalRevenue * 0.40m, 2);
            decimal middleTotalRev = totalRevenue - firstRev - lastRev;
            decimal middlePerRev = Math.Round(middleTotalRev / middle.Count, 2);
            decimal middleWeight = Math.Round(20m / middle.Count, 2);

            shares.Add(new AttributedTouchpointShareDto(
                first.Id, first.Channel, first.CampaignName, first.Stage, 40m, firstRev, first.Cost, first.OccurredAtUtc));
            channelRev[first.Channel] = channelRev.GetValueOrDefault(first.Channel, 0m) + firstRev;
            campaignRev[first.CampaignName] = campaignRev.GetValueOrDefault(first.CampaignName, 0m) + firstRev;

            decimal allocatedMiddle = 0m;
            for (int i = 0; i < middle.Count; i++)
            {
                var tp = middle[i];
                decimal rev = (i == middle.Count - 1) ? (middleTotalRev - allocatedMiddle) : middlePerRev;
                allocatedMiddle += rev;

                shares.Add(new AttributedTouchpointShareDto(
                    tp.Id, tp.Channel, tp.CampaignName, tp.Stage, middleWeight, rev, tp.Cost, tp.OccurredAtUtc));
                channelRev[tp.Channel] = channelRev.GetValueOrDefault(tp.Channel, 0m) + rev;
                campaignRev[tp.CampaignName] = campaignRev.GetValueOrDefault(tp.CampaignName, 0m) + rev;
            }

            shares.Add(new AttributedTouchpointShareDto(
                last.Id, last.Channel, last.CampaignName, last.Stage, 40m, lastRev, last.Cost, last.OccurredAtUtc));
            channelRev[last.Channel] = channelRev.GetValueOrDefault(last.Channel, 0m) + lastRev;
            campaignRev[last.CampaignName] = campaignRev.GetValueOrDefault(last.CampaignName, 0m) + lastRev;
        }

        return new AttributionModelResultDto(
            AttributionModelType.UShaped,
            "U-Shaped",
            "40% First-Touch, 40% Lead/Conversion Touch, and 20% distributed across intermediate touches.",
            shares, channelRev, campaignRev);
    }

    // 5. Time-Decay (Exponential decay with 7-day half-life: 2^(-deltaDays / 7))
    public static AttributionModelResultDto ComputeTimeDecay(List<AttributionTouchpoint> touchpoints, decimal totalRevenue)
    {
        var shares = new List<AttributedTouchpointShareDto>();
        var channelRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var campaignRev = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (touchpoints.Count == 0)
        {
            return new AttributionModelResultDto(
                AttributionModelType.TimeDecay,
                "Time-Decay",
                "Exponential decay weighting (half-life τ = 7 days) prioritizing recent touchpoints.",
                shares, channelRev, campaignRev);
        }

        var latestTime = touchpoints.Last().OccurredAtUtc;
        const double halfLifeDays = 7.0;

        var rawWeights = touchpoints.Select(tp =>
        {
            var deltaDays = Math.Max(0.0, (latestTime - tp.OccurredAtUtc).TotalDays);
            return Math.Pow(2.0, -deltaDays / halfLifeDays);
        }).ToList();

        var totalWeight = rawWeights.Sum();
        if (totalWeight <= 0) totalWeight = 1.0;

        decimal allocatedSum = 0m;
        for (int i = 0; i < touchpoints.Count; i++)
        {
            var tp = touchpoints[i];
            decimal fraction = (decimal)(rawWeights[i] / totalWeight);
            decimal weightPercent = Math.Round(fraction * 100m, 2);

            decimal rev = (i == touchpoints.Count - 1)
                ? (totalRevenue - allocatedSum)
                : Math.Round(totalRevenue * fraction, 2);
            allocatedSum += rev;

            shares.Add(new AttributedTouchpointShareDto(
                tp.Id,
                tp.Channel,
                tp.CampaignName,
                tp.Stage,
                weightPercent,
                rev,
                tp.Cost,
                tp.OccurredAtUtc
            ));

            channelRev[tp.Channel] = channelRev.GetValueOrDefault(tp.Channel, 0m) + rev;
            campaignRev[tp.CampaignName] = campaignRev.GetValueOrDefault(tp.CampaignName, 0m) + rev;
        }

        return new AttributionModelResultDto(
            AttributionModelType.TimeDecay,
            "Time-Decay",
            "Exponential decay weighting (half-life τ = 7 days) prioritizing recent touchpoints.",
            shares, channelRev, campaignRev);
    }

    #endregion
}
