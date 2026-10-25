namespace SGrowing.Core.Entities;

public class AppointmentSlot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 30;
    public string HostAgentName { get; set; } = "Sales Specialist";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private AppointmentSlot() { }

    public static AppointmentSlot Create(
        Guid tenantId,
        string title,
        int durationMinutes = 30,
        string hostAgentName = "Sales Specialist")
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

        return new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            DurationMinutes = durationMinutes,
            HostAgentName = hostAgentName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
