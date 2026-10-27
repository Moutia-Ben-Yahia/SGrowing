using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Core.Diagnostics;

namespace SGrowing.Worker.Services;

public class DaemonWatchdogService : BackgroundService
{
    private readonly IDaemonHeartbeatService _heartbeatService;
    private readonly ILogger<DaemonWatchdogService>? _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

    public DaemonWatchdogService(
        IDaemonHeartbeatService heartbeatService,
        ILogger<DaemonWatchdogService>? logger = null)
    {
        _heartbeatService = heartbeatService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("DaemonWatchdogService started monitoring daemons every 10 seconds.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var alerts = await _heartbeatService.CheckHeartbeatsAndAlertAsync(stoppingToken);
                if (alerts.Count > 0)
                {
                    _logger?.LogWarning("DaemonWatchdogService detected {AlertCount} daemon health breaches.", alerts.Count);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error occurred during daemon watchdog heartbeat sweep.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
