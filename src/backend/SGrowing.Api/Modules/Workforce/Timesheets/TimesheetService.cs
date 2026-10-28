using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Workforce.Timesheets;

public class TimesheetService : ITimesheetService
{
    private readonly SGrowingDbContext _db;

    public TimesheetService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<TimesheetEntryRecord> LogTimecardAsync(Guid tenantId, LogTimecardRequest req, CancellationToken ct = default)
    {
        decimal rate = req.HourlyRate > 0 ? req.HourlyRate : 25.00m;
        decimal hours = Math.Max(0.1m, req.HoursWorked);
        decimal laborCost = Math.Round(hours * rate, 2);

        var entry = new TimesheetEntryRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TeamMemberId = req.TeamMemberId,
            TeamMemberName = string.IsNullOrWhiteSpace(req.TeamMemberName) ? "Operations Specialist" : req.TeamMemberName.Trim(),
            TaskId = req.TaskId,
            TaskTitle = req.TaskTitle,
            DealId = req.DealId,
            DealTitle = req.DealTitle,
            Description = req.Description?.Trim() ?? string.Empty,
            HoursWorked = hours,
            HourlyRate = rate,
            TotalLaborCost = laborCost,
            Status = "Submitted",
            WorkDateUtc = req.WorkDateUtc ?? DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TimesheetEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<TimesheetEntryRecord?> ApproveTimecardAsync(Guid tenantId, Guid timesheetId, ApproveTimecardRequest req, CancellationToken ct = default)
    {
        var entry = await _db.TimesheetEntries
            .FirstOrDefaultAsync(t => t.Id == timesheetId && t.TenantId == tenantId, ct);

        if (entry == null) return null;

        entry.Status = "Approved";
        entry.ApprovedAtUtc = DateTime.UtcNow;
        entry.ApprovedBy = string.IsNullOrWhiteSpace(req.ApproverName) ? "System Manager" : req.ApproverName.Trim();

        // Feed approved labor directly into True Gross Margin Ledger
        if (entry.DealId.HasValue)
        {
            var dealMargin = await _db.DealMarginRecords
                .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.DealId == entry.DealId.Value, ct);

            if (dealMargin != null)
            {
                dealMargin.OperatorLaborHours += entry.HoursWorked;
                dealMargin.OperatorLaborCost += entry.TotalLaborCost;
                dealMargin.TrueGrossMargin = dealMargin.Revenue - dealMargin.CostOfGoodsSold - dealMargin.CourierShippingFees - dealMargin.OperatorLaborCost;
                dealMargin.GrossMarginPercentage = dealMargin.Revenue > 0
                    ? Math.Round((dealMargin.TrueGrossMargin / dealMargin.Revenue) * 100m, 2)
                    : 0m;
                dealMargin.CalculatedAtUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<IEnumerable<TimesheetEntryRecord>> GetTimesheetsAsync(Guid tenantId, Guid? dealId = null, Guid? teamMemberId = null, string? status = null, CancellationToken ct = default)
    {
        var query = _db.TimesheetEntries.Where(t => t.TenantId == tenantId);

        if (dealId.HasValue)
        {
            query = query.Where(t => t.DealId == dealId.Value);
        }

        if (teamMemberId.HasValue)
        {
            query = query.Where(t => t.TeamMemberId == teamMemberId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        return await query.OrderByDescending(t => t.WorkDateUtc).ToListAsync(ct);
    }

    public async Task<TimesheetSummaryDto> GetTimesheetSummaryAsync(Guid tenantId, Guid? dealId = null, CancellationToken ct = default)
    {
        var entries = await GetTimesheetsAsync(tenantId, dealId, ct: ct);
        var entryList = entries.ToList();

        decimal totalHours = entryList.Sum(e => e.HoursWorked);
        decimal approvedHours = entryList.Where(e => e.Status == "Approved").Sum(e => e.HoursWorked);
        decimal totalLaborCost = entryList.Where(e => e.Status == "Approved").Sum(e => e.TotalLaborCost);

        return new TimesheetSummaryDto(
            TenantId: tenantId,
            DealId: dealId,
            TotalHoursLogged: totalHours,
            TotalApprovedHours: approvedHours,
            TotalLaborCost: totalLaborCost,
            TotalEntriesCount: entryList.Count,
            ApprovedEntriesCount: entryList.Count(e => e.Status == "Approved"),
            Entries: entryList
        );
    }
}
