using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SGrowing.Core.Diagnostics;
using StackExchange.Redis;

namespace SGrowing.Api.Modules.Diagnostics;

public class DaemonHeartbeatService : IDaemonHeartbeatService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<DaemonHeartbeatService>? _logger;

    // In-memory fallback dictionary shared across scopes
    private static readonly ConcurrentDictionary<string, DateTime> _inMemoryHeartbeats = new();

    public static void ResetHeartbeatsForTesting() => _inMemoryHeartbeats.Clear();
    public static void SetHeartbeatForTesting(string daemonName, DateTime timestampUtc) => _inMemoryHeartbeats[daemonName] = timestampUtc;

    public static readonly string[] DefaultMonitoredDaemons =
    [
        "P2PWarmupRescueDaemon",
        "AutonomousSdrTickDaemon",
        "DurableSocialPublishingDaemon",
        "DealRottingWatchdogDaemon",
        "AutonomousAgentExecutionDaemon",
        "OutwardWebhookDeliveryDaemon"
    ];

    public IReadOnlyList<string> MonitoredDaemonNames => DefaultMonitoredDaemons;

    public DaemonHeartbeatService(
        IConnectionMultiplexer? redis = null,
        ILogger<DaemonHeartbeatService>? logger = null)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task RecordHeartbeatAsync(string daemonName, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        _inMemoryHeartbeats[daemonName] = now;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = $"daemon:heartbeat:{daemonName}";
                var payload = JsonSerializer.Serialize(new
                {
                    daemonName,
                    timestampUtc = now
                });
                await db.StringSetAsync(key, payload, TimeSpan.FromHours(2));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to write heartbeat for {DaemonName} to Redis. Using in-memory record.", daemonName);
            }
        }

        _logger?.LogDebug("Heartbeat recorded for daemon {DaemonName} at {Timestamp}", daemonName, now);
    }

    public async Task<DaemonHeartbeatStatusDto> GetDaemonStatusAsync(string daemonName, CancellationToken cancellationToken = default)
    {
        DateTime lastHeartbeat = DateTime.MinValue;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = $"daemon:heartbeat:{daemonName}";
                var val = await db.StringGetAsync(key);
                if (!val.IsNullOrEmpty)
                {
                    using var doc = JsonDocument.Parse(val.ToString());
                    if (doc.RootElement.TryGetProperty("timestampUtc", out var tsProp) &&
                        tsProp.TryGetDateTime(out var dt))
                    {
                        lastHeartbeat = dt;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed reading Redis heartbeat for {DaemonName}", daemonName);
            }
        }

        if (lastHeartbeat == DateTime.MinValue && _inMemoryHeartbeats.TryGetValue(daemonName, out var memTs))
        {
            lastHeartbeat = memTs;
        }

        var now = DateTime.UtcNow;
        if (lastHeartbeat == DateTime.MinValue)
        {
            return new DaemonHeartbeatStatusDto(
                DaemonName: daemonName,
                LastHeartbeatUtc: DateTime.MinValue,
                Status: "Unhealthy",
                MissedTicks: 3,
                IsAlive: false,
                SecondsSinceLastHeartbeat: -1,
                Message: "No heartbeat has been recorded since startup."
            );
        }

        var elapsedSeconds = Math.Max(0.0, (now - lastHeartbeat).TotalSeconds);

        // Watchdog evaluation:
        // Expected daemon tick ~20-30s.
        // 3 consecutive missed ticks corresponds to >= 90s elapsed.
        if (elapsedSeconds > 90)
        {
            int missed = (int)(elapsedSeconds / 30);
            return new DaemonHeartbeatStatusDto(
                DaemonName: daemonName,
                LastHeartbeatUtc: lastHeartbeat,
                Status: "Unhealthy",
                MissedTicks: Math.Max(3, missed),
                IsAlive: false,
                SecondsSinceLastHeartbeat: elapsedSeconds,
                Message: $"Daemon missed {Math.Max(3, missed)} consecutive ticks (elapsed {elapsedSeconds:F1}s)."
            );
        }
        else if (elapsedSeconds > 35)
        {
            return new DaemonHeartbeatStatusDto(
                DaemonName: daemonName,
                LastHeartbeatUtc: lastHeartbeat,
                Status: "Degraded",
                MissedTicks: 1,
                IsAlive: true,
                SecondsSinceLastHeartbeat: elapsedSeconds,
                Message: $"Daemon delayed ({elapsedSeconds:F1}s since last heartbeat)."
            );
        }

        return new DaemonHeartbeatStatusDto(
            DaemonName: daemonName,
            LastHeartbeatUtc: lastHeartbeat,
            Status: "Healthy",
            MissedTicks: 0,
            IsAlive: true,
            SecondsSinceLastHeartbeat: elapsedSeconds,
            Message: "Daemon is running normally."
        );
    }

    public async Task<DaemonsHealthReportDto> GetHealthReportAsync(CancellationToken cancellationToken = default)
    {
        var dict = new Dictionary<string, DaemonHeartbeatStatusDto>();
        int healthy = 0;
        int unhealthy = 0;

        foreach (var daemon in DefaultMonitoredDaemons)
        {
            var status = await GetDaemonStatusAsync(daemon, cancellationToken);
            dict[daemon] = status;
            if (status.Status == "Healthy") healthy++;
            else unhealthy++;
        }

        string overallStatus = unhealthy == 0 ? "Healthy" : (healthy > 0 ? "Degraded" : "Unhealthy");

        return new DaemonsHealthReportDto(
            OverallStatus: overallStatus,
            TimestampUtc: DateTime.UtcNow,
            TotalMonitored: DefaultMonitoredDaemons.Length,
            HealthyCount: healthy,
            UnhealthyCount: unhealthy,
            Daemons: dict
        );
    }

    public async Task<List<string>> CheckHeartbeatsAndAlertAsync(CancellationToken cancellationToken = default)
    {
        var alerts = new List<string>();

        foreach (var daemon in DefaultMonitoredDaemons)
        {
            var status = await GetDaemonStatusAsync(daemon, cancellationToken);
            if (status.MissedTicks >= 3 || status.Status == "Unhealthy")
            {
                var alert = $"[DAEMON WATCHDOG ALERT] {daemon} missed {status.MissedTicks} consecutive ticks (last seen: {status.LastHeartbeatUtc:O})";
                alerts.Add(alert);
                _logger?.LogError("{Alert}", alert);
            }
        }

        return alerts;
    }
}
