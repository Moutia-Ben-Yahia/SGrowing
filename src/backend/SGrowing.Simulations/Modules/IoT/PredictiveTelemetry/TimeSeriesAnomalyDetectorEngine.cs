using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.IoT.PredictiveTelemetry;

/// <summary>
/// High-throughput streaming time-series anomaly forecaster combining Holt-Winters
/// double exponential trend smoothing and multivariate Mahalanobis distance outlier detection.
/// </summary>
public sealed class TimeSeriesAnomalyDetectorEngine
{
    private const double AlphaLevel = 0.35;
    private const double BetaTrend = 0.15;
    private const double MahalanobisWarningThreshold = 3.84; // ~95% confidence chi-square (df=1)
    private const double MahalanobisCriticalThreshold = 7.88; // ~99.5% confidence
    private const double MahalanobisEmergencyThreshold = 14.1; // extreme divergence

    // Machine baseline statistics: Mean and Variance per metric
    private readonly ConcurrentDictionary<string, MachineSensorBaseline> _baselines = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HoltWintersState> _forecastStates = new(StringComparer.OrdinalIgnoreCase);

    public AnomalyDetectionResult AnalyzeSample(MachineTelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var baseline = _baselines.GetOrAdd(sample.MachineId, _ => MachineSensorBaseline.Default());
        var hwState = _forecastStates.GetOrAdd(sample.MachineId, _ => new HoltWintersState(sample.VibrationRmsMmSec, 0.0));

        // 1. Holt-Winters 1-step Ahead Forecast on primary metric (Vibration)
        var priorForecast = hwState.Level + hwState.Trend;
        var newLevel = (AlphaLevel * sample.VibrationRmsMmSec) + ((1.0 - AlphaLevel) * (hwState.Level + hwState.Trend));
        var newTrend = (BetaTrend * (newLevel - hwState.Level)) + ((1.0 - BetaTrend) * hwState.Trend);
        _forecastStates[sample.MachineId] = new HoltWintersState(newLevel, newTrend);

        // 2. Multivariate Mahalanobis Distance (diagonalized variance approximation for high throughput)
        var d2Vib = Math.Pow((sample.VibrationRmsMmSec - baseline.MeanVibration) / baseline.StdDevVibration, 2);
        var d2Temp = Math.Pow((sample.BearingTemperatureCelsius - baseline.MeanTemperature) / baseline.StdDevTemperature, 2);
        var d2Curr = Math.Pow((sample.MotorCurrentAmperes - baseline.MeanCurrent) / baseline.StdDevCurrent, 2);
        var d2Press = Math.Pow((sample.CompressorPressurePsi - baseline.MeanPressure) / baseline.StdDevPressure, 2);
        var d2Acoust = Math.Pow((sample.AcousticEmissionDb - baseline.MeanAcoustic) / baseline.StdDevAcoustic, 2);

        var totalD2 = d2Vib + d2Temp + d2Curr + d2Press + d2Acoust;
        var mahalanobisDist = Math.Sqrt(totalD2);

        // 3. Dominant Anomaly Component Identification
        var dominantMetric = "Vibration";
        var maxComponent = d2Vib;
        if (d2Temp > maxComponent) { maxComponent = d2Temp; dominantMetric = "BearingTemperature"; }
        if (d2Curr > maxComponent) { maxComponent = d2Curr; dominantMetric = "MotorCurrent"; }
        if (d2Press > maxComponent) { maxComponent = d2Press; dominantMetric = "CompressorPressure"; }
        if (d2Acoust > maxComponent) { dominantMetric = "AcousticEmission"; }

        // 4. Severity Classification
        var severity = AnomalySeverity.Nominal;
        var isAnomaly = false;

        if (mahalanobisDist >= MahalanobisEmergencyThreshold || sample.BearingTemperatureCelsius > 115.0)
        {
            severity = AnomalySeverity.EmergencyShutdown;
            isAnomaly = true;
        }
        else if (mahalanobisDist >= MahalanobisCriticalThreshold || sample.VibrationRmsMmSec > 8.0)
        {
            severity = AnomalySeverity.Critical;
            isAnomaly = true;
        }
        else if (mahalanobisDist >= MahalanobisWarningThreshold)
        {
            severity = AnomalySeverity.Warning;
            isAnomaly = true;
        }

        var explanation = severity switch
        {
            AnomalySeverity.EmergencyShutdown => $"Emergency thermal runaway or catastrophic vibration spike in {dominantMetric}. Instant load trip required.",
            AnomalySeverity.Critical => $"Critical failure precursor in {dominantMetric} (D_M={mahalanobisDist:F2} >= {MahalanobisCriticalThreshold}). Work order required.",
            AnomalySeverity.Warning => $"Early acoustic or thermal drift in {dominantMetric} (D_M={mahalanobisDist:F2}). Watch closely.",
            _ => "Telemetry nominal within 95% operating envelope."
        };

        return new AnomalyDetectionResult(
            MachineId: sample.MachineId,
            TimestampUtc: sample.TimestampUtc,
            Severity: severity,
            MahalanobisDistance: Math.Round(mahalanobisDist, 3),
            ThresholdDistance: MahalanobisCriticalThreshold,
            IsAnomaly: isAnomaly,
            DominantMetricAnomaly: dominantMetric,
            ForecastedValue: Math.Round(priorForecast, 3),
            ActualValue: sample.VibrationRmsMmSec,
            DiagnosticExplanation: explanation
        );
    }

    public void CalibrateBaseline(string machineId, MachineSensorBaseline customBaseline)
    {
        _baselines[machineId] = customBaseline;
    }
}

public sealed record HoltWintersState(double Level, double Trend);

public sealed record MachineSensorBaseline(
    double MeanVibration,
    double StdDevVibration,
    double MeanTemperature,
    double StdDevTemperature,
    double MeanCurrent,
    double StdDevCurrent,
    double MeanPressure,
    double StdDevPressure,
    double MeanAcoustic,
    double StdDevAcoustic)
{
    public static MachineSensorBaseline Default() => new(
        MeanVibration: 2.5,
        StdDevVibration: 0.8,
        MeanTemperature: 65.0,
        StdDevTemperature: 8.0,
        MeanCurrent: 24.0,
        StdDevCurrent: 3.5,
        MeanPressure: 90.0,
        StdDevPressure: 5.0,
        MeanAcoustic: 72.0,
        StdDevAcoustic: 6.0
    );
}
