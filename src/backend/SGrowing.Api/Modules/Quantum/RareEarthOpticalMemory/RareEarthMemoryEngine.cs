namespace SGrowing.Api.Modules.Quantum.RareEarthOpticalMemory;

public interface IRareEarthMemoryEngine
{
    RareEarthMemoryEvaluation AnalyzeQuantumMemory(RareEarthMemoryTelemetry telemetry);
}

public class RareEarthMemoryEngine : IRareEarthMemoryEngine
{
    public RareEarthMemoryEvaluation AnalyzeQuantumMemory(RareEarthMemoryTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Storage time tau = 1 / Delta
        double storageDelayUs = telemetry.CombToothSpacingMhz > 0 
            ? (1.0 / telemetry.CombToothSpacingMhz) 
            : 0.0;

        // Theoretical AFC efficiency eta_AFC = (d/F)^2 * exp(-d/F) * exp(-7 / F^2) * exp(-d0)
        double d = telemetry.OpticalDepth;
        double F = Math.Max(0.5, telemetry.CombFinesse);
        double d0 = telemetry.UnburnedBackgroundAbsorptionDepth;

        double dephasingFactor = Math.Exp(-7.0 / (F * F));
        double shapeFactor = Math.Pow(d / F, 2.0) * Math.Exp(-d / F);
        double bgLoss = Math.Exp(-d0);
        double afcEffRaw = shapeFactor * dephasingFactor * bgLoss;

        // Dephasing jitter penalty
        double jitterPenalty = Math.Max(0.0, 1.0 - (telemetry.ToothJitterPercent / 100.0) * 1.5);
        double calculatedEfficiencyPercent = Math.Clamp(afcEffRaw * jitterPenalty * 100.0, 0.0, 100.0);

        // Spin wave retention
        double spinT2 = telemetry.DopedIonType.Contains("Eu", StringComparison.OrdinalIgnoreCase) ? 3600.0 : 60.0;
        double spinWaveRetention = Math.Clamp(Math.Exp(-telemetry.SpinWaveStorageTimeSeconds / spinT2) * 100.0, 0.0, 100.0);

        // Output photon fidelity
        double baseFidelity = 98.8;
        if (telemetry.InputSinglePhotonSnr < 20.0)
        {
            baseFidelity -= (20.0 - telemetry.InputSinglePhotonSnr) * 1.2;
        }
        if (telemetry.CryogenicTemperatureKelvin > 3.5)
        {
            baseFidelity -= (telemetry.CryogenicTemperatureKelvin - 3.5) * 8.0;
        }
        double photonFidelity = Math.Clamp(baseFidelity - (100.0 - calculatedEfficiencyPercent) * 0.05, 0.0, 100.0);

        bool isFinesseOptimal = telemetry.CombFinesse >= 3.5 && telemetry.ToothJitterPercent <= 6.0;
        bool isBackgroundLossSuppressed = telemetry.UnburnedBackgroundAbsorptionDepth <= 0.20;
        bool isCryogenicSafe = telemetry.CryogenicTemperatureKelvin <= 3.5;

        if (!isFinesseOptimal)
        {
            regimes.Add("COMB_FINESSE_DEFICIT_DEPHASING");
            mitigations.Add("Refine spectral hole burning laser pulse sequence and tighten lock frequency jitter to boost AFC finesse >= 3.5.");
        }

        if (!isBackgroundLossSuppressed)
        {
            regimes.Add("UNBURNED_BACKGROUND_ABSORPTION_LOSS");
            mitigations.Add("Increase optical pumping clean-up burn sweep duration to deplete unburned hyperfine ground state ions (d0 <= 0.15).");
        }

        if (!isCryogenicSafe)
        {
            regimes.Add("CRYOGENIC_TEMPERATURE_THERMAL_PHONON_DEPHASING");
            mitigations.Add("Rebalance dilution refrigerator or pulse tube cryostat cold plate thermal link to maintain sub-3.2K optical coherence.");
        }

        bool isRepeaterReady = isFinesseOptimal && isBackgroundLossSuppressed && isCryogenicSafe && calculatedEfficiencyPercent >= 10.0 && photonFidelity >= 90.0;

        return new RareEarthMemoryEvaluation
        {
            CrystalId = telemetry.CrystalId,
            CalculatedAfcEfficiencyPercent = Math.Round(calculatedEfficiencyPercent, 2),
            StorageDelayTimeMicroseconds = Math.Round(storageDelayUs, 3),
            OutputPhotonFidelityPercent = Math.Round(photonFidelity, 2),
            SpinWaveRetentionPercent = Math.Round(spinWaveRetention, 2),
            IsFinesseOptimal = isFinesseOptimal,
            IsBackgroundLossSuppressed = isBackgroundLossSuppressed,
            IsCryogenicTemperatureSafe = isCryogenicSafe,
            IsQuantumRepeaterReady = isRepeaterReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
