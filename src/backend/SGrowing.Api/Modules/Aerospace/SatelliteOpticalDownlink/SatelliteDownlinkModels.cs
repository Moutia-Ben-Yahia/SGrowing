namespace SGrowing.Api.Modules.Aerospace.SatelliteOpticalDownlink;

public record GroundStation(
    string StationId,
    string Name,
    double Latitude,
    double Longitude,
    double ElevationMeters,
    double MinElevationDegrees = 10.0);

public record SatellitePass(
    string SatelliteId,
    string StationId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    double MaxElevationDegrees,
    double EstimatedLinkCapacityGbps,
    double CloudCoverFraction);

public record ObservationDataPayload(
    string PayloadId,
    string DatasetName,
    double SizeGigabytes,
    int Priority,
    DateTimeOffset AcquiredUtc,
    DateTimeOffset DeadlineUtc);

public record DownlinkScheduleRequest(
    string SatelliteId,
    double OnboardStorageCapacityGb,
    List<ObservationDataPayload> QueuedPayloads,
    List<GroundStation> AvailableStations,
    List<SatellitePass> UpcomingPasses);

public record ScheduledDownlinkWindow(
    string WindowId,
    string StationId,
    string SatelliteId,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    double DurationSeconds,
    double EffectiveThroughputGbps,
    List<string> ScheduledPayloadIds,
    double TotalDownloadedGb);

public record DownlinkScheduleResult(
    string SatelliteId,
    double TotalDownloadedGb,
    double RemainingOnboardGb,
    double StorageUtilizationPercent,
    List<ScheduledDownlinkWindow> ScheduledWindows,
    List<string> DeferredPayloadIds,
    string BottleneckAnalysis);
