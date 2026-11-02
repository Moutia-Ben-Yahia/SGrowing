namespace SGrowing.Api.Modules.Environmental.QclPhotoacousticSpectroscopy;

public class QclPhotoacousticSpectroscopyEngine
{
    private const double DefaultSpeciesCalibrationFactor = 10.0;

    public QclPhotoacousticAnalysisResult AnalyzeSpectroscopy(QclPhotoacousticTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.SensorId))
        {
            throw new ArgumentException("Sensor ID cannot be null or empty.", nameof(telemetry));
        }

        if (telemetry.LaserOpticalPowerMilliWatts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Laser optical power must be positive.");
        }

        if (telemetry.QepasResonatorQFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Resonator Q factor must be positive.");
        }

        if (telemetry.BaselineNoiseMicroVolts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Baseline noise must be positive.");
        }

        // Signal-to-noise ratio
        double snr = telemetry.Demodulated2fHarmonicSignalMicroVolts / telemetry.BaselineNoiseMicroVolts;

        // Calculate concentration in ppb:
        // C_gas = (S_2f / (P_laser * Q * 1e-6)) * CalibrationFactor
        double cellCouplingFactor = (telemetry.LaserOpticalPowerMilliWatts * telemetry.QepasResonatorQFactor) * 1e-6;
        cellCouplingFactor = Math.Max(cellCouplingFactor, 1e-6);

        double concentrationPpb = (telemetry.Demodulated2fHarmonicSignalMicroVolts / cellCouplingFactor) * DefaultSpeciesCalibrationFactor;
        concentrationPpb = Math.Max(0.0, concentrationPpb);

        bool isLeakSurge = concentrationPpb >= 2500.0; // >= 2.5 ppm

        string status;
        string tuningCorrection;
        string summary;

        if (concentrationPpb >= 10000.0 || snr >= 500.0)
        {
            status = "CRITICAL_HAZARDOUS_GAS_LEAK_SURGE";
            tuningCorrection = "EMERGENCY_VENTILATION_AND_ISOLATION";
            summary = $"CRITICAL GAS LEAK: Concentration of {telemetry.TargetGasSpecies} reached {concentrationPpb:F1} ppb ({concentrationPpb / 1000.0:F2} ppm) with SNR={snr:F0}. Emergency atmospheric scrubbers and gas line cutoff valves engaged.";
        }
        else if (isLeakSurge)
        {
            status = "ELEVATED_EMISSION_WARNING";
            tuningCorrection = "LOCKED_ON_PEAK";
            summary = $"WARNING: Elevated trace gas levels detected ({concentrationPpb:F1} ppb). QCL wavelength locked to {telemetry.LaserWavelengthMicrons:F2} µm absorption peak. Inspection dispatched.";
        }
        else
        {
            status = "NOMINAL_TRACE_BACKGROUND";
            tuningCorrection = "LOCKED_ON_PEAK";
            summary = $"Nominal trace gas concentration: {concentrationPpb:F1} ppb ({telemetry.TargetGasSpecies}) at SNR={snr:F1}. Ambient baseline normal.";
        }

        return new QclPhotoacousticAnalysisResult(
            telemetry.SensorId,
            telemetry.TargetGasSpecies,
            Math.Round(snr, 1),
            Math.Round(concentrationPpb, 1),
            isLeakSurge,
            status,
            tuningCorrection,
            summary);
    }
}
