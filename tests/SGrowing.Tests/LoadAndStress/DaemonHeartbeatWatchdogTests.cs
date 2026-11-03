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

    [Fact]
    public async Task DaemonHeartbeatService_DelayedDaemon_ShouldReportDegraded()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var service = new DaemonHeartbeatService(null);

        // 45 seconds elapsed (> 35s and <= 90s)
        DaemonHeartbeatService.SetHeartbeatForTesting("DealRottingWatchdogDaemon", DateTime.UtcNow.AddSeconds(-45));

        var status = await service.GetDaemonStatusAsync("DealRottingWatchdogDaemon");
        status.Should().NotBeNull();
        status.Status.Should().Be("Degraded");
        status.MissedTicks.Should().Be(1);
        status.IsAlive.Should().BeTrue();
        status.Message.Should().Contain("Daemon delayed");
    }

    [Fact]
    public async Task DaemonHeartbeatService_SeverelyStalledDaemon_ShouldReportUnhealthyWithCalculatedMissedTicks()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var service = new DaemonHeartbeatService(null);

        // 180 seconds elapsed (> 90s) -> 180 / 30 = 6 missed ticks
        DaemonHeartbeatService.SetHeartbeatForTesting("DurableSocialPublishingDaemon", DateTime.UtcNow.AddSeconds(-180));

        var status = await service.GetDaemonStatusAsync("DurableSocialPublishingDaemon");
        status.Should().NotBeNull();
        status.Status.Should().Be("Unhealthy");
        status.MissedTicks.Should().BeGreaterThanOrEqualTo(6);
        status.IsAlive.Should().BeFalse();
        status.Message.Should().Contain("missed");
    }

    [Fact]
    public async Task GetHealthReport_PartiallyHealthyDaemons_ShouldReportDegradedOverall()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var service = new DaemonHeartbeatService(null);

        // Record heartbeat for only first 2 daemons
        await service.RecordHeartbeatAsync(DaemonHeartbeatService.DefaultMonitoredDaemons[0]);
        await service.RecordHeartbeatAsync(DaemonHeartbeatService.DefaultMonitoredDaemons[1]);

        var report = await service.GetHealthReportAsync();
        report.HealthyCount.Should().Be(2);
        report.UnhealthyCount.Should().Be(4);
        report.OverallStatus.Should().Be("Degraded");
    }

    [Fact]
    public async Task GetHealthReport_ZeroHealthyDaemons_ShouldReportUnhealthyOverall()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var service = new DaemonHeartbeatService(null);

        var report = await service.GetHealthReportAsync();
        report.HealthyCount.Should().Be(0);
        report.UnhealthyCount.Should().Be(6);
        report.OverallStatus.Should().Be("Unhealthy");
    }
}
