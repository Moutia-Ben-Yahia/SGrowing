namespace SGrowing.Tests.Aerospace;

using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.SatelliteOpticalDownlink;
using Xunit;

public class SatelliteOpticalDownlinkTests
{
    private readonly SatelliteOpticalDownlinkSchedulerEngine _engine = new();

    [Fact]
    public void OptimizeSchedule_ClearSkyHighElevation_DownlinksAllHighPriorityPayloads()
    {
        var groundStations = new List<GroundStation>
        {
            new("GS-SVALBARD", "Svalbard Satellite Station", 78.22, 15.65, 400, 10.0),
            new("GS-CANARY", "Tenerife Optical Ground Station", 28.30, -16.51, 2390, 15.0)
        };

        var passes = new List<SatellitePass>
        {
            new("SAT-LEO-01", "GS-CANARY",
                DateTimeOffset.UtcNow.AddMinutes(10),
                DateTimeOffset.UtcNow.AddMinutes(20),
                MaxElevationDegrees: 75.0,
                EstimatedLinkCapacityGbps: 10.0,
                CloudCoverFraction: 0.05) // Near-zero clouds, 10 min pass
        };

        var payloads = new List<ObservationDataPayload>
        {
            new("PAYLOAD-SAR-001", "Synthetic Aperture Radar Scan A", 25.0, Priority: 10, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2)),
            new("PAYLOAD-HYPER-002", "Hyperspectral Agriculture Band", 15.0, Priority: 8, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(3)),
            new("PAYLOAD-OPT-003", "Panchromatic Urban 30cm", 10.0, Priority: 5, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4))
        };

        var request = new DownlinkScheduleRequest(
            SatelliteId: "SAT-LEO-01",
            OnboardStorageCapacityGb: 200.0,
            QueuedPayloads: payloads,
            AvailableStations: groundStations,
            UpcomingPasses: passes
        );

        var result = _engine.OptimizeSchedule(request);

        result.SatelliteId.Should().Be("SAT-LEO-01");
        result.ScheduledWindows.Should().HaveCount(1);
        result.TotalDownloadedGb.Should().Be(50.0);
        result.RemainingOnboardGb.Should().Be(0.0);
        result.DeferredPayloadIds.Should().BeEmpty();
        result.BottleneckAnalysis.Should().Contain("Optimal");
    }

    [Fact]
    public void OptimizeSchedule_HeavyCloudCover_ReducesOpticalThroughputAndDefersLowPriority()
    {
        var groundStations = new List<GroundStation>
        {
            new("GS-TROMSO", "Tromso Station", 69.65, 18.96, 120, 10.0)
        };

        // Short pass with 90% cloud cover
        var passes = new List<SatellitePass>
        {
            new("SAT-LEO-02", "GS-TROMSO",
                DateTimeOffset.UtcNow.AddMinutes(30),
                DateTimeOffset.UtcNow.AddMinutes(32), // 2 min = 120s
                MaxElevationDegrees: 30.0,
                EstimatedLinkCapacityGbps: 2.5,
                CloudCoverFraction: 0.90) // Cloud cover severely attenuates laser
        };

        var payloads = new List<ObservationDataPayload>
        {
            new("PAYLOAD-URGENT", "Disaster Relief Flood Map", 1.0, Priority: 10, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            new("PAYLOAD-MASSIVE", "Routine Multi-Spectral Survey", 40.0, Priority: 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(12))
        };

        var request = new DownlinkScheduleRequest(
            SatelliteId: "SAT-LEO-02",
            OnboardStorageCapacityGb: 50.0,
            QueuedPayloads: payloads,
            AvailableStations: groundStations,
            UpcomingPasses: passes
        );

        var result = _engine.OptimizeSchedule(request);

        result.ScheduledWindows.Should().HaveCount(1);
        result.ScheduledWindows[0].ScheduledPayloadIds.Should().Contain("PAYLOAD-URGENT");
        result.ScheduledWindows[0].ScheduledPayloadIds.Should().NotContain("PAYLOAD-MASSIVE");
        result.DeferredPayloadIds.Should().Contain("PAYLOAD-MASSIVE");
        result.BottleneckAnalysis.Should().ContainEquivalentOf("cloud cover");
    }

    [Fact]
    public void OptimizeSchedule_PassBelowMinElevation_IgnoresPass()
    {
        var groundStations = new List<GroundStation>
        {
            new("GS-ALGIERS", "Algiers Ground Station", 36.75, 3.05, 50, MinElevationDegrees: 15.0)
        };

        var passes = new List<SatellitePass>
        {
            new("SAT-LEO-03", "GS-ALGIERS",
                DateTimeOffset.UtcNow.AddMinutes(15),
                DateTimeOffset.UtcNow.AddMinutes(20),
                MaxElevationDegrees: 12.0, // Below 15.0 degree threshold
                EstimatedLinkCapacityGbps: 5.0,
                CloudCoverFraction: 0.10)
        };

        var payloads = new List<ObservationDataPayload>
        {
            new("P-1", "Telemetry Log", 2.0, 5, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))
        };

        var request = new DownlinkScheduleRequest("SAT-LEO-03", 100.0, payloads, groundStations, passes);
        var result = _engine.OptimizeSchedule(request);

        result.ScheduledWindows.Should().BeEmpty();
        result.TotalDownloadedGb.Should().Be(0.0);
        result.DeferredPayloadIds.Should().Contain("P-1");
    }

    [Fact]
    public void OptimizeSchedule_EmptyQueuedPayloads_ReturnsZeroDownloadedAndOptimal()
    {
        var groundStations = new List<GroundStation>
        {
            new("GS-PARIS", "Paris Station", 48.85, 2.35, 100, 10.0)
        };

        var passes = new List<SatellitePass>
        {
            new("SAT-LEO-04", "GS-PARIS",
                DateTimeOffset.UtcNow.AddMinutes(5),
                DateTimeOffset.UtcNow.AddMinutes(15),
                MaxElevationDegrees: 60.0,
                EstimatedLinkCapacityGbps: 10.0,
                CloudCoverFraction: 0.0)
        };

        var request = new DownlinkScheduleRequest("SAT-LEO-04", 100.0, [], groundStations, passes);
        var result = _engine.OptimizeSchedule(request);

        result.TotalDownloadedGb.Should().Be(0.0);
        result.RemainingOnboardGb.Should().Be(0.0);
        result.StorageUtilizationPercent.Should().Be(0.0);
        result.DeferredPayloadIds.Should().BeEmpty();
        result.BottleneckAnalysis.Should().Contain("Optimal");
    }
}
