using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public record WarmupDayScheduleInfo(
    int Day,
    int DailyQuota,
    string PhaseName,
    string Description);

public record WarmupThrottleDecision(
    bool IsAllowed,
    int CurrentDay,
    int DailyLimit,
    int TodaySentCount,
    int RemainingQuota,
    string Status,
    string? ThrottleReason);

public record SimulatedPresenceSequence(
    int ReadReceiptDelayMs,
    string PresenceState,
    int TypingDurationMs,
    double PoissonPacingDelaySeconds,
    double TotalPreDispatchDelaySeconds);

public record AntiBanPacingPlan(
    string PhoneNumber,
    int MessageLength,
    double PoissonDelaySeconds,
    SimulatedPresenceSequence PresenceSequence,
    WarmupThrottleDecision WarmupDecision,
    DateTime PlannedDispatchUtc);

public interface IAntiBanMessagePacingService
{
    double CalculatePoissonJitterDelay(double lambda = 3.2, double minRange = 1.8, double maxRange = 5.4);
    int CalculateTypingDurationMs(int characterCount, int msPerChar = 25, int minMs = 400, int maxMs = 12000);
    SimulatedPresenceSequence SimulatePresenceSequence(string messageText, double lambda = 3.2, double minRange = 1.8, double maxRange = 5.4);
    WarmupThrottleDecision CheckWarmupThrottle(string phoneNumber, int dayOfWarmup = 1, int messageCount = 1);
    AntiBanPacingPlan CalculatePacingPlan(string phoneNumber, string messageText, int dayOfWarmup = 1, double lambda = 3.2, double minRange = 1.8, double maxRange = 5.4);
    bool RecordMessageSent(string phoneNumber, int dayOfWarmup = 1, int count = 1);
    int GetDailyQuotaForDay(int day);
    void ResetWarmupTracker(string phoneNumber);
}

public class AntiBanMessagePacingService : IAntiBanMessagePacingService
{
    private readonly ILogger<AntiBanMessagePacingService>? _logger;

    // Concurrent in-memory tracker per normalized phone number: (SentToday, LastDate, ExplicitDay)
    private readonly ConcurrentDictionary<string, PhoneWarmupTracker> _warmupTrackers = new();

    public static readonly IReadOnlyList<WarmupDayScheduleInfo> WarmupSchedule = [
        new(1, 50, "Initial Contact", "Day 1: Strict sandbox quota of 50 messages to establish sender identity"),
        new(2, 120, "Cautious Engagement", "Day 2: 120 messages with balanced conversation ratios"),
        new(3, 200, "Gradual Scaling", "Day 3: 200 messages with customer response verification"),
        new(4, 350, "Velocity Ramp", "Day 4: 350 messages across verified contacts"),
        new(5, 500, "Full Warmup Capacity", "Day 5: 500 messages establishing high sender reputation score"),
        new(6, 750, "Pre-Enterprise Ramp", "Day 6: 750 messages with proactive health checks"),
        new(7, 1000, "Enterprise Tier 1", "Day 7+: 1,000+ messages per day on fully warmed line")
    ];

    public AntiBanMessagePacingService(ILogger<AntiBanMessagePacingService>? logger = null)
    {
        _logger = logger;
    }

    public double CalculatePoissonJitterDelay(double lambda = 3.2, double minRange = 1.8, double maxRange = 5.4)
    {
        if (lambda <= 0) lambda = 3.2;
        if (minRange <= 0) minRange = 1.8;
        if (maxRange <= minRange) maxRange = minRange + 3.6;

        // Truncated exponential inverse CDF sampling for mathematically sound Poisson process inter-arrival interval
        // F(x) = 1 - exp(-x / lambda)
        double fa = 1.0 - Math.Exp(-minRange / lambda);
        double fb = 1.0 - Math.Exp(-maxRange / lambda);

        // Uniform random double in [0, 1)
        double u = RandomNumberGenerator.GetInt32(1, int.MaxValue) / (double)int.MaxValue;
        double uPrime = fa + (u * (fb - fa));

        // Invert: X = -lambda * ln(1 - u')
        double sampled = -lambda * Math.Log(Math.Max(1e-9, 1.0 - uPrime));

        // Safeguard clamp
        return Math.Round(Math.Clamp(sampled, minRange, maxRange), 3);
    }

    public int CalculateTypingDurationMs(int characterCount, int msPerChar = 25, int minMs = 400, int maxMs = 12000)
    {
        if (characterCount <= 0) return minMs;

        int baseReaction = RandomNumberGenerator.GetInt32(200, 450);
        int typingRaw = baseReaction + (characterCount * msPerChar);

        // Add 10% natural variance
        int jitter = RandomNumberGenerator.GetInt32(-25, 45);
        int finalDuration = typingRaw + jitter;

        return Math.Clamp(finalDuration, minMs, maxMs);
    }

    public SimulatedPresenceSequence SimulatePresenceSequence(string messageText, double lambda = 3.2, double minRange = 1.8, double maxRange = 5.4)
    {
        int charCount = messageText?.Length ?? 0;
        int readReceiptDelay = RandomNumberGenerator.GetInt32(500, 2200);
        int typingDuration = CalculateTypingDurationMs(charCount);
        double poissonDelay = CalculatePoissonJitterDelay(lambda, minRange, maxRange);

        double totalPreDispatch = Math.Round((readReceiptDelay + typingDuration) / 1000.0 + poissonDelay, 3);

        return new SimulatedPresenceSequence(
            ReadReceiptDelayMs: readReceiptDelay,
            PresenceState: "composing",
            TypingDurationMs: typingDuration,
            PoissonPacingDelaySeconds: poissonDelay,
            TotalPreDispatchDelaySeconds: totalPreDispatch);
    }

    public int GetDailyQuotaForDay(int day)
    {
        if (day <= 0) day = 1;
        var matched = WarmupSchedule.FirstOrDefault(s => s.Day == day);
        return matched?.DailyQuota ?? (day >= 7 ? 1000 : 500);
    }

    public WarmupThrottleDecision CheckWarmupThrottle(string phoneNumber, int dayOfWarmup = 1, int messageCount = 1)
    {
        var normalized = NormalizePhone(phoneNumber);
        var tracker = _warmupTrackers.GetOrAdd(normalized, _ => new PhoneWarmupTracker(dayOfWarmup));

        // Rollover if date changed
        tracker.EnsureCurrentDate(dayOfWarmup);

        int quota = GetDailyQuotaForDay(tracker.CurrentDay);
        int projected = tracker.TodaySentCount + messageCount;

        if (projected > quota)
        {
            return new WarmupThrottleDecision(
                IsAllowed: false,
                CurrentDay: tracker.CurrentDay,
                DailyLimit: quota,
                TodaySentCount: tracker.TodaySentCount,
                RemainingQuota: Math.Max(0, quota - tracker.TodaySentCount),
                Status: "DailyLimitExceeded",
                ThrottleReason: $"Anti-ban warmup limit reached for Day {tracker.CurrentDay}: {tracker.TodaySentCount}/{quota} sent. Dispatch throttled to prevent spam bans.");
        }

        return new WarmupThrottleDecision(
            IsAllowed: true,
            CurrentDay: tracker.CurrentDay,
            DailyLimit: quota,
            TodaySentCount: tracker.TodaySentCount,
            RemainingQuota: quota - tracker.TodaySentCount,
            Status: "Allowed",
            ThrottleReason: null);
    }

    public AntiBanPacingPlan CalculatePacingPlan(
        string phoneNumber,
        string messageText,
        int dayOfWarmup = 1,
        double lambda = 3.2,
        double minRange = 1.8,
        double maxRange = 5.4)
    {
        var throttle = CheckWarmupThrottle(phoneNumber, dayOfWarmup, 1);
        var presence = SimulatePresenceSequence(messageText, lambda, minRange, maxRange);

        var plannedUtc = DateTime.UtcNow.AddSeconds(presence.TotalPreDispatchDelaySeconds);

        return new AntiBanPacingPlan(
            PhoneNumber: phoneNumber,
            MessageLength: messageText?.Length ?? 0,
            PoissonDelaySeconds: presence.PoissonPacingDelaySeconds,
            PresenceSequence: presence,
            WarmupDecision: throttle,
            PlannedDispatchUtc: plannedUtc);
    }

    public bool RecordMessageSent(string phoneNumber, int dayOfWarmup = 1, int count = 1)
    {
        var normalized = NormalizePhone(phoneNumber);
        var tracker = _warmupTrackers.GetOrAdd(normalized, _ => new PhoneWarmupTracker(dayOfWarmup));
        tracker.EnsureCurrentDate(dayOfWarmup);

        int quota = GetDailyQuotaForDay(tracker.CurrentDay);
        lock (tracker)
        {
            if (tracker.TodaySentCount + count > quota)
            {
                _logger?.LogWarning("Phone {Phone} exceeded warmup quota on Day {Day} ({Sent}/{Quota})",
                    normalized, tracker.CurrentDay, tracker.TodaySentCount, quota);
                return false;
            }
            tracker.TodaySentCount += count;
            return true;
        }
    }

    public void ResetWarmupTracker(string phoneNumber)
    {
        var normalized = NormalizePhone(phoneNumber);
        _warmupTrackers.TryRemove(normalized, out _);
    }

    private static string NormalizePhone(string phone) =>
        string.IsNullOrWhiteSpace(phone) ? "default" : new string(phone.Where(char.IsAsciiDigit).ToArray());

    private sealed class PhoneWarmupTracker
    {
        public int CurrentDay { get; set; }
        public int TodaySentCount { get; set; }
        public DateTime LastDateUtc { get; set; } = DateTime.UtcNow.Date;

        public PhoneWarmupTracker(int initialDay)
        {
            CurrentDay = Math.Max(1, initialDay);
        }

        public void EnsureCurrentDate(int fallbackDay)
        {
            lock (this)
            {
                var today = DateTime.UtcNow.Date;
                if (today > LastDateUtc)
                {
                    int dayDiff = (int)(today - LastDateUtc).TotalDays;
                    CurrentDay = Math.Min(7, CurrentDay + dayDiff);
                    TodaySentCount = 0;
                    LastDateUtc = today;
                }
                else if (fallbackDay > CurrentDay)
                {
                    CurrentDay = fallbackDay;
                }
            }
        }
    }
}
