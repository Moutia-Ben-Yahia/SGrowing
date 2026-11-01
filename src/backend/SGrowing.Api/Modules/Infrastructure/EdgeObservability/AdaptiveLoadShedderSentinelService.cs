namespace SGrowing.Api.Modules.Infrastructure.EdgeObservability;

/// <summary>
/// Contract for the dynamic adaptive edge load-shedder sentinel service.
/// Enforces priority tier shedding, compute/packet-loss triggers, and hysteresis recovery.
/// </summary>
public interface IAdaptiveLoadShedderSentinelService
{
    /// <summary>
    /// Determines whether an inbound request or container task of the specified tier should be admitted.
    /// Tier 1 (AMR Robotics Safety / Order Confirmation) is NEVER shed.
    /// </summary>
    bool ShouldAdmit(LoadSheddingTier tier, double currentCpuPct, double currentPacketLossPct, DateTimeOffset? now = null);

    /// <summary>
    /// Evaluates current metrics and returns the active load shedding policy.
    /// </summary>
    LoadSheddingPolicy EvaluateCurrentPolicy(double currentCpuPct, double currentPacketLossPct, DateTimeOffset? now = null);

    /// <summary>
    /// Applies a manual override to force load shedding on specified tiers.
    /// Note: Tier 1 can never be shed even via manual override.
    /// </summary>
    void SetManualOverride(bool active, IReadOnlyList<LoadSheddingTier>? tiers, string? reason = null);

    /// <summary>
    /// Clears any manual override, reverting to dynamic threshold-based shedding.
    /// </summary>
    void ClearManualOverride();

    /// <summary>
    /// Retrieves current load shedding status and stabilization telemetry.
    /// </summary>
    LoadSheddingStatusResponse GetStatus(double? sampleCpu = null, double? samplePacketLoss = null, DateTimeOffset? now = null);

    /// <summary>
    /// Resets the hysteresis stabilization timer.
    /// </summary>
    void ResetStabilizationTimer();
}

/// <summary>
/// Dynamic adaptive load shedding sentinel service implementing priority-tier shedding
/// and automatic 30-second hysteresis recovery.
/// </summary>
public class AdaptiveLoadShedderSentinelService : IAdaptiveLoadShedderSentinelService
{
    private readonly Lock _lock = new();

    // Saturation thresholds
    public const double CpuModerateThresholdPct = 85.0;
    public const double CpuHighThresholdPct = 90.0;
    public const double CpuCriticalThresholdPct = 95.0;

    public const double PacketLossModerateThresholdPct = 3.0;
    public const double PacketLossHighThresholdPct = 5.0;
    public const double PacketLossCriticalThresholdPct = 10.0;

    public static readonly TimeSpan HysteresisTotalWindow = TimeSpan.FromSeconds(30.0);
    public static readonly TimeSpan HysteresisTier2RecoveryWindow = TimeSpan.FromSeconds(10.0);
    public static readonly TimeSpan HysteresisTier3RecoveryWindow = TimeSpan.FromSeconds(20.0);

    // Internal state
    private bool _isManualOverrideActive;
    private List<LoadSheddingTier> _manualShedTiers = [];
    private string? _manualOverrideReason;

    private DateTimeOffset? _lastDistressTimeUtc;
    private List<LoadSheddingTier> _lastActiveShedTiers = [];
    private double _lastReportedCpu;
    private double _lastReportedPacketLoss;

    public bool ShouldAdmit(LoadSheddingTier tier, double currentCpuPct, double currentPacketLossPct, DateTimeOffset? now = null)
    {
        // HARD CONSTRAINT: Tier 1 AMR Robotics Safety is NEVER shed under any condition
        if (tier == LoadSheddingTier.Tier1CriticalRoboticsNeverShed)
        {
            return true;
        }

        var policy = EvaluateCurrentPolicy(currentCpuPct, currentPacketLossPct, now);

        // Reject if this tier is currently active in the shed list
        return !policy.ActiveShedTiers.Contains(tier);
    }

    public LoadSheddingPolicy EvaluateCurrentPolicy(double currentCpuPct, double currentPacketLossPct, DateTimeOffset? now = null)
    {
        var currentTime = now ?? DateTimeOffset.UtcNow;

        lock (_lock)
        {
            _lastReportedCpu = currentCpuPct;
            _lastReportedPacketLoss = currentPacketLossPct;

            // Handle manual override
            if (_isManualOverrideActive)
            {
                var manualTiers = _manualShedTiers
                    .Where(t => t != LoadSheddingTier.Tier1CriticalRoboticsNeverShed)
                    .Distinct()
                    .ToList();

                return new LoadSheddingPolicy(
                    IsActive: manualTiers.Count > 0,
                    ActiveShedTiers: manualTiers,
                    CurrentCpuPct: currentCpuPct,
                    CurrentPacketLossPct: currentPacketLossPct,
                    IsManualOverride: true,
                    LastEvaluatedUtc: currentTime,
                    StableDuration: TimeSpan.Zero);
            }

            // Check if metrics exceed any distress thresholds
            var isDistressed = currentCpuPct > CpuModerateThresholdPct || currentPacketLossPct > PacketLossModerateThresholdPct;

            if (isDistressed)
            {
                // Record the timestamp of the active distress
                _lastDistressTimeUtc = currentTime;

                List<LoadSheddingTier> shedTiers;

                if (currentCpuPct > CpuCriticalThresholdPct || currentPacketLossPct > PacketLossCriticalThresholdPct)
                {
                    // Extreme saturation: shed Tier 4, Tier 3, and Tier 2
                    shedTiers = [
                        LoadSheddingTier.Tier4BackgroundBatch,
                        LoadSheddingTier.Tier3AnalyticsTelemetry,
                        LoadSheddingTier.Tier2CoreCheckout
                    ];
                }
                else if (currentCpuPct > CpuHighThresholdPct || currentPacketLossPct > PacketLossHighThresholdPct)
                {
                    // High saturation: shed Tier 4 and Tier 3
                    shedTiers = [
                        LoadSheddingTier.Tier4BackgroundBatch,
                        LoadSheddingTier.Tier3AnalyticsTelemetry
                    ];
                }
                else
                {
                    // Moderate saturation: shed Tier 4
                    shedTiers = [
                        LoadSheddingTier.Tier4BackgroundBatch
                    ];
                }

                _lastActiveShedTiers = shedTiers;

                return new LoadSheddingPolicy(
                    IsActive: true,
                    ActiveShedTiers: shedTiers,
                    CurrentCpuPct: currentCpuPct,
                    CurrentPacketLossPct: currentPacketLossPct,
                    IsManualOverride: false,
                    LastEvaluatedUtc: currentTime,
                    StableDuration: TimeSpan.Zero);
            }

            // Metrics are within normal limits (CPU <= 85% and PacketLoss <= 3%)
            if (_lastActiveShedTiers.Count > 0)
            {
                // System was shedding previously: calculate stable duration since distress ceased
                var stableDuration = _lastDistressTimeUtc.HasValue ? currentTime - _lastDistressTimeUtc.Value : TimeSpan.Zero;

                if (stableDuration >= HysteresisTotalWindow)
                {
                    // Full recovery (> 30s stable): clear all shedding
                    _lastActiveShedTiers.Clear();
                    _lastDistressTimeUtc = null;

                    return new LoadSheddingPolicy(
                        IsActive: false,
                        ActiveShedTiers: [],
                        CurrentCpuPct: currentCpuPct,
                        CurrentPacketLossPct: currentPacketLossPct,
                        IsManualOverride: false,
                        LastEvaluatedUtc: currentTime,
                        StableDuration: stableDuration);
                }

                // Progressive restoration during hysteresis window:
                // 1. After 10s: restore Tier 2 (keep Tier 4 + Tier 3)
                // 2. After 20s: restore Tier 3 (keep Tier 4)
                // 3. After 30s: restore Tier 4 (full recovery handled above)
                List<LoadSheddingTier> progressiveTiers = [.. _lastActiveShedTiers];

                if (stableDuration >= HysteresisTier3RecoveryWindow)
                {
                    // Restored Tier 2 and Tier 3; only Tier 4 remains shed
                    progressiveTiers.Remove(LoadSheddingTier.Tier2CoreCheckout);
                    progressiveTiers.Remove(LoadSheddingTier.Tier3AnalyticsTelemetry);
                }
                else if (stableDuration >= HysteresisTier2RecoveryWindow)
                {
                    // Restored Tier 2; Tier 3 and Tier 4 remain shed
                    progressiveTiers.Remove(LoadSheddingTier.Tier2CoreCheckout);
                }

                return new LoadSheddingPolicy(
                    IsActive: progressiveTiers.Count > 0,
                    ActiveShedTiers: progressiveTiers,
                    CurrentCpuPct: currentCpuPct,
                    CurrentPacketLossPct: currentPacketLossPct,
                    IsManualOverride: false,
                    LastEvaluatedUtc: currentTime,
                    StableDuration: stableDuration);
            }

            // Normal state, no prior shedding
            return new LoadSheddingPolicy(
                IsActive: false,
                ActiveShedTiers: [],
                CurrentCpuPct: currentCpuPct,
                CurrentPacketLossPct: currentPacketLossPct,
                IsManualOverride: false,
                LastEvaluatedUtc: currentTime,
                StableDuration: TimeSpan.Zero);
        }
    }

    public void SetManualOverride(bool active, IReadOnlyList<LoadSheddingTier>? tiers, string? reason = null)
    {
        lock (_lock)
        {
            _isManualOverrideActive = active;
            _manualOverrideReason = reason;

            if (tiers is not null)
            {
                // Ensure Tier 1 can NEVER be added to shed tiers
                _manualShedTiers = tiers
                    .Where(t => t != LoadSheddingTier.Tier1CriticalRoboticsNeverShed)
                    .Distinct()
                    .ToList();
            }
            else
            {
                _manualShedTiers.Clear();
            }
        }
    }

    public void ClearManualOverride()
    {
        lock (_lock)
        {
            _isManualOverrideActive = false;
            _manualShedTiers.Clear();
            _manualOverrideReason = null;
        }
    }

    public LoadSheddingStatusResponse GetStatus(double? sampleCpu = null, double? samplePacketLoss = null, DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            var cpu = sampleCpu ?? _lastReportedCpu;
            var loss = samplePacketLoss ?? _lastReportedPacketLoss;
            var currentTime = now ?? DateTimeOffset.UtcNow;

            var policy = EvaluateCurrentPolicy(cpu, loss, currentTime);

            var dropPercentage = policy.ActiveShedTiers.Count switch
            {
                >= 3 => 75.0,
                2 => 50.0,
                1 => 25.0,
                _ => 0.0
            };

            var inHysteresis = _lastDistressTimeUtc.HasValue && policy.IsActive;
            var secondsStable = _lastDistressTimeUtc.HasValue
                ? (currentTime - _lastDistressTimeUtc.Value).TotalSeconds
                : 0.0;

            return new LoadSheddingStatusResponse(
                IsSheddingActive: policy.IsActive,
                ShedTiers: policy.ActiveShedTiers,
                CurrentCpuPct: cpu,
                CurrentPacketLossPct: loss,
                SheddingDropPercentage: dropPercentage,
                InHysteresisRecovery: inHysteresis,
                SecondsStable: Math.Max(0.0, secondsStable),
                IsManualOverride: _isManualOverrideActive);
        }
    }

    public void ResetStabilizationTimer()
    {
        lock (_lock)
        {
            _lastDistressTimeUtc = null;
            _lastActiveShedTiers.Clear();
        }
    }
}
