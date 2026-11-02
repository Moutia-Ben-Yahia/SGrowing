namespace SGrowing.Tests.Environmental;

using SGrowing.Api.Modules.Environmental.QclPhotoacousticSpectroscopy;
using Xunit;

public class QclPhotoacousticSpectroscopyTests
{
    private readonly QclPhotoacousticSpectroscopyEngine _engine = new();

    [Fact]
    public void AnalyzeSpectroscopy_NominalTraceGas_ReportsNominalBackground()
    {
        // P = 80 mW, Q = 10000 -> factor = 0.8
        // S_2f = 40 uV -> conc = (40 / 0.8) * 10 = 500 ppb (0.5 ppm)
        // Noise = 1.0 uV -> SNR = 40
        var telemetry = new QclPhotoacousticTelemetry(
            SensorId: "QEPAS-CH4-01",
            TargetGasSpecies: "Methane-CH4",
            LaserWavelengthMicrons: 7.8,
            LaserOpticalPowerMilliWatts: 80.0,
            QepasResonatorQFactor: 10000.0,
            Demodulated2fHarmonicSignalMicroVolts: 40.0,
            CellAmbientPressureAtm: 1.0,
            CellTemperatureCelsius: 22.0,
            BaselineNoiseMicroVolts: 1.0);

        var result = _engine.AnalyzeSpectroscopy(telemetry);

        Assert.Equal("QEPAS-CH4-01", result.SensorId);
        Assert.Equal("Methane-CH4", result.TargetGasSpecies);
        Assert.Equal(40.0, result.SignalToNoiseRatio);
        Assert.Equal(500.0, result.CalculatedConcentrationPpb);
        Assert.False(result.IsGasLeakSurgeDetected);
        Assert.Equal("NOMINAL_TRACE_BACKGROUND", result.SensorOperationalStatus);
        Assert.Equal("LOCKED_ON_PEAK", result.LaserTuningCorrection);
    }

    [Fact]
    public void AnalyzeSpectroscopy_ElevatedTraceEmission_ReportsWarning()
    {
        // S_2f = 280 uV, factor = 0.8 -> conc = (280 / 0.8) * 10 = 3500 ppb (3.5 ppm)
        var telemetry = new QclPhotoacousticTelemetry(
            SensorId: "QEPAS-N2O-02",
            TargetGasSpecies: "NitrousOxide-N2O",
            LaserWavelengthMicrons: 4.5,
            LaserOpticalPowerMilliWatts: 80.0,
            QepasResonatorQFactor: 10000.0,
            Demodulated2fHarmonicSignalMicroVolts: 280.0,
            CellAmbientPressureAtm: 1.0,
            CellTemperatureCelsius: 22.0,
            BaselineNoiseMicroVolts: 1.0);

        var result = _engine.AnalyzeSpectroscopy(telemetry);

        Assert.True(result.IsGasLeakSurgeDetected);
        Assert.Equal(3500.0, result.CalculatedConcentrationPpb);
        Assert.Equal("ELEVATED_EMISSION_WARNING", result.SensorOperationalStatus);
        Assert.Contains("Elevated trace gas levels detected", result.DiagnosticSummary);
    }

    [Fact]
    public void AnalyzeSpectroscopy_CriticalHazardousLeak_TriggersEmergencyVentilation()
    {
        // S_2f = 1200 uV, factor = 0.8 -> conc = 15000 ppb (15 ppm)
        var telemetry = new QclPhotoacousticTelemetry(
            SensorId: "QEPAS-SF6-03",
            TargetGasSpecies: "SulfurHexafluoride-SF6",
            LaserWavelengthMicrons: 10.55,
            LaserOpticalPowerMilliWatts: 80.0,
            QepasResonatorQFactor: 10000.0,
            Demodulated2fHarmonicSignalMicroVolts: 1200.0,
            CellAmbientPressureAtm: 1.0,
            CellTemperatureCelsius: 25.0,
            BaselineNoiseMicroVolts: 1.5);

        var result = _engine.AnalyzeSpectroscopy(telemetry);

        Assert.True(result.IsGasLeakSurgeDetected);
        Assert.Equal(15000.0, result.CalculatedConcentrationPpb);
        Assert.Equal("CRITICAL_HAZARDOUS_GAS_LEAK_SURGE", result.SensorOperationalStatus);
        Assert.Equal("EMERGENCY_VENTILATION_AND_ISOLATION", result.LaserTuningCorrection);
        Assert.Contains("CRITICAL GAS LEAK", result.DiagnosticSummary);
    }

    [Fact]
    public void AnalyzeSpectroscopy_NegativeLaserPower_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new QclPhotoacousticTelemetry(
            SensorId: "ERR-001",
            TargetGasSpecies: "CH4",
            LaserWavelengthMicrons: 7.8,
            LaserOpticalPowerMilliWatts: -50.0,
            QepasResonatorQFactor: 10000.0,
            Demodulated2fHarmonicSignalMicroVolts: 20.0,
            CellAmbientPressureAtm: 1.0,
            CellTemperatureCelsius: 20.0,
            BaselineNoiseMicroVolts: 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeSpectroscopy(invalid));
    }
}
