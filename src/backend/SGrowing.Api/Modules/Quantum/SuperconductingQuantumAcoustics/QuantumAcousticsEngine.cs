namespace SGrowing.Api.Modules.Quantum.SuperconductingQuantumAcoustics;

public interface IQuantumAcousticsEngine
{
    QuantumAcousticsEvaluation AnalyzeAcoustics(QuantumAcousticsTelemetry telemetry);
}

public class QuantumAcousticsEngine : IQuantumAcousticsEngine
{
    public QuantumAcousticsEvaluation AnalyzeAcoustics(QuantumAcousticsTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Thermal phonon occupancy n_th = 1 / (exp(h*nu / kB*T) - 1)
        // h / kB = 4.79924e-11 K / Hz = 4.79924e-2 K / GHz
        double freqGhz = telemetry.ResonatorFrequencyGigaHertz;
        double tKelvin = Math.Max(1e-4, telemetry.CryostatPlateTemperatureMilliKelvin * 1e-3);
        double exponent = (0.0479924 * freqGhz) / tKelvin;
        double nTh = 1.0 / Math.Max(1e-4, Math.Exp(Math.Min(50.0, exponent)) - 1.0);

        // Cooperativity C = 4 * g^2 / (kappa_transmon * gamma_phonon)
        // gamma_phonon = 1 / tau_phonon (in MHz: 1 / tau_us)
        double gammaPhononMhz = 1.0 / Math.Max(0.1, telemetry.PhononCoherenceLifetimeMicroseconds);
        double kappaTransmonMhz = 0.05; // Typical high-Q transmon linewidth 50 kHz
        double g = telemetry.TransmonAcousticCouplingMhz;
        double cooperativity = (4.0 * g * g) / (kappaTransmonMhz * gammaPhononMhz);

        // Non-classical negativity margin: -W(0,0) (classical is <= 0)
        double wignerMargin = -telemetry.MeasuredWignerOriginValue;

        bool isQualityOptimal = telemetry.MechanicalQualityFactor >= 5.0e6 && telemetry.PhononCoherenceLifetimeMicroseconds >= 80.0;
        bool isThermalSuppressed = nTh <= 0.035 && telemetry.CryostatPlateTemperatureMilliKelvin <= 20.0;
        bool isWignerNonClassical = telemetry.MeasuredWignerOriginValue <= -0.40;

        if (!isQualityOptimal)
        {
            regimes.Add("ACOUSTIC_PHONON_DISSIPATION_DECAY");
            mitigations.Add("Re-polish LiNbO3/sapphire piezoelectric crystal facets to suppress acoustic bulk surface Rayleigh scattering losses.");
        }

        if (!isThermalSuppressed)
        {
            regimes.Add("THERMAL_PHONON_BATH_EXCITATION");
            mitigations.Add("Optimize gold-plated OFHC copper cold finger clamping to dilution refrigerator mixing chamber to lower acoustic base temperature below 15 mK.");
        }

        if (!isWignerNonClassical)
        {
            regimes.Add("WIGNER_NEGATIVITY_DECOHERENCE_COLLAPSE");
            mitigations.Add("Recalibrate transmon pi-pulse swap timing and correct phase drift during phononic state quantum state tomography.");
        }

        bool isSynthesisReady = isQualityOptimal && isThermalSuppressed && isWignerNonClassical && telemetry.SinglePhononGenerationFidelityPercent >= 92.0;

        return new QuantumAcousticsEvaluation
        {
            ResonatorUnitId = telemetry.ResonatorUnitId,
            CalculatedThermalPhononOccupancy = Math.Round(nTh, 5),
            AcousticWignerNegativityMargin = Math.Round(wignerMargin, 3),
            TransmonPhononCooperativityC = Math.Round(cooperativity, 1),
            IsMechanicalQualityOptimal = isQualityOptimal,
            IsThermalPhononBathSuppressed = isThermalSuppressed,
            IsNonClassicalWignerConfirmed = isWignerNonClassical,
            IsPhononFockStateSynthesisReady = isSynthesisReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
