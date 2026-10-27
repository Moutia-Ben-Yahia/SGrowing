using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.Telemetry;

public record PartitionInfo(string TableName, string PartitionName, DateTime FromUtc, DateTime ToUtc, bool IsDefault = false);

public interface IDatabasePartitioningService
{
    string GenerateCreatePartitionDdl(string baseTable, DateTime month);
    List<PartitionInfo> GetPreconfiguredPartitions(string baseTable, DateTime startMonth, int monthsAhead = 3);
    Task<int> EnsureMonthlyPartitionsAsync(DateTime month, CancellationToken cancellationToken = default);
}

public class DatabasePartitioningService : IDatabasePartitioningService
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<DatabasePartitioningService>? _logger;

    public static readonly string[] PartitionedTables =
    [
        "TelemetryEvents",
        "MessageLogs",
        "WarmupExchangeLogs",
        "AuditLogs"
    ];

    public DatabasePartitioningService(SGrowingDbContext db, ILogger<DatabasePartitioningService>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public string GenerateCreatePartitionDdl(string baseTable, DateTime month)
    {
        var startOfMonth = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1);
        var partitionName = $"{baseTable}_{startOfMonth:yyyy_MM}";

        return $"""
            CREATE TABLE IF NOT EXISTS "{partitionName}" PARTITION OF "{baseTable}_Partitioned"
                FOR VALUES FROM ('{startOfMonth:yyyy-MM-dd HH:mm:ss}+00') TO ('{endOfMonth:yyyy-MM-dd HH:mm:ss}+00');
            """;
    }

    public List<PartitionInfo> GetPreconfiguredPartitions(string baseTable, DateTime startMonth, int monthsAhead = 3)
    {
        var list = new List<PartitionInfo>();
        var current = new DateTime(startMonth.Year, startMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < monthsAhead; i++)
        {
            var from = current.AddMonths(i);
            var to = from.AddMonths(1);
            var name = $"{baseTable}_{from:yyyy_MM}";
            list.Add(new PartitionInfo(baseTable, name, from, to));
        }

        list.Add(new PartitionInfo(baseTable, $"{baseTable}_Default", DateTime.MinValue, DateTime.MaxValue, IsDefault: true));
        return list;
    }

    public async Task<int> EnsureMonthlyPartitionsAsync(DateTime month, CancellationToken cancellationToken = default)
    {
        int createdCount = 0;

        foreach (var table in PartitionedTables)
        {
            var ddl = GenerateCreatePartitionDdl(table, month);
            _logger?.LogInformation("Ensuring PostgreSQL partition: {Table} for {Month:yyyy-MM}", table, month);

            try
            {
                if (_db.Database.IsRelational())
                {
                    await _db.Database.ExecuteSqlRawAsync(ddl, cancellationToken);
                }
                createdCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Partition DDL execution skipped or already exists for {Table}", table);
            }
        }

        return createdCount;
    }
}
