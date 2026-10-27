using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class DripCampaignCadenceDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DripCampaignCadenceDaemon>? _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(15);

    public DripCampaignCadenceDaemon(IServiceProvider serviceProvider, ILogger<DripCampaignCadenceDaemon>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("DripCampaignCadenceDaemon background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await ProcessDueCadencesAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing due sales outreach cadences.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> ProcessDueCadencesAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // 1. Process Due Sales Cadence Leads
        var dueEnrollments = await db.SalesCadenceLeadEnrollments
            .Where(e => e.State == CadenceEnrollmentState.Active && e.NextExecutionAtUtc <= now)
            .ToListAsync(cancellationToken);

        int processed = 0;

        foreach (var enrollment in dueEnrollments)
        {
            var nextStepNumber = enrollment.CurrentStepNumber + 1;
            var nextStep = await db.SalesCadenceStepItems
                .Where(s => s.CadenceId == enrollment.CadenceId && s.StepNumber == nextStepNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextStep != null)
            {
                // Create SDR Task item
                var sdrTask = new SdrTaskItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = enrollment.TenantId,
                    EnrollmentId = enrollment.Id,
                    CadenceId = enrollment.CadenceId,
                    LeadName = enrollment.LeadName,
                    PhoneNumber = enrollment.PhoneNumber,
                    Email = enrollment.Email,
                    Company = enrollment.Company,
                    TaskType = nextStep.Channel == CadenceChannel.Email ? "EmailDispatch" : "PhoneCall",
                    Instruction = nextStep.BodyTemplate,
                    DueAtUtc = now,
                    IsCompleted = false
                };
                db.SdrTaskItems.Add(sdrTask);

                enrollment.CurrentStepNumber = nextStepNumber;
                enrollment.NextExecutionAtUtc = now.AddDays(Math.Max(1, nextStep.DayOffset));
                enrollment.LastExecutedAtUtc = now;
                processed++;
            }
            else
            {
                // Reached the end of cadence
                enrollment.State = CadenceEnrollmentState.Completed;
                enrollment.LastExecutedAtUtc = now;
                processed++;
            }
        }

        // 2. Process Mailbox Warmup Exchanges
        var activeWarmups = await db.MailboxWarmupCadences
            .Where(w => w.IsActive && w.CurrentDailyVolume < w.DailyTargetLimit)
            .ToListAsync(cancellationToken);

        foreach (var warmup in activeWarmups)
        {
            warmup.CurrentDailyVolume++;
            warmup.TotalSimulatedSent++;
            db.WarmupExchangeLogs.Add(WarmupExchangeLog.Create(
                warmup.TenantId,
                warmup.Id,
                $"peer-{Guid.NewGuid():N}.sandbox@warmbly.internal",
                "Sent"
            ));
            processed++;
        }

        if (processed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }
}
