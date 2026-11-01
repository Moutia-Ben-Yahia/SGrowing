namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// Interface for SGP4-derived LEO satellite orbital propagation and pass window scheduling.
/// </summary>
public interface ISatelliteOrbitalPassScheduler
{
    /// <summary>
    /// Standard minimum elevation threshold angle (15.0 degrees) required for reliable Line-of-Sight (LOS).
    /// </summary>
    double MinimumElevationThresholdDegrees { get; }

    /// <summary>
    /// Evaluates whether an observer at given coordinates currently has direct Line-of-Sight visibility
    /// to at least one satellite in the active constellation(s).
    /// </summary>
    bool CheckLineOfSight(
        double latitude,
        double longitude,
        DateTime atUtc,
        out OrbitalPassWindow? bestVisiblePass,
        ConstellationType? constellation = null);

    /// <summary>
    /// Predicts upcoming orbital pass windows for an observer location over a lookahead time horizon.
    /// </summary>
    IReadOnlyList<OrbitalPassWindow> GetUpcomingPasses(
        double latitude,
        double longitude,
        DateTime fromUtc,
        TimeSpan lookaheadHorizon,
        ConstellationType? constellation = null);

    /// <summary>
    /// Finds the earliest upcoming opportunistic burst pass window for an asset.
    /// </summary>
    OrbitalPassWindow? GetNextBurstOpportunity(
        double latitude,
        double longitude,
        DateTime fromUtc,
        ConstellationType? constellation = null);

    /// <summary>
    /// Calculates the elevation and azimuth look angles from observer to satellite in ECEF coordinates.
    /// </summary>
    (double ElevationDegrees, double AzimuthDegrees) CalculateLookAngles(
        double satXKm,
        double satYKm,
        double satZKm,
        double obsLatDeg,
        double obsLonDeg,
        double obsAltMeters = 0);
}
