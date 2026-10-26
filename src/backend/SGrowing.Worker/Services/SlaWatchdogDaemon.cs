using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.Services;

public class SlaWatchdogDaemon : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaWatchdogDaemon> _logger;
    private readonly TimeSpan _checkInterval;

    public SlaWatchdogDaemon(
        IServiceProvider serviceProvider,
        ILogger<SlaWatchdogDaemon> logger,
        TimeSpan? checkInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SlaWatchdogDaemon started. Running breach scanner every {Interval}s.", _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckSlaBreachesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error while checking SLA breaches.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("SlaWatchdogDaemon stopping.");
    }

    public async Task<int> CheckSlaBreachesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();

        var activePolicies = await db.SlaPolicies
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        if (activePolicies.Count == 0)
        {
            return 0;
        }

        var openConversations = await db.Conversations
            .Where(c => c.Status == ConversationStatus.Open && !c.IsSlaBreached)
            .ToListAsync(cancellationToken);

        if (openConversations.Count == 0)
        {
            return 0;
        }

        int breachCount = 0;
        var now = DateTime.UtcNow;

        foreach (var conv in openConversations)
        {
            var policy = activePolicies.FirstOrDefault(p => p.TenantId == conv.TenantId && p.Priority == conv.Priority)
                         ?? activePolicies.FirstOrDefault(p => p.TenantId == conv.TenantId);

            if (policy == null) continue;

            bool isBreached = false;

            // 1. First Response SLA
            if (conv.FirstResponseAtUtc == null)
            {
                var elapsedMinutes = (now - conv.CreatedAtUtc).TotalMinutes;
                if (elapsedMinutes > policy.TargetFirstResponseMinutes)
                {
                    isBreached = true;
                }
            }

            // 2. Resolution SLA
            var totalElapsedMinutes = (now - conv.CreatedAtUtc).TotalMinutes;
            if (totalElapsedMinutes > policy.TargetResolutionMinutes)
            {
                isBreached = true;
            }

            if (isBreached)
            {
                conv.IsSlaBreached = true;
                conv.Priority = ConversationPriority.Urgent;
                conv.UpdatedAtUtc = now;
                breachCount++;

                var outboxEvent = OutboxMessage.Create(
                    conv.TenantId,
                    "SlaBreachedEvent",
                    $"{{\"conversationId\":\"{conv.Id}\",\"tenantId\":\"{conv.TenantId}\",\"targetMinutes\":{policy.TargetFirstResponseMinutes}}}"
                );
                await db.OutboxMessages.AddAsync(outboxEvent, cancellationToken);

                _logger.LogWarning("SLA breach detected for conversation {ConversationId} (Tenant {TenantId}). Escalated to Urgent.",
                    conv.Id, conv.TenantId);
            }
        }

        if (breachCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return breachCount;
    }
}
