using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Infrastructure.FeatureFlags;

public interface IFeatureFlagEngine
{
    EvaluateFlagResult EvaluateFlag(EvaluateFlagRequest request);
    IReadOnlyList<FeatureFlagDefinition> GetAllFlags();
    FeatureFlagDefinition AdvanceCanaryRing(CanaryAdvanceRequest request);
    FeatureFlagDefinition EmergencyKill(EmergencyKillRequest request);
}

public class FeatureFlagEngine : IFeatureFlagEngine
{
    private readonly ConcurrentDictionary<string, FeatureFlagDefinition> _flags = new(StringComparer.OrdinalIgnoreCase);

    public FeatureFlagEngine()
    {
        var f1 = new FeatureFlagDefinition(
            FlagKey: "voice_webrtc_sub150ms",
            DisplayName: "Next-Gen WebRTC Voice Sub-150ms Engine",
            Description: "Enables ultra-low latency WebRTC audio packetization with Opus speech codec",
            IsEnabled: true,
            RolloutPercentage: 25,
            CurrentRing: CanaryRing.Ring1Canary,
            WhitelistedTenantIds: new List<string> { "org_enterprise_vip", "org_beta_partners" },
            BlacklistedTenantIds: new List<string>(),
            CanaryErrorRatePercent: 0.12,
            AutoRollbackTriggered: false,
            UpdatedAtUtc: DateTime.UtcNow.AddHours(-3)
        );

        var f2 = new FeatureFlagDefinition(
            FlagKey: "zk_differential_privacy_cleanroom",
            DisplayName: "Zero-Knowledge Data Clean Room PSI",
            Description: "Cryptographic Private Set Intersection (PSI) and Laplace differential noise injection",
            IsEnabled: true,
            RolloutPercentage: 50,
            CurrentRing: CanaryRing.Ring2Staged,
            WhitelistedTenantIds: new List<string> { "org_enterprise_vip" },
            BlacklistedTenantIds: new List<string>(),
            CanaryErrorRatePercent: 0.04,
            AutoRollbackTriggered: false,
            UpdatedAtUtc: DateTime.UtcNow.AddHours(-1)
        );

        _flags[f1.FlagKey] = f1;
        _flags[f2.FlagKey] = f2;
    }

    public EvaluateFlagResult EvaluateFlag(EvaluateFlagRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_flags.TryGetValue(request.FlagKey, out var flag))
        {
            return new EvaluateFlagResult(
                FlagKey: request.FlagKey,
                TenantId: request.TenantId,
                IsEnabled: false,
                EvaluationReason: "FlagNotFound",
                AssignedBucket: 0,
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        if (flag.AutoRollbackTriggered)
        {
            return new EvaluateFlagResult(
                FlagKey: flag.FlagKey,
                TenantId: request.TenantId,
                IsEnabled: false,
                EvaluationReason: "CircuitBreakerRollback",
                AssignedBucket: 0,
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        if (!flag.IsEnabled)
        {
            return new EvaluateFlagResult(
                FlagKey: flag.FlagKey,
                TenantId: request.TenantId,
                IsEnabled: false,
                EvaluationReason: "FlagDisabled",
                AssignedBucket: 0,
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        if (flag.BlacklistedTenantIds.Contains(request.TenantId, StringComparer.OrdinalIgnoreCase))
        {
            return new EvaluateFlagResult(
                FlagKey: flag.FlagKey,
                TenantId: request.TenantId,
                IsEnabled: false,
                EvaluationReason: "ExplicitBlacklist",
                AssignedBucket: 0,
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        if (flag.WhitelistedTenantIds.Contains(request.TenantId, StringComparer.OrdinalIgnoreCase))
        {
            return new EvaluateFlagResult(
                FlagKey: flag.FlagKey,
                TenantId: request.TenantId,
                IsEnabled: true,
                EvaluationReason: "ExplicitWhitelist",
                AssignedBucket: 0,
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        // Deterministic SHA-256 hash bucket (0 - 99)
        var bucket = ComputeBucket(flag.FlagKey, request.TenantId);
        var isEnabled = bucket < flag.RolloutPercentage;

        return new EvaluateFlagResult(
            FlagKey: flag.FlagKey,
            TenantId: request.TenantId,
            IsEnabled: isEnabled,
            EvaluationReason: isEnabled ? "BucketPercentageMatch" : "BucketExceededRollout",
            AssignedBucket: bucket,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    public IReadOnlyList<FeatureFlagDefinition> GetAllFlags()
    {
        return _flags.Values.OrderBy(f => f.FlagKey).ToList();
    }

    public FeatureFlagDefinition AdvanceCanaryRing(CanaryAdvanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_flags.TryGetValue(request.FlagKey, out var existing))
        {
            var updated = existing with
            {
                CurrentRing = request.TargetRing,
                RolloutPercentage = (int)request.TargetRing,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _flags[request.FlagKey] = updated;
            return updated;
        }

        throw new KeyNotFoundException($"Feature flag '{request.FlagKey}' was not found.");
    }

    public FeatureFlagDefinition EmergencyKill(EmergencyKillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_flags.TryGetValue(request.FlagKey, out var existing))
        {
            var killed = existing with
            {
                IsEnabled = false,
                AutoRollbackTriggered = true,
                RolloutPercentage = 0,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _flags[request.FlagKey] = killed;
            return killed;
        }

        throw new KeyNotFoundException($"Feature flag '{request.FlagKey}' was not found.");
    }

    private static int ComputeBucket(string flagKey, string tenantId)
    {
        var input = $"{flagKey.Trim().ToLowerInvariant()}:{tenantId.Trim().ToLowerInvariant()}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % 100);
    }
}
