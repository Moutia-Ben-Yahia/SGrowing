namespace SGrowing.Api.Modules.Automation;

public record PacingEvaluationResult(
    string TimezoneId,
    DateTime RecipientLocalTime,
    bool IsInQuietHours,
    bool IsWeekend,
    bool CanDispatchNow,
    DateTime NextSafeDispatchUtc,
    string Reason
);

public interface ILocalTimezonePacingService
{
    string InferTimezoneFromPhoneNumber(string? phoneNumber);
    string InferTimezoneFromCoordinates(double latitude, double longitude);
    bool IsInQuietHours(DateTime localTime, TimeSpan quietStart, TimeSpan quietEnd);
    bool IsWeekend(DateTime localTime, string timezoneId);
    bool IsMenaTimezone(string timezoneId);
    DateTime CalculateNextSafeDispatchUtc(
        DateTime currentUtc,
        string timezoneId,
        TimeSpan quietStart,
        TimeSpan quietEnd,
        bool excludeWeekends = true);

    PacingEvaluationResult EvaluatePacing(
        DateTime currentUtc,
        string? phoneNumber = null,
        double? latitude = null,
        double? longitude = null,
        string? overrideTimezone = null,
        TimeSpan? quietStart = null,
        TimeSpan? quietEnd = null,
        bool excludeWeekends = true);
}

public class LocalTimezonePacingService : ILocalTimezonePacingService
{
    private static readonly (string Prefix, string Timezone)[] PhonePrefixMappings =
    [
        ("+213", "Africa/Algiers"),
        ("+216", "Africa/Tunis"),
        ("+212", "Africa/Casablanca"),
        ("+20",  "Africa/Cairo"),
        ("+971", "Asia/Dubai"),
        ("+966", "Asia/Riyadh"),
        ("+974", "Asia/Qatar"),
        ("+965", "Asia/Kuwait"),
        ("+968", "Asia/Muscat"),
        ("+973", "Asia/Bahrain"),
        ("+962", "Asia/Amman"),
        ("+961", "Asia/Beirut"),
        ("+964", "Asia/Baghdad"),
        ("+44",  "Europe/London"),
        ("+33",  "Europe/Paris"),
        ("+49",  "Europe/Berlin"),
        ("+34",  "Europe/Madrid"),
        ("+39",  "Europe/Rome"),
        ("+31",  "Europe/Amsterdam"),
        ("+41",  "Europe/Zurich"),
        ("+32",  "Europe/Brussels"),
        ("+81",  "Asia/Tokyo"),
        ("+86",  "Asia/Shanghai"),
        ("+65",  "Asia/Singapore"),
        ("+91",  "Asia/Kolkata"),
        ("+61",  "Australia/Sydney"),
        ("+55",  "America/Sao_Paulo"),
        ("+27",  "Africa/Johannesburg"),
        ("+90",  "Europe/Istanbul"),
        ("+1",   "America/New_York")
    ];

    private static readonly HashSet<string> MenaTimezones = new(StringComparer.OrdinalIgnoreCase)
    {
        "Africa/Algiers",
        "Africa/Tunis",
        "Africa/Casablanca",
        "Africa/Cairo",
        "Asia/Dubai",
        "Asia/Riyadh",
        "Asia/Qatar",
        "Asia/Kuwait",
        "Asia/Muscat",
        "Asia/Bahrain",
        "Asia/Amman",
        "Asia/Beirut",
        "Asia/Baghdad",
        "Arabian Standard Time",
        "Arab Standard Time",
        "Egypt Standard Time",
        "Morocco Standard Time",
        "W. Central Africa Standard Time"
    };

    public string InferTimezoneFromPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return "UTC";
        }

        var cleaned = phoneNumber.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (!cleaned.StartsWith('+') && cleaned.Length > 2)
        {
            cleaned = "+" + cleaned;
        }

        // Longest prefix match
        foreach (var (prefix, timezone) in PhonePrefixMappings.OrderByDescending(p => p.Prefix.Length))
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return timezone;
            }
        }

        return "UTC";
    }

    public string InferTimezoneFromCoordinates(double latitude, double longitude)
    {
        // 1. Algiers / Algeria
        if (latitude is >= 19.0 and <= 37.5 && longitude is >= -8.6 and <= 12.0)
            return "Africa/Algiers";

        // 2. Tunis / Tunisia
        if (latitude is >= 30.0 and <= 38.0 && longitude is >= 7.5 and <= 11.8)
            return "Africa/Tunis";

        // 3. Cairo / Egypt
        if (latitude is >= 22.0 and <= 32.0 && longitude is >= 24.5 and <= 37.0)
            return "Africa/Cairo";

        // 4. UAE / Dubai
        if (latitude is >= 22.5 and <= 26.5 && longitude is >= 51.0 and <= 56.5)
            return "Asia/Dubai";

        // 5. Saudi Arabia / Riyadh
        if (latitude is >= 16.0 and <= 32.5 && longitude is >= 34.5 and <= 55.5)
            return "Asia/Riyadh";

        // 6. London / UK
        if (latitude is >= 49.5 and <= 61.0 && longitude is >= -8.5 and <= 2.0)
            return "Europe/London";

        // 7. Paris / Western Europe
        if (latitude is >= 42.0 and <= 52.0 && longitude is >= -5.0 and <= 9.0)
            return "Europe/Paris";

        // 8. North America
        if (latitude is >= 24.0 and <= 50.0 && longitude is >= -126.0 and <= -66.0)
        {
            if (longitude < -114.0) return "America/Los_Angeles";
            if (longitude < -100.0) return "America/Denver";
            if (longitude < -85.0)  return "America/Chicago";
            return "America/New_York";
        }

        // Mathematical Longitude Offset Fallback
        var offsetHours = (int)Math.Round(longitude / 15.0);
        return offsetHours switch
        {
            -8 => "America/Los_Angeles",
            -7 => "America/Denver",
            -6 => "America/Chicago",
            -5 => "America/New_York",
            0  => "Europe/London",
            1  => "Europe/Paris",
            2  => "Africa/Cairo",
            3  => "Asia/Riyadh",
            4  => "Asia/Dubai",
            5  => "Asia/Karachi",
            6  => "Asia/Dhaka",
            8  => "Asia/Singapore",
            9  => "Asia/Tokyo",
            10 => "Australia/Sydney",
            _  => "UTC"
        };
    }

    public bool IsMenaTimezone(string timezoneId)
    {
        return MenaTimezones.Contains(timezoneId);
    }

    public bool IsWeekend(DateTime localTime, string timezoneId)
    {
        if (IsMenaTimezone(timezoneId))
        {
            // MENA weekend is Friday and Saturday
            return localTime.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
        }

        // Standard Western weekend is Saturday and Sunday
        return localTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    public bool IsInQuietHours(DateTime localTime, TimeSpan quietStart, TimeSpan quietEnd)
    {
        var tod = localTime.TimeOfDay;
        if (quietStart > quietEnd)
        {
            // Wraps past midnight (e.g., 21:00 to 08:30)
            return tod >= quietStart || tod < quietEnd;
        }

        // Same day window (e.g., 01:00 to 06:00)
        return tod >= quietStart && tod < quietEnd;
    }

    public DateTime CalculateNextSafeDispatchUtc(
        DateTime currentUtc,
        string timezoneId,
        TimeSpan quietStart,
        TimeSpan quietEnd,
        bool excludeWeekends = true)
    {
        var localTime = ConvertUtcToLocal(currentUtc, timezoneId);
        var inQuiet = IsInQuietHours(localTime, quietStart, quietEnd);
        var isWknd = excludeWeekends && IsWeekend(localTime, timezoneId);

        if (!inQuiet && !isWknd)
        {
            return DateTime.SpecifyKind(currentUtc, DateTimeKind.Utc);
        }

        // Advance local time forward until safe
        var candidate = localTime;

        if (inQuiet)
        {
            if (candidate.TimeOfDay < quietEnd)
            {
                // In morning quiet hours: advance to quietEnd today
                candidate = candidate.Date.Add(quietEnd);
            }
            else
            {
                // In evening quiet hours: advance to quietEnd tomorrow morning
                candidate = candidate.Date.AddDays(1).Add(quietEnd);
            }
        }
        else if (isWknd)
        {
            // If already on weekend during day, align time to quietEnd or current time
            if (candidate.TimeOfDay < quietEnd)
            {
                candidate = candidate.Date.Add(quietEnd);
            }
        }

        // Advance through any weekend days to the next safe business day
        while (excludeWeekends && IsWeekend(candidate, timezoneId))
        {
            candidate = candidate.Date.AddDays(1).Add(quietEnd);
        }

        var safeUtc = ConvertLocalToUtc(candidate, timezoneId);
        return DateTime.SpecifyKind(safeUtc, DateTimeKind.Utc);
    }

    public PacingEvaluationResult EvaluatePacing(
        DateTime currentUtc,
        string? phoneNumber = null,
        double? latitude = null,
        double? longitude = null,
        string? overrideTimezone = null,
        TimeSpan? quietStart = null,
        TimeSpan? quietEnd = null,
        bool excludeWeekends = true)
    {
        var qStart = quietStart ?? new TimeSpan(21, 0, 0); // 21:00 (9 PM)
        var qEnd = quietEnd ?? new TimeSpan(8, 30, 0);     // 08:30 (8:30 AM)

        var tz = overrideTimezone;
        if (string.IsNullOrWhiteSpace(tz))
        {
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                tz = InferTimezoneFromPhoneNumber(phoneNumber);
            }
            else if (latitude.HasValue && longitude.HasValue)
            {
                tz = InferTimezoneFromCoordinates(latitude.Value, longitude.Value);
            }
            else
            {
                tz = "UTC";
            }
        }

        var localTime = ConvertUtcToLocal(currentUtc, tz);
        var inQuiet = IsInQuietHours(localTime, qStart, qEnd);
        var isWknd = IsWeekend(localTime, tz);
        var canDispatch = !inQuiet && (!excludeWeekends || !isWknd);

        var nextSafe = CalculateNextSafeDispatchUtc(currentUtc, tz, qStart, qEnd, excludeWeekends);

        var reason = canDispatch
            ? "Recipient is in an active safe window."
            : inQuiet && isWknd && excludeWeekends
                ? $"Blocked: Recipient local time ({localTime:t}) is in quiet hours and on weekend ({localTime.DayOfWeek})."
                : inQuiet
                    ? $"Blocked: Recipient local time ({localTime:t}) is in quiet hours ({qStart:hh\\:mm} - {qEnd:hh\\:mm})."
                    : $"Blocked: Recipient is in weekend ({localTime.DayOfWeek}) in timezone {tz}.";

        return new PacingEvaluationResult(
            TimezoneId: tz,
            RecipientLocalTime: localTime,
            IsInQuietHours: inQuiet,
            IsWeekend: isWknd,
            CanDispatchNow: canDispatch,
            NextSafeDispatchUtc: nextSafe,
            Reason: reason
        );
    }

    public static TimeZoneInfo ResolveTimeZone(string timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId) || timezoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows ID vs IANA fallback mappings
            var fallbackId = timezoneId switch
            {
                "America/New_York" => "Eastern Standard Time",
                "America/Chicago" => "Central Standard Time",
                "America/Denver" => "Mountain Standard Time",
                "America/Los_Angeles" => "Pacific Standard Time",
                "Africa/Algiers" => "W. Central Africa Standard Time",
                "Africa/Tunis" => "W. Central Africa Standard Time",
                "Africa/Casablanca" => "Morocco Standard Time",
                "Africa/Cairo" => "Egypt Standard Time",
                "Asia/Dubai" => "Arabian Standard Time",
                "Asia/Riyadh" => "Arab Standard Time",
                "Asia/Qatar" => "Arab Standard Time",
                "Asia/Kuwait" => "Arab Standard Time",
                "Asia/Muscat" => "Arabian Standard Time",
                "Asia/Bahrain" => "Arab Standard Time",
                "Asia/Amman" => "Jordan Standard Time",
                "Asia/Beirut" => "Middle East Standard Time",
                "Asia/Baghdad" => "Arabic Standard Time",
                "Europe/London" => "GMT Standard Time",
                "Europe/Paris" => "Romance Standard Time",
                "Europe/Berlin" => "W. Europe Standard Time",
                "Europe/Madrid" => "Romance Standard Time",
                "Europe/Rome" => "W. Europe Standard Time",
                "Europe/Amsterdam" => "W. Europe Standard Time",
                "Europe/Zurich" => "W. Europe Standard Time",
                "Europe/Brussels" => "Romance Standard Time",
                "Asia/Tokyo" => "Tokyo Standard Time",
                "Asia/Shanghai" => "China Standard Time",
                "Asia/Singapore" => "Singapore Standard Time",
                "Asia/Kolkata" => "India Standard Time",
                "Australia/Sydney" => "AUS Eastern Standard Time",
                "America/Sao_Paulo" => "E. South America Standard Time",
                "Africa/Johannesburg" => "South Africa Standard Time",
                "Europe/Istanbul" => "Turkey Standard Time",
                _ => null
            };

            if (fallbackId != null)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(fallbackId);
                }
                catch
                {
                    // Ignore and fall through to fallback
                }
            }

            // Offset-based approximation if standard lookup fails
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTime ConvertUtcToLocal(DateTime currentUtc, string timezoneId)
    {
        var utc = currentUtc.Kind == DateTimeKind.Utc ? currentUtc : DateTime.SpecifyKind(currentUtc, DateTimeKind.Utc);
        var tz = ResolveTimeZone(timezoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
    }

    private static DateTime ConvertLocalToUtc(DateTime localTime, string timezoneId)
    {
        var tz = ResolveTimeZone(timezoneId);
        var unspec = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspec, tz);
    }
}
