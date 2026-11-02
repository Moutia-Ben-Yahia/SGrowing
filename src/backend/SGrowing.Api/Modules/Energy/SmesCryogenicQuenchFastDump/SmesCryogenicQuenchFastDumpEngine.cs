using System;

namespace SGrowing.Api.Modules.Energy.SmesCryogenicQuenchFastDump;

public interface ISmesCryogenicQuenchFastDumpEngine
{
    SmesQuenchAnalysisResult AnalyzeQuenchState(SmesQuenchTelemetry telemetry);
}

public sealed class SmesCryogenicQuenchFastDumpEngine : ISmesCryogenicQuenchFastDumpEngine
{
    public SmesQuenchAnalysisResult AnalyzeQuenchState(SmesQuenchTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.CoilInductanceHenries <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Coil inductance must be strictly positive.");
        }

        double resistiveVoltageMillivolts = Math.Max(0.0, telemetry.MeasuredTotalVoltageMillivolts - telemetry.CoWoundInductiveVoltageMillivolts);
        double resistiveVoltageMicrovolts = resistiveVoltageMillivolts * 1000.0;

        double dumpResistor = Math.Max(0.01, telemetry.DumpResistorOhms);
        double dumpTimeConstantSeconds = telemetry.CoilInductanceHenries / dumpResistor;

        // Estimated adiabatic hot spot temperature
        double estimatedHotSpotTemperatureKelvin = telemetry.ColdHeadTemperatureKelvin +
            (telemetry.HotSpotHeatingRateKelvinPerSec * (0.010 + (dumpTimeConstantSeconds * 0.25)));

        string status;
        string action;
        bool isFastDumpTriggered;

        if (resistiveVoltageMicrovolts >= 100.0 && (telemetry.HotSpotHeatingRateKelvinPerSec > 15.0 || telemetry.ColdHeadTemperatureKelvin > 35.0))
        {
            status = "QUENCH_DETECTED_CRITICAL";
            action = "TRIGGER_PYROTECHNIC_FAST_DUMP_AND_ISOLATE_CONVERTER";
            isFastDumpTriggered = true;
        }
        else if (telemetry.ColdHeadTemperatureKelvin > 28.0 || resistiveVoltageMicrovolts >= 40.0)
        {
            status = "CRYOGENIC_TEMPERATURE_WARNING";
            action = "BOOST_HELIUM_CIRCULATOR_SPEED_AND_RAMP_DOWN_CURRENT_20_PERCENT";
            isFastDumpTriggered = false;
        }
        else
        {
            status = "SUPERCONDUCTING_STATE_NOMINAL";
            action = "MAINTAIN_PERSISTENT_CURRENT_SWITCH_CONDUCTION";
            isFastDumpTriggered = false;
        }

        return new SmesQuenchAnalysisResult(
            telemetry.CoilId,
            Math.Round(resistiveVoltageMicrovolts, 2),
            Math.Round(estimatedHotSpotTemperatureKelvin, 2),
            Math.Round(dumpTimeConstantSeconds, 4),
            status,
            action,
            isFastDumpTriggered,
            DateTimeOffset.UtcNow);
    }
}
