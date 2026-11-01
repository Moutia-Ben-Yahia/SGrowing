namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// Thread-safe environmental telemetry ingestion and decentralized oracle engine.
/// Evaluates continuous Mean Kinetic Temperature (MKT), photic tamper breaches, and impact shocks.
/// </summary>
public sealed class ColdChainTelemetryOracleEngine : IColdChainTelemetryOracleEngine
{
    private const double GasConstantR = 8.3144; // J / (mol * K)
    private const double DefaultActivationEnergy = 83144.0; // J / mol (FDA / USP standard)
    private const double KelvinOffset = 273.15;

    private readonly Lock _syncLock = new();
    private readonly Dictionary<string, List<ColdChainTelemetryPoint>> _telemetryHistory = [];
    private readonly HashSet<(string ConsignmentId, string SensorId, DateTime TimestampUtc)> _dedupIndex = [];

    /// <inheritdoc />
    public TelemetryIngestResponse IngestTelemetry(ColdChainTelemetryPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (string.IsNullOrWhiteSpace(point.ConsignmentId))
        {
            throw new ArgumentException("ConsignmentId cannot be null or whitespace.", nameof(point));
        }

        lock (_syncLock)
        {
            var dedupKey = (point.ConsignmentId, point.SensorId, point.TimestampUtc);
            if (_dedupIndex.Contains(dedupKey))
            {
                var existingCount = _telemetryHistory.TryGetValue(point.ConsignmentId, out var existingList) ? existingList.Count : 0;
                return new TelemetryIngestResponse(
                    Success: true,
                    ConsignmentId: point.ConsignmentId,
                    SensorId: point.SensorId,
                    TotalPointsRecorded: existingCount,
                    CurrentCompliance: ThermalComplianceStatus.Optimal,
                    Message: "Duplicate telemetry point deduplicated and skipped.",
                    DuplicateFiltered: true);
            }

            _dedupIndex.Add(dedupKey);

            if (!_telemetryHistory.TryGetValue(point.ConsignmentId, out var points))
            {
                points = [];
                _telemetryHistory[point.ConsignmentId] = points;
            }

            points.Add(point);

            var defaultProfile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);
            var mktResult = CalculateMktInternal(point.ConsignmentId, points, defaultProfile);

            return new TelemetryIngestResponse(
                Success: true,
                ConsignmentId: point.ConsignmentId,
                SensorId: point.SensorId,
                TotalPointsRecorded: points.Count,
                CurrentCompliance: mktResult.ComplianceStatus,
                Message: "Telemetry point ingested successfully.",
                DuplicateFiltered: false);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TelemetryIngestResponse> IngestBatch(IEnumerable<ColdChainTelemetryPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var responses = new List<TelemetryIngestResponse>();
        foreach (var point in points)
        {
            responses.Add(IngestTelemetry(point));
        }

        return responses;
    }

    /// <inheritdoc />
    public MeanKineticTemperatureResult CalculateMkt(string consignmentId, CargoThermalProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);
        ArgumentNullException.ThrowIfNull(profile);

        lock (_syncLock)
        {
            if (!_telemetryHistory.TryGetValue(consignmentId, out var points) || points.Count == 0)
            {
                return new MeanKineticTemperatureResult(
                    ConsignmentId: consignmentId,
                    MktCelsius: 0.0,
                    TotalPoints: 0,
                    ExcursionDurationMinutes: 0.0,
                    ComplianceStatus: ThermalComplianceStatus.Optimal,
                    MinRecordedTempCelsius: 0.0,
                    MaxRecordedTempCelsius: 0.0);
            }

            return CalculateMktInternal(consignmentId, points, profile);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ColdChainTelemetryPoint> GetTelemetryHistory(string consignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            if (!_telemetryHistory.TryGetValue(consignmentId, out var points))
            {
                return [];
            }

            return points.OrderBy(p => p.TimestampUtc).ToList();
        }
    }

    /// <inheritdoc />
    public ColdChainTelemetryPoint? GetLatestTelemetryPoint(string consignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            if (!_telemetryHistory.TryGetValue(consignmentId, out var points) || points.Count == 0)
            {
                return null;
            }

            return points.MaxBy(p => p.TimestampUtc);
        }
    }

    /// <inheritdoc />
    public bool HasLightExposureBreach(string consignmentId, double thresholdLux = 50.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            if (!_telemetryHistory.TryGetValue(consignmentId, out var points))
            {
                return false;
            }

            return points.Any(p => p.LightLux > thresholdLux);
        }
    }

    /// <inheritdoc />
    public bool HasShockBreach(string consignmentId, double thresholdG = 3.5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            if (!_telemetryHistory.TryGetValue(consignmentId, out var points))
            {
                return false;
            }

            return points.Any(p => p.ShockGForce > thresholdG);
        }
    }

    /// <inheritdoc />
    public double GetMaxRecordedShock(string consignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            if (!_telemetryHistory.TryGetValue(consignmentId, out var points) || points.Count == 0)
            {
                return 0.0;
            }

            return points.Max(p => p.ShockGForce);
        }
    }

    /// <inheritdoc />
    public double GetMaxRecordedLight(string consignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            if (!_telemetryHistory.TryGetValue(consignmentId, out var points) || points.Count == 0)
            {
                return 0.0;
            }

            return points.Max(p => p.LightLux);
        }
    }

    /// <inheritdoc />
    public void Clear(string? consignmentId = null)
    {
        lock (_syncLock)
        {
            if (string.IsNullOrWhiteSpace(consignmentId))
            {
                _telemetryHistory.Clear();
                _dedupIndex.Clear();
            }
            else
            {
                _telemetryHistory.Remove(consignmentId);
                _dedupIndex.RemoveWhere(k => k.ConsignmentId == consignmentId);
            }
        }
    }

    private static MeanKineticTemperatureResult CalculateMktInternal(
        string consignmentId,
        List<ColdChainTelemetryPoint> points,
        CargoThermalProfile profile)
    {
        var sorted = points.OrderBy(p => p.TimestampUtc).ToList();
        var n = sorted.Count;
        if (n == 0)
        {
            return new MeanKineticTemperatureResult(
                ConsignmentId: consignmentId,
                MktCelsius: 0.0,
                TotalPoints: 0,
                ExcursionDurationMinutes: 0.0,
                ComplianceStatus: ThermalComplianceStatus.Optimal);
        }

        var dH = profile.ActivationEnergyJPerMol > 0 ? profile.ActivationEnergyJPerMol : DefaultActivationEnergy;
        var dH_over_R = dH / GasConstantR;

        double sumExp = 0.0;
        double minTemp = double.MaxValue;
        double maxTemp = double.MinValue;

        for (int i = 0; i < n; i++)
        {
            var tempC = sorted[i].TemperatureCelsius;
            if (tempC < minTemp) minTemp = tempC;
            if (tempC > maxTemp) maxTemp = tempC;

            var tempK = tempC + KelvinOffset;
            if (tempK < 0.001) tempK = 0.001;

            sumExp += Math.Exp(-dH_over_R / tempK);
        }

        var avgExp = sumExp / n;
        var mktKelvin = dH_over_R / (-Math.Log(avgExp));
        var mktCelsius = Math.Round(mktKelvin - KelvinOffset, 2);

        // Excursion Duration Calculation
        double excursionMinutes = 0.0;
        bool hasAnyOutlier = false;

        for (int i = 0; i < n; i++)
        {
            var isOut = sorted[i].TemperatureCelsius < profile.MinTempCelsius || sorted[i].TemperatureCelsius > profile.MaxTempCelsius;
            if (isOut)
            {
                hasAnyOutlier = true;
                if (i > 0)
                {
                    var dt = (sorted[i].TimestampUtc - sorted[i - 1].TimestampUtc).TotalMinutes;
                    if (dt > 0)
                    {
                        excursionMinutes += dt;
                    }
                }
            }
        }

        // Compliance status evaluation
        ThermalComplianceStatus complianceStatus;
        if (excursionMinutes <= 0.0)
        {
            if (hasAnyOutlier || mktCelsius < profile.MinTempCelsius - 1.0 || mktCelsius > profile.MaxTempCelsius + 1.0)
            {
                complianceStatus = ThermalComplianceStatus.MinorThermalExcursion;
            }
            else
            {
                complianceStatus = ThermalComplianceStatus.Optimal;
            }
        }
        else
        {
            if (excursionMinutes > 2.0 * profile.MaxExcursionMinutes ||
                mktCelsius > profile.MaxTempCelsius + 8.0 ||
                mktCelsius < profile.MinTempCelsius - 8.0)
            {
                complianceStatus = ThermalComplianceStatus.Spoiled;
            }
            else if (excursionMinutes > profile.MaxExcursionMinutes)
            {
                complianceStatus = ThermalComplianceStatus.SevereExcursionBreached;
            }
            else
            {
                complianceStatus = ThermalComplianceStatus.MinorThermalExcursion;
            }
        }

        return new MeanKineticTemperatureResult(
            ConsignmentId: consignmentId,
            MktCelsius: mktCelsius,
            TotalPoints: n,
            ExcursionDurationMinutes: Math.Round(excursionMinutes, 2),
            ComplianceStatus: complianceStatus,
            MinRecordedTempCelsius: Math.Round(minTemp, 2),
            MaxRecordedTempCelsius: Math.Round(maxTemp, 2));
    }
}
