using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Worker.Services;

public class ResilientDlqDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ResilientDlqDaemon>? _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public ResilientDlqDaemon(IServiceProvider serviceProvider, ILogger<ResilientDlqDaemon>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public static TimeSpan GetBackoffInterval(int retryCount)
    {
        return retryCount switch
        {
            1 => TimeSpan.FromSeconds(10),
            2 => TimeSpan.FromMinutes(1),
            3 => TimeSpan.FromMinutes(5),
            4 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(2)
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("ResilientDlqDaemon started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
                await ProcessEligibleRetriesAsync(db, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing Dead-Letter Queue retries.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    public static async Task<int> ProcessEligibleRetriesAsync(SGrowingDbContext db, CancellationToken cancellationToken = default)
    {
        var eligibleLogs = await db.WorkflowDeadLetterLogs
            .Where(l => !l.IsResolved && l.RetryCount < 5)
            .ToListAsync(cancellationToken);

        int processed = 0;
        foreach (var log in eligibleLogs)
        {
            var backoff = GetBackoffInterval(log.RetryCount + 1);
            if (DateTime.UtcNow >= log.FailedAtUtc.Add(backoff))
            {
                log.RetryCount++;
                // In production, invoke replay handler
                processed++;
            }
        }

        if (processed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }
}
