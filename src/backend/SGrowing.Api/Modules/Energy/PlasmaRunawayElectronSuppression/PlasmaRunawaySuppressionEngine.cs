namespace SGrowing.Api.Modules.Energy.PlasmaRunawayElectronSuppression;

public class PlasmaRunawaySuppressionEngine
{
    public PlasmaRunawayAnalysisResult AnalyzeDisruption(PlasmaRunawayTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.PlasmaCurrentMegaAmps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Plasma current must be positive.");
        }

        if (telemetry.ElectronDensity1e19PerM3 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Electron density must be positive.");
        }

        if (telemetry.CoulombLogarithm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Coulomb logarithm must be positive.");
        }

        // Relativistic critical field: E_crit ~ 0.0077 * n_e_19 * (ln_Lambda / 15.0) [V/m]
        double eCrit = 0.0077 * telemetry.ElectronDensity1e19PerM3 * (telemetry.CoulombLogarithm / 15.0);
        eCrit = Math.Max(eCrit, 1e-4);

        // Electric field overdrive ratio
        double overdriveRatio = telemetry.InducedToroidalElectricFieldVPerM / eCrit;

        // Rosenbluth density required for complete collisional runaway suppression:
        // n_Rosenbluth = n_e * (E_|| / E_crit)
        double rosenbluthDensity = telemetry.ElectronDensity1e19PerM3 * Math.Max(overdriveRatio, 1.0);

        // Avalanche growth rate: gamma_RE ~ 0.12 * (E_|| - E_crit) / (ln_Lambda / 15.0) [ms^-1]
        double effectiveOverfield = Math.Max(0.0, telemetry.InducedToroidalElectricFieldVPerM - eCrit);
        double growthRatePerMs = 0.12 * effectiveOverfield / (telemetry.CoulombLogarithm / 15.0);

        bool isAvalanche = overdriveRatio >= 10.0 || telemetry.CurrentQuenchRateMaPerSec >= 350.0 || telemetry.HardXRayPhotonCountsPerMs >= 4000.0;

        string state;
        string command;
        string advisory;

        if (telemetry.CurrentQuenchRateMaPerSec >= 600.0 || telemetry.HardXRayPhotonCountsPerMs >= 8000.0 || overdriveRatio >= 100.0)
        {
            state = "EMERGENCY_SPI_KILLER_PELLET_FIRE";
            command = "FIRE_SHATTERED_PELLET_INJECTION_IMMEDIATELY";
            advisory = $"CATASTROPHIC RUNAWAY AVALANCHE: Quench rate {telemetry.CurrentQuenchRateMaPerSec:F0} MA/s, HXR {telemetry.HardXRayPhotonCountsPerMs:F0} cps, growth rate {growthRatePerMs:F2} ms⁻¹. Firing {telemetry.ShatteredPelletGasSpecies} shattered killer pellets in <2.0 ms to flood core to Rosenbluth density ({rosenbluthDensity:F1}x10¹⁹ m⁻³).";
        }
        else if (isAvalanche)
        {
            state = "RUNAWAY_PRECURSOR_WARNING";
            command = "ARM_MASSIVE_GAS_INJECTORS";
            advisory = $"RUNAWAY PRECURSOR DETECTED: Toroidal electric field {telemetry.InducedToroidalElectricFieldVPerM:F1} V/m is {overdriveRatio:F1}x critical field. Massive gas injectors armed for rapid pre-emptive impurity mitigation.";
        }
        else
        {
            state = "STABLE_THERMAL_CONFINEMENT";
            command = "STANDBY";
            advisory = $"Stable plasma discharge at {telemetry.PlasmaCurrentMegaAmps:F1} MA. Electric field overdrive ratio {overdriveRatio:F2} below runaway threshold with negligible HXR emission.";
        }

        return new PlasmaRunawayAnalysisResult(
            telemetry.TokamakId,
            Math.Round(eCrit, 4),
            Math.Round(overdriveRatio, 2),
            Math.Round(growthRatePerMs, 3),
            Math.Round(rosenbluthDensity, 1),
            isAvalanche,
            state,
            command,
            advisory);
    }
}
