using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Diagnostics;

namespace SGrowing.Worker.Services;

public class AutonomousSdrTickDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutonomousSdrTickDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(20);

    public AutonomousSdrTickDaemon(
        IServiceProvider serviceProvider,
        ILogger<AutonomousSdrTickDaemon>? logger = null,
        IDaemonHeartbeatService? heartbeatService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _heartbeatService = heartbeatService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("AutonomousSdrTickDaemon background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("AutonomousSdrTickDaemon", stoppingToken);
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await ProcessSdrTicksAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing outbound SDR slot scheduling.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> ProcessSdrTicksAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var dueEnrollments = await db.ProspectEnrollments
            .Where(p => p.Status == EnrollmentStatus.Active && p.NextRunAtUtc <= DateTime.UtcNow)
            .Take(25)
            .ToListAsync(cancellationToken);

        int processed = 0;

        foreach (var enrollment in dueEnrollments)
        {
            var nextStep = enrollment.CurrentStepNumber + 1;
            if (nextStep > 4)
            {
                enrollment.Complete();
            }
            else
            {
                enrollment.AdvanceStep(nextStep, 2);
            }

            processed++;
        }

        if (processed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }
}
