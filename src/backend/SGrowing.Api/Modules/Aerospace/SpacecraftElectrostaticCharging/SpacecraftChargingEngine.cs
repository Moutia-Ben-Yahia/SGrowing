namespace SGrowing.Api.Modules.Aerospace.SpacecraftElectrostaticCharging;

public interface ISpacecraftChargingEngine
{
    SpacecraftChargingEvaluation AnalyzeCharging(SpacecraftChargingTelemetry telemetry);
}

public class SpacecraftChargingEngine : ISpacecraftChargingEngine
{
    private const double BreakdownThresholdVolts = 500.0;

    public SpacecraftChargingEvaluation AnalyzeCharging(SpacecraftChargingTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // ESD breakdown margin: 500V - V_diff
        double esdMargin = Math.Max(0.0, BreakdownThresholdVolts - telemetry.DifferentialDielectricPotentialVolts);

        // Frame potential margin: how far above -1000V
        double frameMargin = telemetry.SpacecraftFramePotentialVolts - (-1000.0);

        bool isDifferentialSafe = telemetry.DifferentialDielectricPotentialVolts <= 450.0 && telemetry.ArcDischargeTransientRatePerHour <= 0.05;
        bool isDeepSafe = telemetry.DeepDielectricCurrentDensityPicoAmpsPerCm2 <= 2.0;
        bool isContactorOk = telemetry.IsHollowCathodeNeutralizerActive && telemetry.PlasmaContactorEmissionCurrentMilliAmps >= 12.0;

        if (telemetry.DifferentialDielectricPotentialVolts > 550.0 || telemetry.ArcDischargeTransientRatePerHour > 0.1)
        {
            regimes.Add("DIFFERENTIAL_ESD_ARC_BREAKDOWN_SURGE");
            mitigations.Add("Apply conductive ITO/indium-tin-oxide coating grounded to spacecraft chassis and purge localized charge with neutralizer plasma plume.");
        }

        if (!isDeepSafe)
        {
            regimes.Add("SUBSTORM_GEOSYNCHRONOUS_DEEP_CHARGING");
            mitigations.Add("Increase internal harness shielding thickness with tantalum foil tape to limit bulk dielectric electron deposition rates.");
        }

        if (!isContactorOk || telemetry.SpacecraftFramePotentialVolts < -500.0)
        {
            regimes.Add("PLASMA_CONVENTIONAL_NEUTRALIZER_DEPLETION");
            mitigations.Add("Increase xenon flow rate to hollow cathode neutralizer and ignite secondary cathode heater to clamp chassis potential.");
        }

        bool isElectrostaticSafe = isDifferentialSafe && isDeepSafe && isContactorOk &&
                                   telemetry.SpacecraftFramePotentialVolts >= -250.0;

        return new SpacecraftChargingEvaluation
        {
            SpacecraftSubsystemId = telemetry.SpacecraftSubsystemId,
            EsdBreakdownVoltageMarginVolts = Math.Round(esdMargin, 1),
            FramePotentialSafetyMarginVolts = Math.Round(frameMargin, 1),
            IsDifferentialPotentialSafe = isDifferentialSafe,
            IsDeepDielectricChargingSuppressed = isDeepSafe,
            IsPlasmaContactorOperational = isContactorOk,
            IsSpacecraftElectrostaticSafe = isElectrostaticSafe,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
