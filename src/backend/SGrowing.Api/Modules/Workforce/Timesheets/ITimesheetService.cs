using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Workforce.Timesheets;

public interface ITimesheetService
{
    Task<TimesheetEntryRecord> LogTimecardAsync(Guid tenantId, LogTimecardRequest req, CancellationToken ct = default);
    Task<TimesheetEntryRecord?> ApproveTimecardAsync(Guid tenantId, Guid timesheetId, ApproveTimecardRequest req, CancellationToken ct = default);
    Task<IEnumerable<TimesheetEntryRecord>> GetTimesheetsAsync(Guid tenantId, Guid? dealId = null, Guid? teamMemberId = null, string? status = null, CancellationToken ct = default);
    Task<TimesheetSummaryDto> GetTimesheetSummaryAsync(Guid tenantId, Guid? dealId = null, CancellationToken ct = default);
}
