namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// Continuous 2D exponential decay time surface filter with neuromorphic optical flow velocity estimation.
/// </summary>
public interface IAsynchronousTimeSurfaceFilter
{
    /// <summary>
    /// Updates the time surface with an incoming microsecond DVS event.
    /// </summary>
    void UpdateSurface(DvsEvent evt);

    /// <summary>
    /// Updates the time surface with a batch of microsecond DVS events.
    /// </summary>
    void UpdateSurface(IEnumerable<DvsEvent> events);

    /// <summary>
    /// Computes the normalized exponential decay value Sigma(x, y) at a given timestamp.
    /// </summary>
    double GetTimeSurfaceValue(int x, int y, long currentTimestampUs);

    /// <summary>
    /// Computes the local spatial gradient (dSigma/dx, dSigma/dy) at (x, y).
    /// </summary>
    (double GradientX, double GradientY) ComputeGradient(int x, int y, long currentTimestampUs);

    /// <summary>
    /// Estimates local optical flow velocity vector (Vx, Vy) in meters/second.
    /// </summary>
    (double VelocityX, double VelocityY) EstimateOpticalFlowAt(int x, int y, long currentTimestampUs);

    /// <summary>
    /// Calculates aggregate global velocity across active events in the batch.
    /// </summary>
    (double MeanVx, double MeanVy, double SpeedMetersPerSec) EstimateGlobalVelocity(
        IReadOnlyList<DvsEvent> events,
        long currentTimestampUs);

    /// <summary>
    /// Resets the time surface state.
    /// </summary>
    void Reset();
}
