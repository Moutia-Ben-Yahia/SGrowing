namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// Simplified SGP4 / LEO orbital mechanics pass prediction engine for
/// Iridium, Starlink Direct-to-Cell, and AST SpaceMobile constellations.
/// </summary>
public sealed class SatelliteOrbitalPassScheduler : ISatelliteOrbitalPassScheduler
{
    private const double EarthEquatorialRadiusKm = 6378.137;
    private const double EarthMuKm3S2 = 398600.4418;
    private const double EarthRotationRateRadPerSec = 7.2921151467e-5; // 360 deg / 86164.0905 s
    private const double J2Harmonic = 1.08263e-3;
    private const double MinElevationDefault = 15.0;

    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Orbital ephemeris representation for deterministic LEO propagation.
    /// </summary>
    public sealed record SatelliteEphemeris(
        string SatelliteId,
        ConstellationType Constellation,
        double AltitudeKm,
        double InclinationDegrees,
        double RaanDegrees,
        double MeanAnomalyEpochDegrees,
        double ArgumentOfPerigeeDegrees = 0.0
    )
    {
        public double SemiMajorAxisKm => EarthEquatorialRadiusKm + AltitudeKm;

        public double OrbitalPeriodSeconds =>
            2.0 * Math.PI * Math.Sqrt(Math.Pow(SemiMajorAxisKm, 3) / EarthMuKm3S2);

        public double MeanMotionRadPerSec => 2.0 * Math.PI / OrbitalPeriodSeconds;

        public double NodalPrecessionRadPerSec
        {
            get
            {
                var incRad = InclinationDegrees * (Math.PI / 180.0);
                var p = SemiMajorAxisKm;
                return -1.5 * J2Harmonic * Math.Pow(EarthEquatorialRadiusKm / p, 2) * MeanMotionRadPerSec * Math.Cos(incRad);
            }
        }
    }

    private readonly List<SatelliteEphemeris> _satellites = [];

    public double MinimumElevationThresholdDegrees { get; }

    public SatelliteOrbitalPassScheduler(double minimumElevationThresholdDegrees = MinElevationDefault)
    {
        MinimumElevationThresholdDegrees = minimumElevationThresholdDegrees;
        SeedConstellationCatalogue();
    }

    /// <summary>
    /// Seeds realistic constellation ephemeris for Iridium NEXT, Starlink D2C, and AST BlueBird.
    /// </summary>
    private void SeedConstellationCatalogue()
    {
        // 1. Iridium NEXT: 6 polar planes, ~780 km altitude, 86.4 deg inclination
        _satellites.AddRange([
            new("IRIDIUM-NEXT-101", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 0.0, 15.0),
            new("IRIDIUM-NEXT-102", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 30.0, 75.0),
            new("IRIDIUM-NEXT-103", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 60.0, 135.0),
            new("IRIDIUM-NEXT-104", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 90.0, 195.0),
            new("IRIDIUM-NEXT-105", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 120.0, 255.0),
            new("IRIDIUM-NEXT-106", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 150.0, 315.0),
            new("IRIDIUM-NEXT-107", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 180.0, 45.0),
            new("IRIDIUM-NEXT-108", ConstellationType.IridiumShortBurstData, 780.0, 86.4, 210.0, 105.0)
        ]);

        // 2. Starlink Direct-to-Cell (D2C): 53 deg inclination, ~350 km altitude
        _satellites.AddRange([
            new("STARLINK-D2C-3011", ConstellationType.StarlinkDirectToCell, 350.0, 53.0, 15.0, 25.0),
            new("STARLINK-D2C-3024", ConstellationType.StarlinkDirectToCell, 350.0, 53.0, 75.0, 85.0),
            new("STARLINK-D2C-3037", ConstellationType.StarlinkDirectToCell, 350.0, 53.0, 135.0, 145.0),
            new("STARLINK-D2C-3050", ConstellationType.StarlinkDirectToCell, 350.0, 53.0, 195.0, 205.0),
            new("STARLINK-D2C-3063", ConstellationType.StarlinkDirectToCell, 350.0, 53.0, 255.0, 265.0),
            new("STARLINK-D2C-3076", ConstellationType.StarlinkDirectToCell, 350.0, 53.0, 315.0, 325.0)
        ]);

        // 3. AST SpaceMobile 3GPP NTN (BlueBird): 53.2 deg inclination, ~510 km altitude
        _satellites.AddRange([
            new("AST-BLUEBIRD-01", ConstellationType.AstSpaceMobile3GppNtn, 510.0, 53.2, 40.0, 50.0),
            new("AST-BLUEBIRD-02", ConstellationType.AstSpaceMobile3GppNtn, 510.0, 53.2, 160.0, 170.0),
            new("AST-BLUEBIRD-03", ConstellationType.AstSpaceMobile3GppNtn, 510.0, 53.2, 280.0, 290.0)
        ]);
    }

    /// <summary>
    /// Evaluates if an observer at given coordinates currently has direct Line-of-Sight (elevation >= 15 deg).
    /// </summary>
    public bool CheckLineOfSight(
        double latitude,
        double longitude,
        DateTime atUtc,
        out OrbitalPassWindow? bestVisiblePass,
        ConstellationType? constellation = null)
    {
        bestVisiblePass = null;
        var candidates = _satellites
            .Where(s => constellation is null || s.Constellation == constellation.Value);

        double maxElevation = -90.0;
        SatelliteEphemeris? bestSat = null;
        double bestAzimuth = 0.0;

        foreach (var sat in candidates)
        {
            var (satX, satY, satZ) = PropagateToEcef(sat, atUtc);
            var (elevation, azimuth) = CalculateLookAngles(satX, satY, satZ, latitude, longitude);

            if (elevation >= MinimumElevationThresholdDegrees && elevation > maxElevation)
            {
                maxElevation = elevation;
                bestSat = sat;
                bestAzimuth = azimuth;
            }
        }

        if (bestSat is not null)
        {
            // Calculate current pass window boundaries
            var window = ComputePassWindowAround(bestSat, latitude, longitude, atUtc, maxElevation, bestAzimuth);
            bestVisiblePass = window;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Predicts upcoming orbital pass windows for an observer location over a lookahead time horizon.
    /// </summary>
    public IReadOnlyList<OrbitalPassWindow> GetUpcomingPasses(
        double latitude,
        double longitude,
        DateTime fromUtc,
        TimeSpan lookaheadHorizon,
        ConstellationType? constellation = null)
    {
        var passes = new List<OrbitalPassWindow>();
        var candidates = _satellites
            .Where(s => constellation is null || s.Constellation == constellation.Value)
            .ToList();

        var stepSeconds = 30.0;
        var totalSteps = (int)Math.Ceiling(lookaheadHorizon.TotalSeconds / stepSeconds);

        foreach (var sat in candidates)
        {
            bool inPass = false;
            DateTime passStart = fromUtc;
            double peakElevation = -90.0;
            double peakAzimuth = 0.0;

            for (int i = 0; i <= totalSteps; i++)
            {
                var currentTime = fromUtc.AddSeconds(i * stepSeconds);
                var (satX, satY, satZ) = PropagateToEcef(sat, currentTime);
                var (elevation, azimuth) = CalculateLookAngles(satX, satY, satZ, latitude, longitude);

                if (elevation >= MinimumElevationThresholdDegrees)
                {
                    if (!inPass)
                    {
                        inPass = true;
                        passStart = currentTime;
                        peakElevation = elevation;
                        peakAzimuth = azimuth;
                    }
                    else if (elevation > peakElevation)
                    {
                        peakElevation = elevation;
                        peakAzimuth = azimuth;
                    }
                }
                else
                {
                    if (inPass)
                    {
                        // Pass completed
                        var passEnd = currentTime;
                        var duration = (passEnd - passStart).TotalSeconds;
                        var isCurrentlyVisible = fromUtc >= passStart && fromUtc <= passEnd;

                        passes.Add(new OrbitalPassWindow(
                            SatelliteId: sat.SatelliteId,
                            Constellation: sat.Constellation,
                            StartUtc: passStart,
                            EndUtc: passEnd,
                            MaxElevationDegrees: Math.Round(peakElevation, 2),
                            DurationSeconds: Math.Round(duration, 1),
                            IsCurrentlyVisible: isCurrentlyVisible,
                            AzimuthDegrees: Math.Round(peakAzimuth, 2),
                            CurrentElevationDegrees: isCurrentlyVisible ? Math.Round(peakElevation, 2) : 0.0
                        ));

                        inPass = false;
                    }
                }
            }

            // If still in pass at horizon boundary
            if (inPass)
            {
                var passEnd = fromUtc.Add(lookaheadHorizon);
                var duration = (passEnd - passStart).TotalSeconds;
                var isCurrentlyVisible = fromUtc >= passStart && fromUtc <= passEnd;

                passes.Add(new OrbitalPassWindow(
                    SatelliteId: sat.SatelliteId,
                    Constellation: sat.Constellation,
                    StartUtc: passStart,
                    EndUtc: passEnd,
                    MaxElevationDegrees: Math.Round(peakElevation, 2),
                    DurationSeconds: Math.Round(duration, 1),
                    IsCurrentlyVisible: isCurrentlyVisible,
                    AzimuthDegrees: Math.Round(peakAzimuth, 2),
                    CurrentElevationDegrees: isCurrentlyVisible ? Math.Round(peakElevation, 2) : 0.0
                ));
            }
        }

        return passes.OrderBy(p => p.StartUtc).ThenByDescending(p => p.MaxElevationDegrees).ToList();
    }

    /// <summary>
    /// Finds the earliest upcoming opportunistic burst pass window for an asset.
    /// </summary>
    public OrbitalPassWindow? GetNextBurstOpportunity(
        double latitude,
        double longitude,
        DateTime fromUtc,
        ConstellationType? constellation = null)
    {
        // Check if currently visible
        if (CheckLineOfSight(latitude, longitude, fromUtc, out var visiblePass, constellation) && visiblePass is not null)
        {
            return visiblePass;
        }

        // Search within next 12 hours
        var upcoming = GetUpcomingPasses(latitude, longitude, fromUtc, TimeSpan.FromHours(12), constellation);
        return upcoming.FirstOrDefault(p => p.EndUtc > fromUtc);
    }

    /// <summary>
    /// Calculates elevation and azimuth look angles from observer to satellite in ECEF frame.
    /// </summary>
    public (double ElevationDegrees, double AzimuthDegrees) CalculateLookAngles(
        double satXKm,
        double satYKm,
        double satZKm,
        double obsLatDeg,
        double obsLonDeg,
        double obsAltMeters = 0)
    {
        var phi = obsLatDeg * (Math.PI / 180.0);
        var lambda = obsLonDeg * (Math.PI / 180.0);
        var rObsKm = EarthEquatorialRadiusKm + (obsAltMeters / 1000.0);

        // Observer ECEF
        var obsX = rObsKm * Math.Cos(phi) * Math.Cos(lambda);
        var obsY = rObsKm * Math.Cos(phi) * Math.Sin(lambda);
        var obsZ = rObsKm * Math.Sin(phi);

        // Range vector rho = sat - obs
        var rhoX = satXKm - obsX;
        var rhoY = satYKm - obsY;
        var rhoZ = satZKm - obsZ;
        var rhoDist = Math.Sqrt((rhoX * rhoX) + (rhoY * rhoY) + (rhoZ * rhoZ));

        if (rhoDist < 1e-6)
        {
            return (90.0, 0.0);
        }

        // Topocentric Up, East, North unit vectors
        // Up: (cos phi cos lambda, cos phi sin lambda, sin phi)
        var rhoUp = (rhoX * Math.Cos(phi) * Math.Cos(lambda)) +
                    (rhoY * Math.Cos(phi) * Math.Sin(lambda)) +
                    (rhoZ * Math.Sin(phi));

        // East: (-sin lambda, cos lambda, 0)
        var rhoEast = (-rhoX * Math.Sin(lambda)) + (rhoY * Math.Cos(lambda));

        // North: (-sin phi cos lambda, -sin phi sin lambda, cos phi)
        var rhoNorth = (-rhoX * Math.Sin(phi) * Math.Cos(lambda)) -
                       (rhoY * Math.Sin(phi) * Math.Sin(lambda)) +
                       (rhoZ * Math.Cos(phi));

        // Elevation angle
        var sinElevation = Math.Clamp(rhoUp / rhoDist, -1.0, 1.0);
        var elevationDeg = Math.Asin(sinElevation) * (180.0 / Math.PI);

        // Azimuth angle from North clockwise
        var azimuthRad = Math.Atan2(rhoEast, rhoNorth);
        var azimuthDeg = azimuthRad * (180.0 / Math.PI);
        if (azimuthDeg < 0.0)
        {
            azimuthDeg += 360.0;
        }

        return (elevationDeg, azimuthDeg % 360.0);
    }

    /// <summary>
    /// Propagates satellite ephemeris to Earth-Centered Earth-Fixed (ECEF) coordinates at specified time.
    /// </summary>
    public static (double X, double Y, double Z) PropagateToEcef(SatelliteEphemeris sat, DateTime atUtc)
    {
        var deltaSec = (atUtc - Epoch).TotalSeconds;

        // Mean anomaly & argument of latitude
        var meanAnomaly = (sat.MeanAnomalyEpochDegrees * (Math.PI / 180.0)) + (sat.MeanMotionRadPerSec * deltaSec);
        var u = (meanAnomaly + (sat.ArgumentOfPerigeeDegrees * (Math.PI / 180.0))) % (2.0 * Math.PI);

        // Nodal precession (RAAN regression)
        var raan = ((sat.RaanDegrees * (Math.PI / 180.0)) + (sat.NodalPrecessionRadPerSec * deltaSec)) % (2.0 * Math.PI);
        var inc = sat.InclinationDegrees * (Math.PI / 180.0);
        var r = sat.SemiMajorAxisKm;

        // Position in orbital plane
        var xOrb = r * Math.Cos(u);
        var yOrb = r * Math.Sin(u);

        // Transform to Earth-Centered Inertial (ECI)
        var xEci = (xOrb * Math.Cos(raan)) - (yOrb * Math.Sin(raan) * Math.Cos(inc));
        var yEci = (xOrb * Math.Sin(raan)) + (yOrb * Math.Cos(raan) * Math.Cos(inc));
        var zEci = yOrb * Math.Sin(inc);

        // Greenwich Sidereal Time (rotation of Earth)
        var thetaG = (EarthRotationRateRadPerSec * deltaSec) % (2.0 * Math.PI);

        // Rotate ECI to ECEF around Z-axis
        var xEcef = (xEci * Math.Cos(thetaG)) + (yEci * Math.Sin(thetaG));
        var yEcef = (-xEci * Math.Sin(thetaG)) + (yEci * Math.Cos(thetaG));
        var zEcef = zEci;

        return (xEcef, yEcef, zEcef);
    }

    private OrbitalPassWindow ComputePassWindowAround(
        SatelliteEphemeris sat,
        double latitude,
        double longitude,
        DateTime atUtc,
        double currentElevation,
        double currentAzimuth)
    {
        // Step backward to find AOS (Acquisition of Signal)
        var aos = atUtc;
        while (aos > atUtc.AddHours(-1))
        {
            var testTime = aos.AddSeconds(-20);
            var (x, y, z) = PropagateToEcef(sat, testTime);
            var (el, _) = CalculateLookAngles(x, y, z, latitude, longitude);
            if (el < MinimumElevationThresholdDegrees)
            {
                break;
            }
            aos = testTime;
        }

        // Step forward to find LOS (Loss of Signal) and peak
        var los = atUtc;
        double peakEl = currentElevation;
        double peakAz = currentAzimuth;

        while (los < atUtc.AddHours(1))
        {
            var testTime = los.AddSeconds(20);
            var (x, y, z) = PropagateToEcef(sat, testTime);
            var (el, az) = CalculateLookAngles(x, y, z, latitude, longitude);
            if (el < MinimumElevationThresholdDegrees)
            {
                break;
            }
            if (el > peakEl)
            {
                peakEl = el;
                peakAz = az;
            }
            los = testTime;
        }

        var duration = Math.Max((los - aos).TotalSeconds, 1.0);

        return new OrbitalPassWindow(
            SatelliteId: sat.SatelliteId,
            Constellation: sat.Constellation,
            StartUtc: aos,
            EndUtc: los,
            MaxElevationDegrees: Math.Round(peakEl, 2),
            DurationSeconds: Math.Round(duration, 1),
            IsCurrentlyVisible: true,
            AzimuthDegrees: Math.Round(peakAz, 2),
            CurrentElevationDegrees: Math.Round(currentElevation, 2)
        );
    }
}
