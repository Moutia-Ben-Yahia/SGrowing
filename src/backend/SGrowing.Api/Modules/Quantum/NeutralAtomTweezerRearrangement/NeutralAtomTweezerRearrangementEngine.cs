using System;

namespace SGrowing.Api.Modules.Quantum.NeutralAtomTweezerRearrangement;

public interface INeutralAtomTweezerRearrangementEngine
{
    NeutralAtomTweezerAnalysisResult AnalyzeRearrangement(NeutralAtomTweezerTelemetry telemetry);
}

public sealed class NeutralAtomTweezerRearrangementEngine : INeutralAtomTweezerRearrangementEngine
{
    private const double CriticalClearanceDistanceUm = 3.0; // minimum distance to avoid blockade crosstalk/collisions
    private const double MinAdiabaticityThreshold = 10.0; // gamma_adiab >= 10 for motional ground-state preservation
    private const double TargetMinSurvivalProbability = 0.95;

    public NeutralAtomTweezerAnalysisResult AnalyzeRearrangement(NeutralAtomTweezerTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.TargetSitesCount <= 0)
        {
            throw new ArgumentException("Target sites count must be positive.", nameof(telemetry));
        }

        // 1. Initial Loading Fraction
        double initialLoadingFraction = (double)telemetry.InitialLoadedAtomsCount / telemetry.TargetSitesCount;

        // 2. Adiabaticity Parameter: gamma = (omega_trap * w0) / v_max
        // trap frequency in rad/s = 2 * pi * f_kHz * 1e3
        double omegaTrapRadPerSec = 2.0 * Math.PI * telemetry.TrapFrequencyKhz * 1000.0;
        double waistMeters = telemetry.TrapWaistUm * 1e-6;
        double maxVelocityMPerSec = telemetry.MaxShuttleVelocityMmPerSec * 1e-3;

        double adiabaticityGamma = maxVelocityMPerSec > 0
            ? (omegaTrapRadPerSec * waistMeters) / maxVelocityMPerSec
            : 100.0;

        // 3. Motional Heating Rate: n_dot_heat approx a_max^2 / (2 * omega_trap^2) or empirical acoustic scaling
        // Normalized heating rate in vibrational quanta per second:
        double massKg = telemetry.AtomicSpecies.Contains("Yb", StringComparison.OrdinalIgnoreCase)
            ? 171.0 * 1.66054e-27
            : 87.0 * 1.66054e-27; // default Rb-87
        double hbar = 1.054571817e-34;

        double motionalHeatingRateNbarPerSec = (massKg * Math.Pow(telemetry.MaxShuttleAccelerationMPerSec2, 2.0))
            / (2.0 * hbar * Math.Pow(omegaTrapRadPerSec, 2.0) * 1e-4 + 1e-12) * 1e-18; // scaled practical model

        // Cap to reasonable physical range (0.01 - 50 quanta/s)
        motionalHeatingRateNbarPerSec = Math.Clamp(motionalHeatingRateNbarPerSec, 0.05, 100.0);

        // 4. Survival Probabilities:
        // Vacuum survival: P_vac = exp(-t_sort / tau_vac)
        double sortDurationSec = telemetry.TotalSortingDurationMs * 1e-3;
        double vacuumLifetime = Math.Max(1.0, telemetry.VacuumLifetimeSec);
        double pVacuum = Math.Exp(-sortDurationSec / vacuumLifetime);

        // Heating survival: P_heat = exp(-0.01 * n_dot * t_sort)
        double pHeating = Math.Exp(-0.015 * motionalHeatingRateNbarPerSec * sortDurationSec);
        double totalSurvival = Math.Clamp(pVacuum * pHeating, 0.0, 1.0);

        // 5. Defect Vacancy Rate
        double vacancyRatePercent = ((double)telemetry.UnfilledDefectVacanciesCount / telemetry.TargetSitesCount) * 100.0;

        // 6. Validation Checks
        bool isClearanceOk = telemetry.MinInterAtomClearanceUm >= CriticalClearanceDistanceUm;
        bool isAdiabaticOk = adiabaticityGamma >= MinAdiabaticityThreshold;
        bool isDefectFree = telemetry.UnfilledDefectVacanciesCount == 0 && totalSurvival >= TargetMinSurvivalProbability;

        // 7. Regime & Recommendations
        string regime;
        string recommendation;

        if (!isClearanceOk)
        {
            regime = "INTERTWEEZER_CROSSTALK_COLLISION_HAZARD";
            recommendation = $"Minimum clearance distance ({telemetry.MinInterAtomClearanceUm:F2} um) violated critical clearance threshold ({CriticalClearanceDistanceUm:F2} um). Re-route AOD path planning to avoid static trap proximity.";
        }
        else if (!isAdiabaticOk)
        {
            regime = "NON_ADIABATIC_MOTIONAL_EXCITATION_COLLAPSE";
            recommendation = $"Adiabaticity parameter ({adiabaticityGamma:F1}) dropped below minimum safety threshold ({MinAdiabaticityThreshold:F1}). Reduce maximum shuttle velocity or increase optical trap depth.";
        }
        else if (vacancyRatePercent > 0.0)
        {
            regime = "DEFECT_VACANCY_RESIDUAL_SUB_UNITY_ASSEMBLY";
            recommendation = $"Target register contains {telemetry.UnfilledDefectVacanciesCount} unfilled vacancies ({vacancyRatePercent:F1}% defect rate). Execute secondary replenishment pass with surplus reservoir atoms.";
        }
        else if (totalSurvival < TargetMinSurvivalProbability)
        {
            regime = "VACUUM_AND_HEATING_ATTRITION_ELEVATED";
            recommendation = $"Single-atom survival probability ({totalSurvival * 100.0:F1}%) fell below target threshold ({TargetMinSurvivalProbability * 100.0:F1}%). Compress sorting path duration or improve ultra-high vacuum chamber pressure.";
        }
        else
        {
            regime = "DETERMINISTIC_DEFECT_FREE_REGISTER_READY";
            recommendation = "Register rearrangement fully optimal: high adiabaticity preserved, collisionless clearance maintained, and 100% defect-free array initialized for quantum logic gates.";
        }

        return new NeutralAtomTweezerAnalysisResult
        {
            RegisterId = telemetry.RegisterId,
            InitialLoadingFraction = Math.Round(initialLoadingFraction, 3),
            AdiabaticityParameterGamma = Math.Round(adiabaticityGamma, 2),
            MotionalHeatingRateNbarPerSec = Math.Round(motionalHeatingRateNbarPerSec, 2),
            BackgroundVacuumSurvivalProbability = Math.Round(pVacuum, 4),
            TotalWavepacketSurvivalProbability = Math.Round(totalSurvival, 4),
            MinimumClearanceSeparationUm = Math.Round(telemetry.MinInterAtomClearanceUm, 2),
            VacancyDefectRatePercent = Math.Round(vacancyRatePercent, 2),
            IsCollisionlessSeparationMaintained = isClearanceOk,
            IsAdiabaticTransportPreserved = isAdiabaticOk,
            IsDefectFreeRegisterReady = isDefectFree,
            RearrangementRegime = regime,
            MitigationRecommendation = recommendation
        };
    }
}
