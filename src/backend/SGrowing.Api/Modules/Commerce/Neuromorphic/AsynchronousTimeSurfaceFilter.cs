using System.Threading;

namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// Maintains continuous 2D exponential decay time surface:
/// Sigma(x, y) = exp( -(t_current - t_last(x, y)) / tau )
/// Computes local spatial gradient grad(Sigma) to estimate optical flow velocity vectors.
/// </summary>
public sealed class AsynchronousTimeSurfaceFilter : IAsynchronousTimeSurfaceFilter
{
    private const int DefaultWidth = 256;
    private const int DefaultHeight = 256;

    private readonly Lock _lock = new();
    private readonly int _width;
    private readonly int _height;
    private readonly double _tauUs;
    private readonly double _metersPerPixel;
    private readonly long[,] _lastTimestamp;

    public AsynchronousTimeSurfaceFilter(TimeSurfaceConfig? config = null)
    {
        var cfg = config ?? new TimeSurfaceConfig();
        _width = cfg.Width > 0 ? cfg.Width : DefaultWidth;
        _height = cfg.Height > 0 ? cfg.Height : DefaultHeight;
        _tauUs = cfg.DecayTimeConstantUs > 0 ? cfg.DecayTimeConstantUs : 10_000.0;
        _metersPerPixel = cfg.MetersPerPixel > 0 ? cfg.MetersPerPixel : 0.001;
        _lastTimestamp = new long[_width, _height];
        Reset();
    }

    /// <inheritdoc />
    public void UpdateSurface(DvsEvent evt)
    {
        if (evt.X < 0 || evt.X >= _width || evt.Y < 0 || evt.Y >= _height)
        {
            return;
        }

        lock (_lock)
        {
            if (evt.TimestampMicroseconds > _lastTimestamp[evt.X, evt.Y] || _lastTimestamp[evt.X, evt.Y] < 0)
            {
                _lastTimestamp[evt.X, evt.Y] = evt.TimestampMicroseconds;
            }
        }
    }

    /// <inheritdoc />
    public void UpdateSurface(IEnumerable<DvsEvent> events)
    {
        if (events == null) return;

        lock (_lock)
        {
            foreach (var evt in events)
            {
                if (evt.X >= 0 && evt.X < _width && evt.Y >= 0 && evt.Y < _height)
                {
                    if (evt.TimestampMicroseconds > _lastTimestamp[evt.X, evt.Y] || _lastTimestamp[evt.X, evt.Y] < 0)
                    {
                        _lastTimestamp[evt.X, evt.Y] = evt.TimestampMicroseconds;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public double GetTimeSurfaceValue(int x, int y, long currentTimestampUs)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
        {
            return 0.0;
        }

        long tLast;
        lock (_lock)
        {
            tLast = _lastTimestamp[x, y];
        }

        if (tLast < 0)
        {
            return 0.0;
        }

        if (currentTimestampUs <= tLast)
        {
            return 1.0;
        }

        double dt = currentTimestampUs - tLast;
        return Math.Exp(-dt / _tauUs);
    }

    /// <inheritdoc />
    public (double GradientX, double GradientY) ComputeGradient(int x, int y, long currentTimestampUs)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
        {
            return (0.0, 0.0);
        }

        int x0 = Math.Max(0, x - 1);
        int x1 = Math.Min(_width - 1, x + 1);
        int y0 = Math.Max(0, y - 1);
        int y1 = Math.Min(_height - 1, y + 1);

        double dx = Math.Max(1, x1 - x0);
        double dy = Math.Max(1, y1 - y0);

        double sX1 = GetTimeSurfaceValue(x1, y, currentTimestampUs);
        double sX0 = GetTimeSurfaceValue(x0, y, currentTimestampUs);
        double sY1 = GetTimeSurfaceValue(x, y1, currentTimestampUs);
        double sY0 = GetTimeSurfaceValue(x, y0, currentTimestampUs);

        double gx = (sX1 - sX0) / dx;
        double gy = (sY1 - sY0) / dy;

        return (gx, gy);
    }

    /// <inheritdoc />
    public (double VelocityX, double VelocityY) EstimateOpticalFlowAt(int x, int y, long currentTimestampUs)
    {
        double sigma = GetTimeSurfaceValue(x, y, currentTimestampUs);
        if (sigma < 1e-4)
        {
            return (0.0, 0.0);
        }

        var (gx, gy) = ComputeGradient(x, y, currentTimestampUs);
        double gradMagSquared = (gx * gx) + (gy * gy);

        if (gradMagSquared < 1e-7)
        {
            return (0.0, 0.0);
        }

        // Velocity in pixels/us: v = (sigma / (tau * gradMagSquared)) * grad
        // Scale to meters/second: meters/sec = (pixels/us) * metersPerPixel * 1,000,000
        double factor = (sigma / (_tauUs * gradMagSquared)) * _metersPerPixel * 1_000_000.0;
        double vx = gx * factor;
        double vy = gy * factor;

        // Physical clamping to sensor envelope (-10.0 to 10.0 m/s)
        double speed = Math.Sqrt((vx * vx) + (vy * vy));
        if (speed > 10.0)
        {
            double scale = 10.0 / speed;
            vx *= scale;
            vy *= scale;
        }

        return (vx, vy);
    }

    /// <inheritdoc />
    public (double MeanVx, double MeanVy, double SpeedMetersPerSec) EstimateGlobalVelocity(
        IReadOnlyList<DvsEvent> events,
        long currentTimestampUs)
    {
        if (events == null || events.Count == 0)
        {
            return (0.0, 0.0, 0.0);
        }

        double totalVx = 0.0;
        double totalVy = 0.0;
        int validPoints = 0;

        // Sample up to 100 recent events to bound processing time while maintaining microsecond precision
        int step = Math.Max(1, events.Count / 100);
        for (int i = 0; i < events.Count; i += step)
        {
            var e = events[i];
            var (vx, vy) = EstimateOpticalFlowAt(e.X, e.Y, currentTimestampUs);
            if (Math.Abs(vx) > 1e-4 || Math.Abs(vy) > 1e-4)
            {
                totalVx += vx;
                totalVy += vy;
                validPoints++;
            }
        }

        if (validPoints == 0)
        {
            return (0.0, 0.0, 0.0);
        }

        double meanVx = totalVx / validPoints;
        double meanVy = totalVy / validPoints;
        double speed = Math.Sqrt((meanVx * meanVx) + (meanVy * meanVy));

        return (meanVx, meanVy, speed);
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    _lastTimestamp[x, y] = -1L;
                }
            }
        }
    }
}
