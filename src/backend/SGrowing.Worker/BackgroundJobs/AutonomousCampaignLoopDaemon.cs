using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Modules.AI;

namespace SGrowing.Worker.BackgroundJobs;

/// <summary>
/// Autonomous Closed-Loop Campaign Execution Daemon.
/// Periodically audits ClickHouse OLAP ROAS metrics and triggers swarm missions to rebalance spend.
/// </summary>
public class AutonomousCampaignLoopDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutonomousCampaignLoopDaemon> _logger;
    private readonly TimeSpan _pollInterval;

    public AutonomousCampaignLoopDaemon(
        IServiceProvider serviceProvider,
        ILogger<AutonomousCampaignLoopDaemon> logger,
        TimeSpan? pollInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromMinutes(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutonomousCampaignLoopDaemon started. Polling ROAS anomalies every {Interval}m.", _pollInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var loopService = scope.ServiceProvider.GetService<IAutonomousCampaignLoopDaemonService>();
                if (loopService != null)
                {
                    // Audit system default tenant
                    var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                    var alerts = await loopService.AuditCampaignRoasAsync(tenantId, 2.5m, stoppingToken);

                    foreach (var alert in alerts)
                    {
                        _logger.LogWarning("Underperforming campaign detected: {Channel} ROAS={Roas:F2}", alert.Channel, alert.CurrentRoas);
                        await loopService.ExecuteInterventionAsync(alert, stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during autonomous campaign loop execution.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
