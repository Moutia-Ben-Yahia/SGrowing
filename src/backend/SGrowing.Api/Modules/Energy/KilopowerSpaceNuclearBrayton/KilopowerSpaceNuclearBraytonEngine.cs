using System;

namespace SGrowing.Api.Modules.Energy.KilopowerSpaceNuclearBrayton;

public interface IKilopowerSpaceNuclearBraytonEngine
{
    KilopowerAnalysisResult AnalyzeSurfacePowerCycle(KilopowerTelemetry telemetry);
}

public sealed class KilopowerSpaceNuclearBraytonEngine : IKilopowerSpaceNuclearBraytonEngine
{
    private const double SpecificHeatCpKjPerKgK = 5.19; // He-Xe mixture (M = 40)
    private const double StefanBoltzmann = 5.670374419e-8;
    private const double RadiatorEmissivity = 0.88;

    public KilopowerAnalysisResult AnalyzeSurfacePowerCycle(KilopowerTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.WorkingFluidMassFlowKgPerSec <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Mass flow rate must be strictly positive.");
        }

        double tTurbInK = Math.Max(300.0, telemetry.BraytonTurbineInletTempCelsius + 273.15);
        double tCompInK = Math.Max(200.0, telemetry.BraytonCompressorInletTempCelsius + 273.15);

        // Brayton cycle shaft power: turbine expands, compressor compresses
        double deltaTTurbine = tTurbInK * (1.0 - (0.88 * (1.0 - Math.Pow(2.0, -0.24))));
        double turbinePowerKw = telemetry.WorkingFluidMassFlowKgPerSec * SpecificHeatCpKjPerKgK * (tTurbInK - deltaTTurbine);

        double deltaTCompressor = tCompInK * (1.0 + (1.19 * (Math.Pow(2.0, 0.24) - 1.0)));
        double compressorPowerKw = telemetry.WorkingFluidMassFlowKgPerSec * SpecificHeatCpKjPerKgK * (deltaTCompressor - tCompInK);

        double netShaftPowerKw = Math.Max(0.0, turbinePowerKw - compressorPowerKw);
        double netElectricalPowerKw = netShaftPowerKw * 0.92; // Generator mechanical-to-electrical efficiency

        // Radiator heat rejection: Q_rad = eps * sigma * A * (T_rad^4 - T_sink^4)
        double tRadMeanK = Math.Max(100.0, telemetry.RadiatorReturnFluidTempCelsius + 273.15 + 40.0);
        double tSinkK = Math.Max(10.0, telemetry.DeepSpaceSinkTempKelvin);
        double radArea = Math.Max(1.0, telemetry.RadiatorPanelAreaM2);
        double rejectedPowerWatts = RadiatorEmissivity * StefanBoltzmann * radArea * (Math.Pow(tRadMeanK, 4.0) - Math.Pow(tSinkK, 4.0));
        double rejectedPowerKw = Math.Max(0.0, rejectedPowerWatts / 1000.0);

        // Freeze-out safety margin
        double freezeOutMarginK = telemetry.RadiatorReturnFluidTempCelsius - telemetry.RadiatorFreezeThresholdTempCelsius;

        string state;
        string action;
        bool isPowerAdequate;

        if (freezeOutMarginK < 5.0 || telemetry.RadiatorReturnTempRateCelsiusPerMin < -2.0)
        {
            state = "RADIATOR_FREEZE_OUT_IMMINENT";
            action = "RETRACT_RADIATOR_PANEL_AND_ENGAGE_ELECTRIC_HEAT_TRACING";
            isPowerAdequate = false;
        }
        else if (telemetry.CoreSodiumHeatPipeTempCelsius > 780.0)
        {
            state = "REACTOR_CORE_OVERHEAT_WARNING";
            action = "RAMP_UP_BRAYTON_COMPRESSOR_SPEED_TO_MAXIMUM_FLOW";
            isPowerAdequate = true;
        }
        else
        {
            state = "STEADY_STATE_SURFACE_POWER";
            action = "MAINTAIN_CLOSED_BRAYTON_EQUILIBRIUM";
            isPowerAdequate = netElectricalPowerKw >= telemetry.ElectricalLoadDemandKilowatts;
        }

        return new KilopowerAnalysisResult(
            telemetry.ReactorId,
            Math.Round(netElectricalPowerKw, 2),
            Math.Round(rejectedPowerKw, 2),
            Math.Round(freezeOutMarginK, 1),
            state,
            action,
            isPowerAdequate,
            DateTimeOffset.UtcNow);
    }
}
