using System;

namespace SGrowing.Api.Modules.Aerospace.DualCombSpectroscopyGreenhouseFlux;

public interface IDualCombSpectroscopyEngine
{
    DualCombSpectroscopyAnalysisResult AnalyzeAtmosphericFlux(DualCombSpectroscopyTelemetry telemetry);
}

public sealed class DualCombSpectroscopyEngine : IDualCombSpectroscopyEngine
{
    private const double MinRequiredSnr = 1200.0; // Minimum SNR for sub-ppm Voigt profile inversion
    private const double MaxAllowableAllanDeviation = 2.0e-13; // Max comb optical phase instability at 100s
    private const double SignificantAnomalyThresholdCo2Ppm = 2.0; // Anomaly >= 2.0 ppm flags point source emission plume
    private const double SignificantAnomalyThresholdCh4Ppb = 15.0; // Anomaly >= 15 ppb flags fugitive methane leak

    public DualCombSpectroscopyAnalysisResult AnalyzeAtmosphericFlux(DualCombSpectroscopyTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.CombRepetitionRateMhz <= 0.0 || telemetry.RepetitionRateOffsetKhz <= 0.0 || telemetry.OpticalPathLengthMeters <= 0.0)
        {
            throw new ArgumentException("Comb repetition rate, offset, and optical path length must be positive.", nameof(telemetry));
        }

        // 1. Multiheterodyne Downconversion Factor: m_scale = f_r1 / Delta_f_r
        double fr1Hz = telemetry.CombRepetitionRateMhz * 1e6;
        double deltaFrHz = telemetry.RepetitionRateOffsetKhz * 1e3;
        double downconversionFactor = fr1Hz / deltaFrHz;

        // 2. Non-Aliased Optical Bandwidth: B_opt = f_r1^2 / (2 * Delta_f_r) in THz
        double nonAliasedBandwidthHz = Math.Pow(fr1Hz, 2.0) / (2.0 * deltaFrHz);
        double nonAliasedBandwidthThz = nonAliasedBandwidthHz * 1e-12;
        nonAliasedBandwidthThz = Math.Round(nonAliasedBandwidthThz, 2);

        // 3. Anomaly Delta from Baseline:
        double anomalyDelta = telemetry.RetrievedColumnAverageDryAirMoleFraction - telemetry.BaselineExpectedMoleFraction;
        anomalyDelta = Math.Round(anomalyDelta, 2);

        // 4. Spectroscopic Uncertainty from SNR and path length:
        // sigma_X ~ (X / SNR) * sqrt(1000 / L_path)
        double baselineFrac = telemetry.TargetGasSpecies.Contains("CH4", StringComparison.OrdinalIgnoreCase)
            ? 1900.0
            : 420.0;
        double snr = Math.Max(1.0, telemetry.MeasuredSnrRatio);
        double pathScaling = Math.Sqrt(1000.0 / Math.Max(100.0, telemetry.OpticalPathLengthMeters));
        double uncertainty = (baselineFrac / snr) * pathScaling;
        uncertainty = Math.Round(Math.Clamp(uncertainty, 0.01, 10.0), 3);

        // 5. Validation Checks
        bool isSnrOk = telemetry.MeasuredSnrRatio >= MinRequiredSnr;
        bool isCombStable = telemetry.AllanDeviationAt100s <= MaxAllowableAllanDeviation;

        double anomalyThreshold = telemetry.TargetGasSpecies.Contains("CH4", StringComparison.OrdinalIgnoreCase)
            ? SignificantAnomalyThresholdCh4Ppb
            : SignificantAnomalyThresholdCo2Ppm;
        bool isFluxSignificant = Math.Abs(anomalyDelta) >= anomalyThreshold;

        // 6. Regime & Guidance
        string regime;
        string guidance;

        if (!isCombStable)
        {
            regime = "COMB_OPTICAL_PHASE_COHERENCE_INSTABILITY";
            guidance = $"Comb Allan deviation ({telemetry.AllanDeviationAt100s:E2}) exceeded stability limit ({MaxAllowableAllanDeviation:E2}). Lock comb carrier-envelope offset (f_ceo) and repetition rate to ultra-low phase noise GPS-disciplined rubidium clocks.";
        }
        else if (!isSnrOk)
        {
            regime = "LOW_SNR_ATMOSPHERIC_TURBULENCE_SCINTILLATION";
            guidance = $"Spectroscopic SNR ({telemetry.MeasuredSnrRatio:F0}) fell below threshold ({MinRequiredSnr:F0}). Active atmospheric adaptive optics tracking or dwell time co-addition required to penetrate haze and optical scintillation.";
        }
        else if (isFluxSignificant && anomalyDelta > 0.0)
        {
            regime = "POINT_SOURCE_GREENHOUSE_GAS_PLUME_DETECTED";
            guidance = $"Significant column mole fraction elevation detected (+{anomalyDelta:F2} over baseline). Triangulate plume origin using cross-wind dispersion modeling and log georeferenced anomaly coordinates.";
        }
        else if (isFluxSignificant && anomalyDelta < 0.0)
        {
            regime = "TERRESTRIAL_BIOSPHERE_CARBON_SINK_ENHANCEMENT";
            guidance = $"Negative mole fraction anomaly ({anomalyDelta:F2}) indicates active photosynthetic vegetative sink drawdown along open path.";
        }
        else
        {
            regime = "NOMINAL_BACKGROUND_ATMOSPHERIC_COLUMN_VERIFIED";
            guidance = "Dual-comb spectrometer functioning in high-precision mode: full multiheterodyne fidelity, negligible instrument phase drift, and baseline atmospheric gas mole fraction confirmed.";
        }

        return new DualCombSpectroscopyAnalysisResult
        {
            FlightMissionId = telemetry.FlightMissionId,
            DownconversionMultiheterodyneScaleFactor = Math.Round(downconversionFactor, 0),
            NonAliasedOpticalBandwidthThz = nonAliasedBandwidthThz,
            RetrievedGasMoleFraction = Math.Round(telemetry.RetrievedColumnAverageDryAirMoleFraction, 2),
            AnomalyDeltaFromBaseline = anomalyDelta,
            MeasurementUncertainty = uncertainty,
            IsSnrSufficientForRetrieval = isSnrOk,
            IsCombPhaseCoherenceStable = isCombStable,
            IsAtmosphericFluxSignificant = isFluxSignificant,
            SpectroscopyRetrievalRegime = regime,
            FluxSurveillanceRecommendation = guidance
        };
    }
}
