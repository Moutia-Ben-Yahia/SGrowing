using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.Shrinkage;
using Xunit;

namespace SGrowing.Tests.Commerce;

/// <summary>
/// Comprehensive unit test suite for Phase 48:
/// Autonomous Real-Time Inventory Shrinkage Radar, RFID/BLE Mesh &amp; Theft Prevention.
/// </summary>
public sealed class RfidBleShrinkagePreventionTests
{
    private readonly RfidBleIndoorPositioningEngine _engine = new();
    private readonly AisleDiscrepancyDetectorService _discrepancyDetector;
    private readonly DockShrinkageAlertService _dockAlertService;

    public RfidBleShrinkagePreventionTests()
    {
        _discrepancyDetector = new AisleDiscrepancyDetectorService(_engine);
        _dockAlertService = new DockShrinkageAlertService();
    }

    private static double DistanceToRssi(double distanceMeters, double a = -59.0, double n = 2.2)
    {
        return -10.0 * n * Math.Log10(Math.Max(distanceMeters, 0.05)) + a;
    }

    #region 1. Log-Distance Path Loss Propagation Model

    [Fact]
    public void CalculateDistanceMeters_AtReferencePowerMinus59Dbm_ReturnsExactlyOneMeter()
    {
        double distance = _engine.CalculateDistanceMeters(-59.0);

        distance.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CalculateDistanceMeters_AtMinus74Point38Dbm_ReturnsApproximatelyFiveMeters()
    {
        // RSSI = -10 * 2.2 * log10(5) - 59 = -22 * 0.69897 - 59 = -74.377 dBm
        double expectedRssi = DistanceToRssi(5.0);
        double distance = _engine.CalculateDistanceMeters(expectedRssi);

        distance.Should().BeApproximately(5.0, 0.01);
    }

    [Fact]
    public void CalculateDistanceMeters_AtMinus81Dbm_ReturnsApproximatelyTenMeters()
    {
        // RSSI = -10 * 2.2 * log10(10) - 59 = -22 * 1 - 59 = -81.0 dBm
        double distance = _engine.CalculateDistanceMeters(-81.0);

        distance.Should().BeApproximately(10.0, 0.01);
    }

    [Fact]
    public void CalculateDistanceMeters_VeryStrongSignal_ClampsToMinDistanceThreshold()
    {
        double distance = _engine.CalculateDistanceMeters(+5.0);

        distance.Should().Be(0.05);
    }

    [Fact]
    public void CalculateDistanceMeters_VeryWeakSignal_ClampsToMaxDistanceThreshold()
    {
        double distance = _engine.CalculateDistanceMeters(-150.0);

        distance.Should().Be(200.0);
    }

    [Fact]
    public void CalculateDistanceMeters_CustomPathLossParameters_ComputesExpectedDistance()
    {
        // a = -50 dBm, n = 3.0, RSSI for 4m = -10 * 3.0 * log10(4) - 50 = -30 * 0.60206 - 50 = -68.06 dBm
        double distance = _engine.CalculateDistanceMeters(-68.0618, txPowerA: -50.0, pathLossExponent: 3.0);

        distance.Should().BeApproximately(4.0, 0.02);
    }

    #endregion

    #region 2. Anchor Registry & Fixed Nodes

    [Fact]
    public void RegisterAnchor_ValidAnchor_AddsToMeshAndCanBeRetrieved()
    {
        var anchor = new FixedAnchorNode("ANC-TEST-01", 12.5, 34.0, 4.0, 1, WarehouseZone.HighValueAisle, "MeshGate-V3");

        _engine.RegisterAnchor(anchor);
        var retrieved = _engine.GetAnchor("ANC-TEST-01");

        retrieved.Should().NotBeNull();
        retrieved!.X.Should().Be(12.5);
        retrieved.Y.Should().Be(34.0);
        retrieved.Zone.Should().Be(WarehouseZone.HighValueAisle);
    }

    [Fact]
    public void RegisterAnchor_DuplicateAnchorId_OverwritesExistingAnchor()
    {
        var anchor1 = new FixedAnchorNode("ANC-DUP-01", 10.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle);
        var anchor2 = new FixedAnchorNode("ANC-DUP-01", 20.0, 20.0, 4.5, 2, WarehouseZone.BulkStorageRack);

        _engine.RegisterAnchor(anchor1);
        _engine.RegisterAnchor(anchor2);
        var retrieved = _engine.GetAnchor("ANC-DUP-01");

        retrieved.Should().NotBeNull();
        retrieved!.X.Should().Be(20.0);
        retrieved.Y.Should().Be(20.0);
        retrieved.Floor.Should().Be(2);
        retrieved.Zone.Should().Be(WarehouseZone.BulkStorageRack);
    }

    [Fact]
    public void GetAllAnchors_ReturnsAllRegisteredAndDefaultAnchors()
    {
        var anchors = _engine.GetAllAnchors();

        anchors.Should().NotBeEmpty();
        anchors.Count.Should().BeGreaterThanOrEqualTo(8);
        anchors.Should().Contain(a => a.AnchorId == "ANC-HVA-01");
        anchors.Should().Contain(a => a.AnchorId == "ANC-DCK-01");
    }

    [Fact]
    public void GetAnchor_UnknownId_ReturnsNull()
    {
        var anchor = _engine.GetAnchor("ANC-NON-EXISTENT");

        anchor.Should().BeNull();
    }

    [Fact]
    public void RegisterAnchor_NullAnchor_ThrowsArgumentNullException()
    {
        var act = () => _engine.RegisterAnchor(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 3. 2D Trilateration & Positioning Accuracy

    [Fact]
    public void EstimateLocation_ThreeAnchors_CalculatesExactCoordinatesWithLowErrorMargin()
    {
        // Setup known anchors
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-TRI-1", 0.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-TRI-2", 30.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-TRI-3", 0.0, 40.0, 4.0, 1, WarehouseZone.HighValueAisle));

        // Target point (10, 15)
        double targetX = 10.0;
        double targetY = 15.0;

        double d1 = Math.Sqrt(Math.Pow(targetX - 0.0, 2) + Math.Pow(targetY - 0.0, 2));
        double d2 = Math.Sqrt(Math.Pow(targetX - 30.0, 2) + Math.Pow(targetY - 0.0, 2));
        double d3 = Math.Sqrt(Math.Pow(targetX - 0.0, 2) + Math.Pow(targetY - 40.0, 2));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-TRI-001",
            Sku: "SKU-TRI-001",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-TRI-1", DistanceToRssi(d1)),
                new RssiAnchorReading("ANC-TRI-2", DistanceToRssi(d2)),
                new RssiAnchorReading("ANC-TRI-3", DistanceToRssi(d3))
            ],
            BatteryPct: 98.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        location.X.Should().BeApproximately(targetX, 0.1);
        location.Y.Should().BeApproximately(targetY, 0.1);
        location.AccuracyMeters.Should().BeLessThan(0.5);
    }

    [Fact]
    public void EstimateLocation_FourAnchors_OverdeterminedSystemSolvesTargetWithHighAccuracy()
    {
        // 4 corner anchors forming a 30m x 40m bounding grid
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-GRID-1", 0.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-GRID-2", 30.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-GRID-3", 0.0, 40.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-GRID-4", 30.0, 40.0, 4.0, 1, WarehouseZone.HighValueAisle));

        // Target point (18.5, 24.2)
        double targetX = 18.5;
        double targetY = 24.2;

        double d1 = Math.Sqrt(Math.Pow(targetX - 0.0, 2) + Math.Pow(targetY - 0.0, 2));
        double d2 = Math.Sqrt(Math.Pow(targetX - 30.0, 2) + Math.Pow(targetY - 0.0, 2));
        double d3 = Math.Sqrt(Math.Pow(targetX - 0.0, 2) + Math.Pow(targetY - 40.0, 2));
        double d4 = Math.Sqrt(Math.Pow(targetX - 30.0, 2) + Math.Pow(targetY - 40.0, 2));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-GRID-002",
            Sku: "SKU-LAPTOP-X1",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-GRID-1", DistanceToRssi(d1)),
                new RssiAnchorReading("ANC-GRID-2", DistanceToRssi(d2)),
                new RssiAnchorReading("ANC-GRID-3", DistanceToRssi(d3)),
                new RssiAnchorReading("ANC-GRID-4", DistanceToRssi(d4))
            ],
            BatteryPct: 95.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        location.X.Should().BeApproximately(targetX, 0.1);
        location.Y.Should().BeApproximately(targetY, 0.1);
        location.AccuracyMeters.Should().BeLessThan(0.5);
    }

    [Fact]
    public void EstimateLocation_WithSimulatedNoisyRssi_MaintainsAccuracyUnderOneMeter()
    {
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-NOISE-1", 0.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-NOISE-2", 10.0, 0.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-NOISE-3", 0.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-NOISE-4", 10.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));

        double targetX = 5.0;
        double targetY = 5.0;

        double d1 = Math.Sqrt(Math.Pow(targetX - 0.0, 2) + Math.Pow(targetY - 0.0, 2));
        double d2 = Math.Sqrt(Math.Pow(targetX - 10.0, 2) + Math.Pow(targetY - 0.0, 2));
        double d3 = Math.Sqrt(Math.Pow(targetX - 0.0, 2) + Math.Pow(targetY - 10.0, 2));
        double d4 = Math.Sqrt(Math.Pow(targetX - 10.0, 2) + Math.Pow(targetY - 10.0, 2));

        // Inject realistic sensor noise (+/- 0.3 dBm)
        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-NOISE-003",
            Sku: "SKU-NOISE-003",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-NOISE-1", DistanceToRssi(d1) + 0.3),
                new RssiAnchorReading("ANC-NOISE-2", DistanceToRssi(d2) - 0.2),
                new RssiAnchorReading("ANC-NOISE-3", DistanceToRssi(d3) + 0.25),
                new RssiAnchorReading("ANC-NOISE-4", DistanceToRssi(d4) - 0.25)
            ],
            BatteryPct: 88.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        // Position error must remain under 1.0m
        double error = Math.Sqrt(Math.Pow(location.X - targetX, 2) + Math.Pow(location.Y - targetY, 2));
        error.Should().BeLessThan(1.0);
    }

    [Fact]
    public void EstimateLocation_SingleAnchor_FallsBackToAnchorPosition()
    {
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-SINGLE-01", 14.0, 22.0, 3.5, 1, WarehouseZone.HighValueAisle));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-SINGLE-01",
            Sku: "SKU-SINGLE-01",
            RssiReadings: [new RssiAnchorReading("ANC-SINGLE-01", -65.0)],
            BatteryPct: 100.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        location.X.Should().Be(14.0);
        location.Y.Should().Be(22.0);
        location.Floor.Should().Be(1);
        location.Zone.Should().Be(WarehouseZone.HighValueAisle);
    }

    [Fact]
    public void EstimateLocation_TwoAnchors_FallsBackToWeightedAverageCoordinates()
    {
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-TWO-1", 10.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-TWO-2", 20.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));

        // Equal distance 5m to both
        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-TWO-01",
            Sku: "SKU-TWO-01",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-TWO-1", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-TWO-2", DistanceToRssi(5.0))
            ],
            BatteryPct: 90.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        location.X.Should().BeApproximately(15.0, 0.1);
        location.Y.Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public void EstimateLocation_ZeroRecognizedAnchors_ReturnsDefaultOriginPoint()
    {
        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-ZERO-01",
            Sku: "SKU-ZERO-01",
            RssiReadings: [new RssiAnchorReading("UNKNOWN-ANC", -70.0)],
            BatteryPct: 75.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        location.X.Should().Be(0.0);
        location.Y.Should().Be(0.0);
        location.AccuracyMeters.Should().Be(50.0);
    }

    [Fact]
    public void EstimateLocation_CollinearAnchors_DegenerateMatrixFallsBackGracefullyWithoutException()
    {
        // Anchors in a straight horizontal line
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-LINE-1", 10.0, 20.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-LINE-2", 20.0, 20.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-LINE-3", 30.0, 20.0, 4.0, 1, WarehouseZone.HighValueAisle));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-LINE-01",
            Sku: "SKU-LINE-01",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-LINE-1", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-LINE-2", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-LINE-3", DistanceToRssi(5.0))
            ],
            BatteryPct: 92.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        location.Should().NotBeNull();
        location.Y.Should().BeApproximately(20.0, 0.5);
    }

    #endregion

    #region 4. Warehouse Zone Bounding Box Resolution

    [Theory]
    [InlineData(10.0, 15.0, WarehouseZone.HighValueAisle)]
    [InlineData(30.0, 40.0, WarehouseZone.HighValueAisle)]
    [InlineData(60.0, 20.0, WarehouseZone.BulkStorageRack)]
    [InlineData(75.0, 40.0, WarehouseZone.BulkStorageRack)]
    [InlineData(25.0, 60.0, WarehouseZone.PackingStation)]
    [InlineData(45.0, 70.0, WarehouseZone.PackingStation)]
    [InlineData(65.0, 65.0, WarehouseZone.QuarantineZone)]
    [InlineData(80.0, 80.0, WarehouseZone.QuarantineZone)]
    [InlineData(91.0, 92.0, WarehouseZone.DockExitGate)]
    [InlineData(96.0, 98.0, WarehouseZone.DockExitGate)]
    public void ResolveZone_CoordinatesAcrossWarehouse_MapsToCorrectBoundingBox(double x, double y, WarehouseZone expectedZone)
    {
        var zone = _engine.ResolveZone(x, y);

        zone.Should().Be(expectedZone);
    }

    [Fact]
    public void IndoorLocationPoint_IsInDockExitPerimeter_ReturnsTrueOnlyWhenWithinPerimeter()
    {
        var dockPoint = new IndoorLocationPoint(92.0, 94.0, 4.0, 1, WarehouseZone.DockExitGate, 0.4, DateTime.UtcNow);
        var aislePoint = new IndoorLocationPoint(20.0, 30.0, 4.0, 1, WarehouseZone.HighValueAisle, 0.3, DateTime.UtcNow);

        dockPoint.IsInDockExitPerimeter().Should().BeTrue();
        aislePoint.IsInDockExitPerimeter().Should().BeFalse();
    }

    #endregion

    #region 5. Multi-Floor & Asset State Tracking

    [Fact]
    public void EstimateLocation_MultiFloorAnchors_SelectsFloorOfNearestAnchor()
    {
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-FL1", 10.0, 10.0, 3.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-FL2", 10.0, 10.0, 7.0, 2, WarehouseZone.HighValueAisle));

        // Tag is much closer to Floor 2 anchor (distance 1.5m vs 8m)
        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-FL-02",
            Sku: "SKU-FL-02",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-FL1", DistanceToRssi(8.0)),
                new RssiAnchorReading("ANC-FL2", DistanceToRssi(1.5))
            ],
            BatteryPct: 85.0,
            TimestampUtc: DateTime.UtcNow);

        var location = _engine.EstimateLocation(beacon);

        location.Floor.Should().Be(2);
    }

    [Fact]
    public void EstimateLocation_TracksAssetLocationAndUpdatesRegistry()
    {
        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-ASSET-01",
            Sku: "SKU-VALUABLE-PHONE",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-HVA-01", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-HVA-02", DistanceToRssi(25.0)),
                new RssiAnchorReading("ANC-HVA-03", DistanceToRssi(35.0))
            ],
            BatteryPct: 91.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beacon);

        var assetLoc = _engine.GetAssetLocation("TAG-ASSET-01");
        assetLoc.Should().NotBeNull();
        assetLoc!.Zone.Should().Be(WarehouseZone.HighValueAisle);

        var allAssets = _engine.GetAllTrackedAssets();
        allAssets.Should().ContainKey("TAG-ASSET-01");
        allAssets["TAG-ASSET-01"].Sku.Should().Be("SKU-VALUABLE-PHONE");
        allAssets["TAG-ASSET-01"].BatteryPct.Should().Be(91.0);
    }

    #endregion

    #region 6. Aisle Discrepancy Detection & Planograms

    [Fact]
    public void AuditAssetPlacement_AssetInExpectedZone_ReturnsNull()
    {
        // Position asset inside HighValueAisle
        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-MATCH-01",
            Sku: "SKU-GOLD-WATCH",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-HVA-01", DistanceToRssi(15.0)),
                new RssiAnchorReading("ANC-HVA-02", DistanceToRssi(15.0)),
                new RssiAnchorReading("ANC-HVA-03", DistanceToRssi(25.0))
            ],
            BatteryPct: 99.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beacon);

        var incident = _discrepancyDetector.AuditAssetPlacement(
            tagId: "TAG-MATCH-01",
            sku: "SKU-GOLD-WATCH",
            expectedZone: WarehouseZone.HighValueAisle,
            hasActivePickTask: false);

        incident.Should().BeNull();
        _discrepancyDetector.GetActiveDiscrepancies().Should().BeEmpty();
    }

    [Fact]
    public void AuditAssetPlacement_AssetMisplacedWithoutPickTask_RaisesSuspiciousMovement()
    {
        // Position asset in BulkStorageRack (e.g. x=60, y=20)
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-BSR-T1", 50.0, 10.0, 4.0, 1, WarehouseZone.BulkStorageRack));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-BSR-T2", 70.0, 10.0, 4.0, 1, WarehouseZone.BulkStorageRack));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-BSR-T3", 60.0, 30.0, 4.0, 1, WarehouseZone.BulkStorageRack));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-MISPLACED-01",
            Sku: "SKU-PREMIUM-GPU",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-BSR-T1", DistanceToRssi(Math.Sqrt(100 + 100))),
                new RssiAnchorReading("ANC-BSR-T2", DistanceToRssi(Math.Sqrt(100 + 100))),
                new RssiAnchorReading("ANC-BSR-T3", DistanceToRssi(10.0))
            ],
            BatteryPct: 94.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beacon);

        // Expected in HighValueAisle, but found in BulkStorageRack without an authorized pick task
        var incident = _discrepancyDetector.AuditAssetPlacement(
            tagId: "TAG-MISPLACED-01",
            sku: "SKU-PREMIUM-GPU",
            expectedZone: WarehouseZone.HighValueAisle,
            hasActivePickTask: false);

        incident.Should().NotBeNull();
        incident!.ActualZone.Should().Be(WarehouseZone.BulkStorageRack);
        incident.ExpectedZone.Should().Be(WarehouseZone.HighValueAisle);
        incident.HasActivePickTask.Should().BeFalse();
        incident.Severity.Should().Be(ShrinkageAlertSeverity.SuspiciousMovement);
        incident.DistanceDeviationMeters.Should().BeGreaterThan(30.0);

        _discrepancyDetector.GetActiveDiscrepancies().Should().ContainSingle();
    }

    [Fact]
    public void AuditAssetPlacement_AssetMisplacedWithActivePickTask_RaisesInformationalMisplacement()
    {
        // Position asset in PackingStation (e.g. x=25, y=65)
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-PST-T1", 10.0, 50.0, 3.5, 1, WarehouseZone.PackingStation));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-PST-T2", 40.0, 50.0, 3.5, 1, WarehouseZone.PackingStation));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-PST-T3", 25.0, 80.0, 3.5, 1, WarehouseZone.PackingStation));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-PICK-01",
            Sku: "SKU-HEADSET-PRO",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-PST-T1", DistanceToRssi(Math.Sqrt(225 + 225))),
                new RssiAnchorReading("ANC-PST-T2", DistanceToRssi(Math.Sqrt(225 + 225))),
                new RssiAnchorReading("ANC-PST-T3", DistanceToRssi(15.0))
            ],
            BatteryPct: 87.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beacon);

        // Expected in BulkStorageRack, currently at PackingStation with authorized pick task
        var incident = _discrepancyDetector.AuditAssetPlacement(
            tagId: "TAG-PICK-01",
            sku: "SKU-HEADSET-PRO",
            expectedZone: WarehouseZone.BulkStorageRack,
            hasActivePickTask: true);

        incident.Should().NotBeNull();
        incident!.Severity.Should().Be(ShrinkageAlertSeverity.InformationalMisplacement);
        incident.HasActivePickTask.Should().BeTrue();
    }

    [Fact]
    public void AuditAssetPlacement_AssetInDockExitWithoutPickTask_RaisesCriticalTheftAlert()
    {
        // Position asset at DockExitGate (95, 95)
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-DCK-T1", 90.0, 90.0, 5.0, 1, WarehouseZone.DockExitGate));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-DCK-T2", 98.0, 90.0, 5.0, 1, WarehouseZone.DockExitGate));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-DCK-T3", 95.0, 98.0, 5.0, 1, WarehouseZone.DockExitGate));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-THEFT-01",
            Sku: "SKU-CAMERA-LENS",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-DCK-T1", DistanceToRssi(Math.Sqrt(25 + 25))),
                new RssiAnchorReading("ANC-DCK-T2", DistanceToRssi(Math.Sqrt(9 + 25))),
                new RssiAnchorReading("ANC-DCK-T3", DistanceToRssi(3.0))
            ],
            BatteryPct: 96.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beacon);

        var incident = _discrepancyDetector.AuditAssetPlacement(
            tagId: "TAG-THEFT-01",
            sku: "SKU-CAMERA-LENS",
            expectedZone: WarehouseZone.HighValueAisle,
            hasActivePickTask: false);

        incident.Should().NotBeNull();
        incident!.Severity.Should().Be(ShrinkageAlertSeverity.CriticalTheftAlert);
        incident.ActualZone.Should().Be(WarehouseZone.DockExitGate);
    }

    [Fact]
    public void AuditAssetPlacement_ReturningToExpectedZone_AutoResolvesActiveDiscrepancy()
    {
        // First place in QuarantineZone (discrepancy)
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-QZ-1", 60.0, 60.0, 4.0, 1, WarehouseZone.QuarantineZone));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-QZ-2", 70.0, 60.0, 4.0, 1, WarehouseZone.QuarantineZone));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-QZ-3", 65.0, 75.0, 4.0, 1, WarehouseZone.QuarantineZone));

        var beaconMisplaced = new RfidBleTelemetryBeacon(
            TagId: "TAG-RETURN-01",
            Sku: "SKU-TABLET-01",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-QZ-1", DistanceToRssi(7.0)),
                new RssiAnchorReading("ANC-QZ-2", DistanceToRssi(7.0)),
                new RssiAnchorReading("ANC-QZ-3", DistanceToRssi(10.0))
            ],
            BatteryPct: 80.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beaconMisplaced);
        var incident = _discrepancyDetector.AuditAssetPlacement("TAG-RETURN-01", "SKU-TABLET-01", WarehouseZone.HighValueAisle, false);
        incident.Should().NotBeNull();
        _discrepancyDetector.GetActiveDiscrepancies().Should().ContainSingle();

        // Now move asset back to HighValueAisle
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-HV-R1", 10.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-HV-R2", 20.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-HV-R3", 15.0, 25.0, 4.0, 1, WarehouseZone.HighValueAisle));

        var beaconReturned = new RfidBleTelemetryBeacon(
            TagId: "TAG-RETURN-01",
            Sku: "SKU-TABLET-01",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-HV-R1", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-HV-R2", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-HV-R3", DistanceToRssi(10.0))
            ],
            BatteryPct: 79.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beaconReturned);
        var auditReturn = _discrepancyDetector.AuditAssetPlacement("TAG-RETURN-01", "SKU-TABLET-01", WarehouseZone.HighValueAisle, false);

        auditReturn.Should().BeNull();
        _discrepancyDetector.GetActiveDiscrepancies().Should().BeEmpty();
    }

    [Fact]
    public void AuditAssetPlacement_UntrackedAsset_ReturnsNull()
    {
        var incident = _discrepancyDetector.AuditAssetPlacement(
            tagId: "TAG-NOT-TRACKED",
            sku: "SKU-UNKNOWN",
            expectedZone: WarehouseZone.HighValueAisle,
            hasActivePickTask: false);

        incident.Should().BeNull();
    }

    [Fact]
    public void ResolveDiscrepancy_ValidIncidentId_MarksAsResolvedAndRemovesFromActive()
    {
        // Position asset in QuarantineZone
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-Q1", 60.0, 60.0, 4.0, 1, WarehouseZone.QuarantineZone));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-Q2", 70.0, 60.0, 4.0, 1, WarehouseZone.QuarantineZone));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-Q3", 65.0, 75.0, 4.0, 1, WarehouseZone.QuarantineZone));

        var beacon = new RfidBleTelemetryBeacon(
            TagId: "TAG-RES-01",
            Sku: "SKU-ITEM-01",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-Q1", DistanceToRssi(6.0)),
                new RssiAnchorReading("ANC-Q2", DistanceToRssi(6.0)),
                new RssiAnchorReading("ANC-Q3", DistanceToRssi(9.0))
            ],
            BatteryPct: 90.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beacon);
        var incident = _discrepancyDetector.AuditAssetPlacement("TAG-RES-01", "SKU-ITEM-01", WarehouseZone.HighValueAisle, false);

        incident.Should().NotBeNull();
        _discrepancyDetector.GetActiveDiscrepancies().Should().ContainSingle();

        bool resolved = _discrepancyDetector.ResolveDiscrepancy(incident!.IncidentId, "Supervisor verified transfer order #492");

        resolved.Should().BeTrue();
        _discrepancyDetector.GetActiveDiscrepancies().Should().BeEmpty();
    }

    [Fact]
    public void ResolveDiscrepancy_NonExistentIncidentId_ReturnsFalse()
    {
        bool resolved = _discrepancyDetector.ResolveDiscrepancy("DISC-NONEXISTENT");

        resolved.Should().BeFalse();
    }

    [Fact]
    public void AuditAllTrackedAssets_AuditsAllAssetsAndAggregatesDiscrepancies()
    {
        // Tag 1 in HighValueAisle (Matches expected)
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-HV-AUD1", 10.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-HV-AUD2", 20.0, 10.0, 4.0, 1, WarehouseZone.HighValueAisle));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-HV-AUD3", 15.0, 20.0, 4.0, 1, WarehouseZone.HighValueAisle));

        var beacon1 = new RfidBleTelemetryBeacon(
            TagId: "TAG-AUD-01",
            Sku: "SKU-AUD-01",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-HV-AUD1", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-HV-AUD2", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-HV-AUD3", DistanceToRssi(8.0))
            ],
            BatteryPct: 95.0,
            TimestampUtc: DateTime.UtcNow);

        // Tag 2 in DockExitGate (Misplaced without pick task)
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-DCK-AUD1", 90.0, 90.0, 5.0, 1, WarehouseZone.DockExitGate));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-DCK-AUD2", 98.0, 90.0, 5.0, 1, WarehouseZone.DockExitGate));
        _engine.RegisterAnchor(new FixedAnchorNode("ANC-DCK-AUD3", 95.0, 98.0, 5.0, 1, WarehouseZone.DockExitGate));

        var beacon2 = new RfidBleTelemetryBeacon(
            TagId: "TAG-AUD-02",
            Sku: "SKU-AUD-02",
            RssiReadings:
            [
                new RssiAnchorReading("ANC-DCK-AUD1", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-DCK-AUD2", DistanceToRssi(5.0)),
                new RssiAnchorReading("ANC-DCK-AUD3", DistanceToRssi(5.0))
            ],
            BatteryPct: 90.0,
            TimestampUtc: DateTime.UtcNow);

        _engine.EstimateLocation(beacon1);
        _engine.EstimateLocation(beacon2);

        var auditResponse = _discrepancyDetector.AuditAllTrackedAssets(tagId =>
        {
            return tagId switch
            {
                "TAG-AUD-01" => (WarehouseZone.HighValueAisle, false),
                "TAG-AUD-02" => (WarehouseZone.HighValueAisle, false), // Mismatch!
                _ => (WarehouseZone.BulkStorageRack, false)
            };
        });

        auditResponse.HasDiscrepancies.Should().BeTrue();
        auditResponse.TotalAudited.Should().Be(2);
        auditResponse.Incidents.Should().ContainSingle();
        auditResponse.Incidents[0].TagId.Should().Be("TAG-AUD-02");
    }

    #endregion

    #region 7. Dock Perimeter Theft Interception & Alert Management

    [Fact]
    public void EvaluateDockPerimeter_LocationOutsideDockPerimeter_ReturnsNull()
    {
        var location = new IndoorLocationPoint(20.0, 30.0, 4.0, 1, WarehouseZone.HighValueAisle, 0.4, DateTime.UtcNow);

        var alert = _dockAlertService.EvaluateDockPerimeter("TAG-SAFE-01", "SKU-SAFE-01", location, 75000m);

        alert.Should().BeNull();
        _dockAlertService.GetActiveSecurityAlerts().Should().BeEmpty();
    }

    [Fact]
    public void EvaluateDockPerimeter_LocationInDockPerimeter_RaisesCriticalTheftAlertWithItemValue()
    {
        var location = new IndoorLocationPoint(94.5, 96.2, 5.0, 1, WarehouseZone.DockExitGate, 0.3, DateTime.UtcNow);
        decimal itemValue = 185000m; // 185,000 DZD

        var alert = _dockAlertService.EvaluateDockPerimeter("TAG-THEFT-ALERT-01", "SKU-DIAMOND-WATCH", location, itemValue);

        alert.Should().NotBeNull();
        alert!.Severity.Should().Be(ShrinkageAlertSeverity.CriticalTheftAlert);
        alert.Status.Should().Be(SecurityInterceptStatus.ActiveAlarm);
        alert.ItemValueDzd.Should().Be(185000m);
        alert.TagId.Should().Be("TAG-THEFT-ALERT-01");
        alert.DockGateId.Should().NotBeNullOrWhiteSpace();
        alert.InterceptNotes.Should().Contain("Immediate security intercept");

        _dockAlertService.GetActiveSecurityAlerts().Should().ContainSingle();
    }

    [Fact]
    public void EvaluateDockPerimeter_RepeatedBreachForSameTag_UpdatesTriggerLocationWithoutDuplicating()
    {
        var loc1 = new IndoorLocationPoint(91.0, 91.0, 5.0, 1, WarehouseZone.DockExitGate, 0.5, DateTime.UtcNow);
        var loc2 = new IndoorLocationPoint(96.0, 97.0, 5.0, 1, WarehouseZone.DockExitGate, 0.3, DateTime.UtcNow);

        var alert1 = _dockAlertService.EvaluateDockPerimeter("TAG-REPEAT-01", "SKU-LAPTOP-01", loc1, 120000m);
        var alert2 = _dockAlertService.EvaluateDockPerimeter("TAG-REPEAT-01", "SKU-LAPTOP-01", loc2, 120000m);

        alert1!.AlertId.Should().Be(alert2!.AlertId);
        _dockAlertService.GetActiveSecurityAlerts().Should().ContainSingle();
    }

    [Fact]
    public void AcknowledgeAlert_AdvanceToSecurityDispatched_UpdatesStatusAndRetainsInActiveAlerts()
    {
        var loc = new IndoorLocationPoint(93.0, 95.0, 5.0, 1, WarehouseZone.DockExitGate, 0.3, DateTime.UtcNow);
        var alert = _dockAlertService.EvaluateDockPerimeter("TAG-DISPATCH-01", "SKU-HIGH-VAL", loc, 90000m);

        bool updated = _dockAlertService.AcknowledgeAlert(
            alert!.AlertId,
            SecurityInterceptStatus.SecurityDispatched,
            "Security Officer Ahmed dispatched to Gate 1");

        updated.Should().BeTrue();
        var activeAlerts = _dockAlertService.GetActiveSecurityAlerts();
        activeAlerts.Should().ContainSingle();
        activeAlerts[0].Status.Should().Be(SecurityInterceptStatus.SecurityDispatched);
        activeAlerts[0].InterceptNotes.Should().Contain("Security Officer Ahmed");
    }

    [Fact]
    public void AcknowledgeAlert_AdvanceToRecovered_ClearsFromActiveAlerts()
    {
        var loc = new IndoorLocationPoint(94.0, 94.0, 5.0, 1, WarehouseZone.DockExitGate, 0.4, DateTime.UtcNow);
        var alert = _dockAlertService.EvaluateDockPerimeter("TAG-RECOVER-01", "SKU-RECOVER-01", loc, 60000m);

        bool updated = _dockAlertService.AcknowledgeAlert(
            alert!.AlertId,
            SecurityInterceptStatus.Recovered,
            "Item intercepted at dock perimeter by physical security.");

        updated.Should().BeTrue();
        _dockAlertService.GetActiveSecurityAlerts().Should().BeEmpty();
        _dockAlertService.GetAllSecurityAlerts().Should().ContainSingle();
        _dockAlertService.GetAllSecurityAlerts()[0].Status.Should().Be(SecurityInterceptStatus.Recovered);
    }

    [Fact]
    public void AcknowledgeAlert_AdvanceToFalsePositiveCleared_ClearsFromActiveAlerts()
    {
        var loc = new IndoorLocationPoint(92.0, 92.0, 5.0, 1, WarehouseZone.DockExitGate, 0.4, DateTime.UtcNow);
        var alert = _dockAlertService.EvaluateDockPerimeter("TAG-FP-01", "SKU-FP-01", loc, 45000m);

        bool updated = _dockAlertService.AcknowledgeAlert(
            alert!.AlertId,
            SecurityInterceptStatus.FalsePositiveCleared,
            "Tag was tested by field engineer, false alarm.");

        updated.Should().BeTrue();
        _dockAlertService.GetActiveSecurityAlerts().Should().BeEmpty();
    }

    [Fact]
    public void AcknowledgeAlert_UnknownAlertId_ReturnsFalse()
    {
        bool updated = _dockAlertService.AcknowledgeAlert(
            "ALERT-DCK-UNKNOWN",
            SecurityInterceptStatus.SecurityDispatched);

        updated.Should().BeFalse();
    }

    [Fact]
    public void GetTotalAtRiskValueDzd_SumsActiveAlertValuesAccurately()
    {
        var loc1 = new IndoorLocationPoint(92.0, 92.0, 5.0, 1, WarehouseZone.DockExitGate, 0.3, DateTime.UtcNow);
        var loc2 = new IndoorLocationPoint(96.0, 96.0, 5.0, 1, WarehouseZone.DockExitGate, 0.3, DateTime.UtcNow);

        _dockAlertService.EvaluateDockPerimeter("TAG-VAL-1", "SKU-V1", loc1, 100000m);
        _dockAlertService.EvaluateDockPerimeter("TAG-VAL-2", "SKU-V2", loc2, 250000m);

        decimal totalValue = _dockAlertService.GetTotalAtRiskValueDzd();
        totalValue.Should().Be(350000m);
    }

    #endregion

    #region 8. Concurrency & Thread-Safety

    [Fact]
    public async Task ConcurrentRssiStreaming_FiftySimultaneousBeacons_ProcessesWithoutExceptionsOrDataCorruption()
    {
        const int concurrentBeacons = 50;
        var tasks = new Task[concurrentBeacons];

        for (int i = 0; i < concurrentBeacons; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                var beacon = new RfidBleTelemetryBeacon(
                    TagId: $"CONC-TAG-{index:D3}",
                    Sku: $"SKU-CONC-{index:D3}",
                    RssiReadings:
                    [
                        new RssiAnchorReading("ANC-HVA-01", -65.0 - (index % 10)),
                        new RssiAnchorReading("ANC-HVA-02", -70.0 - (index % 5)),
                        new RssiAnchorReading("ANC-HVA-03", -68.0 - (index % 7))
                    ],
                    BatteryPct: 90.0 + (index % 10),
                    TimestampUtc: DateTime.UtcNow);

                var loc = _engine.EstimateLocation(beacon);
                loc.Should().NotBeNull();
            });
        }

        await Task.WhenAll(tasks);

        var allAssets = _engine.GetAllTrackedAssets();
        allAssets.Count.Should().BeGreaterThanOrEqualTo(concurrentBeacons);
    }

    [Fact]
    public async Task ConcurrentAnchorRegistrationAndPositioning_ThreadSafeUnderLoad()
    {
        const int iterations = 30;
        var registerTasks = Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                _engine.RegisterAnchor(new FixedAnchorNode(
                    AnchorId: $"DYN-ANC-{i}",
                    X: 10.0 + i,
                    Y: 15.0 + i,
                    Z: 4.0,
                    Floor: 1,
                    Zone: WarehouseZone.HighValueAisle));
            }
        });

        var queryTasks = Task.Run(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var beacon = new RfidBleTelemetryBeacon(
                    TagId: $"QUERY-TAG-{i}",
                    Sku: "SKU-QUERY",
                    RssiReadings: [new RssiAnchorReading("ANC-HVA-01", -60.0)],
                    BatteryPct: 99.0,
                    TimestampUtc: DateTime.UtcNow);

                _engine.EstimateLocation(beacon);
            }
        });

        await Task.WhenAll(registerTasks, queryTasks);

        _engine.GetAllAnchors().Count.Should().BeGreaterThanOrEqualTo(iterations);
    }

    #endregion

    #region 9. Dependency Injection Assertions (Rule: NEVER call BuildServiceProvider())

    [Fact]
    public void AddShrinkageServices_RegistersAllRequiredServicesAsSingletons_WithoutBuildingProvider()
    {
        // Critical Rule: Direct ServiceDescriptor assertion on ServiceCollection without calling BuildServiceProvider()
        var services = new ServiceCollection();
        services.AddShrinkageServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IRfidBleIndoorPositioningEngine) &&
            d.ImplementationType == typeof(RfidBleIndoorPositioningEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAisleDiscrepancyDetectorService) &&
            d.ImplementationType == typeof(AisleDiscrepancyDetectorService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDockShrinkageAlertService) &&
            d.ImplementationType == typeof(DockShrinkageAlertService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion

    #region 10. Models & Distance Helpers

    [Fact]
    public void IndoorLocationPoint_Distance2DAnd3D_ComputesAccurateEuclideanDistance()
    {
        var p1 = new IndoorLocationPoint(0.0, 0.0, 0.0, 1, WarehouseZone.HighValueAisle, 0.2, DateTime.UtcNow);
        var p2 = new IndoorLocationPoint(3.0, 4.0, 12.0, 1, WarehouseZone.HighValueAisle, 0.2, DateTime.UtcNow);

        p1.DistanceTo2D(p2).Should().Be(5.0);
        p1.DistanceTo3D(p2).Should().Be(13.0); // sqrt(3^2 + 4^2 + 12^2) = sqrt(9 + 16 + 144) = sqrt(169) = 13.0
    }

    [Fact]
    public void FixedAnchorNode_RecordEqualityAndDefaults_BehavePredictably()
    {
        var anchor1 = new FixedAnchorNode("A1", 10, 20, 4, 1, WarehouseZone.HighValueAisle);
        var anchor2 = new FixedAnchorNode("A1", 10, 20, 4, 1, WarehouseZone.HighValueAisle);

        anchor1.Should().Be(anchor2);
        anchor1.IsActive.Should().BeTrue();
    }

    #endregion
}
