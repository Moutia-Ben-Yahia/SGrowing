using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Hubs;

namespace SGrowing.Api.Modules.Attribution;

public record FunnelStepEvent(
    Guid TenantId,
    string StepName,
    string? VisitorId = null,
    string? SessionId = null,
    decimal? Value = null,
    DateTime? TimestampUtc = null,
    Dictionary<string, string>? Metadata = null
);

public record FunnelStepMetric(
    string StepName,
    int StepOrder,
    long EventCount,
    double DropOffRateFromPrevious,
    double RetentionRateFromTop
);

public record FunnelTransitionMetric(
    string FromStep,
    string ToStep,
    long FromCount,
    long ToCount,
    double CurrentRate,
    double MovingAverage7Day,
    double StandardDeviation,
    double ZScore,
    bool IsSlipping,
    string Status
);

public record FunnelSlippageReport(
    Guid TenantId,
    DateTime GeneratedAtUtc,
    IReadOnlyList<FunnelStepMetric> StepMetrics,
    IReadOnlyList<FunnelTransitionMetric> Transitions,
    IReadOnlyList<FunnelSlippageAlertDto> ActiveAlerts,
    double OverallConversionRate,
    bool HasActiveSlippage
);

public interface IFunnelSlippageMonitorService
{
    Task<FunnelSlippageAlertDto?> IngestStepEventAsync(FunnelStepEvent stepEvent, CancellationToken ct = default);
    Task<FunnelSlippageReport> GetSlippageReportAsync(Guid tenantId, CancellationToken ct = default);
    void SeedHistoricalDailyRates(Guid tenantId, string fromStep, string toStep, IEnumerable<double> dailyRates);
    void ClearTenantData(Guid tenantId);
}

public class FunnelSlippageMonitorService : IFunnelSlippageMonitorService
{
    public static readonly string[] DefaultFunnelSteps = ["ViewItem", "AddToCart", "InitiateCheckout", "SubmitPayment"];

    private readonly IHubContext<OmniHub, IOmniClient>? _hubContext;
    private readonly ILogger<FunnelSlippageMonitorService>? _logger;

    // Concurrent multi-tenant state
    // TenantId -> StepName -> Count
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, long>> _stepCountsByTenant = new();

    // TenantId -> TransitionKey ("ViewItem->AddToCart") -> List of 7 daily historical rates
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, List<double>>> _historicalRatesByTenant = new();

    // TenantId -> List of alerts
    private readonly ConcurrentDictionary<Guid, List<FunnelSlippageAlertDto>> _activeAlertsByTenant = new();

    public FunnelSlippageMonitorService(
        IHubContext<OmniHub, IOmniClient>? hubContext = null,
        ILogger<FunnelSlippageMonitorService>? logger = null)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public void SeedHistoricalDailyRates(Guid tenantId, string fromStep, string toStep, IEnumerable<double> dailyRates)
    {
        var tenantDict = _historicalRatesByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase));
        var key = BuildTransitionKey(fromStep, toStep);
        tenantDict[key] = dailyRates.ToList();
    }

    public void ClearTenantData(Guid tenantId)
    {
        _stepCountsByTenant.TryRemove(tenantId, out _);
        _historicalRatesByTenant.TryRemove(tenantId, out _);
        _activeAlertsByTenant.TryRemove(tenantId, out _);
    }

    public async Task<FunnelSlippageAlertDto?> IngestStepEventAsync(FunnelStepEvent stepEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stepEvent);

        var tenantId = stepEvent.TenantId;
        var stepName = stepEvent.StepName.Trim();

        // Increment count
        var tenantSteps = _stepCountsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase));
        tenantSteps.AddOrUpdate(stepName, 1, (_, current) => current + 1);

        // Check each transition in which this step participates
        FunnelSlippageAlertDto? primaryAlert = null;
        FunnelSlippageAlertDto? secondaryAlert = null;

        for (int i = 0; i < DefaultFunnelSteps.Length - 1; i++)
        {
            var fromStep = DefaultFunnelSteps[i];
            var toStep = DefaultFunnelSteps[i + 1];

            // If this event affects incoming transition (step just completed)
            if (string.Equals(stepName, toStep, StringComparison.OrdinalIgnoreCase))
            {
                var alert = await EvaluateTransitionForSlippageAsync(tenantId, fromStep, toStep, ct);
                if (alert != null)
                {
                    primaryAlert = alert;
                }
            }
            // If this event affects outgoing transition (from this step to next)
            else if (string.Equals(stepName, fromStep, StringComparison.OrdinalIgnoreCase))
            {
                var alert = await EvaluateTransitionForSlippageAsync(tenantId, fromStep, toStep, ct);
                if (alert != null)
                {
                    secondaryAlert = alert;
                }
            }
        }

        return primaryAlert ?? secondaryAlert;
    }

    public Task<FunnelSlippageReport> GetSlippageReportAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenantSteps = _stepCountsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase));

        var stepMetrics = new List<FunnelStepMetric>();
        long topFunnelCount = tenantSteps.GetValueOrDefault(DefaultFunnelSteps[0], 0);
        long prevCount = topFunnelCount;

        for (int i = 0; i < DefaultFunnelSteps.Length; i++)
        {
            var step = DefaultFunnelSteps[i];
            var count = tenantSteps.GetValueOrDefault(step, 0);

            double dropOffFromPrev = 0.0;
            if (i > 0 && prevCount > 0)
            {
                dropOffFromPrev = Math.Max(0.0, Math.Round((1.0 - ((double)count / prevCount)) * 100.0, 2));
            }

            double retentionFromTop = topFunnelCount > 0
                ? Math.Round(((double)count / topFunnelCount) * 100.0, 2)
                : (count > 0 ? 100.0 : 0.0);

            stepMetrics.Add(new FunnelStepMetric(
                StepName: step,
                StepOrder: i + 1,
                EventCount: count,
                DropOffRateFromPrevious: dropOffFromPrev,
                RetentionRateFromTop: retentionFromTop
            ));

            prevCount = count;
        }

        var transitionMetrics = new List<FunnelTransitionMetric>();
        bool hasSlippage = false;

        for (int i = 0; i < DefaultFunnelSteps.Length - 1; i++)
        {
            var fromStep = DefaultFunnelSteps[i];
            var toStep = DefaultFunnelSteps[i + 1];

            var fromCount = tenantSteps.GetValueOrDefault(fromStep, 0);
            var toCount = tenantSteps.GetValueOrDefault(toStep, 0);
            double currentRate = fromCount > 0 ? (double)toCount / fromCount : 0.0;

            var stats = GetTransitionStatistics(tenantId, fromStep, toStep, currentRate);
            if (stats.IsSlipping)
            {
                hasSlippage = true;
            }

            transitionMetrics.Add(new FunnelTransitionMetric(
                FromStep: fromStep,
                ToStep: toStep,
                FromCount: fromCount,
                ToCount: toCount,
                CurrentRate: Math.Round(currentRate, 4),
                MovingAverage7Day: Math.Round(stats.Mean, 4),
                StandardDeviation: Math.Round(stats.StdDev, 4),
                ZScore: Math.Round(stats.ZScore, 2),
                IsSlipping: stats.IsSlipping,
                Status: stats.Status
            ));
        }

        var activeAlerts = _activeAlertsByTenant.TryGetValue(tenantId, out var alerts)
            ? alerts.ToList()
            : [];

        var submitPaymentCount = tenantSteps.GetValueOrDefault(DefaultFunnelSteps[^1], 0);
        double overallConversionRate = topFunnelCount > 0
            ? Math.Round(((double)submitPaymentCount / topFunnelCount) * 100.0, 2)
            : 0.0;

        return Task.FromResult(new FunnelSlippageReport(
            TenantId: tenantId,
            GeneratedAtUtc: DateTime.UtcNow,
            StepMetrics: stepMetrics,
            Transitions: transitionMetrics,
            ActiveAlerts: activeAlerts,
            OverallConversionRate: overallConversionRate,
            HasActiveSlippage: hasSlippage
        ));
    }

    private async Task<FunnelSlippageAlertDto?> EvaluateTransitionForSlippageAsync(
        Guid tenantId,
        string fromStep,
        string toStep,
        CancellationToken ct)
    {
        var tenantSteps = _stepCountsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase));
        var fromCount = tenantSteps.GetValueOrDefault(fromStep, 0);
        var toCount = tenantSteps.GetValueOrDefault(toStep, 0);

        // Require minimum sample size before triggering statistical slippage alert
        if (fromCount < 5)
        {
            return null;
        }

        double currentRate = (double)toCount / fromCount;
        var stats = GetTransitionStatistics(tenantId, fromStep, toStep, currentRate);

        if (stats.IsSlipping)
        {
            var alertId = Guid.NewGuid();
            var alertDto = new FunnelSlippageAlertDto(
                AlertId: alertId,
                TenantId: tenantId,
                StepFrom: fromStep,
                StepTo: toStep,
                CurrentRate: Math.Round(currentRate, 4),
                BaselineMeanRate: Math.Round(stats.Mean, 4),
                StandardDeviation: Math.Round(stats.StdDev, 4),
                ZScore: Math.Round(stats.ZScore, 2),
                Severity: stats.ZScore <= -3.0 ? "Critical" : "Warning",
                Message: $"Funnel conversion slippage detected between '{fromStep}' and '{toStep}'. Current rate {currentRate:P1} is {Math.Abs(stats.ZScore):F1} standard deviations below 7-day baseline {stats.Mean:P1}.",
                DetectedAtUtc: DateTime.UtcNow
            );

            // Store in active alerts
            var alertsList = _activeAlertsByTenant.GetOrAdd(tenantId, _ => new List<FunnelSlippageAlertDto>());
            lock (alertsList)
            {
                alertsList.Add(alertDto);
            }

            // Broadcast via SignalR OmniHub
            if (_hubContext != null)
            {
                try
                {
                    await _hubContext.Clients.Group(OmniHub.GetTenantGroup(tenantId))
                        .OnFunnelSlippageAlert(alertDto);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to broadcast FunnelSlippageAlert via OmniHub for tenant {TenantId}", tenantId);
                }
            }

            _logger?.LogWarning("Triggered Funnel Slippage Alert: {Message}", alertDto.Message);
            return alertDto;
        }

        return null;
    }

    private (double Mean, double StdDev, double ZScore, bool IsSlipping, string Status) GetTransitionStatistics(
        Guid tenantId,
        string fromStep,
        string toStep,
        double currentRate)
    {
        var key = BuildTransitionKey(fromStep, toStep);
        List<double>? historical = null;

        if (_historicalRatesByTenant.TryGetValue(tenantId, out var tenantHist))
        {
            tenantHist.TryGetValue(key, out historical);
        }

        // Default synthetic baseline if no prior days seeded: 50% baseline with 5% stddev
        if (historical == null || historical.Count == 0)
        {
            historical = [0.45, 0.48, 0.50, 0.49, 0.52, 0.47, 0.51];
        }

        double mean = historical.Average();
        double sumSq = historical.Sum(r => Math.Pow(r - mean, 2));
        double stdDev = Math.Sqrt(sumSq / Math.Max(1, historical.Count - 1));

        // Enforce non-zero stddev floor to avoid division by zero
        if (stdDev < 0.005)
        {
            stdDev = 0.01;
        }

        double zScore = (currentRate - mean) / stdDev;
        bool isSlipping = zScore < -2.0;

        string status;
        if (zScore <= -3.0)
        {
            status = "CriticalSlippage";
        }
        else if (zScore < -2.0)
        {
            status = "Warning";
        }
        else
        {
            status = "Healthy";
        }

        return (mean, stdDev, zScore, isSlipping, status);
    }

    private static string BuildTransitionKey(string fromStep, string toStep) => $"{fromStep}->{toStep}";
}
