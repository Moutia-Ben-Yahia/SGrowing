using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public enum WhatsAppAccountTier
{
    Tier1_1K = 1000,
    Tier2_10K = 10000,
    Tier3_100K = 100000,
    Unlimited = 10000000
}

public enum BanRiskLevel
{
    Low,
    Moderate,
    Elevated,
    Critical
}

public record WarmupDaySchedule(
    int Day,
    int DailyLimit,
    int RecommendedBatchSize,
    double SafeRatePerMinute,
    string PhaseDescription);

public record PhoneNumberWarmupState
{
    public string PhoneNumberId { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public WhatsAppAccountTier AccountTier { get; init; } = WhatsAppAccountTier.Tier1_1K;
    public DateTime RegistrationDateUtc { get; init; } = DateTime.UtcNow;
    public int CurrentDay { get; init; } = 1;
    public int TodaySentCount { get; init; } = 0;
    public int DailyLimit { get; init; } = 50;
    public int RemainingToday => Math.Max(0, DailyLimit - TodaySentCount);
    public bool IsWarmupCompleted => CurrentDay >= 14;
    public DateTime LastSentDateUtc { get; init; } = DateTime.UtcNow.Date;
}

public record PacingIntervalResult(
    double RatePerMinute,
    double BaseIntervalSeconds,
    double JitterFactor,
    double EffectiveIntervalSeconds,
    double CooldownMultiplier,
    TimeSpan IntervalDelay);

public record PacingIntervalSample(
    int MessageIndex,
    double IntervalSeconds,
    double CumulativeDelaySeconds,
    double JitterFactor);

public record OptOutInterceptResult(
    bool IsOptOut,
    string? DetectedKeyword,
    string PhoneNumber,
    string NormalizedPhoneNumber,
    DateTime InterceptedAtUtc,
    string? OriginalMessage,
    string ActionTaken);

public record SuppressionRecord(
    string PhoneNumber,
    DateTime SuppressedAtUtc,
    string Reason,
    string? TriggerKeyword,
    string Source,
    bool IsActive);

public record BanRiskReport
{
    public string PhoneNumberId { get; init; } = string.Empty;
    public double BanRiskIndex { get; init; }
    public BanRiskLevel RiskLevel { get; init; } = BanRiskLevel.Low;
    public double BlockRate { get; init; }
    public double ComplaintRate { get; init; }
    public double TemplateFailureRate { get; init; }
    public double CurrentVelocityRatePerMin { get; init; }
    public double SafeVelocityRatePerMin { get; init; } = 30.0;
    public double CooldownMultiplier { get; init; } = 1.0;
    public bool IsEmergencyPaused { get; init; }
    public string DiagnosticSummary { get; init; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}

public record CampaignBatchSlice(
    int BatchIndex,
    int MessageCount,
    double EstimatedDurationSeconds,
    int CooldownPauseAfterSeconds);

public record CampaignSimulationRequest
{
    public string PhoneNumberId { get; init; } = "default-phone";
    public int TotalRecipients { get; init; } = 500;
    public double RatePerMinute { get; init; } = 30;
    public double JitterPercentage { get; init; } = 20; // 20%
    public int WarmupDay { get; init; } = 1;
    public WhatsAppAccountTier? AccountTier { get; init; }
    public List<string>? RecipientPhoneNumbers { get; init; }
}

public record CampaignSimulationResult
{
    public string PhoneNumberId { get; init; } = string.Empty;
    public int TotalRecipients { get; init; }
    public int EligibleRecipients { get; init; }
    public int SuppressedRecipientsCount { get; init; }
    public double TargetRatePerMinute { get; init; }
    public double EffectiveRatePerMinute { get; init; }
    public double CooldownMultiplier { get; init; }
    public double MinIntervalSeconds { get; init; }
    public double MaxIntervalSeconds { get; init; }
    public double AverageIntervalSeconds { get; init; }
    public double EstimatedDurationMinutes { get; init; }
    public DateTime EstimatedDeliveryStartUtc { get; init; }
    public DateTime EstimatedDeliveryEndUtc { get; init; }
    public int BatchCount { get; init; }
    public int BatchSize { get; init; }
    public int InterBatchPauseSeconds { get; init; }
    public double ProjectedBanRiskIndex { get; init; }
    public BanRiskLevel ProjectedBanRiskLevel { get; init; }
    public bool RequiresMultiDaySplitting { get; init; }
    public int DailyCapacityLimit { get; init; }
    public List<string> SafetyMarginRecommendations { get; init; } = new();
    public List<CampaignBatchSlice> BatchPlan { get; init; } = new();
}

public record ScheduleBroadcastRequest
{
    public string CampaignId { get; init; } = Guid.NewGuid().ToString("N");
    public string PhoneNumberId { get; init; } = "default-phone";
    public string CampaignName { get; init; } = "Broadcast Campaign";
    public List<string> Recipients { get; init; } = new();
    public double RatePerMinute { get; init; } = 30;
    public double JitterPercentage { get; init; } = 20;
}

public record ScheduleBroadcastResponse(
    string CampaignId,
    string PhoneNumberId,
    string CampaignName,
    int TotalRecipients,
    int EligibleRecipients,
    int SuppressedFiltered,
    double ScheduledRatePerMinute,
    double CooldownMultiplier,
    bool IsEmergencyPaused,
    DateTime ScheduledStartUtc,
    string Status,
    string Message);

public record SuppressionListResponse(
    IReadOnlyList<SuppressionRecord> Items,
    int TotalCount,
    int Skip,
    int Take);

public interface IWhatsAppBroadcastPacingService
{
    // Leaky-bucket pacing & jitter
    PacingIntervalResult CalculatePacingInterval(
        double ratePerMinute,
        double jitterFactor = 0.20,
        double cooldownMultiplier = 1.0,
        Random? rng = null);

    TimeSpan CalculateNextDelay(
        double ratePerMinute,
        double jitterFactor = 0.20,
        double cooldownMultiplier = 1.0);

    IReadOnlyList<PacingIntervalSample> GeneratePacingSchedule(
        int messageCount,
        double ratePerMinute,
        double jitterFactor = 0.20,
        double cooldownMultiplier = 1.0);

    // Warmup ramp
    int CalculateWarmupDailyLimit(
        int day,
        WhatsAppAccountTier tier = WhatsAppAccountTier.Tier1_1K,
        int initialCap = 50);

    IReadOnlyList<WarmupDaySchedule> GetWarmupTrajectory(
        string phoneNumberId,
        WhatsAppAccountTier tier = WhatsAppAccountTier.Tier1_1K,
        int initialCap = 50);

    PhoneNumberWarmupState GetWarmupStatus(string phoneNumberId);

    PhoneNumberWarmupState RegisterOrUpdatePhoneNumber(
        string phoneNumberId,
        string phoneNumber,
        WhatsAppAccountTier tier = WhatsAppAccountTier.Tier1_1K,
        int? overrideDay = null,
        DateTime? registrationDate = null);

    bool RecordSentMessage(string phoneNumberId, int count = 1);

    // Defensive opt-out interceptor
    OptOutInterceptResult InterceptInboundMessage(
        string phoneNumber,
        string messageText,
        string? tenantId = null);

    bool IsSuppressed(string phoneNumber);

    SuppressionRecord AddToSuppression(
        string phoneNumber,
        string reason,
        string? triggerKeyword = null,
        string source = "Manual");

    bool RemoveFromSuppression(string phoneNumber);

    IReadOnlyList<SuppressionRecord> GetSuppressionList(
        string? search = null,
        int skip = 0,
        int take = 50);

    // Ban risk index & cooldown shaper
    BanRiskReport EvaluateBanRisk(
        string phoneNumberId,
        double blockRate,
        double complaintRate,
        double templateFailureRate,
        double currentVelocityRatePerMin,
        double safeVelocityRatePerMin = 30.0);

    BanRiskReport GetBanRiskReport(string phoneNumberId);

    BanRiskReport AdjustCooldown(
        string phoneNumberId,
        double cooldownMultiplier,
        bool? emergencyPause = null,
        string? reason = null);

    // Campaign simulation & batching
    CampaignSimulationResult SimulateCampaign(CampaignSimulationRequest request);

    ScheduleBroadcastResponse ScheduleBroadcast(ScheduleBroadcastRequest request);

    void Reset();
}

public class WhatsAppBroadcastPacingService : IWhatsAppBroadcastPacingService
{
    private readonly ConcurrentDictionary<string, PhoneNumberWarmupState> _warmupStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SuppressionRecord> _suppressionLedger = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BanRiskReport> _banRiskReports = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ScheduleBroadcastResponse> _activeCampaigns = new(StringComparer.OrdinalIgnoreCase);

    // Multilingual Opt-Out Keywords
    private static readonly HashSet<string> OptOutKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // English
        "STOP", "UNSUBSCRIBE", "CANCEL", "QUIT", "PAUSE", "END", "OPT OUT", "OPTOUT", "REVOKE",
        // French
        "ARRETER", "ARRÊTER", "DESABONNER", "DÉSABONNER", "QUITTER", "FIN", "ANNULER",
        // Spanish
        "DESINSCRIBIR", "DESINSCRIBIRSE", "CANCELAR", "BAJA", "ALTO", "PARAR", "DETENER",
        // German
        "STOPP", "BEENDEN", "ABMELDEN",
        // Portuguese
        "SAIR", "DESCADASTRO",
        // Arabic
        "قف", "توقف", "الغاء", "إلغاء", "إيقاف", "ايقاف", "خروج", "إنهاء", "انهاء", "إيقاف الاشتراك", "ارسل قف"
    };

    public WhatsAppBroadcastPacingService()
    {
        SeedDefaultState();
    }

    private void SeedDefaultState()
    {
        var defaultPhoneId = "default-phone";
        var defaultPhone = "+21698123456";
        _warmupStates[defaultPhoneId] = new PhoneNumberWarmupState
        {
            PhoneNumberId = defaultPhoneId,
            PhoneNumber = defaultPhone,
            AccountTier = WhatsAppAccountTier.Tier1_1K,
            RegistrationDateUtc = DateTime.UtcNow.AddDays(-2),
            CurrentDay = 3,
            TodaySentCount = 14,
            DailyLimit = CalculateWarmupDailyLimit(3, WhatsAppAccountTier.Tier1_1K),
            LastSentDateUtc = DateTime.UtcNow.Date
        };

        _banRiskReports[defaultPhoneId] = new BanRiskReport
        {
            PhoneNumberId = defaultPhoneId,
            BanRiskIndex = 12.5,
            RiskLevel = BanRiskLevel.Low,
            BlockRate = 0.005,
            ComplaintRate = 0.001,
            TemplateFailureRate = 0.012,
            CurrentVelocityRatePerMin = 20.0,
            SafeVelocityRatePerMin = 25.0,
            CooldownMultiplier = 1.0,
            IsEmergencyPaused = false,
            DiagnosticSummary = "Healthy delivery metrics. Quality rating green.",
            EvaluatedAtUtc = DateTime.UtcNow
        };

        // Seed some suppression samples
        AddToSuppression("+15551234567", "Opt-Out Keyword [STOP]", "STOP", "InboundInterceptor");
        AddToSuppression("+33612345678", "Opt-Out Keyword [ARRETER]", "ARRETER", "InboundInterceptor");
        AddToSuppression("+21699887766", "User requested opt-out", "DESINSCRIBIR", "InboundInterceptor");
    }

    public void Reset()
    {
        _warmupStates.Clear();
        _suppressionLedger.Clear();
        _banRiskReports.Clear();
        _activeCampaigns.Clear();
        SeedDefaultState();
    }

    #region Leaky-Bucket Message Pacing Engine

    public PacingIntervalResult CalculatePacingInterval(
        double ratePerMinute,
        double jitterFactor = 0.20,
        double cooldownMultiplier = 1.0,
        Random? rng = null)
    {
        if (ratePerMinute <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ratePerMinute), "Rate per minute must be strictly greater than 0.");
        }

        jitterFactor = Math.Clamp(jitterFactor, 0.0, 1.0);
        cooldownMultiplier = Math.Max(1.0, cooldownMultiplier);

        var baseInterval = 60.0 / ratePerMinute;

        double randomDispersion;
        if (rng != null)
        {
            randomDispersion = (rng.NextDouble() * 2.0 - 1.0) * jitterFactor;
        }
        else
        {
            var bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            var uintVal = BitConverter.ToUInt32(bytes, 0);
            var unitUniform = (double)uintVal / uint.MaxValue; // 0.0 to 1.0
            randomDispersion = (unitUniform * 2.0 - 1.0) * jitterFactor; // [-jitterFactor, +jitterFactor]
        }

        // interval Δt = (60.0 / ratePerMin) * (1.0 + jitterFactor)
        var intervalWithJitter = baseInterval * (1.0 + randomDispersion);

        // Apply active cooldown multiplier
        var effectiveInterval = intervalWithJitter * cooldownMultiplier;

        // Ensure floor safety delay of at least 50 milliseconds
        effectiveInterval = Math.Max(0.05, effectiveInterval);

        return new PacingIntervalResult(
            RatePerMinute: ratePerMinute,
            BaseIntervalSeconds: Math.Round(baseInterval, 4),
            JitterFactor: Math.Round(randomDispersion, 4),
            EffectiveIntervalSeconds: Math.Round(effectiveInterval, 4),
            CooldownMultiplier: Math.Round(cooldownMultiplier, 2),
            IntervalDelay: TimeSpan.FromSeconds(effectiveInterval));
    }

    public TimeSpan CalculateNextDelay(
        double ratePerMinute,
        double jitterFactor = 0.20,
        double cooldownMultiplier = 1.0)
    {
        var result = CalculatePacingInterval(ratePerMinute, jitterFactor, cooldownMultiplier);
        return result.IntervalDelay;
    }

    public IReadOnlyList<PacingIntervalSample> GeneratePacingSchedule(
        int messageCount,
        double ratePerMinute,
        double jitterFactor = 0.20,
        double cooldownMultiplier = 1.0)
    {
        if (messageCount <= 0) return Array.Empty<PacingIntervalSample>();

        var samples = new List<PacingIntervalSample>(messageCount);
        double cumulativeDelay = 0.0;
        var rng = new Random(42); // deterministic sequence for schedule preview

        for (int i = 0; i < messageCount; i++)
        {
            var interval = CalculatePacingInterval(ratePerMinute, jitterFactor, cooldownMultiplier, rng);
            cumulativeDelay += interval.EffectiveIntervalSeconds;
            samples.Add(new PacingIntervalSample(
                MessageIndex: i + 1,
                IntervalSeconds: interval.EffectiveIntervalSeconds,
                CumulativeDelaySeconds: Math.Round(cumulativeDelay, 2),
                JitterFactor: interval.JitterFactor));
        }

        return samples;
    }

    #endregion

    #region 14-Day Phone Number Warmup Ramp Curve

    public int CalculateWarmupDailyLimit(
        int day,
        WhatsAppAccountTier tier = WhatsAppAccountTier.Tier1_1K,
        int initialCap = 50)
    {
        if (day <= 0) day = 1;
        initialCap = Math.Max(1, initialCap);
        int maxTierLimit = (int)tier;

        // Formula: DailyLimit(day) = Min(MaxTierLimit, InitialCap * (1.4 ^ (day - 1)))
        double computed = initialCap * Math.Pow(1.4, day - 1);
        int rounded = (int)Math.Round(computed, MidpointRounding.AwayFromZero);

        return Math.Min(maxTierLimit, rounded);
    }

    public IReadOnlyList<WarmupDaySchedule> GetWarmupTrajectory(
        string phoneNumberId,
        WhatsAppAccountTier tier = WhatsAppAccountTier.Tier1_1K,
        int initialCap = 50)
    {
        var schedule = new List<WarmupDaySchedule>(14);

        for (int day = 1; day <= 14; day++)
        {
            int limit = CalculateWarmupDailyLimit(day, tier, initialCap);

            // Recommended safe batch size scales from 25 to 250
            int recommendedBatch = Math.Min(250, Math.Max(25, (int)Math.Round(limit * 0.25)));

            // Recommended safe rate scales gently from 10 to 60 msgs/min
            double safeRate = Math.Min(60.0, Math.Max(10.0, 10.0 + (day - 1) * 3.5));

            string phaseDescription = day switch
            {
                <= 3 => "Incubation & Trust Seeding: Strict low volume, 1:1 engagement priority",
                <= 7 => "Progressive Ramp: Bi-directional chat flows and moderate batches",
                <= 11 => "Accelerated Expansion: Scaled broadcast windows with safety jitter",
                _ => "High-Throughput Maturity: Full account tier capacity unlocked"
            };

            schedule.Add(new WarmupDaySchedule(
                Day: day,
                DailyLimit: limit,
                RecommendedBatchSize: recommendedBatch,
                SafeRatePerMinute: Math.Round(safeRate, 1),
                PhaseDescription: phaseDescription));
        }

        return schedule;
    }

    public PhoneNumberWarmupState GetWarmupStatus(string phoneNumberId)
    {
        return _warmupStates.GetOrAdd(phoneNumberId, id =>
        {
            var tier = WhatsAppAccountTier.Tier1_1K;
            return new PhoneNumberWarmupState
            {
                PhoneNumberId = id,
                PhoneNumber = "+216" + RandomNumberGenerator.GetInt32(10000000, 99999999),
                AccountTier = tier,
                RegistrationDateUtc = DateTime.UtcNow,
                CurrentDay = 1,
                TodaySentCount = 0,
                DailyLimit = CalculateWarmupDailyLimit(1, tier),
                LastSentDateUtc = DateTime.UtcNow.Date
            };
        });
    }

    public PhoneNumberWarmupState RegisterOrUpdatePhoneNumber(
        string phoneNumberId,
        string phoneNumber,
        WhatsAppAccountTier tier = WhatsAppAccountTier.Tier1_1K,
        int? overrideDay = null,
        DateTime? registrationDate = null)
    {
        var regDate = registrationDate ?? DateTime.UtcNow;
        int currentDay;

        if (overrideDay.HasValue)
        {
            currentDay = Math.Max(1, overrideDay.Value);
        }
        else
        {
            var daysSince = (int)(DateTime.UtcNow.Date - regDate.Date).TotalDays + 1;
            currentDay = Math.Max(1, daysSince);
        }

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        var limit = CalculateWarmupDailyLimit(currentDay, tier);

        var state = new PhoneNumberWarmupState
        {
            PhoneNumberId = phoneNumberId,
            PhoneNumber = normalizedPhone,
            AccountTier = tier,
            RegistrationDateUtc = regDate,
            CurrentDay = currentDay,
            TodaySentCount = 0,
            DailyLimit = limit,
            LastSentDateUtc = DateTime.UtcNow.Date
        };

        _warmupStates[phoneNumberId] = state;
        return state;
    }

    public bool RecordSentMessage(string phoneNumberId, int count = 1)
    {
        if (count <= 0) return true;

        var state = GetWarmupStatus(phoneNumberId);
        var today = DateTime.UtcNow.Date;

        int newSentCount = (state.LastSentDateUtc == today)
            ? state.TodaySentCount + count
            : count; // new day reset

        var updated = state with
        {
            TodaySentCount = newSentCount,
            LastSentDateUtc = today
        };

        _warmupStates[phoneNumberId] = updated;
        return updated.RemainingToday >= 0;
    }

    #endregion

    #region Defensive Opt-Out Keyword Interceptor

    public OptOutInterceptResult InterceptInboundMessage(
        string phoneNumber,
        string messageText,
        string? tenantId = null)
    {
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return new OptOutInterceptResult(
                IsOptOut: false,
                DetectedKeyword: null,
                PhoneNumber: phoneNumber,
                NormalizedPhoneNumber: normalizedPhone,
                InterceptedAtUtc: DateTime.UtcNow,
                OriginalMessage: messageText,
                ActionTaken: "NoContent");
        }

        var detected = DetectOptOutKeyword(messageText);

        if (detected != null)
        {
            // Defensive update to suppression ledger
            AddToSuppression(
                normalizedPhone,
                $"Opt-Out Keyword [{detected}] detected in inbound message",
                detected,
                "InboundInterceptor");

            return new OptOutInterceptResult(
                IsOptOut: true,
                DetectedKeyword: detected,
                PhoneNumber: phoneNumber,
                NormalizedPhoneNumber: normalizedPhone,
                InterceptedAtUtc: DateTime.UtcNow,
                OriginalMessage: messageText,
                ActionTaken: "SuppressedAndBlockedFutureBroadcasts");
        }

        return new OptOutInterceptResult(
            IsOptOut: false,
            DetectedKeyword: null,
            PhoneNumber: phoneNumber,
            NormalizedPhoneNumber: normalizedPhone,
            InterceptedAtUtc: DateTime.UtcNow,
            OriginalMessage: messageText,
            ActionTaken: "PassedThrough");
    }

    public bool IsSuppressed(string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        if (_suppressionLedger.TryGetValue(normalized, out var record))
        {
            return record.IsActive;
        }
        return false;
    }

    public SuppressionRecord AddToSuppression(
        string phoneNumber,
        string reason,
        string? triggerKeyword = null,
        string source = "Manual")
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        var record = new SuppressionRecord(
            PhoneNumber: normalized,
            SuppressedAtUtc: DateTime.UtcNow,
            Reason: reason,
            TriggerKeyword: triggerKeyword,
            Source: source,
            IsActive: true);

        _suppressionLedger[normalized] = record;
        return record;
    }

    public bool RemoveFromSuppression(string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        return _suppressionLedger.TryRemove(normalized, out _);
    }

    public IReadOnlyList<SuppressionRecord> GetSuppressionList(
        string? search = null,
        int skip = 0,
        int take = 50)
    {
        var query = _suppressionLedger.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(r =>
                r.PhoneNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.Reason.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (r.TriggerKeyword != null && r.TriggerKeyword.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        return query
            .OrderByDescending(r => r.SuppressedAtUtc)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 500))
            .ToList();
    }

    private string? DetectOptOutKeyword(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Clean punctuation and extraneous whitespace
        var cleaned = Regex.Replace(text.Trim(), @"[^\w\s\u0600-\u06FF]", " ");
        var normalizedFull = Regex.Replace(cleaned, @"\s+", " ").Trim();

        // 1. Multi-word phrase check FIRST (e.g. "OPT OUT", "إيقاف الاشتراك", "ارسل قف")
        foreach (var keyword in OptOutKeywords)
        {
            if (keyword.Contains(' ') &&
                normalizedFull.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return keyword.ToUpperInvariant();
            }
        }

        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // 2. Direct single-word exact match
        foreach (var token in tokens)
        {
            if (OptOutKeywords.Contains(token))
            {
                return token.ToUpperInvariant();
            }
        }

        // 3. Prefix match for commands like "STOP please" or "ARRÊTER mes messages"
        if (tokens.Length > 0 && OptOutKeywords.Contains(tokens[0]))
        {
            return tokens[0].ToUpperInvariant();
        }

        return null;
    }

    public static string NormalizePhoneNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digitsOnly = Regex.Replace(raw, @"[^\d+]", "");
        if (digitsOnly.StartsWith("00"))
        {
            digitsOnly = "+" + digitsOnly[2..];
        }
        else if (!digitsOnly.StartsWith("+"))
        {
            digitsOnly = "+" + digitsOnly;
        }
        return digitsOnly;
    }

    #endregion

    #region Ban Risk Index & Cooldown Shaper

    public BanRiskReport EvaluateBanRisk(
        string phoneNumberId,
        double blockRate,
        double complaintRate,
        double templateFailureRate,
        double currentVelocityRatePerMin,
        double safeVelocityRatePerMin = 30.0)
    {
        // Normalize rates if provided as percentages > 1.0 (e.g. 5.0 -> 0.05)
        if (blockRate > 1.0) blockRate /= 100.0;
        if (complaintRate > 1.0) complaintRate /= 100.0;
        if (templateFailureRate > 1.0) templateFailureRate /= 100.0;

        blockRate = Math.Max(0.0, blockRate);
        complaintRate = Math.Max(0.0, complaintRate);
        templateFailureRate = Math.Max(0.0, templateFailureRate);
        currentVelocityRatePerMin = Math.Max(0.0, currentVelocityRatePerMin);
        safeVelocityRatePerMin = Math.Max(5.0, safeVelocityRatePerMin);

        // Component ratios normalized to critical thresholds:
        // - Complaint critical threshold: 1.5% (0.015)
        // - Block critical threshold: 4.0% (0.04)
        // - Template failure critical threshold: 12.0% (0.12)
        // - Velocity overshoot ratio: current / safe
        double normComplaint = Math.Clamp(complaintRate / 0.015, 0.0, 1.0);
        double normBlock = Math.Clamp(blockRate / 0.04, 0.0, 1.0);
        double normFailure = Math.Clamp(templateFailureRate / 0.12, 0.0, 1.0);
        double velocityOvershoot = Math.Max(0.0, currentVelocityRatePerMin - safeVelocityRatePerMin) / safeVelocityRatePerMin;
        double normVelocity = Math.Clamp(velocityOvershoot, 0.0, 1.0);

        // Weighted sum: 35% Complaint + 35% Block + 15% Template Failure + 15% Velocity
        double score = (0.35 * normComplaint + 0.35 * normBlock + 0.15 * normFailure + 0.15 * normVelocity) * 100.0;

        // Compound risk multiplier if both complaints and blocks are simultaneously high
        if (normComplaint > 0.6 && normBlock > 0.6)
        {
            score = Math.Min(100.0, score * 1.25);
        }

        score = Math.Round(Math.Clamp(score, 0.0, 100.0), 1);

        BanRiskLevel level;
        double cooldownMultiplier = 1.0;
        bool isEmergencyPaused = false;
        string diagnostic;

        if (score > 85.0)
        {
            level = BanRiskLevel.Critical;
            cooldownMultiplier = 3.0;
            isEmergencyPaused = true;
            diagnostic = $"CRITICAL BAN RISK ({score}%): Spam complaints ({complaintRate:P1}) or block rates ({blockRate:P1}) exceeded safety limits. Automated emergency pause engaged to protect WhatsApp Business number.";
        }
        else if (score > 65.0)
        {
            level = BanRiskLevel.Elevated;
            cooldownMultiplier = 2.0; // trigger cooldown multiplier when > 65%
            isEmergencyPaused = false;
            diagnostic = $"ELEVATED BAN RISK ({score}%): Algorithmic risk elevated. 2.0x cooldown pacing multiplier activated to disperse message burst velocity.";
        }
        else if (score > 40.0)
        {
            level = BanRiskLevel.Moderate;
            cooldownMultiplier = 1.25;
            isEmergencyPaused = false;
            diagnostic = $"MODERATE BAN RISK ({score}%): Slight metric fluctuations detected. 1.25x safe pacing applied.";
        }
        else
        {
            level = BanRiskLevel.Low;
            cooldownMultiplier = 1.0;
            isEmergencyPaused = false;
            diagnostic = $"LOW BAN RISK ({score}%): All channel health metrics well within Meta safety guidelines.";
        }

        var report = new BanRiskReport
        {
            PhoneNumberId = phoneNumberId,
            BanRiskIndex = score,
            RiskLevel = level,
            BlockRate = Math.Round(blockRate, 4),
            ComplaintRate = Math.Round(complaintRate, 4),
            TemplateFailureRate = Math.Round(templateFailureRate, 4),
            CurrentVelocityRatePerMin = Math.Round(currentVelocityRatePerMin, 1),
            SafeVelocityRatePerMin = Math.Round(safeVelocityRatePerMin, 1),
            CooldownMultiplier = cooldownMultiplier,
            IsEmergencyPaused = isEmergencyPaused,
            DiagnosticSummary = diagnostic,
            EvaluatedAtUtc = DateTime.UtcNow
        };

        _banRiskReports[phoneNumberId] = report;
        return report;
    }

    public BanRiskReport GetBanRiskReport(string phoneNumberId)
    {
        return _banRiskReports.GetOrAdd(phoneNumberId, id =>
            EvaluateBanRisk(id, blockRate: 0.004, complaintRate: 0.001, templateFailureRate: 0.015, currentVelocityRatePerMin: 20.0, safeVelocityRatePerMin: 30.0));
    }

    public BanRiskReport AdjustCooldown(
        string phoneNumberId,
        double cooldownMultiplier,
        bool? emergencyPause = null,
        string? reason = null)
    {
        var existing = GetBanRiskReport(phoneNumberId);
        double multiplier = Math.Max(1.0, cooldownMultiplier);
        bool paused = emergencyPause ?? (multiplier >= 3.0 || existing.BanRiskIndex > 85.0);

        var updated = existing with
        {
            CooldownMultiplier = multiplier,
            IsEmergencyPaused = paused,
            DiagnosticSummary = !string.IsNullOrWhiteSpace(reason)
                ? $"Manual adjustment: {reason} (Multiplier: {multiplier:F1}x, Paused: {paused})"
                : $"Manual cooldown updated to {multiplier:F1}x (Paused: {paused})",
            EvaluatedAtUtc = DateTime.UtcNow
        };

        _banRiskReports[phoneNumberId] = updated;
        return updated;
    }

    #endregion

    #region Campaign Simulation & Batching

    public CampaignSimulationResult SimulateCampaign(CampaignSimulationRequest request)
    {
        var warmupState = GetWarmupStatus(request.PhoneNumberId);
        var warmupDay = request.WarmupDay > 0 ? request.WarmupDay : warmupState.CurrentDay;
        var tier = request.AccountTier ?? warmupState.AccountTier;
        var dailyLimit = CalculateWarmupDailyLimit(warmupDay, tier);

        var banReport = GetBanRiskReport(request.PhoneNumberId);
        var cooldownMultiplier = banReport.CooldownMultiplier;

        int totalRecipients = request.TotalRecipients;
        int suppressedCount = 0;

        // If specific recipient phone numbers were provided, check suppression ledger
        if (request.RecipientPhoneNumbers != null && request.RecipientPhoneNumbers.Count > 0)
        {
            totalRecipients = request.RecipientPhoneNumbers.Count;
            suppressedCount = request.RecipientPhoneNumbers.Count(p => IsSuppressed(p));
        }

        int eligibleRecipients = Math.Max(0, totalRecipients - suppressedCount);

        double targetRate = Math.Max(1.0, request.RatePerMinute);
        double effectiveRate = targetRate / cooldownMultiplier;

        var jitterRatio = Math.Clamp(request.JitterPercentage / 100.0, 0.0, 1.0);
        var baseInterval = 60.0 / effectiveRate;
        var minInterval = Math.Round(baseInterval * (1.0 - jitterRatio), 2);
        var maxInterval = Math.Round(baseInterval * (1.0 + jitterRatio), 2);
        var avgInterval = Math.Round(baseInterval, 2);

        // Safe batch sizing based on warmup phase
        int batchSize = warmupDay switch
        {
            <= 3 => 30,
            <= 7 => 60,
            <= 10 => 120,
            _ => 250
        };

        int batchCount = eligibleRecipients > 0 ? (int)Math.Ceiling((double)eligibleRecipients / batchSize) : 0;
        int interBatchPauseSeconds = warmupDay <= 3 ? 90 : (warmupDay <= 7 ? 60 : 30);

        // Calculate delivery time windows
        double baseTransmissionSeconds = eligibleRecipients * avgInterval;
        double totalBatchCooldownSeconds = Math.Max(0, batchCount - 1) * interBatchPauseSeconds;
        double totalDurationSeconds = baseTransmissionSeconds + totalBatchCooldownSeconds;
        double totalDurationMinutes = Math.Round(totalDurationSeconds / 60.0, 1);

        var startUtc = DateTime.UtcNow;
        var endUtc = startUtc.AddSeconds(totalDurationSeconds);

        // Batch plan generation
        var batchPlan = new List<CampaignBatchSlice>(batchCount);
        int remaining = eligibleRecipients;
        for (int i = 0; i < batchCount; i++)
        {
            int currentBatchCount = Math.Min(batchSize, remaining);
            double batchDuration = currentBatchCount * avgInterval;
            int pause = (i == batchCount - 1) ? 0 : interBatchPauseSeconds;
            batchPlan.Add(new CampaignBatchSlice(
                BatchIndex: i + 1,
                MessageCount: currentBatchCount,
                EstimatedDurationSeconds: Math.Round(batchDuration, 1),
                CooldownPauseAfterSeconds: pause));
            remaining -= currentBatchCount;
        }

        // Projected ban risk based on target rate vs safe velocity and volume
        var safeRate = Math.Min(60.0, 10.0 + (warmupDay - 1) * 3.5);
        double projectedRisk = Math.Min(100.0, Math.Max(10.0, banReport.BanRiskIndex + (targetRate > safeRate ? (targetRate - safeRate) * 1.2 : 0)));
        var projectedLevel = projectedRisk > 85 ? BanRiskLevel.Critical : (projectedRisk > 65 ? BanRiskLevel.Elevated : (projectedRisk > 40 ? BanRiskLevel.Moderate : BanRiskLevel.Low));

        var recommendations = new List<string>();
        bool requiresMultiDaySplitting = eligibleRecipients > dailyLimit;

        if (requiresMultiDaySplitting)
        {
            recommendations.Add($"Campaign volume ({eligibleRecipients:N0}) exceeds Day {warmupDay} daily capacity limit ({dailyLimit:N0}). Multi-day broadcast window required.");
        }

        if (targetRate > safeRate)
        {
            recommendations.Add($"Requested rate ({targetRate:F0} msg/min) exceeds Day {warmupDay} safe velocity ({safeRate:F0} msg/min). Pacing throttled with {cooldownMultiplier:F1}x cooldown multiplier.");
        }

        if (suppressedCount > 0)
        {
            recommendations.Add($"{suppressedCount} recipient(s) automatically filtered out via defensive opt-out suppression ledger.");
        }

        if (cooldownMultiplier > 1.0)
        {
            recommendations.Add($"Active safety cooldown ({cooldownMultiplier:F1}x) extended total campaign duration by {Math.Round(baseTransmissionSeconds * (1.0 - 1.0 / cooldownMultiplier) / 60.0, 1)} minutes.");
        }

        if (banReport.IsEmergencyPaused)
        {
            recommendations.Add("EMERGENCY SAFETY PAUSE ACTIVE: Phone number is in protective lockdown. Resolve ban risk before sending.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Optimal safety parameters: All velocity, warmup, and compliance checks passed.");
        }

        return new CampaignSimulationResult
        {
            PhoneNumberId = request.PhoneNumberId,
            TotalRecipients = totalRecipients,
            EligibleRecipients = eligibleRecipients,
            SuppressedRecipientsCount = suppressedCount,
            TargetRatePerMinute = targetRate,
            EffectiveRatePerMinute = Math.Round(effectiveRate, 1),
            CooldownMultiplier = cooldownMultiplier,
            MinIntervalSeconds = minInterval,
            MaxIntervalSeconds = maxInterval,
            AverageIntervalSeconds = avgInterval,
            EstimatedDurationMinutes = totalDurationMinutes,
            EstimatedDeliveryStartUtc = startUtc,
            EstimatedDeliveryEndUtc = endUtc,
            BatchCount = batchCount,
            BatchSize = batchSize,
            InterBatchPauseSeconds = interBatchPauseSeconds,
            ProjectedBanRiskIndex = Math.Round(projectedRisk, 1),
            ProjectedBanRiskLevel = projectedLevel,
            RequiresMultiDaySplitting = requiresMultiDaySplitting,
            DailyCapacityLimit = dailyLimit,
            SafetyMarginRecommendations = recommendations,
            BatchPlan = batchPlan
        };
    }

    public ScheduleBroadcastResponse ScheduleBroadcast(ScheduleBroadcastRequest request)
    {
        var banReport = GetBanRiskReport(request.PhoneNumberId);

        if (banReport.IsEmergencyPaused)
        {
            var pauseResponse = new ScheduleBroadcastResponse(
                CampaignId: request.CampaignId,
                PhoneNumberId: request.PhoneNumberId,
                CampaignName: request.CampaignName,
                TotalRecipients: request.Recipients.Count,
                EligibleRecipients: 0,
                SuppressedFiltered: 0,
                ScheduledRatePerMinute: 0,
                CooldownMultiplier: banReport.CooldownMultiplier,
                IsEmergencyPaused: true,
                ScheduledStartUtc: DateTime.UtcNow,
                Status: "BlockedByEmergencyPause",
                Message: "Broadcast blocked: WhatsApp number is currently in emergency safety pause due to elevated ban risk.");

            _activeCampaigns[request.CampaignId] = pauseResponse;
            return pauseResponse;
        }

        // Filter suppressed recipients
        var eligible = request.Recipients.Where(r => !IsSuppressed(r)).ToList();
        var suppressedCount = request.Recipients.Count - eligible.Count;

        var status = banReport.CooldownMultiplier > 1.0 ? "ScheduledWithCooldownThrottle" : "ScheduledReady";
        var message = suppressedCount > 0
            ? $"Scheduled {eligible.Count} recipients ({suppressedCount} suppressed contacts safely excluded)."
            : $"Scheduled {eligible.Count} recipients with humanized leaky-bucket pacing.";

        var response = new ScheduleBroadcastResponse(
            CampaignId: request.CampaignId,
            PhoneNumberId: request.PhoneNumberId,
            CampaignName: request.CampaignName,
            TotalRecipients: request.Recipients.Count,
            EligibleRecipients: eligible.Count,
            SuppressedFiltered: suppressedCount,
            ScheduledRatePerMinute: request.RatePerMinute / banReport.CooldownMultiplier,
            CooldownMultiplier: banReport.CooldownMultiplier,
            IsEmergencyPaused: false,
            ScheduledStartUtc: DateTime.UtcNow,
            Status: status,
            Message: message);

        _activeCampaigns[request.CampaignId] = response;
        return response;
    }

    #endregion
}
