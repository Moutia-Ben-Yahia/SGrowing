namespace SGrowing.Api.Modules.Aerospace.SatelliteOpticalDownlink;

public class SatelliteOpticalDownlinkSchedulerEngine
{
    public DownlinkScheduleResult OptimizeSchedule(DownlinkScheduleRequest request)
    {
        var stationsMap = request.AvailableStations.ToDictionary(s => s.StationId);
        var scheduledWindows = new List<ScheduledDownlinkWindow>();
        var allocatedPayloadIds = new HashSet<string>();
        double totalDownloadedGb = 0.0;

        // Sort upcoming passes by start time
        var sortedPasses = request.UpcomingPasses
            .OrderBy(p => p.StartUtc)
            .ToList();

        // Sort queued payloads by Priority descending, then DeadlineUtc ascending
        var pendingPayloads = request.QueuedPayloads
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.DeadlineUtc)
            .ToList();

        int windowCounter = 1;
        foreach (var pass in sortedPasses)
        {
            if (!stationsMap.TryGetValue(pass.StationId, out var station))
            {
                continue;
            }

            // Verify minimum elevation
            if (pass.MaxElevationDegrees < station.MinElevationDegrees)
            {
                continue;
            }

            var durationSeconds = (pass.EndUtc - pass.StartUtc).TotalSeconds;
            if (durationSeconds <= 0)
            {
                continue;
            }

            // Optical cloud attenuation (0% cloud -> factor 1.0; 100% cloud -> factor 0.10)
            var cloudAttenuation = Math.Max(0.10, 1.0 - (pass.CloudCoverFraction * 0.90));

            // Atmospheric air-mass elevation factor (higher elevation gives clearer line-of-sight)
            var elevationRadians = pass.MaxElevationDegrees * Math.PI / 180.0;
            var elevationFactor = Math.Sin(elevationRadians);

            var effectiveThroughputGbps = Math.Round(pass.EstimatedLinkCapacityGbps * cloudAttenuation * elevationFactor, 3);
            var windowCapacityGb = (effectiveThroughputGbps * durationSeconds) / 8.0;

            var windowPayloadIds = new List<string>();
            double windowDownloadedGb = 0.0;

            foreach (var payload in pendingPayloads)
            {
                if (allocatedPayloadIds.Contains(payload.PayloadId))
                {
                    continue;
                }

                if (windowDownloadedGb + payload.SizeGigabytes <= windowCapacityGb)
                {
                    windowPayloadIds.Add(payload.PayloadId);
                    allocatedPayloadIds.Add(payload.PayloadId);
                    windowDownloadedGb += payload.SizeGigabytes;
                }
            }

            scheduledWindows.Add(new ScheduledDownlinkWindow(
                WindowId: $"WINDOW-L2-{windowCounter++:D3}",
                StationId: pass.StationId,
                SatelliteId: request.SatelliteId,
                StartUtc: pass.StartUtc,
                EndUtc: pass.EndUtc,
                DurationSeconds: durationSeconds,
                EffectiveThroughputGbps: effectiveThroughputGbps,
                ScheduledPayloadIds: windowPayloadIds,
                TotalDownloadedGb: Math.Round(windowDownloadedGb, 2)
            ));

            totalDownloadedGb += windowDownloadedGb;
        }

        var totalQueuedGb = request.QueuedPayloads.Sum(p => p.SizeGigabytes);
        var remainingOnboardGb = Math.Max(0.0, Math.Round(totalQueuedGb - totalDownloadedGb, 2));
        var storageUtilizationPercent = request.OnboardStorageCapacityGb > 0
            ? Math.Round((remainingOnboardGb / request.OnboardStorageCapacityGb) * 100.0, 2)
            : 0.0;

        var deferredPayloadIds = request.QueuedPayloads
            .Where(p => !allocatedPayloadIds.Contains(p.PayloadId))
            .Select(p => p.PayloadId)
            .ToList();

        string bottleneckAnalysis;
        if (deferredPayloadIds.Count == 0)
        {
            bottleneckAnalysis = "Optimal: 100% data queue downlinked successfully across scheduled passes.";
        }
        else if (request.UpcomingPasses.Any(p => p.CloudCoverFraction > 0.70))
        {
            bottleneckAnalysis = "Atmospheric Attenuation: Heavy cloud cover at ground stations reduced optical throughput.";
        }
        else if (storageUtilizationPercent > 80.0)
        {
            bottleneckAnalysis = "Storage Critical: Onboard buffer near capacity (>80%); inter-satellite optical routing recommended.";
        }
        else
        {
            bottleneckAnalysis = "Contact Constrained: Additional ground station passes required to clear residual payload queue.";
        }

        return new DownlinkScheduleResult(
            SatelliteId: request.SatelliteId,
            TotalDownloadedGb: Math.Round(totalDownloadedGb, 2),
            RemainingOnboardGb: remainingOnboardGb,
            StorageUtilizationPercent: storageUtilizationPercent,
            ScheduledWindows: scheduledWindows,
            DeferredPayloadIds: deferredPayloadIds,
            BottleneckAnalysis: bottleneckAnalysis
        );
    }
}
