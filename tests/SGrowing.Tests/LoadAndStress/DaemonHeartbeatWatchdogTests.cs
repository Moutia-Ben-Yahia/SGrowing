using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Modules.Diagnostics;
using SGrowing.Core.Diagnostics;
using Xunit;

namespace SGrowing.Tests.LoadAndStress;

public class DaemonHeartbeatWatchdogTests
{
    [Fact]
    public async Task DaemonHeartbeatService_WhenHeartbeatRecorded_ShouldReportHealthy()
    {
        var loggerMock = new Mock<ILogger<DaemonHeartbeatService>>();
        var service = new DaemonHeartbeatService(null, loggerMock.Object);

        await service.RecordHeartbeatAsync("P2PWarmupRescueDaemon");

        var status = await service.GetDaemonStatusAsync("P2PWarmupRescueDaemon");
        status.Should().NotBeNull();
        status.DaemonName.Should().Be("P2PWarmupRescueDaemon");
        status.Status.Should().Be("Healthy");
        status.MissedTicks.Should().Be(0);
        status.IsAlive.Should().BeTrue();
        status.SecondsSinceLastHeartbeat.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DaemonHeartbeatService_WhenNoHeartbeatRecorded_ShouldReportUnhealthyWithThreeMissedTicks()
    {
        var loggerMock = new Mock<ILogger<DaemonHeartbeatService>>();
        var service = new DaemonHeartbeatService(null, loggerMock.Object);

        var status = await service.GetDaemonStatusAsync("UnregisteredMysteryDaemon_" + Guid.NewGuid());
        status.Status.Should().Be("Unhealthy");
        status.MissedTicks.Should().BeGreaterThanOrEqualTo(3);
        status.IsAlive.Should().BeFalse();
    }

    [Fact]
    public async Task GetHealthReport_ShouldIncludeAllSixRequiredDaemons()
    {
        var loggerMock = new Mock<ILogger<DaemonHeartbeatService>>();
        var service = new DaemonHeartbeatService(null, loggerMock.Object);

        // Record heartbeat for all 6
        foreach (var daemon in DaemonHeartbeatService.DefaultMonitoredDaemons)
        {
            await service.RecordHeartbeatAsync(daemon);
        }

        var report = await service.GetHealthReportAsync();
        report.Should().NotBeNull();
        report.TotalMonitored.Should().Be(6);
        report.HealthyCount.Should().Be(6);
        report.UnhealthyCount.Should().Be(0);
        report.OverallStatus.Should().Be("Healthy");

        report.Daemons.Should().ContainKey("P2PWarmupRescueDaemon");
        report.Daemons.Should().ContainKey("AutonomousSdrTickDaemon");
        report.Daemons.Should().ContainKey("DurableSocialPublishingDaemon");
        report.Daemons.Should().ContainKey("DealRottingWatchdogDaemon");
        report.Daemons.Should().ContainKey("AutonomousAgentExecutionDaemon");
        report.Daemons.Should().ContainKey("OutwardWebhookDeliveryDaemon");
    }

    [Fact]
    public async Task CheckHeartbeatsAndAlertAsync_ShouldAlertWhenDaemonMissesTicks()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();

        var loggerMock = new Mock<ILogger<DaemonHeartbeatService>>();
        var service = new DaemonHeartbeatService(null, loggerMock.Object);

        // Check alerts without recording heartbeats (all 6 should alert)
        var alerts = await service.CheckHeartbeatsAndAlertAsync();
        alerts.Should().NotBeEmpty();
        alerts.Count.Should().Be(6);
        alerts.All(a => a.Contains("DAEMON WATCHDOG ALERT")).Should().BeTrue();
    }

    [Fact]
    public async Task DaemonHeartbeatService_ClockSkewInFuture_ShouldClampElapsedToZeroAndReportHealthy()
    {
        var loggerMock = new Mock<ILogger<DaemonHeartbeatService>>();
        var service = new DaemonHeartbeatService(null, loggerMock.Object);

        // Simulate clock skew where timestamp is 5 seconds in the future
        DaemonHeartbeatService.SetHeartbeatForTesting("AutonomousSdrTickDaemon", DateTime.UtcNow.AddSeconds(5));

        var status = await service.GetDaemonStatusAsync("AutonomousSdrTickDaemon");
        status.Should().NotBeNull();
        status.Status.Should().Be("Healthy");
        status.SecondsSinceLastHeartbeat.Should().Be(0.0);
        status.IsAlive.Should().BeTrue();
    }
}
