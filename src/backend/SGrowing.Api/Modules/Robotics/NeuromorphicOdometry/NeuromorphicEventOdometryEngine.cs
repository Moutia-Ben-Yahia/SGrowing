namespace SGrowing.Api.Modules.Robotics.NeuromorphicOdometry;

public class NeuromorphicEventOdometryEngine
{
    private const double DefaultSceneDepthMeters = 1.20;

    public NeuromorphicOdometryResult ProcessEventStream(NeuromorphicOdometryRequest request)
    {
        if (request.Events.Count == 0 || request.TimeWindowMicroseconds <= 0)
        {
            return new NeuromorphicOdometryResult(
                SensorId: request.SensorId,
                TotalEventsProcessed: 0,
                SpikeCount: 0,
                OpticalFlow: new OpticalFlowVector(0.0, 0.0, 0.0),
                EstimatedPoseIncrement: new PoseIncrement3D(0.0, 0.0, 0.0, 0.0),
                ConfidenceScore: 1.0,
                MotionState: "Stationary"
            );
        }

        int totalEvents = request.Events.Count;
        double timeWindowSeconds = request.TimeWindowMicroseconds / 1_000_000.0;
        double eventRatePerSecond = timeWindowSeconds > 0 ? totalEvents / timeWindowSeconds : 0.0;

        // Background noise / low-activity threshold
        if (totalEvents < 5 || eventRatePerSecond < 20.0)
        {
            return new NeuromorphicOdometryResult(
                SensorId: request.SensorId,
                TotalEventsProcessed: totalEvents,
                SpikeCount: 0,
                OpticalFlow: new OpticalFlowVector(0.0, 0.0, 0.0),
                EstimatedPoseIncrement: new PoseIncrement3D(0.0, 0.0, 0.0, 0.0),
                ConfidenceScore: 0.90,
                MotionState: "Stationary"
            );
        }

        int spikeCount = 0;

        // Leaky Integrate-and-Fire (LIF) threshold simulation over 32x32 spatial bins
        var membranePotentials = new double[32, 32];
        const double vThreshold = 1.0;
        const double leakFactor = 0.95;

        double sumWeightedVx = 0.0;
        double sumWeightedVy = 0.0;

        // Sort events chronologically
        var sortedEvents = request.Events.OrderBy(e => e.TimestampMicroseconds).ToList();
        double t0 = sortedEvents[0].TimestampMicroseconds;

        foreach (var ev in sortedEvents)
        {
            int binX = Math.Clamp(ev.X / 16, 0, 31);
            int binY = Math.Clamp(ev.Y / 16, 0, 31);

            membranePotentials[binX, binY] = (membranePotentials[binX, binY] * leakFactor) + (ev.Polarity > 0 ? 0.35 : -0.35);

            if (Math.Abs(membranePotentials[binX, binY]) >= vThreshold)
            {
                spikeCount++;
                membranePotentials[binX, binY] = 0.0; // reset
            }

            // Relative position from optical center (assume 256x256 frame)
            double cx = ev.X - 128.0;
            double cy = ev.Y - 128.0;
            double dtSeconds = Math.Max(1e-6, (ev.TimestampMicroseconds - t0) / 1_000_000.0);

            sumWeightedVx += (cx / dtSeconds) * ev.Polarity;
            sumWeightedVy += (cy / dtSeconds) * ev.Polarity;
        }

        double meanVx = Math.Round(sumWeightedVx / totalEvents, 2);
        double meanVy = Math.Round(sumWeightedVy / totalEvents, 2);
        double flowMagnitude = Math.Round(Math.Sqrt((meanVx * meanVx) + (meanVy * meanVy)), 2);

        // Convert pixel velocity to metric displacement: delta = (V_px * pitch_m * depth_m) / focal_m
        double pixelPitchMeters = request.PixelPitchMicrons * 1e-6;
        double focalLengthMeters = request.FocalLengthMm * 1e-3;
        double scaleFactor = (pixelPitchMeters * DefaultSceneDepthMeters) / focalLengthMeters;

        double deltaXMeters = Math.Round(meanVx * scaleFactor * timeWindowSeconds, 4);
        double deltaYMeters = Math.Round(meanVy * scaleFactor * timeWindowSeconds, 4);
        double deltaZMeters = Math.Round((flowMagnitude * 0.10) * scaleFactor * timeWindowSeconds, 4);
        double deltaYawDegrees = Math.Round(Math.Atan2(meanVy, meanVx) * (180.0 / Math.PI) * 0.10, 2);

        double confidenceScore = Math.Min(1.0, Math.Round(0.50 + ((double)spikeCount / totalEvents), 2));

        string motionState;
        if (flowMagnitude < 50.0)
        {
            motionState = "Stationary";
        }
        else if (Math.Abs(meanVx) > Math.Abs(meanVy) * 2.0)
        {
            motionState = "Lateral Drift";
        }
        else if (Math.Abs(deltaYawDegrees) > 15.0)
        {
            motionState = "High-Angular Rotation";
        }
        else
        {
            motionState = "Linear High-Speed Forward";
        }

        return new NeuromorphicOdometryResult(
            SensorId: request.SensorId,
            TotalEventsProcessed: totalEvents,
            SpikeCount: spikeCount,
            OpticalFlow: new OpticalFlowVector(meanVx, meanVy, flowMagnitude),
            EstimatedPoseIncrement: new PoseIncrement3D(deltaXMeters, deltaYMeters, deltaZMeters, deltaYawDegrees),
            ConfidenceScore: confidenceScore,
            MotionState: motionState
        );
    }
}
