using System;

namespace SGrowing.Api.Modules.Quantum.NvCenterNanoscaleMagnetometry;

public sealed class NvCenterNanoscaleMagnetometryEngine : INvCenterNanoscaleMagnetometryEngine
{
    private const double GyromagneticRatioMhzPerMilliTesla = 28.024; // gamma_e / 2pi

    public NvCenterMagnetometryAnalysisResult AnalyzeMagnetometry(NvCenterMagnetometryTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var fPlus = Math.Max(telemetry.ZeroFieldSplittingFrequencyGigaHertz, telemetry.MeasuredUpperDipFrequencyGigaHertz);
        var fMinus = Math.Min(telemetry.ZeroFieldSplittingFrequencyGigaHertz, telemetry.MeasuredLowerDipFrequencyGigaHertz);

        // Zeeman splitting: Delta f = f_+ - f_-
        var splittingGhz = Math.Max(0.0, fPlus - fMinus);
        var splittingMhz = splittingGhz * 1000.0;

        // B = Delta f / (2 * gamma_e) [mT]
        var magneticFieldMilliTesla = splittingMhz / (2.0 * GyromagneticRatioMhzPerMilliTesla);

        // Shot-noise limited sensitivity: eta_B in nT / sqrt(Hz)
        var contrast = Math.Clamp(telemetry.OpticalFringeContrastPercent / 100.0, 0.01, 1.0);
        var t2StarUs = Math.Max(0.05, telemetry.SpinDephasingCoherenceTimeT2StarMicroseconds);
        var photonCount = Math.Max(10.0, telemetry.OpticalReadoutPhotonCountPerShot);

        // Sensitivity formula (nT/sqrt(Hz))
        var photonNorm = Math.Sqrt(photonCount / 1000.0);
        var sensitivityNanoTeslaPerRootHz = Math.Clamp(15.0 / (contrast * Math.Sqrt(t2StarUs) * photonNorm), 0.5, 600.0);

        string regime;
        string guidance;
        bool isOptimal;

        if (telemetry.OpticalFringeContrastPercent < 4.0)
        {
            regime = "ODMR_CONTRAST_WASHOUT_COLLAPSE";
            guidance = "Optically Detected Magnetic Resonance (ODMR) contrast dropped below 4%. Increase green 532 nm laser polarization power and tune microwave waveguide impedance.";
            isOptimal = false;
        }
        else if (telemetry.SpinDephasingCoherenceTimeT2StarMicroseconds < 0.30)
        {
            regime = "COHERENCE_DEPHASING_PARAMAGNETIC_NOISE";
            guidance = "Electron spin dephasing time T2* severely degraded (< 0.3 us) by surface 13C nuclear spin bath or dangling bonds. Apply XY8/CPMG dynamical decoupling pulse sequence.";
            isOptimal = false;
        }
        else if (sensitivityNanoTeslaPerRootHz > 50.0)
        {
            regime = "SUB_OPTIMAL_SENSITIVITY_MARGIN_WARNING";
            guidance = "Magnetic field sensitivity above 50 nT/sqrt(Hz) threshold. Optimize avalanche photodiode (APD) fluorescence collection efficiency.";
            isOptimal = false;
        }
        else
        {
            regime = "OPTIMAL_NANOSCALE_QUANTUM_MAGNETOMETRY";
            guidance = "Sub-10 nm NV color center spin magnetometry operating at high quantum sensitivity. Clear Zeeman splitting and high ODMR dip contrast confirmed.";
            isOptimal = true;
        }

        return new NvCenterMagnetometryAnalysisResult(
            SensorId: telemetry.SensorId,
            ReconstructedZeemanSplittingMegaHertz: splittingMhz,
            CalculatedMagneticFieldMilliTesla: magneticFieldMilliTesla,
            MagneticSensitivityNanoTeslaPerRootHertz: sensitivityNanoTeslaPerRootHz,
            OdmrMagnetometryRegime: regime,
            NanoscaleProbeGuidance: guidance,
            IsSensitivityOptimal: isOptimal
        );
    }
}
