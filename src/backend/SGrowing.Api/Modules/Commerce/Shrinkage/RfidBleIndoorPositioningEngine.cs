using System.Threading;

namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Thread-safe real-time indoor positioning engine for RFID/BLE mesh networks.
/// Implements log-distance path loss propagation and 2D/3D linear least-squares trilateration.
/// </summary>
public sealed class RfidBleIndoorPositioningEngine : IRfidBleIndoorPositioningEngine
{
    private const double DefaultTxPowerA = -59.0; // Reference RSSI at 1m in dBm
    private const double DefaultPathLossExponentN = 2.2; // Indoor warehouse environmental path loss exponent
    private const double MinClampedDistanceMeters = 0.05;
    private const double MaxClampedDistanceMeters = 200.0;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, FixedAnchorNode> _anchors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (IndoorLocationPoint Location, string Sku, double BatteryPct)> _trackedAssets = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the positioning engine with standard warehouse anchor gateways.
    /// </summary>
    public RfidBleIndoorPositioningEngine()
    {
        InitializeDefaultAnchors();
    }

    private void InitializeDefaultAnchors()
    {
        // Standard perimeter and zone anchor coverage
        RegisterAnchorInternal(new FixedAnchorNode("ANC-HVA-01", 0.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle, "MeshGate-V3"));
        RegisterAnchorInternal(new FixedAnchorNode("ANC-HVA-02", 30.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle, "MeshGate-V3"));
        RegisterAnchorInternal(new FixedAnchorNode("ANC-HVA-03", 0.0, 40.0, 4.0, 1, WarehouseZone.HighValueAisle, "MeshGate-V3"));
        RegisterAnchorInternal(new FixedAnchorNode("ANC-HVA-04", 30.0, 40.0, 4.0, 1, WarehouseZone.HighValueAisle, "MeshGate-V3"));
        RegisterAnchorInternal(new FixedAnchorNode("ANC-BSR-01", 60.0, 20.0, 4.5, 1, WarehouseZone.BulkStorageRack, "MeshGate-V3"));
        RegisterAnchorInternal(new FixedAnchorNode("ANC-PST-01", 25.0, 65.0, 3.5, 1, WarehouseZone.PackingStation, "MeshGate-V3"));
        RegisterAnchorInternal(new FixedAnchorNode("ANC-QZN-01", 65.0, 65.0, 4.0, 1, WarehouseZone.QuarantineZone, "MeshGate-V3"));
        RegisterAnchorInternal(new FixedAnchorNode("ANC-DCK-01", 95.0, 95.0, 5.0, 1, WarehouseZone.DockExitGate, "MeshGate-V3"));
    }

    private void RegisterAnchorInternal(FixedAnchorNode anchor)
    {
        _anchors[anchor.AnchorId] = anchor;
    }

    /// <inheritdoc />
    public void RegisterAnchor(FixedAnchorNode anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        lock (_lock)
        {
            _anchors[anchor.AnchorId] = anchor;
        }
    }

    /// <inheritdoc />
    public FixedAnchorNode? GetAnchor(string anchorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorId);
        lock (_lock)
        {
            return _anchors.GetValueOrDefault(anchorId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FixedAnchorNode> GetAllAnchors()
    {
        lock (_lock)
        {
            return [.. _anchors.Values];
        }
    }

    /// <inheritdoc />
    public double CalculateDistanceMeters(double rssiDb, double txPowerA = DefaultTxPowerA, double pathLossExponent = DefaultPathLossExponentN)
    {
        // Log-distance path loss: RSSI = -10 * n * log10(d) + A
        // d = 10 ^ ((A - RSSI) / (10 * n))
        double denominator = 10.0 * pathLossExponent;
        if (Math.Abs(denominator) < 1e-6)
        {
            return 1.0;
        }

        double exponent = (txPowerA - rssiDb) / denominator;
        double distance = Math.Pow(10.0, exponent);

        return Math.Clamp(distance, MinClampedDistanceMeters, MaxClampedDistanceMeters);
    }

    /// <inheritdoc />
    public WarehouseZone ResolveZone(double x, double y)
    {
        // DockExitGate perimeter: X >= 90m and Y >= 90m
        if (x >= 90.0 && y >= 90.0)
        {
            return WarehouseZone.DockExitGate;
        }

        if (x >= 85.0 && y >= 85.0)
        {
            return WarehouseZone.DockExitGate;
        }

        // HighValueAisle: X in [0..35], Y in [0..45]
        if (x >= 0.0 && x <= 35.0 && y >= 0.0 && y <= 45.0)
        {
            return WarehouseZone.HighValueAisle;
        }

        // PackingStation: X in [0..50], Y in (45..85]
        if (x >= 0.0 && x <= 50.0 && y > 45.0 && y <= 85.0)
        {
            return WarehouseZone.PackingStation;
        }

        // QuarantineZone: X in (50..85], Y in (45..85]
        if (x > 50.0 && x <= 85.0 && y > 45.0 && y <= 85.0)
        {
            return WarehouseZone.QuarantineZone;
        }

        // BulkStorageRack covers remaining warehouse space
        return WarehouseZone.BulkStorageRack;
    }

    /// <inheritdoc />
    public IndoorLocationPoint EstimateLocation(RfidBleTelemetryBeacon beacon)
    {
        ArgumentNullException.ThrowIfNull(beacon);

        List<(FixedAnchorNode Anchor, double Distance)> resolvedAnchors;
        lock (_lock)
        {
            resolvedAnchors = [];
            foreach (var reading in beacon.RssiReadings)
            {
                if (_anchors.TryGetValue(reading.AnchorId, out var anchor) && anchor.IsActive)
                {
                    double dist = CalculateDistanceMeters(reading.RssiDb);
                    resolvedAnchors.Add((anchor, dist));
                }
            }
        }

        IndoorLocationPoint estimatedPoint;

        if (resolvedAnchors.Count == 0)
        {
            // Default point when no anchors detected
            estimatedPoint = new IndoorLocationPoint(
                X: 0.0,
                Y: 0.0,
                Z: 0.0,
                Floor: 1,
                Zone: WarehouseZone.BulkStorageRack,
                AccuracyMeters: 50.0,
                TimestampUtc: beacon.TimestampUtc);
        }
        else if (resolvedAnchors.Count == 1)
        {
            var single = resolvedAnchors[0];
            estimatedPoint = new IndoorLocationPoint(
                X: single.Anchor.X,
                Y: single.Anchor.Y,
                Z: single.Anchor.Z,
                Floor: single.Anchor.Floor,
                Zone: single.Anchor.Zone,
                AccuracyMeters: Math.Max(single.Distance, 1.0),
                TimestampUtc: beacon.TimestampUtc);
        }
        else if (resolvedAnchors.Count == 2)
        {
            // Weighted average between the 2 anchors
            var a1 = resolvedAnchors[0];
            var a2 = resolvedAnchors[1];
            double w1 = 1.0 / Math.Max(a1.Distance, 0.1);
            double w2 = 1.0 / Math.Max(a2.Distance, 0.1);
            double totalW = w1 + w2;

            double x = (a1.Anchor.X * w1 + a2.Anchor.X * w2) / totalW;
            double y = (a1.Anchor.Y * w1 + a2.Anchor.Y * w2) / totalW;
            double z = (a1.Anchor.Z * w1 + a2.Anchor.Z * w2) / totalW;
            int floor = a1.Distance <= a2.Distance ? a1.Anchor.Floor : a2.Anchor.Floor;
            var zone = ResolveZone(x, y);
            double accuracy = (a1.Distance + a2.Distance) / 2.0;

            estimatedPoint = new IndoorLocationPoint(
                X: Math.Round(x, 2),
                Y: Math.Round(y, 2),
                Z: Math.Round(z, 2),
                Floor: floor,
                Zone: zone,
                AccuracyMeters: Math.Round(accuracy, 2),
                TimestampUtc: beacon.TimestampUtc);
        }
        else
        {
            // 2D Trilateration via Linear Least Squares with N >= 3 anchors
            // 2(x_i - x_1)x + 2(y_i - y_1)y = d_1^2 - d_i^2 + x_i^2 - x_1^2 + y_i^2 - y_1^2
            var (x, y, accuracy) = SolveTrilaterationLeastSquares(resolvedAnchors);

            // Compute weighted Z and floor
            double weightSum = 0.0;
            double weightedZ = 0.0;
            double minDistance = double.MaxValue;
            int bestFloor = resolvedAnchors[0].Anchor.Floor;

            foreach (var (anchor, dist) in resolvedAnchors)
            {
                double w = 1.0 / Math.Max(dist, 0.1);
                weightedZ += anchor.Z * w;
                weightSum += w;
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestFloor = anchor.Floor;
                }
            }

            double z = weightSum > 0 ? weightedZ / weightSum : 0.0;
            var zone = ResolveZone(x, y);

            estimatedPoint = new IndoorLocationPoint(
                X: Math.Round(x, 2),
                Y: Math.Round(y, 2),
                Z: Math.Round(z, 2),
                Floor: bestFloor,
                Zone: zone,
                AccuracyMeters: Math.Round(accuracy, 2),
                TimestampUtc: beacon.TimestampUtc);
        }

        // Record tracked asset state thread-safely
        lock (_lock)
        {
            _trackedAssets[beacon.TagId] = (estimatedPoint, beacon.Sku, beacon.BatteryPct);
        }

        return estimatedPoint;
    }

    private static (double X, double Y, double AccuracyMeters) SolveTrilaterationLeastSquares(
        List<(FixedAnchorNode Anchor, double Distance)> anchors)
    {
        var refAnchor = anchors[0].Anchor;
        double d1 = anchors[0].Distance;
        double d1Sq = d1 * d1;
        double x1 = refAnchor.X;
        double y1 = refAnchor.Y;
        double x1Sq = x1 * x1;
        double y1Sq = y1 * y1;

        // Normal equations: A^T * A * [x, y]^T = A^T * b
        // C = A^T * A (2x2), D = A^T * b (2x1)
        double c00 = 0.0;
        double c01 = 0.0;
        double c11 = 0.0;
        double d0 = 0.0;
        double d1Vec = 0.0;

        for (int i = 1; i < anchors.Count; i++)
        {
            var node = anchors[i].Anchor;
            double di = anchors[i].Distance;
            double diSq = di * di;
            double xi = node.X;
            double yi = node.Y;
            double xiSq = xi * xi;
            double yiSq = yi * yi;

            double a0 = 2.0 * (xi - x1);
            double a1 = 2.0 * (yi - y1);
            double b = d1Sq - diSq + xiSq - x1Sq + yiSq - y1Sq;

            c00 += a0 * a0;
            c01 += a0 * a1;
            c11 += a1 * a1;
            d0 += a0 * b;
            d1Vec += a1 * b;
        }

        double det = c00 * c11 - c01 * c01;

        double x;
        double y;

        if (Math.Abs(det) > 1e-7)
        {
            // Cramers Rule / Direct 2x2 matrix inversion:
            // [x] = 1/det * [ c11  -c01 ] [ d0    ]
            // [y]   1/det * [-c01   c00 ] [ d1Vec ]
            x = (c11 * d0 - c01 * d1Vec) / det;
            y = (-c01 * d0 + c00 * d1Vec) / det;
        }
        else
        {
            // Collinear or degenerate anchors fallback: weighted centroid
            double wSum = 0.0;
            double wx = 0.0;
            double wy = 0.0;
            foreach (var (anchor, dist) in anchors)
            {
                double w = 1.0 / Math.Max(dist, 0.1);
                wx += anchor.X * w;
                wy += anchor.Y * w;
                wSum += w;
            }
            x = wSum > 0 ? wx / wSum : refAnchor.X;
            y = wSum > 0 ? wy / wSum : refAnchor.Y;
        }

        // Calculate Root Mean Square Error (RMSE) of distance residuals for accuracy metric
        double sumSqResiduals = 0.0;
        foreach (var (anchor, dist) in anchors)
        {
            double calculatedDist = Math.Sqrt(Math.Pow(x - anchor.X, 2) + Math.Pow(y - anchor.Y, 2));
            double residual = calculatedDist - dist;
            sumSqResiduals += residual * residual;
        }

        double rmse = Math.Sqrt(sumSqResiduals / anchors.Count);
        double accuracy = Math.Max(0.20, rmse);

        return (x, y, accuracy);
    }

    /// <inheritdoc />
    public IndoorLocationPoint? GetAssetLocation(string tagId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagId);
        lock (_lock)
        {
            return _trackedAssets.TryGetValue(tagId, out var item) ? item.Location : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, (IndoorLocationPoint Location, string Sku, double BatteryPct)> GetAllTrackedAssets()
    {
        lock (_lock)
        {
            return new Dictionary<string, (IndoorLocationPoint Location, string Sku, double BatteryPct)>(
                _trackedAssets,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
