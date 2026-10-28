namespace SGrowing.Core.Entities;

public class TimesheetEntryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid TeamMemberId { get; set; }
    public string TeamMemberName { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public Guid? DealId { get; set; }
    public string? DealTitle { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal HoursWorked { get; set; }
    public decimal HourlyRate { get; set; } = 25.00m;
    public decimal TotalLaborCost { get; set; }
    public string Status { get; set; } = "Submitted"; // Draft, Submitted, Approved, Rejected
    public DateTime WorkDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
}

public record LogTimecardRequest(
    Guid TeamMemberId,
    string TeamMemberName,
    Guid? TaskId,
    string? TaskTitle,
    Guid? DealId,
    string? DealTitle,
    string Description,
    decimal HoursWorked,
    decimal HourlyRate = 25.00m,
    DateTime? WorkDateUtc = null);

public record ApproveTimecardRequest(
    string ApproverName);

public record TimesheetSummaryDto(
    Guid TenantId,
    Guid? DealId,
    decimal TotalHoursLogged,
    decimal TotalApprovedHours,
    decimal TotalLaborCost,
    int TotalEntriesCount,
    int ApprovedEntriesCount,
    List<TimesheetEntryRecord> Entries);
