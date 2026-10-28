using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Personas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SGrowing.Worker.BackgroundJobs;

public class AgentHeartbeatJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentHeartbeatJob> _logger;
    private readonly TimeSpan _cycleInterval;

    public AgentHeartbeatJob(
        IServiceProvider serviceProvider,
        ILogger<AgentHeartbeatJob> logger,
        TimeSpan? cycleInterval = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _cycleInterval = cycleInterval ?? TimeSpan.FromMinutes(15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentHeartbeatJob started. Executing dual-layer persona fleet heartbeat every {Interval}m.",
            _cycleInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunHeartbeatOnceAsync(null, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during persona fleet heartbeat cycle.");
            }

            try
            {
                await Task.Delay(_cycleInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("AgentHeartbeatJob stopping.");
    }

    public async Task<int> RunHeartbeatOnceAsync(Guid? targetTenantId = null, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
        var personaService = scope.ServiceProvider.GetService<IAgentPersonaService>();

        if (personaService == null)
        {
            _logger.LogWarning("IAgentPersonaService is not registered in service provider. Skipping cycle.");
            return 0;
        }

        List<Guid> tenantIds;
        if (targetTenantId.HasValue)
        {
            tenantIds = new List<Guid> { targetTenantId.Value };
        }
        else
        {
            tenantIds = await db.Tenants
                .Select(t => t.Id)
                .ToListAsync(ct);

            if (tenantIds.Count == 0)
            {
                // Fallback to default system tenant ID
                tenantIds.Add(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            }
        }

        var executedCount = 0;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                var cycleResult = await personaService.RunHeartbeatCycleAsync(tenantId, ct);
                executedCount++;

                _logger.LogInformation(
                    "Fleet Heartbeat complete for tenant {TenantId}. Evaluated: {Count}, Reports: {Reports}, Anomalies: {Anomalies}",
                    tenantId,
                    cycleResult.AgentsEvaluatedCount,
                    cycleResult.Reports.Count,
                    cycleResult.AnomaliesDetectedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run fleet heartbeat for tenant {TenantId}.", tenantId);
            }
        }

        return executedCount;
    }
}
