using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Automation;

public class QuietHoursCheckResult
{
    public bool CanSendNow { get; set; }
    public bool InQuietHours { get; set; }
    public DateTime? NextAvailableWindowUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class QuietHoursGuardService
{
    private readonly SGrowingDbContext _db;

    public QuietHoursGuardService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<QuietHoursCheckResult> EvaluateDispatchTimeAsync(
        Guid tenantId,
        DateTime recipientLocalTime,
        bool isUrgent = false,
        CancellationToken cancellationToken = default)
    {
        var policy = await _db.QuietHoursPolicies
            .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

        if (policy == null || !policy.IsEnabled)
        {
            return new QuietHoursCheckResult
            {
                CanSendNow = true,
                InQuietHours = false,
                Reason = "Quiet hours disabled."
            };
        }

        bool inQuiet = policy.IsInQuietHours(recipientLocalTime);

        if (inQuiet && isUrgent && policy.AllowBypassForUrgent)
        {
            return new QuietHoursCheckResult
            {
                CanSendNow = true,
                InQuietHours = true,
                Reason = "Bypassed quiet hours due to urgent priority."
            };
        }

        if (inQuiet)
        {
            // Calculate next morning EndHour
            var nextMorning = recipientLocalTime.Date;
            if (recipientLocalTime.Hour >= policy.StartHourLocal)
            {
                nextMorning = nextMorning.AddDays(1);
            }
            var nextAvailable = nextMorning.AddHours(policy.EndHourLocal);

            return new QuietHoursCheckResult
            {
                CanSendNow = false,
                InQuietHours = true,
                NextAvailableWindowUtc = nextAvailable,
                Reason = $"Blocked: recipient local time ({recipientLocalTime:t}) is inside quiet hours ({policy.StartHourLocal}:00 to {policy.EndHourLocal}:00)."
            };
        }

        return new QuietHoursCheckResult
        {
            CanSendNow = true,
            InQuietHours = false,
            Reason = "Recipient is in active business hours."
        };
    }
}
