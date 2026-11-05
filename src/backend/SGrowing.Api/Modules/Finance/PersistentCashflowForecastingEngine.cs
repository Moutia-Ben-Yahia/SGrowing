using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Finance;

public record CashflowForecastSummary(
    Guid TenantId,
    decimal CurrentProjectedBalance,
    int RunwayDays,
    decimal DailyBurnRate,
    string OverallSeverity,
    IReadOnlyList<CashflowForecastEntity> Trajectory
);

public interface IPersistentCashflowForecastingEngine
{
    Task<CashflowForecastEntity> RecordForecastPointAsync(
        Guid tenantId,
        DateTime forecastDateUtc,
        decimal projectedCashBalance,
        decimal netBurnRate,
        int runwayDays,
        decimal confidenceLower,
        decimal confidenceUpper,
        CancellationToken ct = default);

    Task<CashflowForecastSummary> GetTenantForecastSummaryAsync(
        Guid tenantId,
        int horizonDays = 30,
        CancellationToken ct = default);
}

public class PersistentCashflowForecastingEngine : IPersistentCashflowForecastingEngine
{
    private readonly SGrowingDbContext _db;
    private readonly ILogger<PersistentCashflowForecastingEngine>? _logger;
    private readonly ConcurrentDictionary<string, CashflowForecastEntity> _l1Cache = new();

    public PersistentCashflowForecastingEngine(
        SGrowingDbContext db,
        ILogger<PersistentCashflowForecastingEngine>? logger = null)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CashflowForecastEntity> RecordForecastPointAsync(
        Guid tenantId,
        DateTime forecastDateUtc,
        decimal projectedCashBalance,
        decimal netBurnRate,
        int runwayDays,
        decimal confidenceLower,
        decimal confidenceUpper,
        CancellationToken ct = default)
    {
        string severity = projectedCashBalance switch
        {
            < 0 => "Critical",
            < 50000 => "Warning",
            _ => "Healthy"
        };

        var dateKey = forecastDateUtc.Date;
        var existing = await _db.CashflowForecasts
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.ForecastDateUtc.Date == dateKey, ct);

        if (existing != null)
        {
            existing.ProjectedCashBalance = projectedCashBalance;
            existing.NetBurnRate = netBurnRate;
            existing.RunwayDays = runwayDays;
            existing.ConfidenceIntervalLower = confidenceLower;
            existing.ConfidenceIntervalUpper = confidenceUpper;
            existing.DeficitSeverity = severity;
            existing.CalculatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new CashflowForecastEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ForecastDateUtc = forecastDateUtc,
                ProjectedCashBalance = projectedCashBalance,
                NetBurnRate = netBurnRate,
                RunwayDays = runwayDays,
                ConfidenceIntervalLower = confidenceLower,
                ConfidenceIntervalUpper = confidenceUpper,
                DeficitSeverity = severity,
                CalculatedAtUtc = DateTime.UtcNow
            };
            await _db.CashflowForecasts.AddAsync(existing, ct);
        }

        await _db.SaveChangesAsync(ct);

        // Update L1 Cache
        var cacheKey = $"{tenantId}_{dateKey:yyyyMMdd}";
        _l1Cache[cacheKey] = existing;

        _logger?.LogInformation("Saved cashflow projection for tenant {TenantId} on {Date:d}: Balance={Balance:C}, Runway={Runway}d", tenantId, dateKey, projectedCashBalance, runwayDays);
        return existing;
    }

    public async Task<CashflowForecastSummary> GetTenantForecastSummaryAsync(
        Guid tenantId,
        int horizonDays = 30,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow.Date;
        var maxDate = now.AddDays(horizonDays);

        var points = await _db.CashflowForecasts
            .Where(f => f.TenantId == tenantId && f.ForecastDateUtc >= now && f.ForecastDateUtc <= maxDate)
            .OrderBy(f => f.ForecastDateUtc)
            .ToListAsync(ct);

        if (points.Count == 0)
        {
            // Seed a baseline if empty
            var baseline = await RecordForecastPointAsync(
                tenantId: tenantId,
                forecastDateUtc: now,
                projectedCashBalance: 125000m,
                netBurnRate: 1500m,
                runwayDays: 83,
                confidenceLower: 110000m,
                confidenceUpper: 140000m,
                ct: ct);

            points = new List<CashflowForecastEntity> { baseline };
        }

        var latest = points.Last();
        var lowestBalance = points.Min(p => p.ProjectedCashBalance);
        var avgBurn = points.Average(p => p.NetBurnRate);

        string overallSeverity = lowestBalance switch
        {
            < 0 => "Critical",
            < 50000 => "Warning",
            _ => "Healthy"
        };

        return new CashflowForecastSummary(
            TenantId: tenantId,
            CurrentProjectedBalance: latest.ProjectedCashBalance,
            RunwayDays: latest.RunwayDays,
            DailyBurnRate: Math.Round(avgBurn, 2),
            OverallSeverity: overallSeverity,
            Trajectory: points);
    }
}
