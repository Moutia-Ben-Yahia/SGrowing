using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Logistics.ColdChain;
using Xunit;

namespace SGrowing.Tests.Logistics;

/// <summary>
/// Unit test suite for Phase 42:
/// Real-Time Decentralized Oracle Network, Satellite Geo-Fence &amp; Cargo Cold-Chain Verification.
/// </summary>
public sealed class ColdChainOracleAndSatelliteGeofenceTests
{
    private readonly ColdChainTelemetryOracleEngine _oracleEngine = new();
    private readonly SatelliteGeofenceTrackerService _geofenceTracker = new();
    private readonly ParametricCargoInsuranceLiquidator _liquidator;

    public ColdChainOracleAndSatelliteGeofenceTests()
    {
        _liquidator = new ParametricCargoInsuranceLiquidator(_oracleEngine);
    }

    #region 1. Telemetry Ingestion, Deduplication & History

    [Fact]
    public void IngestTelemetry_ValidPoint_SucceedsAndStoresInHistory()
    {
        var point = new ColdChainTelemetryPoint(
            ConsignmentId: "CSG-ALGIERS-1001",
            SensorId: "SNS-TH-01",
            TimestampUtc: new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            TemperatureCelsius: 4.5,
            RelativeHumidityPct: 45.0,
            ShockGForce: 0.8,
            LightLux: 5.0,
            BatteryPct: 98.0,
            Latitude: 36.7525,
            Longitude: 3.0420);

        var response = _oracleEngine.IngestTelemetry(point);

        response.Success.Should().BeTrue();
        response.ConsignmentId.Should().Be("CSG-ALGIERS-1001");
        response.DuplicateFiltered.Should().BeFalse();
        response.TotalPointsRecorded.Should().Be(1);
        response.CurrentCompliance.Should().Be(ThermalComplianceStatus.Optimal);

        var history = _oracleEngine.GetTelemetryHistory("CSG-ALGIERS-1001");
        history.Should().HaveCount(1);
        history[0].TemperatureCelsius.Should().Be(4.5);
    }

    [Fact]
    public void IngestTelemetry_DuplicatePoint_DedupesAndMaintainsHistoryCount()
    {
        var point = new ColdChainTelemetryPoint(
            ConsignmentId: "CSG-ALGIERS-1002",
            SensorId: "SNS-TH-02",
            TimestampUtc: new DateTime(2026, 11, 1, 10, 15, 0, DateTimeKind.Utc),
            TemperatureCelsius: 5.0,
            RelativeHumidityPct: 50.0,
            ShockGForce: 0.5,
            LightLux: 8.0,
            BatteryPct: 97.0,
            Latitude: 36.7530,
            Longitude: 3.0425);

        var firstResp = _oracleEngine.IngestTelemetry(point);
        var dupResp = _oracleEngine.IngestTelemetry(point);

        firstResp.DuplicateFiltered.Should().BeFalse();
        firstResp.TotalPointsRecorded.Should().Be(1);

        dupResp.DuplicateFiltered.Should().BeTrue();
        dupResp.TotalPointsRecorded.Should().Be(1);

        var history = _oracleEngine.GetTelemetryHistory("CSG-ALGIERS-1002");
        history.Should().HaveCount(1);
    }

    [Fact]
    public void IngestTelemetry_NullPoint_ThrowsArgumentNullException()
    {
        var act = () => _oracleEngine.IngestTelemetry(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IngestTelemetry_EmptyConsignmentId_ThrowsArgumentException()
    {
        var point = new ColdChainTelemetryPoint(
            ConsignmentId: "",
            SensorId: "SNS-01",
            TimestampUtc: DateTime.UtcNow,
            TemperatureCelsius: 4.0,
            RelativeHumidityPct: 50.0,
            ShockGForce: 1.0,
            LightLux: 10.0,
            BatteryPct: 100.0,
            Latitude: 36.0,
            Longitude: 3.0);

        var act = () => _oracleEngine.IngestTelemetry(point);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IngestBatch_MultiplePoints_IngestsAllChronologically()
    {
        var baseTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var points = Enumerable.Range(0, 5).Select(i => new ColdChainTelemetryPoint(
            ConsignmentId: "CSG-ORAN-2001",
            SensorId: "SNS-BATCH-01",
            TimestampUtc: baseTime.AddMinutes(i * 10),
            TemperatureCelsius: 3.0 + (i * 0.5),
            RelativeHumidityPct: 55.0,
            ShockGForce: 0.7,
            LightLux: 2.0,
            BatteryPct: 95.0 - i,
            Latitude: 35.6987,
            Longitude: -0.6349)).ToList();

        var responses = _oracleEngine.IngestBatch(points);

        responses.Should().HaveCount(5);
        responses.All(r => r.Success).Should().BeTrue();

        var history = _oracleEngine.GetTelemetryHistory("CSG-ORAN-2001");
        history.Should().HaveCount(5);
        history.First().TemperatureCelsius.Should().Be(3.0);
        history.Last().TemperatureCelsius.Should().Be(5.0);
    }

    [Fact]
    public void GetLatestTelemetryPoint_MultiplePoints_ReturnsPointWithMaxTimestamp()
    {
        var baseTime = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint("CSG-LATEST-01", "S1", baseTime, 4.0, 50, 0.5, 5, 90, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint("CSG-LATEST-01", "S1", baseTime.AddHours(2), 6.5, 52, 0.6, 5, 88, 36.8, 3.1));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint("CSG-LATEST-01", "S1", baseTime.AddHours(1), 5.2, 51, 0.5, 5, 89, 36.75, 3.05));

        var latest = _oracleEngine.GetLatestTelemetryPoint("CSG-LATEST-01");

        latest.Should().NotBeNull();
        latest!.TimestampUtc.Should().Be(baseTime.AddHours(2));
        latest.TemperatureCelsius.Should().Be(6.5);
    }

    [Fact]
    public void GetTelemetryHistory_NonExistentConsignment_ReturnsEmptyList()
    {
        var history = _oracleEngine.GetTelemetryHistory("NON-EXISTENT");
        history.Should().BeEmpty();
    }

    #endregion

    #region 2. Mean Kinetic Temperature (MKT) Mathematical Accuracy

    [Fact]
    public void CalculateMkt_IdenticalTemperatures_ReturnsExactSameTemperatureInCelsius()
    {
        const string consignmentId = "CSG-MKT-STEADY";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);
        var baseTime = new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc);

        // Constant 5.0 °C across all readings
        for (int i = 0; i < 10; i++)
        {
            _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(
                ConsignmentId: consignmentId,
                SensorId: "S-MKT-01",
                TimestampUtc: baseTime.AddMinutes(i * 15),
                TemperatureCelsius: 5.0,
                RelativeHumidityPct: 50.0,
                ShockGForce: 0.3,
                LightLux: 1.0,
                BatteryPct: 99.0,
                Latitude: 36.75,
                Longitude: 3.05));
        }

        var result = _oracleEngine.CalculateMkt(consignmentId, profile);

        result.TotalPoints.Should().Be(10);
        result.MktCelsius.Should().BeApproximately(5.0, 0.05);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.Optimal);
        result.ExcursionDurationMinutes.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMkt_FluctuatingTemperatures_YieldsHigherMktThanArithmeticMean()
    {
        const string consignmentId = "CSG-MKT-FLUCTUATING";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);
        var baseTime = new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc);

        // Half at 2.0 °C and half at 8.0 °C. Arithmetic mean is (2 + 8) / 2 = 5.0 °C.
        for (int i = 0; i < 5; i++)
        {
            _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(
                consignmentId, "S-MKT-02", baseTime.AddMinutes(i * 10), 2.0, 50, 0.2, 0, 95, 36.75, 3.05));
        }
        for (int i = 5; i < 10; i++)
        {
            _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(
                consignmentId, "S-MKT-02", baseTime.AddMinutes(i * 10), 8.0, 50, 0.2, 0, 95, 36.75, 3.05));
        }

        var result = _oracleEngine.CalculateMkt(consignmentId, profile);

        result.TotalPoints.Should().Be(10);
        // Due to the exponential weighting of higher temperatures in Arrhenius degradation, MKT > 5.0 °C
        result.MktCelsius.Should().BeGreaterThan(5.0);
        result.MktCelsius.Should().BeApproximately(5.55, 0.15);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.Optimal);
    }

    [Fact]
    public void CalculateMkt_EmptyTelemetryHistory_ReturnsZeroCelsiusAndOptimal()
    {
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);
        var result = _oracleEngine.CalculateMkt("CSG-EMPTY", profile);

        result.TotalPoints.Should().Be(0);
        result.MktCelsius.Should().Be(0.0);
        result.ExcursionDurationMinutes.Should().Be(0.0);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.Optimal);
    }

    [Fact]
    public void CalculateMkt_CustomActivationEnergy_ComputesWithSpecifiedEnergyProfile()
    {
        const string consignmentId = "CSG-CUSTOM-DH";
        var customProfile = new CargoThermalProfile(
            CargoType: CargoType.BiotechReagents,
            MinTempCelsius: -80.0,
            MaxTempCelsius: -20.0,
            MaxExcursionMinutes: 15.0,
            ActivationEnergyJPerMol: 100000.0);

        var baseTime = DateTime.UtcNow;
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, -50.0, 30, 0.1, 0, 90, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(10), -50.0, 30, 0.1, 0, 89, 36.7, 3.0));

        var result = _oracleEngine.CalculateMkt(consignmentId, customProfile);
        result.MktCelsius.Should().BeApproximately(-50.0, 0.05);
    }

    [Fact]
    public void CalculateMkt_FrozenFoodSubZeroProfile_ComputesCorrectMktAccurately()
    {
        const string consignmentId = "CSG-FROZEN-01";
        var frozenProfile = CargoThermalProfile.ForCargoType(CargoType.FrozenFood);
        var baseTime = DateTime.UtcNow;

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, -20.0, 70, 0.2, 0, 95, 36.0, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(30), -20.0, 70, 0.2, 0, 94, 36.0, 3.0));

        var result = _oracleEngine.CalculateMkt(consignmentId, frozenProfile);
        result.MktCelsius.Should().BeApproximately(-20.0, 0.05);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.Optimal);
    }

    #endregion

    #region 3. Thermal Excursion Duration & Compliance Status Escalation

    [Fact]
    public void CalculateMkt_StableTemperaturesWithinEnvelope_ReturnsOptimalStatus()
    {
        const string consignmentId = "CSG-STATUS-OPTIMAL";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma); // 2.0 to 8.0 °C
        var baseTime = DateTime.UtcNow;

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, 4.0, 45, 0.5, 0, 90, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(20), 5.5, 45, 0.5, 0, 89, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(40), 6.0, 45, 0.5, 0, 88, 36.7, 3.0));

        var result = _oracleEngine.CalculateMkt(consignmentId, profile);

        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.Optimal);
        result.ExcursionDurationMinutes.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMkt_ShortExcursionBelowThreshold_ReturnsMinorThermalExcursion()
    {
        const string consignmentId = "CSG-STATUS-MINOR";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma); // MaxExcursion = 30 min
        var baseTime = DateTime.UtcNow;

        // Ingest points: safe -> 15 min breach -> safe
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, 5.0, 45, 0.5, 0, 90, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(15), 11.0, 45, 0.5, 0, 89, 36.7, 3.0)); // 15 min excursion

        var result = _oracleEngine.CalculateMkt(consignmentId, profile);

        result.ExcursionDurationMinutes.Should().Be(15.0);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.MinorThermalExcursion);
    }

    [Fact]
    public void CalculateMkt_ExcursionExceedingThreshold_ReturnsSevereExcursionBreached()
    {
        const string consignmentId = "CSG-STATUS-SEVERE";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma); // MaxExcursion = 30 min
        var baseTime = DateTime.UtcNow;

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, 5.0, 45, 0.5, 0, 90, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(20), 12.0, 45, 0.5, 0, 89, 36.7, 3.0)); // 20 min
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(45), 13.0, 45, 0.5, 0, 88, 36.7, 3.0)); // +25 min = 45 min total

        var result = _oracleEngine.CalculateMkt(consignmentId, profile);

        result.ExcursionDurationMinutes.Should().Be(45.0);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.SevereExcursionBreached);
    }

    [Fact]
    public void CalculateMkt_ExtremeExcursionExceedingDoubleThreshold_ReturnsSpoiled()
    {
        const string consignmentId = "CSG-STATUS-SPOILED";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma); // MaxExcursion = 30 min -> 2x = 60 min
        var baseTime = DateTime.UtcNow;

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, 5.0, 45, 0.5, 0, 90, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(35), 14.0, 45, 0.5, 0, 89, 36.7, 3.0)); // 35 min
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(75), 15.0, 45, 0.5, 0, 88, 36.7, 3.0)); // +40 min = 75 min

        var result = _oracleEngine.CalculateMkt(consignmentId, profile);

        result.ExcursionDurationMinutes.Should().Be(75.0);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.Spoiled);
    }

    [Fact]
    public void CalculateMkt_SingleExcursionPoint_FlagsMinorExcursionWithoutInterval()
    {
        const string consignmentId = "CSG-SINGLE-EXCURSION";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", DateTime.UtcNow, 15.0, 45, 0.5, 0, 90, 36.7, 3.0));

        var result = _oracleEngine.CalculateMkt(consignmentId, profile);

        result.TotalPoints.Should().Be(1);
        result.ExcursionDurationMinutes.Should().Be(0.0);
        result.ComplianceStatus.Should().Be(ThermalComplianceStatus.MinorThermalExcursion);
    }

    #endregion

    #region 4. Photic Tamper & Shock Sensors Anomaly Detection

    [Fact]
    public void HasLightExposureBreach_LuxAbove50_DetectsTamperAttempt()
    {
        const string consignmentId = "CSG-LIGHT-BREACH";
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", DateTime.UtcNow, 4.0, 50, 0.2, 85.0, 90, 36.7, 3.0));

        _oracleEngine.HasLightExposureBreach(consignmentId).Should().BeTrue();
    }

    [Fact]
    public void HasLightExposureBreach_LuxBelow50_ReturnsFalse()
    {
        const string consignmentId = "CSG-LIGHT-SAFE";
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", DateTime.UtcNow, 4.0, 50, 0.2, 12.0, 90, 36.7, 3.0));

        _oracleEngine.HasLightExposureBreach(consignmentId).Should().BeFalse();
    }

    [Fact]
    public void HasShockBreach_GForceAbove3Point5_DetectsHighShockDrop()
    {
        const string consignmentId = "CSG-SHOCK-BREACH";
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", DateTime.UtcNow, 4.0, 50, 4.2, 5.0, 90, 36.7, 3.0));

        _oracleEngine.HasShockBreach(consignmentId, 3.5).Should().BeTrue();
    }

    [Fact]
    public void HasShockBreach_GForceBelow3Point5_ReturnsFalse()
    {
        const string consignmentId = "CSG-SHOCK-SAFE";
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", DateTime.UtcNow, 4.0, 50, 1.8, 5.0, 90, 36.7, 3.0));

        _oracleEngine.HasShockBreach(consignmentId, 3.5).Should().BeFalse();
    }

    [Fact]
    public void GetMaxRecordedShockAndLight_CalculatesExtremeExtremesAcrossTelemetryPoints()
    {
        const string consignmentId = "CSG-EXTREMES";
        var baseTime = DateTime.UtcNow;

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, 4.0, 50, 1.2, 10.0, 95, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(5), 4.5, 50, 4.8, 62.0, 94, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(10), 4.2, 50, 2.5, 8.0, 93, 36.7, 3.0));

        _oracleEngine.GetMaxRecordedShock(consignmentId).Should().Be(4.8);
        _oracleEngine.GetMaxRecordedLight(consignmentId).Should().Be(62.0);
    }

    #endregion

    #region 5. Ray-Casting Point-in-Polygon Geofence Collision Detection

    private static PolygonalGeofence CreateSampleAlgiersPortGeofence()
    {
        // Rectangular customs port zone: Lat 36.75 to 36.78, Lon 3.05 to 3.10
        var coords = new List<GeoCoordinate>
        {
            new(36.7500, 3.0500),
            new(36.7800, 3.0500),
            new(36.7800, 3.1000),
            new(36.7500, 3.1000)
        };

        return new PolygonalGeofence(
            GeofenceId: "GEO-PORT-ALGIERS",
            Name: "Port of Algiers Customs Bonded Terminal",
            Type: GeofenceType.CustomsBondedCorridor,
            Coordinates: coords,
            IsActive: true);
    }

    [Fact]
    public void IsPointInPolygon_InteriorPoint_ReturnsTrue()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        var inside = SatelliteGeofenceTrackerService.IsPointInPolygon(36.7650, 3.0750, geofence.Coordinates);
        inside.Should().BeTrue();
    }

    [Fact]
    public void IsPointInPolygon_ExteriorPoint_ReturnsFalse()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        var outside = SatelliteGeofenceTrackerService.IsPointInPolygon(36.7200, 3.0100, geofence.Coordinates);
        outside.Should().BeFalse();
    }

    [Fact]
    public void IsPointInPolygon_ExactVertex_ReturnsTrue()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        var onVertex = SatelliteGeofenceTrackerService.IsPointInPolygon(36.7500, 3.0500, geofence.Coordinates);
        onVertex.Should().BeTrue();
    }

    [Fact]
    public void IsPointInPolygon_PointOnPolygonEdge_ReturnsTrue()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        // Midpoint on south edge (36.7500, 3.0750)
        var onEdge = SatelliteGeofenceTrackerService.IsPointInPolygon(36.7500, 3.0750, geofence.Coordinates);
        onEdge.Should().BeTrue();
    }

    [Fact]
    public void IsPointInPolygon_PolygonWithLessThan3Vertices_ReturnsFalse()
    {
        var invalidCoords = new List<GeoCoordinate> { new(36.0, 3.0), new(36.5, 3.5) };
        var result = SatelliteGeofenceTrackerService.IsPointInPolygon(36.2, 3.2, invalidCoords);
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInsideGeofence_InactiveGeofence_ReturnsFalseEvenIfInside()
    {
        var geofence = new PolygonalGeofence(
            GeofenceId: "GEO-INACTIVE-01",
            Name: "Inactive Zone",
            Type: GeofenceType.WarehouseDock,
            Coordinates: new List<GeoCoordinate>
            {
                new(36.0, 3.0),
                new(37.0, 3.0),
                new(37.0, 4.0),
                new(36.0, 4.0)
            },
            IsActive: false);

        _geofenceTracker.RegisterGeofence(geofence);

        var inside = _geofenceTracker.IsInsideGeofence("GEO-INACTIVE-01", 36.5, 3.5);
        inside.Should().BeFalse();
    }

    #endregion

    #region 6. Boundary Crossing Transitions & Cryptographic Notarization

    [Fact]
    public void EvaluateCoordinate_OutsideToInside_EmitsEnteredTransitionEvent()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        _geofenceTracker.RegisterGeofence(geofence);

        const string consignmentId = "CSG-TRANS-01";
        var time1 = new DateTime(2026, 11, 1, 14, 0, 0, DateTimeKind.Utc);
        var time2 = new DateTime(2026, 11, 1, 14, 15, 0, DateTimeKind.Utc);

        // 1. Initial point outside
        var outsideEvents = _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7000, 3.0100, time1);
        outsideEvents.Should().BeEmpty();

        // 2. Next point moves inside
        var insideEvents = _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7650, 3.0750, time2);
        insideEvents.Should().HaveCount(1);

        var evt = insideEvents[0];
        evt.TransitionType.Should().Be("Entered");
        evt.GeofenceId.Should().Be(geofence.GeofenceId);
        evt.CryptographicNotarizationProof.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EvaluateCoordinate_InsideToOutside_EmitsExitedTransitionEvent()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        _geofenceTracker.RegisterGeofence(geofence);

        const string consignmentId = "CSG-TRANS-02";
        var time1 = new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc);
        var time2 = new DateTime(2026, 11, 1, 15, 30, 0, DateTimeKind.Utc);

        // 1. Starts inside -> Entered event
        var enterEvents = _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7650, 3.0750, time1);
        enterEvents.Should().HaveCount(1);
        enterEvents[0].TransitionType.Should().Be("Entered");

        // 2. Moves outside -> Exited event
        var exitEvents = _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7000, 3.0100, time2);
        exitEvents.Should().HaveCount(1);
        exitEvents[0].TransitionType.Should().Be("Exited");
    }

    [Fact]
    public void EvaluateCoordinate_ContinuousInside_DoesNotEmitDuplicateTransition()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        _geofenceTracker.RegisterGeofence(geofence);

        const string consignmentId = "CSG-TRANS-03";
        var time1 = new DateTime(2026, 11, 1, 16, 0, 0, DateTimeKind.Utc);
        var time2 = new DateTime(2026, 11, 1, 16, 10, 0, DateTimeKind.Utc);

        _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7650, 3.0750, time1);
        var subsequentEvents = _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7700, 3.0800, time2);

        subsequentEvents.Should().BeEmpty();
    }

    [Fact]
    public void EvaluateCoordinate_GeneratesValidHmacSha256NotarizationProof()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        _geofenceTracker.RegisterGeofence(geofence);

        const string consignmentId = "CSG-PROOF-01";
        const string secretKey = "custom-secure-notary-key-xyz";
        var timestamp = new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc);

        var events = _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7650, 3.0750, timestamp, secretKey);
        events.Should().HaveCount(1);

        var transition = events[0];
        _geofenceTracker.VerifyNotarizationProof(transition, secretKey).Should().BeTrue();
    }

    [Fact]
    public void VerifyNotarizationProof_TamperedTransition_FailsVerification()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        _geofenceTracker.RegisterGeofence(geofence);

        const string consignmentId = "CSG-TAMPER-01";
        const string secretKey = "custom-secure-notary-key-xyz";
        var timestamp = DateTime.UtcNow;

        var events = _geofenceTracker.EvaluateCoordinate(consignmentId, 36.7650, 3.0750, timestamp, secretKey);
        var originalEvent = events[0];

        // Tampered event with altered latitude
        var tamperedEvent = originalEvent with { Latitude = 36.8888 };
        _geofenceTracker.VerifyNotarizationProof(tamperedEvent, secretKey).Should().BeFalse();
    }

    #endregion

    #region 7. Autonomous Parametric Cargo Insurance Liquidation

    [Fact]
    public void ProcessTelemetryForClaims_ThermalBreachExceedingThreshold_LiquidatesAndDischargesBuyerCod()
    {
        const string consignmentId = "CSG-CLAIM-THERMAL";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma); // 30 min max excursion
        var baseTime = DateTime.UtcNow;

        // Ingest telemetry with 45 minutes of thermal excursion
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime, 5.0, 45, 0.5, 0, 90, 36.7, 3.0));
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", baseTime.AddMinutes(45), 14.0, 45, 0.5, 0, 89, 36.7, 3.0));

        var claim = _liquidator.ProcessTelemetryForClaims(
            consignmentId: consignmentId,
            profile: profile,
            policyPayoutDzd: 250000m,
            recipientId: "BUYER-PHARMA-CORP",
            secretKey: "test-claim-key-123",
            policyNumber: "POL-COLD-999");

        claim.Status.Should().Be(InsuranceClaimStatus.AutoApprovedLiquidated);
        claim.BuyerCodObligationVoided.Should().BeTrue();
        claim.LiquidationAmountDzd.Should().Be(250000m);
        claim.BreachType.Should().Be(nameof(BreachType.ThermalExcursion));
        claim.LiquidatedAtUtc.Should().NotBeNull();
        claim.LiquidationCertificate.Should().NotBeNullOrWhiteSpace();

        var retrieved = _liquidator.GetClaim(claim.ClaimId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(InsuranceClaimStatus.AutoApprovedLiquidated);
    }

    [Fact]
    public void ProcessTelemetryForClaims_ExtremeShockAbove5Gs_LiquidatesAndDischargesBuyerCod()
    {
        const string consignmentId = "CSG-CLAIM-SHOCK";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);

        // Normal temperature, but catastrophic 6.2 G shock drop
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(
            ConsignmentId: consignmentId,
            SensorId: "S-SHOCK-01",
            TimestampUtc: DateTime.UtcNow,
            TemperatureCelsius: 5.0,
            RelativeHumidityPct: 45.0,
            ShockGForce: 6.2,
            LightLux: 5.0,
            BatteryPct: 95.0,
            Latitude: 36.7,
            Longitude: 3.0));

        var claim = _liquidator.ProcessTelemetryForClaims(
            consignmentId: consignmentId,
            profile: profile,
            policyPayoutDzd: 500000m,
            recipientId: "BUYER-HOSPITAL-CENTRAL",
            secretKey: "test-claim-key-123");

        claim.Status.Should().Be(InsuranceClaimStatus.AutoApprovedLiquidated);
        claim.BuyerCodObligationVoided.Should().BeTrue();
        claim.BreachType.Should().Be(nameof(BreachType.ShockImpact));
        claim.BreachedValue.Should().Be(6.2);
        claim.LiquidationAmountDzd.Should().Be(500000m);
    }

    [Fact]
    public void ProcessTelemetryForClaims_NormalTelemetryWithinLimits_RejectsClaim()
    {
        const string consignmentId = "CSG-CLAIM-NORMAL";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);

        // Optimal temperature, no shock
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(
            ConsignmentId: consignmentId,
            SensorId: "S1",
            TimestampUtc: DateTime.UtcNow,
            TemperatureCelsius: 5.0,
            RelativeHumidityPct: 45.0,
            ShockGForce: 0.5,
            LightLux: 0.0,
            BatteryPct: 95.0,
            Latitude: 36.7,
            Longitude: 3.0));

        var claim = _liquidator.ProcessTelemetryForClaims(
            consignmentId: consignmentId,
            profile: profile,
            policyPayoutDzd: 100000m,
            recipientId: "BUYER-REJECTED");

        claim.Status.Should().Be(InsuranceClaimStatus.Rejected);
        claim.BuyerCodObligationVoided.Should().BeFalse();
        claim.LiquidationAmountDzd.Should().Be(0m);
        claim.LiquidatedAtUtc.Should().BeNull();
        claim.LiquidationCertificate.Should().BeNull();
    }

    [Fact]
    public void ProcessTelemetryForClaims_IssuesVerifiableLiquidationCertificate()
    {
        const string consignmentId = "CSG-VERIFY-CERT";
        const string key = "notary-secret-auth-key-2026";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", DateTime.UtcNow, 4.0, 50, 7.5, 0, 90, 36.7, 3.0));

        var claim = _liquidator.ProcessTelemetryForClaims(consignmentId, profile, 150000m, "RECIPIENT-01", key);

        claim.Status.Should().Be(InsuranceClaimStatus.AutoApprovedLiquidated);
        _liquidator.VerifyLiquidationCertificate(claim, key).Should().BeTrue();
    }

    [Fact]
    public void VerifyLiquidationCertificate_InvalidSignatureOrTamperedClaim_FailsVerification()
    {
        const string consignmentId = "CSG-TAMPER-CERT";
        const string key = "notary-secret-auth-key-2026";
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);

        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint(consignmentId, "S1", DateTime.UtcNow, 4.0, 50, 8.0, 0, 90, 36.7, 3.0));

        var claim = _liquidator.ProcessTelemetryForClaims(consignmentId, profile, 200000m, "RECIPIENT-02", key);

        // 1. Wrong key
        _liquidator.VerifyLiquidationCertificate(claim, "wrong-key").Should().BeFalse();

        // 2. Tampered liquidation amount
        var tamperedClaim = claim with { LiquidationAmountDzd = 999999m };
        _liquidator.VerifyLiquidationCertificate(tamperedClaim, key).Should().BeFalse();
    }

    [Fact]
    public void ListLiquidatedClaims_ReturnsOnlyAutoApprovedLiquidatedClaims()
    {
        var profile = CargoThermalProfile.ForCargoType(CargoType.VaccinesPharma);

        // Consignment 1: breached shock -> liquidated
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint("CSG-LIST-01", "S1", DateTime.UtcNow, 4.0, 50, 6.0, 0, 90, 36.7, 3.0));
        _liquidator.ProcessTelemetryForClaims("CSG-LIST-01", profile, 50000m, "R1");

        // Consignment 2: optimal -> rejected
        _oracleEngine.IngestTelemetry(new ColdChainTelemetryPoint("CSG-LIST-02", "S1", DateTime.UtcNow, 4.0, 50, 0.5, 0, 90, 36.7, 3.0));
        _liquidator.ProcessTelemetryForClaims("CSG-LIST-02", profile, 50000m, "R2");

        var liquidated = _liquidator.ListLiquidatedClaims();
        liquidated.Should().Contain(c => c.ConsignmentId == "CSG-LIST-01");
        liquidated.Should().NotContain(c => c.ConsignmentId == "CSG-LIST-02");
    }

    #endregion

    #region 8. Concurrency & Thread-Safety

    [Fact]
    public void ConcurrentTelemetryIngestion_MaintainsIntegrityUnderParallelFeeds()
    {
        const string consignmentId = "CSG-CONCURRENT-01";
        const int totalThreads = 10;
        const int pointsPerThread = 50;
        var baseTime = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        Parallel.For(0, totalThreads, threadIdx =>
        {
            for (int i = 0; i < pointsPerThread; i++)
            {
                var point = new ColdChainTelemetryPoint(
                    ConsignmentId: consignmentId,
                    SensorId: $"SENSOR-{threadIdx}",
                    TimestampUtc: baseTime.AddSeconds((threadIdx * pointsPerThread) + i),
                    TemperatureCelsius: 4.0 + ((i % 5) * 0.2),
                    RelativeHumidityPct: 50.0,
                    ShockGForce: 0.5,
                    LightLux: 2.0,
                    BatteryPct: 90.0,
                    Latitude: 36.7,
                    Longitude: 3.0);

                _oracleEngine.IngestTelemetry(point);
            }
        });

        var history = _oracleEngine.GetTelemetryHistory(consignmentId);
        history.Should().HaveCount(totalThreads * pointsPerThread);
    }

    [Fact]
    public void ConcurrentGeofenceEvaluation_MaintainsPresenceStateConsistency()
    {
        var geofence = CreateSampleAlgiersPortGeofence();
        _geofenceTracker.RegisterGeofence(geofence);

        const int iterations = 100;
        var time = DateTime.UtcNow;

        Parallel.For(0, iterations, i =>
        {
            var cid = $"CSG-PARALLEL-GEO-{i}";
            var events = _geofenceTracker.EvaluateCoordinate(cid, 36.7650, 3.0750, time);
            events.Should().HaveCount(1);
            events[0].TransitionType.Should().Be("Entered");
        });

        _geofenceTracker.GetActiveGeofences().Should().HaveCount(1);
    }

    #endregion

    #region 9. ServiceCollection Dependency Injection Descriptors (Without BuildServiceProvider)

    [Fact]
    public void AddColdChainServices_RegistersAllServicesAsSingletonsWithoutBuildServiceProvider()
    {
        // Explicit boundary requirement: Direct ServiceDescriptor assertion on ServiceCollection without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddColdChainServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IColdChainTelemetryOracleEngine) &&
            d.ImplementationType == typeof(ColdChainTelemetryOracleEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISatelliteGeofenceTrackerService) &&
            d.ImplementationType == typeof(SatelliteGeofenceTrackerService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IParametricCargoInsuranceLiquidator) &&
            d.ImplementationType == typeof(ParametricCargoInsuranceLiquidator) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
