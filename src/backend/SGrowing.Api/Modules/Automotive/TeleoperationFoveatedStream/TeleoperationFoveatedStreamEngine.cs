namespace SGrowing.Api.Modules.Automotive.TeleoperationFoveatedStream;

public class TeleoperationFoveatedStreamEngine
{
    private const double MaxLatencyBudgetMs = 80.0;

    public FoveatedTranscodeResult TranscodeFoveatedFrame(FoveatedTranscodeRequest request)
    {
        var f = request.Frame;
        var gaze = request.OperatorGaze;
        var qos = request.NetworkQoS;

        int fovealQp = Math.Clamp(f.BaseQuantizationParameter, 12, 35);
        int maxPeripheralQp = Math.Min(51, fovealQp + 18);

        // Network degradation penalty on peripheral quality
        if (qos.AvailableBandwidthMbps < 10.0 || qos.PacketLossPercent > 2.0)
        {
            maxPeripheralQp = Math.Min(51, maxPeripheralQp + 4);
        }

        var tileProfiles = new List<VideoTileCompressionProfile>();
        double totalBits = 0.0;
        int peripheralQpObserved = fovealQp;

        const int gridRows = 8;
        const int gridCols = 8;

        for (int r = 0; r < gridRows; r++)
        {
            for (int c = 0; c < gridCols; c++)
            {
                double tileCenterX = (c + 0.5) / gridCols;
                double tileCenterY = (r + 0.5) / gridRows;

                double dx = tileCenterX - gaze.NormalizedX;
                double dy = tileCenterY - gaze.NormalizedY;
                double distance = Math.Sqrt((dx * dx) + (dy * dy));

                int assignedQp;
                if (distance <= request.FovealRadiusNormalized)
                {
                    assignedQp = fovealQp;
                }
                else
                {
                    double gradient = Math.Min(1.0, (distance - request.FovealRadiusNormalized) / 0.60);
                    assignedQp = (int)Math.Round(fovealQp + (gradient * (maxPeripheralQp - fovealQp)));
                }

                if (assignedQp > peripheralQpObserved)
                {
                    peripheralQpObserved = assignedQp;
                }

                // Estimated bitrate per tile: lower QP -> exponentially higher bits
                double tileBaseBits = 50_000.0 / Math.Pow(2.0, (assignedQp - 20) / 6.0);
                totalBits += tileBaseBits;

                tileProfiles.Add(new VideoTileCompressionProfile(
                    TileRow: r,
                    TileCol: c,
                    DistanceFromGaze: Math.Round(distance, 3),
                    AssignedQp: assignedQp,
                    BitrateAllocatedKbps: Math.Round(tileBaseBits / 1000.0, 1)
                ));
            }
        }

        double frameSizeKb = Math.Round(totalBits / (8.0 * 1024.0), 2);

        // Latency estimate: transmission time + network RTT/2 + jitter + 12ms hardware encode time
        double txTimeMs = qos.AvailableBandwidthMbps > 0
            ? (totalBits / (qos.AvailableBandwidthMbps * 1_000_000.0)) * 1000.0
            : 50.0;
        double totalLatencyMs = Math.Round(12.0 + txTimeMs + (qos.RttMilliseconds / 2.0) + qos.JitterMilliseconds, 1);
        bool withinBudget = totalLatencyMs <= MaxLatencyBudgetMs;

        string strategy;
        if (withinBudget)
        {
            strategy = "Ultra-Low Latency: Gaze-contingent foveation delivers sharp central vision while preserving <80ms budget.";
        }
        else
        {
            strategy = "Bandwidth Backpressure: 5G network jitter or packet loss demands lower target framerate or elevated peripheral QP.";
        }

        return new FoveatedTranscodeResult(
            VehicleId: f.VehicleId,
            FovealQp: fovealQp,
            PeripheralQp: peripheralQpObserved,
            TotalFrameSizeKilobytes: frameSizeKb,
            EstimatedEncodingLatencyMs: totalLatencyMs,
            WithinLatencyBudget: withinBudget,
            TileProfiles: tileProfiles,
            BitrateAdaptationStrategy: strategy
        );
    }
}
