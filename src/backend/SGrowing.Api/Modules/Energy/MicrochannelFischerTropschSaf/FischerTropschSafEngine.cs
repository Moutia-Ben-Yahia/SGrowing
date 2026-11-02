namespace SGrowing.Api.Modules.Energy.MicrochannelFischerTropschSaf;

public interface IFischerTropschSafEngine
{
    FischerTropschSafEvaluation AnalyzeReactor(FischerTropschSafTelemetry telemetry);
}

public class FischerTropschSafEngine : IFischerTropschSafEngine
{
    public FischerTropschSafEvaluation AnalyzeReactor(FischerTropschSafTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Jet fuel fraction (C9 - C16 cut) estimation from ASF distribution:
        // W_n = n * (1 - alpha)^2 * alpha^(n - 1)
        // Summing for n in 9 to 16
        double a = Math.Clamp(telemetry.AsfChainGrowthProbabilityAlpha, 0.5, 0.99);
        double jetYield = 0.0;
        for (int n = 9; n <= 16; n++)
        {
            jetYield += n * Math.Pow(1.0 - a, 2) * Math.Pow(a, n - 1);
        }
        jetYield *= 100.0; // as percent

        bool isThermalStable = telemetry.BedHotspotTemperatureExcessCelsius <= 10.0;
        bool isChainGrowthOptimal = telemetry.AsfChainGrowthProbabilityAlpha >= 0.86 && telemetry.AsfChainGrowthProbabilityAlpha <= 0.94;
        bool isCatalystProtected = telemetry.SteamToHydrogenPartialPressureRatio <= 1.35;

        if (telemetry.BedHotspotTemperatureExcessCelsius > 12.0 || telemetry.MethaneSelectivityPercent > 12.0)
        {
            regimes.Add("MICROCHANNEL_THERMAL_RUNAWAY_HOTSPOT");
            mitigations.Add("Increase microchannel coolant oil circulation velocity and drop reactor inlet temperature by 4 deg C to quell methanation runaway.");
        }

        if (!isCatalystProtected)
        {
            regimes.Add("COBALT_CATALYST_OXIDATION_DEACTIVATION");
            mitigations.Add("Reduce single-pass CO conversion or enrich inlet syngas H2 fraction to maintain water-to-hydrogen ratio below 1.2.");
        }

        if (!isChainGrowthOptimal)
        {
            regimes.Add("ASF_CHAIN_GROWTH_SELECTIVITY_DEVIATION");
            mitigations.Add("Adjust operating pressure or promoter doping levels (Re/Ru) to align chain growth parameter alpha into 0.88 - 0.93 SAF band.");
        }

        bool isSafCertified = isThermalStable && isChainGrowthOptimal && isCatalystProtected &&
                              telemetry.CarbonMonoxideConversionPercent >= 70.0 &&
                              telemetry.MethaneSelectivityPercent <= 8.5;

        return new FischerTropschSafEvaluation
        {
            ReactorUnitId = telemetry.ReactorUnitId,
            JetFuelKeroseneYieldPercent = Math.Round(jetYield, 1),
            IsIsothermalHeatRemovalStable = isThermalStable,
            IsChainGrowthOptimalForSaf = isChainGrowthOptimal,
            IsCatalystOxidationPrevented = isCatalystProtected,
            IsFischerTropschSafCertified = isSafCertified,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
