using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM;

public interface ICustomerIdentityGraphService
{
    Task<UnifiedCustomerProfile> ResolveCustomerAsync(Guid tenantId, string name, string phone, string? email = null, CustomerChannelIdentityType? channelType = null, string? channelValue = null);
    Task<CustomerTimelineEvent> RecordTimelineEventAsync(Guid tenantId, Guid customerId, string eventType, string summary, string payloadJson = "{}");
    Task<Customer360Detail> GetCustomer360Async(Guid tenantId, Guid customerId);
    Task<UnifiedCustomerProfile> MergeCustomerProfilesAsync(Guid tenantId, Guid primaryCustomerId, Guid duplicateCustomerId, string performedBy = "Admin");
}

public record Customer360Detail(UnifiedCustomerProfile Profile, List<CustomerIdentityLink> LinkedIdentities, List<CustomerTimelineEvent> Timeline);

public class CustomerIdentityGraphService : ICustomerIdentityGraphService
{
    private readonly SGrowingDbContext _dbContext;

    public CustomerIdentityGraphService(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string NormalizePhoneToE164(string phone)
    {
        var digits = Regex.Replace(phone, @"[^\d+]", "");
        if (digits.StartsWith("+"))
            return digits;

        // Common defaults for North African formats if no plus
        if (digits.StartsWith("216") || digits.StartsWith("213") || digits.StartsWith("212") || digits.StartsWith("20"))
            return "+" + digits;

        if (digits.Length == 8) // Tunisia
            return "+216" + digits;
        if (digits.Length == 11 && digits.StartsWith("01")) // Egypt
            return "+20" + digits.Substring(1);

        return "+" + digits;
    }

    public async Task<UnifiedCustomerProfile> ResolveCustomerAsync(Guid tenantId, string name, string phone, string? email = null, CustomerChannelIdentityType? channelType = null, string? channelValue = null)
    {
        var normalizedPhone = NormalizePhoneToE164(phone);
        var normalizedEmail = email?.Trim().ToLowerInvariant();

        // 1. Try finding by Phone
        var customer = await _dbContext.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PrimaryPhone == normalizedPhone);

        // 2. Try finding by Email if not found
        if (customer == null && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            customer = await _dbContext.UnifiedCustomerProfiles
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PrimaryEmail == normalizedEmail);
        }

        // 3. Try finding by Linked Identity if provided
        if (customer == null && channelType.HasValue && !string.IsNullOrWhiteSpace(channelValue))
        {
            var link = await _dbContext.CustomerIdentityLinks
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.IdentityType == channelType.Value && l.IdentityValue == channelValue);

            if (link != null)
            {
                customer = await _dbContext.UnifiedCustomerProfiles
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == link.UnifiedCustomerId);
            }
        }

        if (customer == null)
        {
            // Create new Unified Customer Profile
            customer = new UnifiedCustomerProfile
            {
                TenantId = tenantId,
                PrimaryName = name.Trim(),
                PrimaryPhone = normalizedPhone,
                PrimaryEmail = normalizedEmail,
                LifecycleStage = CustomerLifecycleStage.Lead,
                TotalOrdersCount = 0,
                TotalSpent = 0,
                DeliverySuccessRate = 100.0,
                CreatedAtUtc = DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow
            };
            _dbContext.UnifiedCustomerProfiles.Add(customer);
            await _dbContext.SaveChangesAsync();

            // Link primary phone
            _dbContext.CustomerIdentityLinks.Add(new CustomerIdentityLink
            {
                TenantId = tenantId,
                UnifiedCustomerId = customer.Id,
                IdentityType = CustomerChannelIdentityType.PhoneE164,
                IdentityValue = normalizedPhone,
                LinkedAtUtc = DateTime.UtcNow
            });

            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                _dbContext.CustomerIdentityLinks.Add(new CustomerIdentityLink
                {
                    TenantId = tenantId,
                    UnifiedCustomerId = customer.Id,
                    IdentityType = CustomerChannelIdentityType.Email,
                    IdentityValue = normalizedEmail,
                    LinkedAtUtc = DateTime.UtcNow
                });
            }
        }
        else
        {
            customer.LastSeenAtUtc = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(customer.PrimaryEmail) && !string.IsNullOrWhiteSpace(normalizedEmail))
            {
                customer.PrimaryEmail = normalizedEmail;
            }
        }

        // Link channel identity if provided and not already linked
        if (channelType.HasValue && !string.IsNullOrWhiteSpace(channelValue))
        {
            var existingLink = await _dbContext.CustomerIdentityLinks
                .AnyAsync(l => l.TenantId == tenantId && l.UnifiedCustomerId == customer.Id && l.IdentityType == channelType.Value && l.IdentityValue == channelValue);

            if (!existingLink)
            {
                _dbContext.CustomerIdentityLinks.Add(new CustomerIdentityLink
                {
                    TenantId = tenantId,
                    UnifiedCustomerId = customer.Id,
                    IdentityType = channelType.Value,
                    IdentityValue = channelValue,
                    LinkedAtUtc = DateTime.UtcNow
                });

                await RecordTimelineEventAsync(tenantId, customer.Id, "IdentityLinked", $"Linked {channelType.Value} identity: {channelValue}");
            }
        }

        await _dbContext.SaveChangesAsync();
        return customer;
    }

    public async Task<CustomerTimelineEvent> RecordTimelineEventAsync(Guid tenantId, Guid customerId, string eventType, string summary, string payloadJson = "{}")
    {
        var timelineEvent = new CustomerTimelineEvent
        {
            TenantId = tenantId,
            UnifiedCustomerId = customerId,
            EventType = eventType,
            Summary = summary,
            PayloadJson = payloadJson,
            OccurredAtUtc = DateTime.UtcNow
        };

        _dbContext.CustomerTimelineEvents.Add(timelineEvent);
        await _dbContext.SaveChangesAsync();
        return timelineEvent;
    }

    public async Task<Customer360Detail> GetCustomer360Async(Guid tenantId, Guid customerId)
    {
        var profile = await _dbContext.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == customerId)
            ?? throw new InvalidOperationException($"Customer {customerId} not found.");

        var links = await _dbContext.CustomerIdentityLinks
            .Where(l => l.TenantId == tenantId && l.UnifiedCustomerId == customerId)
            .ToListAsync();

        var timeline = await _dbContext.CustomerTimelineEvents
            .Where(t => t.TenantId == tenantId && t.UnifiedCustomerId == customerId)
            .OrderByDescending(t => t.OccurredAtUtc)
            .ToListAsync();

        return new Customer360Detail(profile, links, timeline);
    }

    public async Task<UnifiedCustomerProfile> MergeCustomerProfilesAsync(Guid tenantId, Guid primaryCustomerId, Guid duplicateCustomerId, string performedBy = "Admin")
    {
        var primary = await _dbContext.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == primaryCustomerId)
            ?? throw new InvalidOperationException($"Primary customer {primaryCustomerId} not found.");

        var duplicate = await _dbContext.UnifiedCustomerProfiles
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == duplicateCustomerId)
            ?? throw new InvalidOperationException($"Duplicate customer {duplicateCustomerId} not found.");

        // Move all identity links from duplicate to primary
        var duplicateLinks = await _dbContext.CustomerIdentityLinks
            .Where(l => l.TenantId == tenantId && l.UnifiedCustomerId == duplicateCustomerId)
            .ToListAsync();

        foreach (var link in duplicateLinks)
        {
            var alreadyExistsInPrimary = await _dbContext.CustomerIdentityLinks
                .AnyAsync(l => l.TenantId == tenantId && l.UnifiedCustomerId == primaryCustomerId && l.IdentityType == link.IdentityType && l.IdentityValue == link.IdentityValue);

            if (!alreadyExistsInPrimary)
            {
                link.UnifiedCustomerId = primaryCustomerId;
            }
            else
            {
                _dbContext.CustomerIdentityLinks.Remove(link);
            }
        }

        // Move all timeline events
        var duplicateEvents = await _dbContext.CustomerTimelineEvents
            .Where(t => t.TenantId == tenantId && t.UnifiedCustomerId == duplicateCustomerId)
            .ToListAsync();

        foreach (var ev in duplicateEvents)
        {
            ev.UnifiedCustomerId = primaryCustomerId;
        }

        // Aggregate orders & spend
        primary.TotalOrdersCount += duplicate.TotalOrdersCount;
        primary.TotalSpent += duplicate.TotalSpent;
        primary.DeliverySuccessRate = Math.Round((primary.DeliverySuccessRate + duplicate.DeliverySuccessRate) / 2.0, 1);

        // Record merge event
        await RecordTimelineEventAsync(tenantId, primaryCustomerId, "ProfileMerged", $"Merged duplicate profile {duplicateCustomerId} ({duplicate.PrimaryName}) performed by {performedBy}");

        // Remove duplicate profile
        _dbContext.UnifiedCustomerProfiles.Remove(duplicate);

        await _dbContext.SaveChangesAsync();
        return primary;
    }
}
