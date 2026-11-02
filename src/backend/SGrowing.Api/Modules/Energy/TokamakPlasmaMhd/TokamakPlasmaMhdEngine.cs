namespace SGrowing.Api.Modules.Energy.TokamakPlasmaMhd;

public interface ITokamakPlasmaMhdEngine
{
    MhdDisruptionAssessment EvaluateDisruptionRisk(TokamakDisruptionEvaluationRequest request);
}

public class TokamakPlasmaMhdEngine : ITokamakPlasmaMhdEngine
{
    public MhdDisruptionAssessment EvaluateDisruptionRisk(TokamakDisruptionEvaluationRequest request)
    {
        var shot = request.ShotState;
        var r = Math.Max(0.5, shot.MajorRadiusMeters);
        var a = Math.Max(0.1, shot.MinorRadiusMeters);
        var ip = Math.Max(0.1, shot.PlasmaCurrentMegaAmps);
        var bt = Math.Max(0.5, shot.ToroidalFieldTesla);
        var kappa = Math.Max(1.0, shot.PlasmaElongationKappa);

        // 1. Edge safety factor q95 approximation
        double elongationFactor = (1.0 + kappa * kappa) / 2.0;
        double q95 = (5.0 * a * a * bt / (r * ip)) * elongationFactor;

        // 2. Greenwald density limit: n_G = Ip / (pi * a^2) in 10^20 m^-3 (or 10^19 m^-3 with x10 multiplier)
        double greenwaldLimit10Power19 = (ip / (Math.PI * a * a)) * 10.0;
        double greenwaldFraction = shot.ElectronDensity10Power19PerM3 / Math.Max(0.1, greenwaldLimit10Power19);

        // 3. Troyon Normalized Beta: beta_N = beta_T(%) / (Ip / (a * Bt))
        double betaN = shot.PoloidalBeta * 1.8;

        // 4. Instability indicators
        bool lowQBreached = q95 <= 2.05;
        bool densityLimitExceeded = greenwaldFraction >= 0.95;
        bool lockedMode = shot.LockedModeAmplitudeGauss >= 10.0;
        bool radiativeCollapse = shot.RadiatedPowerFraction >= 0.88;

        // 5. Disruption probability score
        double riskScore = 0.05; // baseline nominal shot risk

        if (lowQBreached) riskScore += 0.55;
        if (densityLimitExceeded) riskScore += 0.35;
        if (lockedMode) riskScore += 0.40;
        if (radiativeCollapse) riskScore += 0.25;
        if (betaN > 2.8) riskScore += 0.15;

        double disruptionProbPercent = Math.Clamp(riskScore * 100.0, 0.0, 99.9);
        bool emergencyMitigation = disruptionProbPercent >= 75.0 || lowQBreached || (lockedMode && greenwaldFraction > 0.8);

        string mitigationTechnique;
        string directive;

        if (disruptionProbPercent >= 75.0 || lowQBreached)
        {
            mitigationTechnique = "SHATTERED_PELLET_INJECTION_QUENCH";
            directive = "EMERGENCY_SPI_FIRE: Trigger cryogenic Neon-Deuterium shattered pellet injection within 15ms. Rapid thermal quench to avert runaway electron avalanche.";
        }
        else if (disruptionProbPercent >= 40.0 || lockedMode)
        {
            mitigationTechnique = "ECCD_ISLAND_STABILIZATION";
            directive = "ACTIVE_ECCD_DEPOSIT: Steer 170 GHz gyrotron microwaves to q=2 resonant surface. Suppress magnetic tearing island growth.";
        }
        else
        {
            mitigationTechnique = "NONE";
            directive = "PLASMA_STABLE: Active feedback magnetic control nominal. Tokamak discharge within operational boundary.";
        }

        var instabilityProfile = new MhdInstabilityProfile(
            EdgeSafetyFactorQ95: Math.Round(q95, 2),
            GreenwaldDensityFraction: Math.Round(greenwaldFraction, 2),
            TroyonNormalizedBeta: Math.Round(betaN, 2),
            TearingModeLocked: lockedMode,
            DensityLimitExceeded: densityLimitExceeded,
            LowQBoundaryBreached: lowQBreached
        );

        return new MhdDisruptionAssessment(
            ReactorId: shot.ReactorId,
            ShotNumber: shot.ShotNumber,
            DisruptionProbabilityPercent: Math.Round(disruptionProbPercent, 1),
            EmergencyMitigationRequired: emergencyMitigation,
            MitigationTechnique: mitigationTechnique,
            OperationalDirective: directive,
            InstabilityProfile: instabilityProfile
        );
    }
}
