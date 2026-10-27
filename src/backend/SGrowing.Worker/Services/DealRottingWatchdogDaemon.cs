using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Diagnostics;

namespace SGrowing.Worker.Services;

public class DealRottingWatchdogDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DealRottingWatchdogDaemon>? _logger;
    private readonly IDaemonHeartbeatService? _heartbeatService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);

    public DealRottingWatchdogDaemon(
        IServiceProvider serviceProvider,
        ILogger<DealRottingWatchdogDaemon>? logger = null,
        IDaemonHeartbeatService? heartbeatService = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _heartbeatService = heartbeatService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("DealRottingWatchdogDaemon background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = _heartbeatService ?? _serviceProvider.GetService<IDaemonHeartbeatService>();
                if (heartbeat != null)
                {
                    await heartbeat.RecordHeartbeatAsync("DealRottingWatchdogDaemon", stoppingToken);
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await ProcessRottenDealsAsync(db, 7, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing pipeline deal rotting checks.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> ProcessRottenDealsAsync(SGrowingDbContext db, int thresholdDays = 7, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-thresholdDays);

        var stagnatingDeals = await db.Deals
            .Where(d => d.Stage != DealStage.Won && d.Stage != DealStage.Lost && d.CreatedAtUtc <= cutoff)
            .Take(20)
            .ToListAsync(cancellationToken);

        int flagged = 0;

        foreach (var deal in stagnatingDeals)
        {
            if (string.IsNullOrWhiteSpace(deal.Notes) || !deal.Notes.Contains("[ROTTEN DEAL ALERT]"))
            {
                deal.Notes = $"[ROTTEN DEAL ALERT] Deal has stagnated in {deal.Stage} for over {thresholdDays} days. Action required.\n{deal.Notes ?? ""}".Trim();
                flagged++;
            }
        }

        if (flagged > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return flagged;
    }
}
