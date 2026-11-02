using System;

namespace SGrowing.Api.Modules.Quantum.CavityOptomechanicsPhononSqueezing;

public sealed class CavityOptomechanicsPhononSqueezingEngine : ICavityOptomechanicsPhononSqueezingEngine
{
    private const double BoltzmannConstant = 1.380649e-23; // J/K
    private const double ReducedPlanckConstant = 1.0545718e-34; // J s

    public CavityOptomechanicsAnalysisResult AnalyzePhononSqueezing(CavityOptomechanicsTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.ResonatorId))
        {
            throw new ArgumentException("Resonator ID must be provided.", nameof(telemetry));
        }

        if (telemetry.MechanicalFrequencyMegahertz <= 0 || telemetry.MechanicalQualityFactor <= 0 ||
            telemetry.CavityLinewidthKilohertz <= 0 || telemetry.EnvironmentalBathTemperatureKelvin <= 0)
        {
            throw new ArgumentException("Physical frequencies, quality factors, and bath temperature must be strictly positive.", nameof(telemetry));
        }

        var omegaM = 2.0 * Math.PI * telemetry.MechanicalFrequencyMegahertz * 1e6; // rad/s
        var kappa = 2.0 * Math.PI * telemetry.CavityLinewidthKilohertz * 1e3; // rad/s
        var gammaM = omegaM / telemetry.MechanicalQualityFactor; // rad/s

        // Initial thermal phonon occupancy: n_th = (k_B * T) / (hbar * omega_m)
        var nTh = (BoltzmannConstant * telemetry.EnvironmentalBathTemperatureKelvin) / (ReducedPlanckConstant * omegaM);

        // Multiphoton coupling: G = g_0 * sqrt(n_c) (g_0 converted to rad/s)
        var g0Rad = 2.0 * Math.PI * telemetry.OptomechanicalCouplingRateG0Hertz;
        var nc = Math.Max(0.0, telemetry.IntracavityPhotonCount);
        var bigG = g0Rad * Math.Sqrt(nc);

        // Optomechanical cooling rate in resolved sideband regime (Delta = -omega_m)
        // Gamma_opt = 4 * G^2 / kappa
        var isRedDetuned = telemetry.LaserDetuningMegahertz < 0;
        var gammaOpt = 0.0;
        if (isRedDetuned && kappa > 0)
        {
            gammaOpt = (4.0 * bigG * bigG) / kappa;
        }

        // Optomechanical cooperativity: C = 4 G^2 / (kappa * gamma_m)
        var cooperativity = gammaM > 0 ? gammaOpt / gammaM : 0.0;

        // Effective cooled phonon occupancy:
        // n_eff = (gamma_m / (gamma_m + gamma_opt)) * n_th + (kappa / (4 * omega_m))^2 (quantum backaction)
        var backactionLimit = Math.Pow(kappa / (4.0 * omegaM), 2.0);
        var effectivePhononOccupancy = nTh;
        if (gammaM + gammaOpt > 0)
        {
            effectivePhononOccupancy = (gammaM / (gammaM + gammaOpt)) * nTh + backactionLimit;
        }

        // Mechanical parametric squeezing parameter r:
        // Squeezing drive modulation depth eta_sq in %
        var eta = Math.Clamp(telemetry.SqueezingDriveModulationDepthPercent / 100.0, 0.0, 0.95);
        var r = eta * 1.5; // parametric gain squeeze factor

        // Quadrature fluctuation variance: <Delta X_1^2> = (n_eff + 0.5) * exp(-2 * r)
        var quadVariance = (effectivePhononOccupancy + 0.5) * Math.Exp(-2.0 * r);

        // Squeezing below Standard Quantum Limit (SQL = 0.5):
        // Squeezing (dB) = -10 * log10(variance / 0.5) = -10 * log10(2 * variance)
        var squeezingDb = -10.0 * Math.Log10(Math.Max(1e-9, 2.0 * quadVariance));

        string regime;
        string guidance;
        bool isSubSqlSqueezingAchieved = squeezingDb > 0.0 && effectivePhononOccupancy < 1.0;

        if (telemetry.LaserDetuningMegahertz > 0)
        {
            regime = "UNSTABLE_BLUE_DETUNED_AMPLIFICATION_WARNING";
            guidance = "Laser detuning is blue-shifted (+Delta); optomechanical anti-damping induces phonon amplification and parametric instability. Shift laser detuning to resolved red sideband (Delta = -Omega_m).";
            isSubSqlSqueezingAchieved = false;
        }
        else if (effectivePhononOccupancy < 0.1 && squeezingDb >= 3.0)
        {
            regime = "DEEP_GROUND_STATE_SUB_SQL_SQUEEZED_REGIME";
            guidance = $"Deep quantum ground state established (n_eff = {effectivePhononOccupancy:F3} < 0.1) with robust sub-SQL mechanical squeezing ({squeezingDb:F2} dB). Ready for quantum metrology beyond SQL.";
        }
        else if (effectivePhononOccupancy < 1.0 && squeezingDb > 0.0)
        {
            regime = "QUANTUM_GROUND_STATE_RESOLVED_SIDEBAND_REGIME";
            guidance = $"Quantum ground state cooling achieved (n_eff = {effectivePhononOccupancy:F3} < 1.0) with moderate quadrature squeezing ({squeezingDb:F2} dB). Increase parametric drive modulation for higher dB squeezing.";
        }
        else if (cooperativity < 1.0)
        {
            regime = "THERMAL_NOISE_DOMINATED_INSUFFICIENT_COOLING";
            guidance = $"Optomechanical cooperativity (C = {cooperativity:F2} < 1.0) is insufficient to overcome thermal bath damping. Increase intracavity pump power or improve cavity finesse.";
        }
        else
        {
            regime = "NOMINAL_OPTOMECHANICAL_COOLING_REGIME";
            guidance = $"Optomechanical dynamical backaction cooling active. Effective phonon occupancy is {effectivePhononOccupancy:F2}. Further cryogenic or optical pumping optimization required for sub-SQL regime.";
        }

        return new CavityOptomechanicsAnalysisResult(
            telemetry.ResonatorId,
            Math.Round(nTh, 2),
            Math.Round(effectivePhononOccupancy, 4),
            Math.Round(cooperativity, 2),
            Math.Round(quadVariance, 4),
            Math.Round(squeezingDb, 2),
            regime,
            guidance,
            isSubSqlSqueezingAchieved
        );
    }
}
