using System;

namespace SGrowing.Api.Modules.Energy.HempSubstationProtection;

public interface IHempSubstationProtectionEngine
{
    SubstationHempAnalysisResult AnalyzeHempThreat(SubstationHempTelemetry telemetry);
}

public sealed class HempSubstationProtectionEngine : IHempSubstationProtectionEngine
{
    public SubstationHempAnalysisResult AnalyzeHempThreat(SubstationHempTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.GridNominalVoltageKv <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Nominal grid voltage must be strictly positive.");
        }

        // E1 fast transient clamping: MOV / GDT suppression
        double clampedVoltageKv = telemetry.PeakSurgeVoltageKilovolts * 0.045;

        // E3 GIC core heating rate: dT/dt = 0.0015 * (I_GIC)^2 (C/min)
        double gicAmperes = Math.Max(0.0, telemetry.NeutralGeomagneticallyInducedCurrentAmperes);
        double coreHeatingRateCelsiusPerMin = 0.0015 * Math.Pow(gicAmperes, 2.0);

        string threatLevel;
        string action;
        bool isProtected;

        if (telemetry.MeasuredDvDtKilovoltsPerNanosecond >= 10.0 || telemetry.PeakSurgeVoltageKilovolts >= 40.0)
        {
            threatLevel = "E1_NANOSECOND_SURGE_INTERCEPTED";
            action = "ISOLATE_MICROPROCESSOR_RELAYS_AND_ACTIVATE_FARADAY_SHIELD_CONDUIT";
            isProtected = true;
        }
        else if (gicAmperes >= 35.0 || telemetry.TransformerReactivePowerSurgeMvar >= 45.0)
        {
            threatLevel = "E3_CORE_SATURATION_GIC_ALARM";
            action = "INSERT_NEUTRAL_GROUNDING_DC_BLOCKING_CAPACITOR_BANK";
            isProtected = telemetry.DcBlockingCapacitorEngaged;
        }
        else
        {
            threatLevel = "GRID_SUBSTATION_HARDENED_NORMAL";
            action = "MAINTAIN_CONTINUOUS_OPTICAL_GIC_MONITORING";
            isProtected = true;
        }

        return new SubstationHempAnalysisResult(
            telemetry.SubstationId,
            Math.Round(clampedVoltageKv, 2),
            Math.Round(coreHeatingRateCelsiusPerMin, 2),
            threatLevel,
            action,
            isProtected,
            DateTimeOffset.UtcNow);
    }
}
