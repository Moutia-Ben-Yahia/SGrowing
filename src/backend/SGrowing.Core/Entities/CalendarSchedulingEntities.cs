namespace SGrowing.Core.Entities;

public enum CalendarBookingStatus
{
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4,
    Confirmed = 5
}

public class BookingEventType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 30; // 15, 30, 45, 60
    public int BufferBeforeMinutes { get; set; } = 5;
    public int BufferAfterMinutes { get; set; } = 10;
    public int MinimumNoticeHours { get; set; } = 2;
    public bool IsRoundRobin { get; set; } = false;
    public string AssignedHostIdsJson { get; set; } = "[]"; // List<Guid>
    public string HostWeightsJson { get; set; } = "{}"; // Dictionary<string, double>
    public string ColorHex { get; set; } = "#6366f1"; // Indigo
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Dictionary<Guid, double> GetHostWeights()
    {
        if (string.IsNullOrWhiteSpace(HostWeightsJson) || HostWeightsJson == "{}")
            return new Dictionary<Guid, double>();
        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(HostWeightsJson);
            if (dict == null) return new Dictionary<Guid, double>();
            return dict.Where(kvp => Guid.TryParse(kvp.Key, out _))
                       .ToDictionary(kvp => Guid.Parse(kvp.Key), kvp => kvp.Value);
        }
        catch
        {
            return new Dictionary<Guid, double>();
        }
    }

    public void SetHostWeights(Dictionary<Guid, double> weights)
    {
        var dict = weights.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
        HostWeightsJson = System.Text.Json.JsonSerializer.Serialize(dict);
    }

    public static BookingEventType Create(
        Guid tenantId,
        string title,
        string slug,
        string description = "",
        int durationMinutes = 30,
        bool isRoundRobin = false,
        string? colorHex = "#6366f1",
        List<Guid>? hostIds = null,
        Dictionary<Guid, double>? hostWeights = null)
    {
        var hosts = hostIds != null 
            ? System.Text.Json.JsonSerializer.Serialize(hostIds) 
            : "[]";

        var weightsJson = hostWeights != null
            ? System.Text.Json.JsonSerializer.Serialize(hostWeights.ToDictionary(k => k.Key.ToString(), v => v.Value))
            : "{}";

        return new BookingEventType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description.Trim(),
            DurationMinutes = durationMinutes,
            IsRoundRobin = isRoundRobin,
            ColorHex = colorHex ?? "#6366f1",
            AssignedHostIdsJson = hosts,
            HostWeightsJson = weightsJson,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class HostAvailabilitySchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid HostUserId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "Africa/Tunis";
    public string WeeklyHoursJson { get; set; } = "[]"; // List of DayAvailability
    public string BlackoutDatesJson { get; set; } = "[]"; // List of "yyyy-MM-dd"
    public double Weight { get; set; } = 1.0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static HostAvailabilitySchedule Create(
        Guid tenantId,
        Guid hostUserId,
        string hostName,
        string timeZone = "Africa/Tunis",
        string? weeklyHoursJson = null,
        double weight = 1.0)
    {
        var defaultHours = weeklyHoursJson ?? System.Text.Json.JsonSerializer.Serialize(new List<DayAvailability>
        {
            new("Monday", "09:00", "18:00"),
            new("Tuesday", "09:00", "18:00"),
            new("Wednesday", "09:00", "18:00"),
            new("Thursday", "09:00", "18:00"),
            new("Friday", "09:00", "18:00")
        });

        return new HostAvailabilitySchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            HostUserId = hostUserId,
            HostName = hostName.Trim(),
            TimeZone = timeZone,
            WeeklyHoursJson = defaultHours,
            BlackoutDatesJson = "[]",
            Weight = weight > 0 ? weight : 1.0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class DayAvailability
{
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;
    public string StartTime { get; set; } = "09:00";
    public string EndTime { get; set; } = "18:00";
    public bool IsActive { get; set; } = true;

    public DayAvailability() { }

    public DayAvailability(string dayOfWeek, string startTime, string endTime, bool isActive = true)
    {
        if (Enum.TryParse<DayOfWeek>(dayOfWeek, true, out var dow))
            DayOfWeek = dow;
        StartTime = startTime;
        EndTime = endTime;
        IsActive = isActive;
    }
}

public class CalendarBookingRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EventTypeId { get; set; }
    public Guid HostUserId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string AttendeeName { get; set; } = string.Empty;
    public string AttendeeEmail { get; set; } = string.Empty;
    public string AttendeePhone { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string TimeZone { get; set; } = "Africa/Tunis";
    public CalendarBookingStatus Status { get; set; } = CalendarBookingStatus.Scheduled;
    public string MeetingUrl { get; set; } = string.Empty;
    public string IcsUid { get; set; } = Guid.NewGuid().ToString();
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CalendarBookingRecord Create(
        Guid tenantId,
        Guid eventTypeId,
        Guid hostUserId,
        string hostName,
        string attendeeName,
        string attendeeEmail,
        string attendeePhone,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        string timeZone = "Africa/Tunis",
        string notes = "")
    {
        var id = Guid.NewGuid();
        return new CalendarBookingRecord
        {
            Id = id,
            TenantId = tenantId,
            EventTypeId = eventTypeId,
            HostUserId = hostUserId,
            HostName = hostName.Trim(),
            AttendeeName = attendeeName.Trim(),
            AttendeeEmail = attendeeEmail.Trim().ToLowerInvariant(),
            AttendeePhone = attendeePhone.Trim(),
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            TimeZone = timeZone,
            Status = CalendarBookingStatus.Scheduled,
            MeetingUrl = $"https://meet.sgrowing.io/room/{id.ToString()[..8]}",
            IcsUid = $"{id}@sgrowing.io",
            Notes = notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
