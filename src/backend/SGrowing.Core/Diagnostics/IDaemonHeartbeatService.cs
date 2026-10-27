namespace SGrowing.Core.Diagnostics;

public record DaemonHeartbeatStatusDto(
    string DaemonName,
    DateTime LastHeartbeatUtc,
    string Status, // "Healthy", "Degraded", "Unhealthy"
    int MissedTicks,
    bool IsAlive,
    double SecondsSinceLastHeartbeat,
    string? Message = null
);

public record DaemonsHealthReportDto(
    string OverallStatus,
    DateTime TimestampUtc,
    int TotalMonitored,
    int HealthyCount,
    int UnhealthyCount,
    Dictionary<string, DaemonHeartbeatStatusDto> Daemons
);

public interface IDaemonHeartbeatService
{
    Task RecordHeartbeatAsync(string daemonName, CancellationToken cancellationToken = default);
    Task<DaemonHeartbeatStatusDto> GetDaemonStatusAsync(string daemonName, CancellationToken cancellationToken = default);
    Task<DaemonsHealthReportDto> GetHealthReportAsync(CancellationToken cancellationToken = default);
    Task<List<string>> CheckHeartbeatsAndAlertAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<string> MonitoredDaemonNames { get; }
}
