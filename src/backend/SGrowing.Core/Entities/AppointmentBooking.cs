namespace SGrowing.Core.Entities;

public enum BookingStatus
{
    Confirmed = 1,
    Rescheduled = 2,
    Cancelled = 3
}

public class AppointmentBooking
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SlotId { get; private set; }
    public Guid ContactId { get; private set; }
    public DateTime ScheduledTimeUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string MeetingUrl { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private AppointmentBooking() { }

    public static AppointmentBooking Create(
        Guid tenantId,
        Guid slotId,
        Guid contactId,
        DateTime scheduledTimeUtc)
    {
        var id = Guid.NewGuid();
        return new AppointmentBooking
        {
            Id = id,
            TenantId = tenantId,
            SlotId = slotId,
            ContactId = contactId,
            ScheduledTimeUtc = scheduledTimeUtc,
            Status = BookingStatus.Confirmed,
            MeetingUrl = $"https://meet.sgrowing.com/room/{id.ToString().Substring(0, 8)}",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
