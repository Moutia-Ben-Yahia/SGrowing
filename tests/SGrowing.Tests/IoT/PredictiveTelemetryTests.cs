using FluentAssertions;
using SGrowing.Api.Modules.IoT.PredictiveTelemetry;
using Xunit;

namespace SGrowing.Tests.IoT;

public sealed class PredictiveTelemetryTests
{
    private readonly TimeSeriesAnomalyDetectorEngine _anomalyEngine = new();
    private readonly MachineryRemainingUsefulLifeEstimator _rulEstimator = new();
    private readonly AutonomousMaintenanceSchedulerService _scheduler = new();

    #region Anomaly Detection Tests

    [Fact]
    public void AnalyzeSample_NominalParameters_ReturnsNominalSeverity()
    {
        var sample = new MachineTelemetrySample(
            MachineId: "CNC-MILL-01",
            TimestampUtc: DateTime.UtcNow,
            VibrationRmsMmSec: 2.4,
            BearingTemperatureCelsius: 64.0,
            MotorCurrentAmperes: 23.5,
            CompressorPressurePsi: 90.2,
            AcousticEmissionDb: 71.0
        );

        var result = _anomalyEngine.AnalyzeSample(sample);

        result.Should().NotBeNull();
        result.MachineId.Should().Be("CNC-MILL-01");
        result.Severity.Should().Be(AnomalySeverity.Nominal);
        result.IsAnomaly.Should().BeFalse();
        result.MahalanobisDistance.Should().BeLessThan(3.84);
    }

    [Fact]
    public void AnalyzeSample_SevereThermalAndVibrationSpike_DetectsEmergencyOrCriticalAnomaly()
    {
        var sample = new MachineTelemetrySample(
            MachineId: "PACKAGING-ROBOT-04",
            TimestampUtc: DateTime.UtcNow,
            VibrationRmsMmSec: 9.5, // > 8.0 mm/s critical threshold
            BearingTemperatureCelsius: 118.0, // > 115C emergency threshold
            MotorCurrentAmperes: 45.0, // high current draw
            CompressorPressurePsi: 70.0,
            AcousticEmissionDb: 95.0
        );

        var result = _anomalyEngine.AnalyzeSample(sample);

        result.IsAnomaly.Should().BeTrue();
        result.Severity.Should().BeOneOf(AnomalySeverity.EmergencyShutdown, AnomalySeverity.Critical);
        result.DominantMetricAnomaly.Should().BeOneOf("BearingTemperature", "Vibration");
        result.DiagnosticExplanation.Should().Match("*required*");
    }

    [Fact]
    public void AnalyzeSample_SequentialSamples_ForecastTracksTrend()
    {
        var machineId = "CONVEYOR-MOTOR-09";
        var t0 = DateTime.UtcNow;

        // Feed rising trend
        _anomalyEngine.AnalyzeSample(new MachineTelemetrySample(machineId, t0, 2.0, 60.0, 20.0, 90.0, 70.0));
        _anomalyEngine.AnalyzeSample(new MachineTelemetrySample(machineId, t0.AddSeconds(1), 3.0, 62.0, 21.0, 90.0, 71.0));
        var res3 = _anomalyEngine.AnalyzeSample(new MachineTelemetrySample(machineId, t0.AddSeconds(2), 4.0, 64.0, 22.0, 90.0, 72.0));

        res3.ForecastedValue.Should().BeGreaterThan(2.0, "Holt-Winters level & trend should adjust to increasing vibration.");
    }

    #endregion

    #region Remaining Useful Life (RUL) Tests

    [Fact]
    public void EstimateRul_LowAccumulatedHoursAndNominalStress_HighRulAndLowFailureRisk()
    {
        var estimate = _rulEstimator.EstimateRul(
            machineId: "BLOWER-FAN-02",
            accumulatedOperatingHours: 1200.0,
            meanVibrationRms: 2.2,
            meanBearingTempCelsius: 62.0
        );

        estimate.Should().NotBeNull();
        estimate.EstimatedRulHours.Should().BeGreaterThan(5000.0);
        estimate.FailureProbabilityNext48Hours.Should().BeLessThan(0.05);
        estimate.RecommendedAction.Should().Contain("NOMINAL");
    }

    [Fact]
    public void EstimateRul_HighStressAndWear_ComputesAcceleratedFatigue()
    {
        var estimate = _rulEstimator.EstimateRul(
            machineId: "STAMPING-PRESS-07",
            accumulatedOperatingHours: 4500.0,
            meanVibrationRms: 6.8, // severe vibration
            meanBearingTempCelsius: 95.0 // severe heat (25C above baseline)
        );

        estimate.EstimatedRulHours.Should().BeLessThan(500.0);
        estimate.FailureProbabilityNext48Hours.Should().BeGreaterThan(0.5);
        estimate.FailureProbabilityNext168Hours.Should().BeGreaterThanOrEqualTo(estimate.FailureProbabilityNext48Hours);
        estimate.RecommendedAction.Should().Match("*CRITICAL*");
    }

    #endregion

    #region Autonomous Maintenance Scheduler Tests

    [Fact]
    public void EvaluateAndSchedule_CriticalAnomaly_GeneratesUrgentWorkOrderAndReservesPart()
    {
        var anomaly = new AnomalyDetectionResult(
            MachineId: "INJECTION-MOLD-03",
            TimestampUtc: DateTime.UtcNow,
            Severity: AnomalySeverity.Critical,
            MahalanobisDistance: 8.5,
            ThresholdDistance: 7.88,
            IsAnomaly: true,
            DominantMetricAnomaly: "Vibration",
            ForecastedValue: 3.5,
            ActualValue: 8.2,
            DiagnosticExplanation: "Critical failure precursor in Vibration."
        );

        var rul = new MachineryRulEstimate(
            MachineId: "INJECTION-MOLD-03",
            EstimatedRulHours: 42.0,
            CumulativeStressCycles: 8500.0,
            FailureProbabilityNext48Hours: 0.85,
            FailureProbabilityNext168Hours: 0.98,
            WeibullBetaShape: 2.4,
            WeibullEtaScaleHours: 8542.0,
            RecommendedAction: "CRITICAL: Immediate bearing replacement before Catastrophic Seizure.",
            EvaluatedAtUtc: DateTime.UtcNow
        );

        var initialStock = _scheduler.GetSparePartStock("SKU-BEARING-6205");

        var wo = _scheduler.EvaluateAndSchedule(anomaly, rul, "SKU-BEARING-6205");

        wo.Should().NotBeNull();
        wo.MachineId.Should().Be("INJECTION-MOLD-03");
        wo.Priority.Should().Be(MaintenancePriority.P1EmergencyRepair);
        wo.AutoReservedSparePart.Should().BeTrue();
        wo.ScheduledWindowUtc.Should().BeBefore(DateTime.UtcNow.AddHours(3));

        _scheduler.GetSparePartStock("SKU-BEARING-6205").Should().Be(initialStock - 1);
        _scheduler.GetActiveWorkOrders().Should().ContainSingle();
    }

    #endregion
}
