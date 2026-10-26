namespace SGrowing.Core.Entities;

public class TelemetryEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string VisitorId { get; set; } = string.Empty;
    public string EventName { get; set; } = "pageview"; // pageview, add_to_cart, begin_checkout, purchase
    public string Url { get; set; } = string.Empty;
    public string Referrer { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "Desktop"; // Desktop, Mobile, Tablet
    public string Browser { get; set; } = "Chrome";
    public string CountryCode { get; set; } = "TN";
    public string City { get; set; } = "Tunis";
    public string PropertiesJson { get; set; } = "{}";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public static TelemetryEvent Create(
        Guid tenantId,
        string sessionId,
        string visitorId,
        string eventName,
        string url,
        string referrer = "",
        string deviceType = "Desktop",
        string browser = "Chrome",
        string countryCode = "TN",
        string city = "Tunis",
        string propertiesJson = "{}")
    {
        return new TelemetryEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId.Trim(),
            VisitorId = visitorId.Trim(),
            EventName = eventName.Trim(),
            Url = url.Trim(),
            Referrer = referrer.Trim(),
            DeviceType = deviceType.Trim(),
            Browser = browser.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            City = city.Trim(),
            PropertiesJson = propertiesJson,
            TimestampUtc = DateTime.UtcNow
        };
    }
}

public class VisitorSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string VisitorId { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "TN";
    public string City { get; set; } = "Tunis";
    public string DeviceType { get; set; } = "Desktop";
    public string Browser { get; set; } = "Chrome";
    public string InitialReferrer { get; set; } = "";
    public int PagesViewedCount { get; set; } = 1;
    public int PageViewsCount { get => PagesViewedCount; set => PagesViewedCount = value; }
    public int SessionDurationSeconds { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAtUtc { get; set; } = DateTime.UtcNow;

    public static VisitorSession Create(
        Guid tenantId,
        string sessionId,
        string visitorId,
        string countryCode = "TN",
        string city = "Tunis",
        string deviceType = "Desktop",
        string browser = "Chrome",
        string initialReferrer = "")
    {
        return new VisitorSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = sessionId.Trim(),
            VisitorId = visitorId.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            City = city.Trim(),
            DeviceType = deviceType.Trim(),
            Browser = browser.Trim(),
            InitialReferrer = initialReferrer.Trim(),
            PagesViewedCount = 1,
            SessionDurationSeconds = 0,
            IsActive = true,
            StartedAtUtc = DateTime.UtcNow,
            LastActiveAtUtc = DateTime.UtcNow
        };
    }
}
