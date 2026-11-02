using SGrowing.Api.Modules.Infrastructure.FeatureFlags;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class FeatureFlagTests
{
    private readonly FeatureFlagEngine _engine = new();

    [Fact]
    public void EvaluateFlag_WhitelistedTenant_ReturnsEnabledImmediately()
    {
        // Arrange
        var request = new EvaluateFlagRequest(
            FlagKey: "voice_webrtc_sub150ms",
            TenantId: "org_enterprise_vip"
        );

        // Act
        var result = _engine.EvaluateFlag(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsEnabled);
        Assert.Equal("ExplicitWhitelist", result.EvaluationReason);
    }

    [Fact]
    public void EvaluateFlag_StandardTenant_ReturnsDeterministicBucketAssignment()
    {
        // Arrange
        var request1 = new EvaluateFlagRequest(FlagKey: "voice_webrtc_sub150ms", TenantId: "tenant_abc_123");
        var request2 = new EvaluateFlagRequest(FlagKey: "voice_webrtc_sub150ms", TenantId: "tenant_abc_123");

        // Act
        var result1 = _engine.EvaluateFlag(request1);
        var result2 = _engine.EvaluateFlag(request2);

        // Assert
        Assert.Equal(result1.AssignedBucket, result2.AssignedBucket);
        Assert.Equal(result1.IsEnabled, result2.IsEnabled);
        Assert.True(result1.AssignedBucket is >= 0 and < 100);
    }

    [Fact]
    public void AdvanceCanaryRing_AndEmergencyKill_ModifiesFlagLifecycle()
    {
        // 1. Advance to GA
        var advance = _engine.AdvanceCanaryRing(new CanaryAdvanceRequest(
            FlagKey: "voice_webrtc_sub150ms",
            TargetRing: CanaryRing.PublicGeneralAvailability
        ));
        Assert.Equal(CanaryRing.PublicGeneralAvailability, advance.CurrentRing);
        Assert.Equal(100, advance.RolloutPercentage);

        // 2. Emergency kill
        var killed = _engine.EmergencyKill(new EmergencyKillRequest(
            FlagKey: "voice_webrtc_sub150ms",
            Reason: "Unexpected P95 audio stutter anomaly detected"
        ));
        Assert.False(killed.IsEnabled);
        Assert.True(killed.AutoRollbackTriggered);

        // 3. Re-evaluating now returns CircuitBreakerRollback
        var eval = _engine.EvaluateFlag(new EvaluateFlagRequest("voice_webrtc_sub150ms", "tenant_xyz"));
        Assert.False(eval.IsEnabled);
        Assert.Equal("CircuitBreakerRollback", eval.EvaluationReason);
    }
}
