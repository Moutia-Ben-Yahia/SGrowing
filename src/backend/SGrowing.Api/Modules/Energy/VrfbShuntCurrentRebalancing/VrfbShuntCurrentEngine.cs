namespace SGrowing.Api.Modules.Energy.VrfbShuntCurrentRebalancing;

public interface IVrfbShuntCurrentEngine
{
    VrfbShuntCurrentEvaluation AnalyzeStack(VrfbShuntCurrentTelemetry telemetry);
}

public class VrfbShuntCurrentEngine : IVrfbShuntCurrentEngine
{
    public VrfbShuntCurrentEvaluation AnalyzeStack(VrfbShuntCurrentTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Shunt current fraction = ShuntLoss / RatedPower * 100
        double rated = Math.Max(1.0, telemetry.RatedStackPowerKilowatts);
        double shuntLossPercent = (telemetry.MeasuredShuntCurrentLossKilowatts / rated) * 100.0;

        // SOC asymmetry = |SOC_pos - SOC_neg|
        double socDiff = Math.Abs(telemetry.PositiveTankStateOfChargePercent - telemetry.NegativeTankStateOfChargePercent);

        bool isShuntLossOk = shuntLossPercent <= 3.8;
        bool isSocSymmetric = socDiff <= 3.0;
        bool isTempSafe = telemetry.PositiveElectrolyteTemperatureCelsius >= 12.0 &&
                          telemetry.PositiveElectrolyteTemperatureCelsius <= 39.0 &&
                          telemetry.NegativeElectrolyteTemperatureCelsius >= 10.0 &&
                          telemetry.NegativeElectrolyteTemperatureCelsius <= 38.0;

        if (shuntLossPercent > 5.0)
        {
            regimes.Add("MANIFOLD_PARASITIC_SHUNT_CURRENT_SURGE");
            mitigations.Add("Lengthen and narrow stack electrolyte inlet channel manifold geometry to increase ionic resistance and throttle parasitic leakage paths.");
        }

        if (!isSocSymmetric)
        {
            regimes.Add("ELECTROLYTE_OXIDATION_STATE_ASYMMETRY");
            mitigations.Add("Run chemical rebalancing cell with organic reductant (e.g. oxalic acid) to reduce excess VO2+ back to VO2+ or perform hydraulic electrolyte remixing.");
        }

        if (!isTempSafe)
        {
            regimes.Add("VANADIUM_PRECIPITATION_TEMPERATURE_BREACH");
            mitigations.Add("Modulate heat exchanger chiller/heater loops to maintain positive electrolyte between 15C and 38C to prevent irreversible V2O5 thermal precipitation.");
        }

        bool isCertified = isShuntLossOk && isSocSymmetric && isTempSafe &&
                           telemetry.RoundTripEnergyEfficiencyPercent >= 81.0 &&
                           telemetry.CoulombicEfficiencyPercent >= 96.0;

        return new VrfbShuntCurrentEvaluation
        {
            VrfbStorageSystemId = telemetry.VrfbStorageSystemId,
            ShuntCurrentPowerLossFractionPercent = Math.Round(shuntLossPercent, 2),
            StateOfChargeAsymmetryPercent = Math.Round(socDiff, 2),
            IsShuntCurrentLossAcceptable = isShuntLossOk,
            IsStateOfChargeSymmetric = isSocSymmetric,
            IsElectrolyteTemperatureSafe = isTempSafe,
            IsVrfbStackHealthCertified = isCertified,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
