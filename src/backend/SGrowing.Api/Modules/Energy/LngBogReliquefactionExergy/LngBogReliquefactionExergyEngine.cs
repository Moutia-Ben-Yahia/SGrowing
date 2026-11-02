using System;

namespace SGrowing.Api.Modules.Energy.LngBogReliquefactionExergy;

public interface ILngBogReliquefactionExergyEngine
{
    LngBogReliquefactionResult AnalyzeReliquefactionEfficiency(LngBogReliquefactionTelemetry telemetry);
}

public sealed class LngBogReliquefactionExergyEngine : ILngBogReliquefactionExergyEngine
{
    public LngBogReliquefactionResult AnalyzeReliquefactionEfficiency(LngBogReliquefactionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.CompressorPowerConsumptionKilowatts <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Compressor power consumption must be strictly positive.");
        }

        double bogRateKgPerHour = Math.Max(1.0, telemetry.MeasuredBogGenerationRateKgPerHour);
        double bogRateKgPerSec = bogRateKgPerHour / 3600.0;

        // Specific refrigeration work: kWh/kg BOG condensed
        double specificRefrigerationWorkKwhPerKg = telemetry.CompressorPowerConsumptionKilowatts / bogRateKgPerHour;

        // Flow exergy rate delivered: Delta_ex ~ 1025 kJ/kg across condensing temperature glide
        double specificExergyKjPerKg = 1025.0;
        double exergyRateDeliveredKilowatts = bogRateKgPerSec * specificExergyKjPerKg;

        // Second-Law Exergy Efficiency
        double secondLawExergyEfficiencyPercent = (exergyRateDeliveredKilowatts / telemetry.CompressorPowerConsumptionKilowatts) * 100.0;
        secondLawExergyEfficiencyPercent = Math.Clamp(secondLawExergyEfficiencyPercent, 1.0, 100.0);

        string state;
        string action;
        bool isFlaringSuppressed;

        if (telemetry.TankUllagePressureBar > 1.30 || bogRateKgPerHour > telemetry.MaxDesignReliquefactionCapacityKgPerHour)
        {
            state = "EXCESS_BOG_PRESSURE_SURGE";
            action = "MAXIMIZE_COMPRESSOR_THROUGHPUT_AND_ENGAGE_DUAL_FUEL_BOILER_SUPPLEMENT";
            isFlaringSuppressed = false;
        }
        else if (secondLawExergyEfficiencyPercent < 32.0)
        {
            state = "EXERGY_EFFICIENCY_DEGRADED";
            action = "MODULATE_JOULE_THOMSON_VALVE_EXPANSION_RATIO_AND_DEFROST_COLDBOX";
            isFlaringSuppressed = true;
        }
        else
        {
            state = "RELIQUEFACTION_NOMINAL";
            action = "MAINTAIN_CLOSED_LOOP_NITROGEN_BRAYTON_STEADY_STATE";
            isFlaringSuppressed = true;
        }

        return new LngBogReliquefactionResult(
            telemetry.CarrierTankId,
            Math.Round(specificRefrigerationWorkKwhPerKg, 3),
            Math.Round(exergyRateDeliveredKilowatts, 1),
            Math.Round(secondLawExergyEfficiencyPercent, 2),
            state,
            action,
            isFlaringSuppressed,
            DateTimeOffset.UtcNow);
    }
}
