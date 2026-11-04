using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Modules.Diagnostics;
using SGrowing.Core.Diagnostics;
using SGrowing.Worker.Services;
using Xunit;

namespace SGrowing.Tests.Worker;

public class DaemonWatchdogServiceTests
{
    private class TestableDaemonWatchdogService : DaemonWatchdogService
    {
        public TestableDaemonWatchdogService(
            IDaemonHeartbeatService heartbeatService,
            ILogger<DaemonWatchdogService>? logger = null)
            : base(heartbeatService, logger)
        {
        }

        public Task InvokeExecuteAsync(CancellationToken stoppingToken) => ExecuteAsync(stoppingToken);
    }

    [Fact]
    public async Task DaemonHeartbeatService_WhenAllDaemonsHealthy_ReturnsZeroAlertsAndHealthyReport()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var heartbeatService = new DaemonHeartbeatService();

        // Mark all monitored daemons healthy
        foreach (var daemon in heartbeatService.MonitoredDaemonNames)
        {
            await heartbeatService.RecordHeartbeatAsync(daemon);
        }

        var alerts = await heartbeatService.CheckHeartbeatsAndAlertAsync();
        alerts.Should().BeEmpty();

        var report = await heartbeatService.GetHealthReportAsync();
        report.OverallStatus.Should().Be("Healthy");
        report.HealthyCount.Should().Be(heartbeatService.MonitoredDaemonNames.Count);
        report.UnhealthyCount.Should().Be(0);
    }

    [Fact]
    public async Task DaemonHeartbeatService_WhenDaemonMissesTicks_ReturnsAlertAndUnhealthyReport()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var heartbeatService = new DaemonHeartbeatService();

        // One daemon missed > 90 seconds (3+ ticks)
        DaemonHeartbeatService.SetHeartbeatForTesting("AutonomousSdrTickDaemon", DateTime.UtcNow.AddMinutes(-5));

        var alerts = await heartbeatService.CheckHeartbeatsAndAlertAsync();
        alerts.Should().NotBeEmpty();
        alerts.Should().Contain(a => a.Contains("AutonomousSdrTickDaemon"));

        var status = await heartbeatService.GetDaemonStatusAsync("AutonomousSdrTickDaemon");
        status.Status.Should().Be("Unhealthy");
        status.IsAlive.Should().BeFalse();
        status.MissedTicks.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task DaemonWatchdogService_ExecuteAsync_CallsHeartbeatSweepAndTerminatesOnCancellation()
    {
        var mockHeartbeatService = new Mock<IDaemonHeartbeatService>();
        mockHeartbeatService
            .Setup(s => s.CheckHeartbeatsAndAlertAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Test Alert" });

        var mockLogger = new Mock<ILogger<DaemonWatchdogService>>();
        var watchdog = new TestableDaemonWatchdogService(mockHeartbeatService.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // cancel promptly

        try
        {
            await watchdog.InvokeExecuteAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }

        mockHeartbeatService.Verify(s => s.CheckHeartbeatsAndAlertAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task DaemonWatchdogService_ExecuteAsync_SurvivesHeartbeatExceptionWithoutCrashing()
    {
        var mockHeartbeatService = new Mock<IDaemonHeartbeatService>();
        int callCount = 0;
        mockHeartbeatService
            .Setup(s => s.CheckHeartbeatsAndAlertAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Simulated Redis heartbeat timeout");
                }
                return Task.FromResult(new List<string>());
            });

        var mockLogger = new Mock<ILogger<DaemonWatchdogService>>();
        var watchdog = new TestableDaemonWatchdogService(mockHeartbeatService.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        try
        {
            await watchdog.InvokeExecuteAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        callCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void MonitoredDaemonNames_IncludesExpectedCoreDaemons()
    {
        var service = new DaemonHeartbeatService();
        service.MonitoredDaemonNames.Should().Contain("P2PWarmupRescueDaemon");
        service.MonitoredDaemonNames.Should().Contain("AutonomousSdrTickDaemon");
        service.MonitoredDaemonNames.Should().Contain("DurableSocialPublishingDaemon");
        service.MonitoredDaemonNames.Should().Contain("DealRottingWatchdogDaemon");
    }

    [Fact]
    public async Task DaemonHeartbeatService_GetDaemonStatusAsync_UnknownDaemon_ReturnsUnhealthyStatusWithZeroHeartbeats()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var service = new DaemonHeartbeatService();

        var status = await service.GetDaemonStatusAsync("NonExistentDaemon");

        status.Should().NotBeNull();
        status.Status.Should().Be("Unhealthy");
        status.IsAlive.Should().BeFalse();
        status.Message.Should().Contain("No heartbeat has been recorded");
    }

    [Fact]
    public async Task DaemonHeartbeatService_WhenElapsedBetween35And90_ReturnsDegradedStatus()
    {
        DaemonHeartbeatService.ResetHeartbeatsForTesting();
        var service = new DaemonHeartbeatService();

        // 50 seconds elapsed -> Degraded (between 35s and 90s)
        DaemonHeartbeatService.SetHeartbeatForTesting("AutonomousSdrTickDaemon", DateTime.UtcNow.AddSeconds(-50));

        var status = await service.GetDaemonStatusAsync("AutonomousSdrTickDaemon");

        status.Should().NotBeNull();
        status.Status.Should().Be("Degraded");
        status.IsAlive.Should().BeTrue();
    }

    [Fact]
    public async Task DaemonHeartbeatService_ResetHeartbeatsForTesting_ClearsAllRecordedHeartbeats()
    {
        var service = new DaemonHeartbeatService();
        await service.RecordHeartbeatAsync("AutonomousSdrTickDaemon");

        var beforeReset = await service.GetDaemonStatusAsync("AutonomousSdrTickDaemon");
        beforeReset.IsAlive.Should().BeTrue();

        DaemonHeartbeatService.ResetHeartbeatsForTesting();

        var afterReset = await service.GetDaemonStatusAsync("AutonomousSdrTickDaemon");
        afterReset.IsAlive.Should().BeFalse();
        afterReset.Status.Should().Be("Unhealthy");
    }
}
