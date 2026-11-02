namespace SGrowing.Api.Modules.Energy.SmrNeutronReactor;

public interface ISmrNeutronReactorEngine
{
    ReactivityAdjustmentDecision EvaluateTransient(XenonTransientEvaluationRequest request);
}

public class SmrNeutronReactorEngine : ISmrNeutronReactorEngine
{
    private const double BoronWorthPcmPerPpm = -8.5; // Each ppm adds -8.5 pcm of negative reactivity
    private const double MaxRodWorthPcm = 4500.0;     // Full bank insertion provides -4,500 pcm
    private const double IodineLambdaPerHour = 0.1055; // ln(2) / 6.57 hr
    private const double XenonLambdaPerHour = 0.0756;  // ln(2) / 9.17 hr

    public ReactivityAdjustmentDecision EvaluateTransient(XenonTransientEvaluationRequest request)
    {
        var core = request.CoreState;
        var pRated = Math.Max(1.0, core.RatedThermalPowerMw);
        var currentPowerFraction = Math.Clamp(core.CurrentThermalPowerMw / pRated, 0.0, 1.2);
        var targetPowerFraction = Math.Clamp(request.TargetThermalPowerMw / pRated, 0.0, 1.2);

        var lookahead = Math.Max(1.0, Math.Min(48.0, request.LookaheadHours));
        var steps = new List<XenonTransientStep>();

        // Baseline equilibrium Xenon reactivity at full power is approx -2800 pcm
        var baseEquilibriumXenonPcm = -2800.0 * currentPowerFraction;
        var targetEquilibriumXenonPcm = -2800.0 * targetPowerFraction;

        double maxNegativeXenon = baseEquilibriumXenonPcm;
        double timeAtMaxNegative = 0.0;

        int numSteps = 12;
        double dt = lookahead / numSteps;

        for (int i = 0; i <= numSteps; i++)
        {
            double tHours = i * dt;
            double xenonReactivity;

            if (targetPowerFraction < currentPowerFraction)
            {
                // Xenon pit transient: Iodine-135 decay creates sharp peak negative reactivity at 6-10 hours
                double transientPeak = 1500.0 * (currentPowerFraction - targetPowerFraction) 
                    * 4.0 * Math.Exp(-0.10 * tHours) * (1.0 - Math.Exp(-0.15 * tHours));

                // Relaxation toward target equilibrium occurs after the xenon pit subsides (> 15 hours)
                double relaxationFactor = 1.0 / (1.0 + Math.Exp(-(tHours - 16.0) / 3.0));
                xenonReactivity = baseEquilibriumXenonPcm - transientPeak + (targetEquilibriumXenonPcm - baseEquilibriumXenonPcm) * relaxationFactor;
            }
            else
            {
                // Power up transient: Flux burnup destroys Xenon faster than new Iodine produces it initially
                double burnoutDip = (targetPowerFraction - currentPowerFraction) * 600.0 * Math.Exp(-XenonLambdaPerHour * tHours);
                xenonReactivity = baseEquilibriumXenonPcm + (targetEquilibriumXenonPcm - baseEquilibriumXenonPcm) * (1.0 - Math.Exp(-XenonLambdaPerHour * tHours)) + burnoutDip;
            }

            // Available core reserve
            double currentRodReactivity = -(core.ControlRodBankInsertionPercent / 100.0) * MaxRodWorthPcm;
            double currentBoronReactivity = core.CurrentBoronConcentrationPpm * BoronWorthPcmPerPpm;
            double netReactivity = xenonReactivity + currentRodReactivity + currentBoronReactivity + 3500.0; // Excess fuel reactivity

            bool canMaintainCritical = netReactivity >= 0 || core.ShutdownMarginPcm > Math.Abs(netReactivity);

            steps.Add(new XenonTransientStep(
                HoursPostTransition: Math.Round(tHours, 1),
                EstimatedXenonReactivityPcm: Math.Round(xenonReactivity, 1),
                EstimatedNetReactivityPcm: Math.Round(netReactivity, 1),
                CriticalityMaintainable: canMaintainCritical
            ));

            if (xenonReactivity < maxNegativeXenon)
            {
                maxNegativeXenon = xenonReactivity;
                timeAtMaxNegative = tHours;
            }
        }

        // Available positive reserve from rod bank withdrawal and soluble boron dilution
        double rodWithdrawalReservePcm = (core.ControlRodBankInsertionPercent / 100.0) * MaxRodWorthPcm;
        double boronDilutionReservePcm = core.CurrentBoronConcentrationPpm * Math.Abs(BoronWorthPcmPerPpm);
        double totalPositiveReservePcm = rodWithdrawalReservePcm + boronDilutionReservePcm;

        double reactivityDeficitPcm = Math.Max(0.0, -maxNegativeXenon - 2800.0);
        bool deadlockRisk = reactivityDeficitPcm > totalPositiveReservePcm;

        double recommendedBoron = core.CurrentBoronConcentrationPpm;
        double recommendedRods = core.ControlRodBankInsertionPercent;
        string operatingAction;

        if (deadlockRisk)
        {
            operatingAction = "CRITICAL_XENON_DEADLOCK_PREVENT: Trip to hot standby or hold power level > 35% rated capacity.";
        }
        else if (reactivityDeficitPcm > 0)
        {
            // First withdraw rods up to 20% margin, then dilute soluble boron
            double rodReactivityAvailable = (core.ControlRodBankInsertionPercent / 100.0) * MaxRodWorthPcm;
            if (rodReactivityAvailable >= reactivityDeficitPcm)
            {
                double rodReductionPercent = (reactivityDeficitPcm / MaxRodWorthPcm) * 100.0;
                recommendedRods = Math.Max(5.0, core.ControlRodBankInsertionPercent - rodReductionPercent);
                operatingAction = $"COMPENSATE_TRANSIENT: Withdraw control bank to {recommendedRods:F1}%.";
            }
            else
            {
                recommendedRods = 5.0; // Maximum safe withdrawal
                double remainingDeficit = reactivityDeficitPcm - rodReactivityAvailable;
                double boronDilutionPpm = remainingDeficit / Math.Abs(BoronWorthPcmPerPpm);
                recommendedBoron = Math.Max(0.0, core.CurrentBoronConcentrationPpm - boronDilutionPpm);
                operatingAction = $"COMPENSATE_TRANSIENT: Withdraw control bank to {recommendedRods:F1}% and dilute soluble boron by {boronDilutionPpm:F1} ppm.";
            }
        }
        else
        {
            operatingAction = "STABLE_TRANSIENT: Maintain nominal control rod and boron trim.";
        }

        return new ReactivityAdjustmentDecision(
            ReactorId: core.ReactorId,
            RecommendedRodBankPositionPercent: Math.Round(recommendedRods, 1),
            RecommendedBoronConcentrationPpm: Math.Round(recommendedBoron, 1),
            PeakNegativeReactivityPcm: Math.Round(maxNegativeXenon, 1),
            TimeToPeakNegativeReactivityHours: Math.Round(timeAtMaxNegative, 1),
            XenonDeadlockRisk: deadlockRisk,
            OperatingAction: operatingAction,
            TransientTrajectory: steps
        );
    }
}
