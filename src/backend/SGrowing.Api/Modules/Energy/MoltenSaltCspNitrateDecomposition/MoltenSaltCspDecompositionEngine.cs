using System;

namespace SGrowing.Api.Modules.Energy.MoltenSaltCspNitrateDecomposition;

public interface IMoltenSaltCspDecompositionEngine
{
    MoltenSaltCspAnalysisResult AnalyzeMoltenSaltStorage(MoltenSaltCspTelemetry telemetry);
}

public sealed class MoltenSaltCspDecompositionEngine : IMoltenSaltCspDecompositionEngine
{
    private const double MinColdTankFreezeMarginCelsius = 40.0; // T_cold - T_freeze >= 40 deg C
    private const double MaxAllowableHotTemperatureCelsius = 585.0; // Exceeding 585 C causes runaway secondary decomposition
    private const double MaxAllowableNitriteWeightPercent = 6.5; // NO2- <= 6.5 wt%
    private const double MaxAllowableCorrosionRateMicronsPerYear = 15.0; // <= 15 um/yr for 30-year life

    public MoltenSaltCspAnalysisResult AnalyzeMoltenSaltStorage(MoltenSaltCspTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.HotTankTemperatureCelsius <= 0.0 || telemetry.ColdTankTemperatureCelsius <= 0.0)
        {
            throw new ArgumentException("Hot and cold tank temperatures must be positive.", nameof(telemetry));
        }

        // 1. Cold Tank Freeze Safety Margin: T_cold - T_freeze
        double freezeMargin = telemetry.ColdTankTemperatureCelsius - telemetry.FreezingPointCelsius;
        freezeMargin = Math.Round(freezeMargin, 1);

        // 2. Thermal Nitrate Decomposition Equilibrium:
        // K_eq ~ exp(-Delta H / (R * T)) * P_O2^(-0.5)
        // High T shifts NO3- -> NO2- + 0.5 O2
        double hotTempKelvin = telemetry.HotTankTemperatureCelsius + 273.15;
        double rGas = 8.314462618;
        double deltaHDecomp = 100000.0; // ~ 100 kJ/mol
        double deltaSDecomp = 95.0; // ~ 95 J/(mol*K)

        double deltaG = deltaHDecomp - hotTempKelvin * deltaSDecomp;
        double kEquil = Math.Exp(-deltaG / (rGas * hotTempKelvin));
        double po2 = Math.Max(0.01, telemetry.UllageCoverGasOxygenPartialPressureAtm);
        double equilibriumRatio = kEquil / Math.Sqrt(po2);
        equilibriumRatio = Math.Round(Math.Clamp(equilibriumRatio, 0.001, 1.0), 4);

        // 3. Oxide Induced Basic Fluxing Factor & Projected Corrosion Rate:
        // Base rate ~ 5 um/yr at 560 C
        // Oxide build-up increases corrosion: (1 + 0.03 * [O2-]_ppm)
        double oxideFactor = 1.0 + 0.035 * Math.Max(0.0, telemetry.OxideIonConcentrationPpm);
        double tempArrheniusFactor = Math.Exp((hotTempKelvin - 833.15) / 35.0); // baseline 560 C = 833.15 K
        double projectedCorrosion = 6.5 * tempArrheniusFactor * oxideFactor;
        projectedCorrosion = Math.Round(Math.Clamp(projectedCorrosion, 1.0, 150.0), 2);

        // 4. Validation Checks:
        bool isFreezeSafe = freezeMargin >= MinColdTankFreezeMarginCelsius;
        bool isThermalControlled = telemetry.HotTankTemperatureCelsius <= MaxAllowableHotTemperatureCelsius
            && telemetry.NitriteIonConcentrationWeightPercent <= MaxAllowableNitriteWeightPercent;
        bool isCorrosionOk = projectedCorrosion <= MaxAllowableCorrosionRateMicronsPerYear;

        // 5. Regime & Protocol:
        string regime;
        string protocol;

        if (!isFreezeSafe)
        {
            regime = "COLD_TANK_SALT_FREEZE_PLUGGING_RISK";
            protocol = $"Cold tank freeze margin ({freezeMargin:F1} deg C) is below safety limit ({MinColdTankFreezeMarginCelsius:F1} deg C). Activate electrical heat tracing on drain valves and pumps to prevent catastrophic salt solidification.";
        }
        else if (telemetry.HotTankTemperatureCelsius > MaxAllowableHotTemperatureCelsius)
        {
            regime = "RUNAWAY_SECONDARY_DECOMPOSITION_NOX_VENTING";
            protocol = $"Hot tank temperature ({telemetry.HotTankTemperatureCelsius:F1} deg C) exceeded severe decomposition threshold ({MaxAllowableHotTemperatureCelsius:F1} deg C). Quench solar receiver flux and introduce air/O2 sparging to recombine nitrites back into nitrates.";
        }
        else if (!isCorrosionOk)
        {
            regime = "ACCELERATED_OXIDE_FLUXING_TANK_CORROSION";
            protocol = $"Projected corrosion rate ({projectedCorrosion:F1} um/yr) exceeds 30-year lifetime limit ({MaxAllowableCorrosionRateMicronsPerYear:F1} um/yr) driven by oxide ion accumulation ({telemetry.OxideIonConcentrationPpm:F1} ppm). Purify salt bath with magnesium salt chemical scavengers to precipitate out basic oxides.";
        }
        else if (telemetry.NitriteIonConcentrationWeightPercent > MaxAllowableNitriteWeightPercent)
        {
            regime = "ELEVATED_NITRITE_EQUILIBRIUM_ACCUMULATION";
            protocol = $"Nitrite level ({telemetry.NitriteIonConcentrationWeightPercent:F2} wt%) is elevated. Increase oxygen partial pressure in tank ullage space to shift chemical equilibrium back to nitrate.";
        }
        else
        {
            regime = "OPTIMAL_MOLTEN_SALT_THERMAL_STORAGE_STABLE";
            protocol = "Solar salt storage operating in optimal thermal regime: robust cold tank freeze margin, stable nitrate-nitrite equilibrium, low oxide content, and minimal containment corrosion verified.";
        }

        return new MoltenSaltCspAnalysisResult
        {
            StorageTankId = telemetry.StorageTankId,
            ColdTankFreezeSafetyMarginCelsius = freezeMargin,
            ThermalNitrateDecompositionEquilibriumRatio = equilibriumRatio,
            ProjectedCorrosionRateMicronsPerYear = projectedCorrosion,
            OxideInducedScaleFluxingFactor = Math.Round(oxideFactor, 2),
            IsFreezeMarginSafe = isFreezeSafe,
            IsThermalDecompositionControlled = isThermalControlled,
            IsTankCorrosionWithinLimit = isCorrosionOk,
            MoltenSaltThermalRegime = regime,
            StorageOperationProtocol = protocol
        };
    }
}
