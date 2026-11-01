using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Logistics.Fleet;
using Xunit;

namespace SGrowing.Tests.Logistics;

/// <summary>
/// Comprehensive unit test suite for Phase 36:
/// Autonomous Fleet Telematics, Real-Time Fuel Optimization &amp; Predictive Vehicle Maintenance.
/// </summary>
public sealed class FleetTelematicsAndMaintenanceTests
{
    private readonly Obd2TelemetryStreamerEngine _streamerEngine = new();
    private readonly DriverSafetyAndEcoScoringService _scoringService = new();
    private readonly PredictiveMaintenanceRadarService _radarService = new();

    #region 1. Telemetry Ingestion, Deadband Filtering & State Tracking

    [Fact]
    public void IngestTelemetry_ValidPacket_ReturnsSuccessAndStoresState()
    {
        var packet = new Obd2TelemetryPacket(
            VehicleId: "VEH-101",
            DriverId: "DRV-001",
            TimestampUtc: DateTime.UtcNow,
            SpeedKmh: 45.0,
            EngineRpm: 2100.0,
            InstantaneousFuelRateLitersPerHour: 4.5,
            CoolantTempCelsius: 88.0,
            BatteryVoltage: 13.8,
            ThrottlePositionPct: 22.0,
            Latitude: 36.7525,
            Longitude: 3.0420);

        var response = _streamerEngine.IngestTelemetry(packet);

        response.Success.Should().BeTrue();
        response.VehicleId.Should().Be("VEH-101");
        response.HealthStatus.Should().Be(VehicleHealthStatus.Optimal);
        response.ActiveDtcCount.Should().Be(0);
        response.FilteredByDeadband.Should().BeFalse();

        var state = _streamerEngine.GetLatestVehicleState("VEH-101");
        state.Should().NotBeNull();
        state!.SpeedKmh.Should().Be(45.0);
        state.EngineRpm.Should().Be(2100.0);
        state.TotalPacketsProcessed.Should().Be(1);
    }

    [Fact]
    public void IngestTelemetry_DeadbandFilter_MicroFluctuationsInSpeed_FilteredByDeadband()
    {
        var baseTime = DateTime.UtcNow;
        var p1 = new Obd2TelemetryPacket("VEH-102", "DRV-002", baseTime, 50.0, 2200.0, 5.0, 90.0, 14.0, 25.0, 36.7, 3.0);
        _streamerEngine.IngestTelemetry(p1);

        // Speed fluctuation of 0.3 km/h (< 0.5 threshold), rpm delta of 10, throttle delta 0.1
        var p2 = new Obd2TelemetryPacket("VEH-102", "DRV-002", baseTime.AddSeconds(1), 50.3, 2210.0, 5.0, 90.1, 14.02, 25.1, 36.7, 3.0);
        var resp2 = _streamerEngine.IngestTelemetry(p2);

        resp2.FilteredByDeadband.Should().BeTrue();
        resp2.Message.Should().Contain("deadband");

        var state = _streamerEngine.GetLatestVehicleState("VEH-102");
        state!.SpeedKmh.Should().Be(50.0); // Retained previous metric
        state.TotalPacketsProcessed.Should().Be(2);
    }

    [Fact]
    public void IngestTelemetry_DeadbandFilter_IdleRpmMicroFluctuation_FilteredByDeadband()
    {
        var baseTime = DateTime.UtcNow;
        var p1 = new Obd2TelemetryPacket("VEH-103", "DRV-003", baseTime, 0.0, 800.0, 1.2, 85.0, 13.9, 0.0, 36.7, 3.0);
        _streamerEngine.IngestTelemetry(p1);

        // Idling speed 0, RPM jitter of +12 (< 20 deadband limit)
        var p2 = new Obd2TelemetryPacket("VEH-103", "DRV-003", baseTime.AddSeconds(1), 0.0, 812.0, 1.2, 85.0, 13.9, 0.0, 36.7, 3.0);
        var resp2 = _streamerEngine.IngestTelemetry(p2);

        resp2.FilteredByDeadband.Should().BeTrue();
    }

    [Fact]
    public void IngestTelemetry_ExceedingSpeedDeadband_IngestedWithoutFiltering()
    {
        var baseTime = DateTime.UtcNow;
        var p1 = new Obd2TelemetryPacket("VEH-104", "DRV-004", baseTime, 60.0, 2400.0, 6.0, 90.0, 14.0, 30.0, 36.7, 3.0);
        _streamerEngine.IngestTelemetry(p1);

        // Speed changes by 2.0 km/h (>= 0.5 threshold)
        var p2 = new Obd2TelemetryPacket("VEH-104", "DRV-004", baseTime.AddSeconds(1), 62.0, 2480.0, 6.5, 90.0, 14.0, 32.0, 36.7, 3.0);
        var resp2 = _streamerEngine.IngestTelemetry(p2);

        resp2.FilteredByDeadband.Should().BeFalse();
        var state = _streamerEngine.GetLatestVehicleState("VEH-104");
        state!.SpeedKmh.Should().Be(62.0);
    }

    [Fact]
    public void IngestTelemetry_ExceedingRpmDeadbandWhenIdle_IngestedWithoutFiltering()
    {
        var baseTime = DateTime.UtcNow;
        var p1 = new Obd2TelemetryPacket("VEH-105", "DRV-005", baseTime, 0.0, 800.0, 1.2, 85.0, 13.9, 0.0, 36.7, 3.0);
        _streamerEngine.IngestTelemetry(p1);

        // RPM jumps by 50 when idling (e.g. AC compressor kicks in or slight throttle blip)
        var p2 = new Obd2TelemetryPacket("VEH-105", "DRV-005", baseTime.AddSeconds(1), 0.0, 850.0, 1.4, 85.0, 13.9, 1.0, 36.7, 3.0);
        var resp2 = _streamerEngine.IngestTelemetry(p2);

        resp2.FilteredByDeadband.Should().BeFalse();
        var state = _streamerEngine.GetLatestVehicleState("VEH-105");
        state!.EngineRpm.Should().Be(850.0);
    }

    [Fact]
    public void IngestTelemetry_ThrottleJumpBreaksDeadband()
    {
        var baseTime = DateTime.UtcNow;
        var p1 = new Obd2TelemetryPacket("VEH-106", "DRV-006", baseTime, 50.0, 2000.0, 4.0, 88.0, 14.0, 20.0, 36.7, 3.0);
        _streamerEngine.IngestTelemetry(p1);

        // Speed same, but throttle stepped on by 5%
        var p2 = new Obd2TelemetryPacket("VEH-106", "DRV-006", baseTime.AddSeconds(1), 50.1, 2010.0, 4.2, 88.0, 14.0, 25.0, 36.7, 3.0);
        var resp2 = _streamerEngine.IngestTelemetry(p2);

        resp2.FilteredByDeadband.Should().BeFalse();
    }

    [Fact]
    public void IngestTelemetry_NullPacket_ThrowsArgumentNullException()
    {
        var act = () => _streamerEngine.IngestTelemetry(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("", "DRV-1")]
    [InlineData("   ", "DRV-1")]
    [InlineData("VEH-1", "")]
    [InlineData("VEH-1", "   ")]
    public void IngestTelemetry_InvalidIdentifiers_ThrowsArgumentException(string vehicleId, string driverId)
    {
        var packet = new Obd2TelemetryPacket(vehicleId, driverId, DateTime.UtcNow, 30, 1500, 3, 85, 14, 15, 36.7, 3.0);
        var act = () => _streamerEngine.IngestTelemetry(packet);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IngestBatch_MultiplePackets_ProcessesSequentially()
    {
        var baseTime = DateTime.UtcNow;
        var packets = new List<Obd2TelemetryPacket>
        {
            new("VEH-BATCH", "DRV-BATCH", baseTime, 20.0, 1200.0, 2.5, 80.0, 13.8, 10.0, 36.7, 3.0),
            new("VEH-BATCH", "DRV-BATCH", baseTime.AddSeconds(1), 25.0, 1400.0, 3.0, 81.0, 13.8, 15.0, 36.7, 3.0),
            new("VEH-BATCH", "DRV-BATCH", baseTime.AddSeconds(2), 30.0, 1600.0, 3.5, 82.0, 13.8, 20.0, 36.7, 3.0)
        };

        var responses = _streamerEngine.IngestBatch(packets);

        responses.Should().HaveCount(3);
        responses.All(r => r.Success).Should().BeTrue();

        var state = _streamerEngine.GetLatestVehicleState("VEH-BATCH");
        state!.SpeedKmh.Should().Be(30.0);
        state.TotalPacketsProcessed.Should().Be(3);
    }

    [Fact]
    public void GetLatestVehicleState_UnknownVehicle_ReturnsNull()
    {
        var state = _streamerEngine.GetLatestVehicleState("VEH-NON-EXISTENT");
        state.Should().BeNull();
    }

    [Fact]
    public void GetTelemetryHistory_ReturnsOrderedChronologicalHistory()
    {
        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            _streamerEngine.IngestTelemetry(new Obd2TelemetryPacket(
                "VEH-HIST", "DRV-HIST", baseTime.AddSeconds(i), 10.0 * (i + 1), 1000 + (i * 200), 2.0, 85, 14, 10, 36.7, 3.0));
        }

        var history = _streamerEngine.GetTelemetryHistory("VEH-HIST", limit: 3);
        history.Should().HaveCount(3);
        history.Last().SpeedKmh.Should().Be(50.0);
    }

    #endregion

    #region 2. Diagnostic Trouble Code (DTC) Parsing & Health Status Degradation

    [Fact]
    public void IngestTelemetry_WarningDtc_DegradesHealthStatusToDegraded()
    {
        var dtc = new DiagnosticTroubleCode("P0171", DtcSeverity.Warning, "System Too Lean (Bank 1)", "FuelSystem");
        var packet = new Obd2TelemetryPacket(
            "VEH-DTC-WARN", "DRV-1", DateTime.UtcNow, 40, 1800, 4, 88, 13.9, 20, 36.7, 3.0,
            DiagnosticTroubleCodes: [dtc]);

        var response = _streamerEngine.IngestTelemetry(packet);

        response.HealthStatus.Should().Be(VehicleHealthStatus.Degraded);
        response.ActiveDtcCount.Should().Be(1);

        var activeCodes = _streamerEngine.GetActiveTroubleCodes("VEH-DTC-WARN");
        activeCodes.Should().ContainSingle(c => c.Code == "P0171" && c.Severity == DtcSeverity.Warning);
    }

    [Fact]
    public void IngestTelemetry_CriticalDtc_ElevatesToCriticalMaintenanceRequired()
    {
        var dtc = new DiagnosticTroubleCode("P0300", DtcSeverity.Critical, "Random/Multiple Cylinder Misfire Detected", "Powertrain");
        var packet = new Obd2TelemetryPacket(
            "VEH-DTC-CRIT", "DRV-1", DateTime.UtcNow, 35, 2200, 5, 92, 13.8, 25, 36.7, 3.0,
            DiagnosticTroubleCodes: [dtc]);

        var response = _streamerEngine.IngestTelemetry(packet);

        response.HealthStatus.Should().Be(VehicleHealthStatus.CriticalMaintenanceRequired);
        response.ActiveDtcCount.Should().Be(1);

        var state = _streamerEngine.GetLatestVehicleState("VEH-DTC-CRIT");
        state!.HealthStatus.Should().Be(VehicleHealthStatus.CriticalMaintenanceRequired);
    }

    [Fact]
    public void IngestTelemetry_MultipleDtcs_PreservesMostSevereHealthStatus()
    {
        var warningDtc = new DiagnosticTroubleCode("P0420", "Warning", "Catalyst System Efficiency Below Threshold", "Exhaust");
        var critDtc = new DiagnosticTroubleCode("P0562", "Critical", "System Voltage Low - Alternator Failure", "Electrical");

        var packet = new Obd2TelemetryPacket(
            "VEH-MULTI-DTC", "DRV-1", DateTime.UtcNow, 45, 2000, 4.5, 90, 11.2, 22, 36.7, 3.0,
            DiagnosticTroubleCodes: [warningDtc, critDtc]);

        var response = _streamerEngine.IngestTelemetry(packet);

        response.HealthStatus.Should().Be(VehicleHealthStatus.CriticalMaintenanceRequired);
        response.ActiveDtcCount.Should().Be(2);
    }

    [Fact]
    public void IngestTelemetry_DtcBypassesDeadbandFilter_EvenIfSpeedAndRpmIdentical()
    {
        var baseTime = DateTime.UtcNow;
        var p1 = new Obd2TelemetryPacket("VEH-DTC-BYPASS", "DRV-1", baseTime, 50.0, 2000.0, 4.0, 88.0, 14.0, 20.0, 36.7, 3.0);
        _streamerEngine.IngestTelemetry(p1);

        // Same exact speed, rpm, temps, but a new DTC arrived
        var dtc = new DiagnosticTroubleCode("P0115", DtcSeverity.Warning, "Engine Coolant Temperature Circuit Malfunction", "Cooling");
        var p2 = new Obd2TelemetryPacket("VEH-DTC-BYPASS", "DRV-1", baseTime.AddSeconds(1), 50.0, 2000.0, 4.0, 88.0, 14.0, 20.0, 36.7, 3.0,
            DiagnosticTroubleCodes: [dtc]);

        var resp2 = _streamerEngine.IngestTelemetry(p2);

        resp2.FilteredByDeadband.Should().BeFalse();
        resp2.HealthStatus.Should().Be(VehicleHealthStatus.Degraded);
        resp2.ActiveDtcCount.Should().Be(1);
    }

    [Fact]
    public void IngestTelemetry_CoolantOverheatingThreshold_TriggersCriticalMaintenance()
    {
        var packet = new Obd2TelemetryPacket(
            "VEH-OVERHEAT", "DRV-1", DateTime.UtcNow, 60, 2500, 7.0, 118.5, 13.9, 35, 36.7, 3.0);

        var response = _streamerEngine.IngestTelemetry(packet);

        response.HealthStatus.Should().Be(VehicleHealthStatus.CriticalMaintenanceRequired);
    }

    [Fact]
    public void IngestTelemetry_CoolantHotThreshold_TriggersDegraded()
    {
        var packet = new Obd2TelemetryPacket(
            "VEH-HOT", "DRV-1", DateTime.UtcNow, 60, 2500, 7.0, 108.0, 13.9, 35, 36.7, 3.0);

        var response = _streamerEngine.IngestTelemetry(packet);

        response.HealthStatus.Should().Be(VehicleHealthStatus.Degraded);
    }

    [Theory]
    [InlineData(11.1)] // Low alternator voltage
    [InlineData(16.0)] // Overcharging regulator failure
    public void IngestTelemetry_BatteryVoltageAbnormal_TriggersDegraded(double voltage)
    {
        var packet = new Obd2TelemetryPacket(
            "VEH-BATT", "DRV-1", DateTime.UtcNow, 40, 1800, 4.0, 90.0, voltage, 20, 36.7, 3.0);

        var response = _streamerEngine.IngestTelemetry(packet);

        response.HealthStatus.Should().Be(VehicleHealthStatus.Degraded);
    }

    [Fact]
    public void ClearTroubleCodes_ResetsActiveDtcsAndRestoresOptimalHealth()
    {
        var dtc = new DiagnosticTroubleCode("P0300", DtcSeverity.Critical, "Cylinder Misfire", "Powertrain");
        _streamerEngine.IngestTelemetry(new Obd2TelemetryPacket(
            "VEH-RESET", "DRV-1", DateTime.UtcNow, 40, 2000, 4.0, 90, 13.8, 20, 36.7, 3.0, [dtc]));

        _streamerEngine.GetActiveTroubleCodes("VEH-RESET").Should().HaveCount(1);
        _streamerEngine.GetLatestVehicleState("VEH-RESET")!.HealthStatus.Should().Be(VehicleHealthStatus.CriticalMaintenanceRequired);

        _streamerEngine.ClearTroubleCodes("VEH-RESET");

        _streamerEngine.GetActiveTroubleCodes("VEH-RESET").Should().BeEmpty();
        _streamerEngine.GetLatestVehicleState("VEH-RESET")!.HealthStatus.Should().Be(VehicleHealthStatus.Optimal);
    }

    #endregion

    #region 3. Harsh Kinematic Event Detection (Acceleration, Braking, Sharp Turns)

    [Fact]
    public void EvaluateDriverSession_HarshAcceleration_SpeedDeltaExceeding15KmhPerSec()
    {
        var baseTime = DateTime.UtcNow;
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-ACCEL", baseTime, 20.0, 1500, 2.5, 88, 14, 15, 36.7, 3.0),
            // Speed jumps from 20 to 45 km/h in 1.0 second (+25 km/h/s > 15 threshold)
            new("VEH-1", "DRV-ACCEL", baseTime.AddSeconds(1), 45.0, 3200, 8.5, 89, 14, 85, 36.7, 3.0),
            new("VEH-1", "DRV-ACCEL", baseTime.AddSeconds(2), 47.0, 2400, 5.0, 89, 14, 25, 36.7, 3.0)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-ACCEL", window);

        score.HarshAccelerationsCount.Should().Be(1);
        score.HarshBrakingsCount.Should().Be(0);
        score.EcoScore.Should().BeLessThan(100.0);
        score.SafetyScore.Should().BeLessThan(100.0);
    }

    [Fact]
    public void EvaluateDriverSession_HarshAcceleration_RpmExceeding4500()
    {
        var baseTime = DateTime.UtcNow;
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-RPM", baseTime, 40.0, 2500, 4.0, 90, 14, 25, 36.7, 3.0),
            // Sudden downshift redlining RPM to 5100 (> 4500)
            new("VEH-1", "DRV-RPM", baseTime.AddSeconds(1), 45.0, 5100, 12.0, 92, 14, 90, 36.7, 3.0),
            new("VEH-1", "DRV-RPM", baseTime.AddSeconds(2), 48.0, 2600, 4.5, 91, 14, 30, 36.7, 3.0)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-RPM", window);

        score.HarshAccelerationsCount.Should().Be(1);
    }

    [Fact]
    public void EvaluateDriverSession_HarshBraking_SpeedDeltaExceedingNegative18KmhPerSec()
    {
        var baseTime = DateTime.UtcNow;
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-BRAKE", baseTime, 65.0, 2600, 6.0, 90, 14, 30, 36.7, 3.0),
            // Emergency brake: drops from 65 to 30 km/h in 1.0 second (-35 km/h/s < -18 threshold)
            new("VEH-1", "DRV-BRAKE", baseTime.AddSeconds(1), 30.0, 1200, 0.8, 90, 14, 0, 36.7, 3.0),
            new("VEH-1", "DRV-BRAKE", baseTime.AddSeconds(2), 28.0, 1100, 1.0, 90, 14, 5, 36.7, 3.0)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-BRAKE", window);

        score.HarshBrakingsCount.Should().Be(1);
        score.HarshAccelerationsCount.Should().Be(0);
        score.SafetyScore.Should().BeLessThan(100.0);
    }

    [Fact]
    public void EvaluateDriverSession_NormalAccelerationAndBraking_NoHarshEvents()
    {
        var baseTime = DateTime.UtcNow;
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-SMOOTH", baseTime, 30.0, 1500, 3.0, 88, 14, 20, 36.700, 3.000),
            // +8 km/h in 1s -> gentle acceleration (< 15 threshold)
            new("VEH-1", "DRV-SMOOTH", baseTime.AddSeconds(1), 38.0, 1800, 3.8, 88, 14, 25, 36.701, 3.000),
            // -10 km/h in 1s -> gentle braking (>-18 threshold)
            new("VEH-1", "DRV-SMOOTH", baseTime.AddSeconds(2), 28.0, 1400, 2.0, 88, 14, 5, 36.702, 3.000)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-SMOOTH", window);

        score.HarshAccelerationsCount.Should().Be(0);
        score.HarshBrakingsCount.Should().Be(0);
        score.SharpTurnsCount.Should().Be(0);
        score.SafetyScore.Should().Be(100.0);
    }

    [Fact]
    public void EvaluateDriverSession_SharpTurn_AtHighSpeed_DetectedViaGpsBearingChange()
    {
        var baseTime = DateTime.UtcNow;
        // Moving at 55 km/h (> 40 km/h threshold)
        // Step 1: Heading East (longitude increasing)
        // Step 2: Sudden 90-degree turn North (latitude increasing) in 1 second
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-TURN", baseTime, 55.0, 2400, 5.0, 90, 14, 30, 36.7000, 3.0000),
            new("VEH-1", "DRV-TURN", baseTime.AddSeconds(1), 55.0, 2400, 5.0, 90, 14, 30, 36.7000, 3.0020), // East bearing ~90°
            new("VEH-1", "DRV-TURN", baseTime.AddSeconds(2), 52.0, 2300, 4.8, 90, 14, 25, 36.7020, 3.0020)  // North bearing ~0° (90° turn!)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-TURN", window);

        score.SharpTurnsCount.Should().BeGreaterThanOrEqualTo(1);
        score.SafetyScore.Should().BeLessThan(100.0);
    }

    [Fact]
    public void EvaluateDriverSession_TurnAtLowSpeedUnder40Kmh_NotCountedAsSharpTurn()
    {
        var baseTime = DateTime.UtcNow;
        // Moving at 20 km/h (< 40 km/h threshold)
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-SLOW-TURN", baseTime, 20.0, 1200, 2.0, 88, 14, 10, 36.7000, 3.0000),
            new("VEH-1", "DRV-SLOW-TURN", baseTime.AddSeconds(1), 20.0, 1200, 2.0, 88, 14, 10, 36.7000, 3.0010),
            new("VEH-1", "DRV-SLOW-TURN", baseTime.AddSeconds(2), 18.0, 1100, 1.8, 88, 14, 5, 36.7010, 3.0010)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-SLOW-TURN", window);

        score.SharpTurnsCount.Should().Be(0);
    }

    #endregion

    #region 4. Excessive Idling Detection & Fuel Wastage Calculation

    [Fact]
    public void EvaluateDriverSession_IdlingUnder5Minutes_NotCountedAsExcessive()
    {
        var baseTime = DateTime.UtcNow;
        // 4 minutes of idling (240 seconds <= 300 seconds threshold)
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-NORMAL-IDLE", baseTime, 0.0, 750, 1.2, 85, 14, 0, 36.7, 3.0),
            new("VEH-1", "DRV-NORMAL-IDLE", baseTime.AddSeconds(120), 0.0, 750, 1.2, 85, 14, 0, 36.7, 3.0),
            new("VEH-1", "DRV-NORMAL-IDLE", baseTime.AddSeconds(240), 0.0, 750, 1.2, 85, 14, 0, 36.7, 3.0),
            // Drives off
            new("VEH-1", "DRV-NORMAL-IDLE", baseTime.AddSeconds(245), 25.0, 1400, 3.0, 87, 14, 15, 36.7, 3.0)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-NORMAL-IDLE", window);

        score.IdlingDurationMinutes.Should().Be(0.0);
        score.FuelWastedIdlingLiters.Should().Be(0.0);
    }

    [Fact]
    public void EvaluateDriverSession_ExcessiveIdlingOver5Minutes_AccumulatesDurationAndWastedFuel()
    {
        var baseTime = DateTime.UtcNow;
        // 10 minutes (600 seconds) of stationary engine idling at ~1.2 L/h
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-1", "DRV-EXCESS-IDLE", baseTime, 0.0, 800, 1.2, 86, 14, 0, 36.7, 3.0),
            new("VEH-1", "DRV-EXCESS-IDLE", baseTime.AddMinutes(5), 0.0, 800, 1.2, 86, 14, 0, 36.7, 3.0),
            new("VEH-1", "DRV-EXCESS-IDLE", baseTime.AddMinutes(10), 0.0, 800, 1.2, 86, 14, 0, 36.7, 3.0)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-EXCESS-IDLE", window);

        score.IdlingDurationMinutes.Should().Be(10.0);
        // 10 min = 1/6 hr -> 1.2 L/h * 1/6 = 0.20 L
        score.FuelWastedIdlingLiters.Should().BeApproximately(0.20, 0.02);
        score.EcoScore.Should().BeLessThan(100.0);
    }

    [Fact]
    public void EvaluateDriverSession_MultipleIdlingIntervals_OnlyCountsIntervalsExceeding5Minutes()
    {
        var baseTime = DateTime.UtcNow;
        var window = new List<Obd2TelemetryPacket>
        {
            // First stop: 2 minutes (traffic light) -> not excessive
            new("VEH-1", "DRV-MULTI-IDLE", baseTime, 0.0, 800, 1.2, 85, 14, 0, 36.7, 3.0),
            new("VEH-1", "DRV-MULTI-IDLE", baseTime.AddMinutes(2), 0.0, 800, 1.2, 85, 14, 0, 36.7, 3.0),
            // Drive for 5 minutes
            new("VEH-1", "DRV-MULTI-IDLE", baseTime.AddMinutes(3), 40.0, 2000, 4.0, 88, 14, 20, 36.7, 3.0),
            new("VEH-1", "DRV-MULTI-IDLE", baseTime.AddMinutes(7), 40.0, 2000, 4.0, 88, 14, 20, 36.7, 3.0),
            // Second stop: 8 minutes (waiting with AC running) -> excessive!
            new("VEH-1", "DRV-MULTI-IDLE", baseTime.AddMinutes(8), 0.0, 800, 1.2, 87, 14, 0, 36.7, 3.0),
            new("VEH-1", "DRV-MULTI-IDLE", baseTime.AddMinutes(16), 0.0, 800, 1.2, 87, 14, 0, 36.7, 3.0)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-MULTI-IDLE", window);

        score.IdlingDurationMinutes.Should().Be(8.0);
        score.FuelWastedIdlingLiters.Should().BeApproximately(0.16, 0.02);
    }

    #endregion

    #region 5. EcoScore, SafetyScore & Green Courier Fuel Bonus Gating

    [Fact]
    public void EvaluateDriverSession_PerfectDriving_Returns100ScoresAndBonusEligible()
    {
        var baseTime = DateTime.UtcNow;
        var window = new List<Obd2TelemetryPacket>
        {
            new("VEH-PERFECT", "DRV-ACE", baseTime, 40.0, 1800, 3.5, 88, 14, 20, 36.70, 3.00),
            new("VEH-PERFECT", "DRV-ACE", baseTime.AddSeconds(10), 45.0, 1900, 3.8, 88, 14, 22, 36.71, 3.00),
            new("VEH-PERFECT", "DRV-ACE", baseTime.AddSeconds(20), 42.0, 1850, 3.6, 88, 14, 21, 36.72, 3.00)
        };

        var score = _scoringService.EvaluateDriverSession("DRV-ACE", window);

        score.EcoScore.Should().Be(100.0);
        score.SafetyScore.Should().Be(100.0);
        score.FuelBonusEligible.Should().BeTrue();
    }

    [Fact]
    public void EvaluateDriverSession_BonusEligibilityGated_FailsIfEcoScoreBelow85()
    {
        var baseTime = DateTime.UtcNow;
        // Generate several harsh accelerations to pull EcoScore below 85
        var window = new List<Obd2TelemetryPacket>();
        for (var i = 0; i < 6; i++)
        {
            var t = baseTime.AddSeconds(i * 10);
            window.Add(new("VEH-1", "DRV-ECO-FAIL", t, 10.0, 1200, 2, 88, 14, 10, 36.7, 3.0));
            // Jump speed by 25 km/h in 1 sec
            window.Add(new("VEH-1", "DRV-ECO-FAIL", t.AddSeconds(1), 35.0, 4200, 8, 88, 14, 80, 36.7, 3.0));
        }

        var score = _scoringService.EvaluateDriverSession("DRV-ECO-FAIL", window);

        score.HarshAccelerationsCount.Should().Be(6);
        // 100 - (3.0 * 6) = 82.0 (< 85)
        score.EcoScore.Should().Be(82.0);
        score.FuelBonusEligible.Should().BeFalse();
    }

    [Fact]
    public void EvaluateDriverSession_BonusEligibilityGated_FailsIfSafetyScoreBelow90()
    {
        var baseTime = DateTime.UtcNow;
        // 3 harsh emergency brakings: SafetyScore = 100 - (4.0 * 3) = 88.0 (< 90)
        var window = new List<Obd2TelemetryPacket>();
        for (var i = 0; i < 3; i++)
        {
            var t = baseTime.AddSeconds(i * 10);
            window.Add(new("VEH-1", "DRV-SAFE-FAIL", t, 60.0, 2500, 5, 88, 14, 30, 36.7, 3.0));
            // Drop speed by 25 km/h in 1s (-25 < -18)
            window.Add(new("VEH-1", "DRV-SAFE-FAIL", t.AddSeconds(1), 35.0, 1200, 1, 88, 14, 0, 36.7, 3.0));
        }

        var score = _scoringService.EvaluateDriverSession("DRV-SAFE-FAIL", window);

        score.HarshBrakingsCount.Should().Be(3);
        score.SafetyScore.Should().Be(88.0);
        score.FuelBonusEligible.Should().BeFalse();
    }

    [Fact]
    public void EvaluateDriverSession_EmptyWindow_ReturnsDefault100Scores()
    {
        var score = _scoringService.EvaluateDriverSession("DRV-EMPTY", []);

        score.EcoScore.Should().Be(100.0);
        score.SafetyScore.Should().Be(100.0);
        score.FuelBonusEligible.Should().BeTrue();
        score.TotalDistanceKm.Should().Be(0.0);
    }

    [Fact]
    public void GetDriverScore_ReturnsCachedEvaluatedScore()
    {
        _scoringService.EvaluateDriverSession("DRV-CACHED", [
            new("VEH-1", "DRV-CACHED", DateTime.UtcNow, 30, 1500, 3, 85, 14, 20, 36.7, 3.0)
        ]);

        var score = _scoringService.GetDriverScore("DRV-CACHED");
        score.Should().NotBeNull();
        score!.DriverId.Should().Be("DRV-CACHED");
    }

    #endregion

    #region 6. Weibull Hazard Reliability Model (Beta Shape, Eta Scale, Failure Probability)

    [Fact]
    public void EstimateComponentRul_BrakePads_UsesCorrectBetaAndEtaParameters()
    {
        // BrakePads: Beta = 2.8, Eta = 40,000 km
        var estimate = _radarService.EstimateComponentRul("VEH-BRAKE-01", "BrakePads", currentKm: 20000.0);

        estimate.BetaShape.Should().Be(2.8);
        estimate.EtaScale.Should().Be(40000.0);
        estimate.ExpectedRemainingKm.Should().Be(20000.0);

        // F(20,000) = 1 - exp(- (20000/40000)^2.8) = 1 - exp(- (0.5)^2.8)
        // 0.5^2.8 = ~0.143587 -> 1 - exp(-0.143587) = ~0.1338 (13.38%)
        estimate.FailureProbabilityPct.Should().BeInRange(13.0, 14.0);
        estimate.Urgency.Should().Be(MaintenanceUrgency.Routine);
    }

    [Fact]
    public void EstimateComponentRul_Tires_UsesCorrectBetaAndEtaParameters()
    {
        // Tires: Beta = 3.2, Eta = 50,000 km
        var estimate = _radarService.EstimateComponentRul("VEH-TIRE-01", "Tires", currentKm: 25000.0);

        estimate.BetaShape.Should().Be(3.2);
        estimate.EtaScale.Should().Be(50000.0);
        estimate.ExpectedRemainingKm.Should().Be(25000.0);

        // F(25,000) = 1 - exp(- (0.5)^3.2)
        // 0.5^3.2 = ~0.1088 -> 1 - exp(-0.1088) = ~0.103 (10.3%)
        estimate.FailureProbabilityPct.Should().BeInRange(10.0, 11.0);
        estimate.Urgency.Should().Be(MaintenanceUrgency.Routine);
    }

    [Fact]
    public void EstimateComponentRul_EngineOil_NormalAmbientTemp_Uses10000KmEta()
    {
        // EngineOil: Beta = 2.2, Eta = 10,000 km at 25°C
        var estimate = _radarService.EstimateComponentRul("VEH-OIL-01", "EngineOil", currentKm: 5000.0, ambientTempCelsius: 25.0);

        estimate.BetaShape.Should().Be(2.2);
        estimate.EtaScale.Should().Be(10000.0);
        estimate.ExpectedRemainingKm.Should().Be(5000.0);

        // F(5,000) = 1 - exp(- (0.5)^2.2) = 1 - exp(-0.2176) = ~19.55%
        estimate.FailureProbabilityPct.Should().BeInRange(19.0, 20.0);
    }

    [Fact]
    public void EstimateComponentRul_EngineOil_HighDesertAmbientTemp_AcceleratesDegradation()
    {
        // In extreme Algerian desert heat (e.g. 48°C > 38°C):
        // 10 degrees above 38°C accelerates shear degradation, reducing effective Eta
        var standardEstimate = _radarService.EstimateComponentRul("VEH-OIL-02", "EngineOil", currentKm: 6000.0, ambientTempCelsius: 25.0);
        var desertEstimate = _radarService.EstimateComponentRul("VEH-OIL-02", "EngineOil", currentKm: 6000.0, ambientTempCelsius: 48.0);

        desertEstimate.EtaScale.Should().BeLessThan(standardEstimate.EtaScale);
        desertEstimate.ExpectedRemainingKm.Should().BeLessThan(standardEstimate.ExpectedRemainingKm);
        desertEstimate.FailureProbabilityPct.Should().BeGreaterThan(standardEstimate.FailureProbabilityPct);
    }

    [Fact]
    public void EstimateComponentRul_EngineOil_FreezingColdStart_AcceleratesDegradation()
    {
        var normalEstimate = _radarService.EstimateComponentRul("VEH-OIL-03", "EngineOil", currentKm: 4000.0, ambientTempCelsius: 20.0);
        var coldEstimate = _radarService.EstimateComponentRul("VEH-OIL-03", "EngineOil", currentKm: 4000.0, ambientTempCelsius: -5.0);

        coldEstimate.EtaScale.Should().Be(9000.0); // 10% penalty
        coldEstimate.FailureProbabilityPct.Should().BeGreaterThan(normalEstimate.FailureProbabilityPct);
    }

    [Fact]
    public void EstimateComponentRul_FailureProbabilityIncreasesMonotonicallyWithMileage()
    {
        var est1 = _radarService.EstimateComponentRul("VEH-MONO", "BrakePads", 10000.0);
        var est2 = _radarService.EstimateComponentRul("VEH-MONO", "BrakePads", 25000.0);
        var est3 = _radarService.EstimateComponentRul("VEH-MONO", "BrakePads", 38000.0);

        est1.FailureProbabilityPct.Should().BeLessThan(est2.FailureProbabilityPct);
        est2.FailureProbabilityPct.Should().BeLessThan(est3.FailureProbabilityPct);
    }

    [Fact]
    public void EstimateComponentRul_NegativeMileage_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _radarService.EstimateComponentRul("VEH-1", "BrakePads", -500.0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region 7. RUL Threshold Checking & Autonomous Work Order Triggering

    [Fact]
    public void EstimateComponentRul_LowMileage_ReturnsRoutineUrgency()
    {
        var estimate = _radarService.EstimateComponentRul("VEH-ROUTINE", "BrakePads", 8000.0);

        estimate.Urgency.Should().Be(MaintenanceUrgency.Routine);
        estimate.RecommendedAction.Should().Contain("Routine");
    }

    [Fact]
    public void EstimateComponentRul_MediumMileage_ReturnsUpcomingUrgency()
    {
        // At 34,000 km, Brake Pads (Eta 40k) has RUL 6,000 km, Failure prob ~58% (>= 40%)
        var estimate = _radarService.EstimateComponentRul("VEH-UPCOMING", "BrakePads", 34000.0);

        estimate.Urgency.Should().Be(MaintenanceUrgency.Upcoming);
        estimate.RecommendedAction.Should().Contain("Upcoming");
    }

    [Fact]
    public void EstimateComponentRul_FailureProbAbove75Percent_TriggersImmediateActionAndAutoWorkOrder()
    {
        // At 39,500 km, Brake Pads (Eta 40k) has Failure prob ~62%, but RUL 500 km (< 1,500 km)
        var estimate = _radarService.EstimateComponentRul("VEH-CRIT-AUTO", "BrakePads", 39500.0);

        estimate.Urgency.Should().Be(MaintenanceUrgency.ImmediateActionRequired);
        estimate.RecommendedAction.Should().Contain("Critical");

        var workOrders = _radarService.GetWorkOrdersForVehicle("VEH-CRIT-AUTO");
        workOrders.Should().ContainSingle();
        var wo = workOrders[0];
        wo.Component.Should().Be("BrakePads");
        wo.Urgency.Should().Be(MaintenanceUrgency.ImmediateActionRequired);
        wo.EstimatedCostDzd.Should().Be(18000m);
        wo.Status.Should().Be(WorkOrderStatus.Pending);
    }

    [Fact]
    public void EstimateComponentRul_RulBelow1500Km_TriggersImmediateActionAndAutoWorkOrder()
    {
        // Engine oil at 9,200 km (Eta 10k): RUL is 800 km (< 1,500 km)
        var estimate = _radarService.EstimateComponentRul("VEH-RUL-LOW", "EngineOil", 9200.0);

        estimate.Urgency.Should().Be(MaintenanceUrgency.ImmediateActionRequired);
        var workOrders = _radarService.GetWorkOrdersForVehicle("VEH-RUL-LOW");
        workOrders.Should().ContainSingle(w => w.Component == "EngineOil");
    }

    [Fact]
    public void GenerateMaintenanceForecast_GeneratesForecastForAllThreeKeyComponents()
    {
        var forecast = _radarService.GenerateMaintenanceForecast("VEH-FORECAST", currentKm: 15000.0);

        forecast.Should().HaveCount(3);
        forecast.Select(f => f.ComponentName).Should().BeEquivalentTo(["BrakePads", "EngineOil", "Tires"]);
    }

    [Fact]
    public void CreateWorkOrder_ManualWorkOrder_StoresAndRetrievesSuccessfully()
    {
        var request = new CreateWorkOrderRequest(
            VehicleId: "VEH-MANUAL-WO",
            Component: "SuspensionBushings",
            Urgency: MaintenanceUrgency.Upcoming,
            EstimatedCostDzd: 22000m,
            Notes: "Periodic mechanical inspection");

        var order = _radarService.CreateWorkOrder(request);

        order.WorkOrderId.Should().StartWith("WO-");
        order.VehicleId.Should().Be("VEH-MANUAL-WO");
        order.Component.Should().Be("SuspensionBushings");
        order.EstimatedCostDzd.Should().Be(22000m);

        var retrieved = _radarService.GetWorkOrder(order.WorkOrderId);
        retrieved.Should().NotBeNull();
        retrieved!.WorkOrderId.Should().Be(order.WorkOrderId);
    }

    [Fact]
    public void GetWorkOrdersForVehicle_ReturnsOrdersInReverseChronologicalOrder()
    {
        _radarService.CreateWorkOrder(new("VEH-ORDERED", "Brakes", MaintenanceUrgency.Routine, 5000m));
        _radarService.CreateWorkOrder(new("VEH-ORDERED", "Tires", MaintenanceUrgency.Upcoming, 10000m));

        var orders = _radarService.GetWorkOrdersForVehicle("VEH-ORDERED");
        orders.Should().HaveCount(2);
        orders[0].CreatedAtUtc.Should().BeOnOrAfter(orders[1].CreatedAtUtc);
    }

    #endregion

    #region 8. Concurrency & Thread-Safety (Native Lock)

    [Fact]
    public void TelemetryStreamerEngine_ConcurrentIngestion_ThreadSafeStateUpdates()
    {
        const int threadCount = 20;
        const int packetsPerThread = 50;
        var vehicleId = "VEH-CONCURRENT";

        Parallel.For(0, threadCount, t =>
        {
            for (var p = 0; p < packetsPerThread; p++)
            {
                var packet = new Obd2TelemetryPacket(
                    VehicleId: vehicleId,
                    DriverId: $"DRV-{t}",
                    TimestampUtc: DateTime.UtcNow.AddMilliseconds((t * packetsPerThread) + p),
                    SpeedKmh: 30.0 + (p * 0.8), // intentionally breaks deadband
                    EngineRpm: 1500 + p,
                    InstantaneousFuelRateLitersPerHour: 3.5,
                    CoolantTempCelsius: 85.0 + (p * 0.1),
                    BatteryVoltage: 14.0,
                    ThrottlePositionPct: 15.0 + (p * 0.2),
                    Latitude: 36.75,
                    Longitude: 3.05);

                _streamerEngine.IngestTelemetry(packet);
            }
        });

        var state = _streamerEngine.GetLatestVehicleState(vehicleId);
        state.Should().NotBeNull();
        state!.TotalPacketsProcessed.Should().Be(threadCount * packetsPerThread);
    }

    [Fact]
    public void PredictiveMaintenanceRadarService_ConcurrentWorkOrderCreation_ThreadSafe()
    {
        const int orderCount = 50;
        var vehicleId = "VEH-CONC-WO";

        Parallel.For(0, orderCount, i =>
        {
            _radarService.CreateWorkOrder(new CreateWorkOrderRequest(
                VehicleId: vehicleId,
                Component: $"Part-{i}",
                Urgency: MaintenanceUrgency.Routine,
                EstimatedCostDzd: 1000m + i));
        });

        var orders = _radarService.GetWorkOrdersForVehicle(vehicleId);
        orders.Should().HaveCount(orderCount);
    }

    #endregion

    #region 9. ServiceCollection Dependency Injection Descriptors (Without BuildServiceProvider)

    [Fact]
    public void AddFleetServices_RegistersAllServicesAsSingletonsWithoutBuildServiceProvider()
    {
        // Explicit boundary requirement: Direct ServiceDescriptor assertion on ServiceCollection without BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddFleetServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IObd2TelemetryStreamerEngine) &&
            d.ImplementationType == typeof(Obd2TelemetryStreamerEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IDriverSafetyAndEcoScoringService) &&
            d.ImplementationType == typeof(DriverSafetyAndEcoScoringService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IPredictiveMaintenanceRadarService) &&
            d.ImplementationType == typeof(PredictiveMaintenanceRadarService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
