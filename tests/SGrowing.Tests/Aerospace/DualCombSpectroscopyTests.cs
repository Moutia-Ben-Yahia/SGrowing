using System;
using SGrowing.Api.Modules.Aerospace.DualCombSpectroscopyGreenhouseFlux;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class DualCombSpectroscopyTests
{
    private readonly DualCombSpectroscopyEngine _engine = new();

    [Fact]
    public void AnalyzeAtmosphericFlux_NominalBackground_ReturnsVerifiedColumn()
    {
        var telemetry = new DualCombSpectroscopyTelemetry
        {
            FlightMissionId = "AIRBORNE-DCS-FLIGHT-01",
            TargetGasSpecies = "CO2",
            CombRepetitionRateMhz = 100.0,
            RepetitionRateOffsetKhz = 2.5,
            OpticalPathLengthMeters = 3000.0,
            MeasuredSnrRatio = 1800.0,
            AllanDeviationAt100s = 1.1e-13,
            RetrievedColumnAverageDryAirMoleFraction = 420.5,
            BaselineExpectedMoleFraction = 420.0
        };

        var result = _engine.AnalyzeAtmosphericFlux(telemetry);

        Assert.Equal("AIRBORNE-DCS-FLIGHT-01", result.FlightMissionId);
        Assert.True(result.IsSnrSufficientForRetrieval);
        Assert.True(result.IsCombPhaseCoherenceStable);
        Assert.False(result.IsAtmosphericFluxSignificant);
        Assert.True(result.MeasurementUncertainty < 0.5); // < 0.5 ppm
        Assert.Equal("NOMINAL_BACKGROUND_ATMOSPHERIC_COLUMN_VERIFIED", result.SpectroscopyRetrievalRegime);
    }

    [Fact]
    public void AnalyzeAtmosphericFlux_PlumeDetected_TriggersPointSourceAlarm()
    {
        var telemetry = new DualCombSpectroscopyTelemetry
        {
            FlightMissionId = "SATELLITE-CH4-FUGITIVE-02",
            TargetGasSpecies = "CH4",
            CombRepetitionRateMhz = 120.0,
            RepetitionRateOffsetKhz = 3.0,
            OpticalPathLengthMeters = 5000.0,
            MeasuredSnrRatio = 1500.0,
            AllanDeviationAt100s = 1.3e-13,
            RetrievedColumnAverageDryAirMoleFraction = 1945.0, // Significant elevation > 15 ppb
            BaselineExpectedMoleFraction = 1900.0
        };

        var result = _engine.AnalyzeAtmosphericFlux(telemetry);

        Assert.True(result.IsAtmosphericFluxSignificant);
        Assert.True(result.AnomalyDeltaFromBaseline >= 45.0);
        Assert.Equal("POINT_SOURCE_GREENHOUSE_GAS_PLUME_DETECTED", result.SpectroscopyRetrievalRegime);
        Assert.Contains("Triangulate plume origin", result.FluxSurveillanceRecommendation);
    }

    [Fact]
    public void AnalyzeAtmosphericFlux_PhaseInstability_TriggersCoherenceAlert()
    {
        var telemetry = new DualCombSpectroscopyTelemetry
        {
            FlightMissionId = "AIRBORNE-DCS-DRIFT-03",
            TargetGasSpecies = "CO2",
            CombRepetitionRateMhz = 100.0,
            RepetitionRateOffsetKhz = 2.5,
            OpticalPathLengthMeters = 2000.0,
            MeasuredSnrRatio = 1600.0,
            AllanDeviationAt100s = 5.5e-13, // Instability > 2.0e-13
            RetrievedColumnAverageDryAirMoleFraction = 420.0,
            BaselineExpectedMoleFraction = 420.0
        };

        var result = _engine.AnalyzeAtmosphericFlux(telemetry);

        Assert.False(result.IsCombPhaseCoherenceStable);
        Assert.Equal("COMB_OPTICAL_PHASE_COHERENCE_INSTABILITY", result.SpectroscopyRetrievalRegime);
        Assert.Contains("exceeded stability limit", result.FluxSurveillanceRecommendation);
    }

    [Fact]
    public void AnalyzeAtmosphericFlux_LowSnrTurbulence_TriggersScintillationWarning()
    {
        var telemetry = new DualCombSpectroscopyTelemetry
        {
            FlightMissionId = "GROUND-DCS-HAZE-04",
            TargetGasSpecies = "CO2",
            CombRepetitionRateMhz = 80.0,
            RepetitionRateOffsetKhz = 2.0,
            OpticalPathLengthMeters = 8000.0,
            MeasuredSnrRatio = 450.0, // Low SNR < 1200
            AllanDeviationAt100s = 1.0e-13,
            RetrievedColumnAverageDryAirMoleFraction = 421.0,
            BaselineExpectedMoleFraction = 420.0
        };

        var result = _engine.AnalyzeAtmosphericFlux(telemetry);

        Assert.False(result.IsSnrSufficientForRetrieval);
        Assert.Equal("LOW_SNR_ATMOSPHERIC_TURBULENCE_SCINTILLATION", result.SpectroscopyRetrievalRegime);
        Assert.Contains("adaptive optics tracking", result.FluxSurveillanceRecommendation);
    }

    [Fact]
    public void AnalyzeAtmosphericFlux_ZeroOrNegativeRepetitionRate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeAtmosphericFlux(null!));

        var invalidTelemetry = new DualCombSpectroscopyTelemetry
        {
            FlightMissionId = "INV-DCS",
            CombRepetitionRateMhz = 0.0,
            RepetitionRateOffsetKhz = 2.0,
            OpticalPathLengthMeters = 1000.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeAtmosphericFlux(invalidTelemetry));
    }
}
