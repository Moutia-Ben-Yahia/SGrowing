namespace SGrowing.Api.Modules.Healthcare.MriMagnetQuenchArbiter;

public interface IMriMagnetQuenchArbiterEngine
{
    MriMagnetQuenchDecision EvaluateQuenchRisk(MriMagnetQuenchRequest request);
}

public class MriMagnetQuenchArbiterEngine : IMriMagnetQuenchArbiterEngine
{
    public MriMagnetQuenchDecision EvaluateQuenchRisk(MriMagnetQuenchRequest request)
    {
        var state = request.State;
        var limits = request.Limits;

        // Stored magnetic energy: E = 0.5 * L * I^2 approx 2.5 MJ for 3T, scales with B^2
        double storedEnergyMj = 2.5 * Math.Pow(Math.Max(0.5, state.MagneticFieldTesla) / 3.0, 2.0);

        // Quench detection thresholds:
        // 1. Resistive coil voltage indicating normal zone transition (superconductor losing zero resistance)
        bool voltageWarning = state.CoilResistiveVoltageMilliVolts > limits.MaxAllowableResistiveVoltageMilliVolts;
        bool boilOffWarning = state.LiquidHeliumBoilOffRateLitersPerMin > limits.MaxAllowableBoilOffRateLitersPerMin;
        bool vacuumLossWarning = state.CryostatVacuumPressureMbar > limits.MaxAllowableVacuumPressureMbar;

        bool quenchPrecursor = voltageWarning || boilOffWarning || vacuumLossWarning;
        bool emergencyDump = state.CoilResistiveVoltageMilliVolts > (limits.MaxAllowableResistiveVoltageMilliVolts * 2.5) ||
                             state.LiquidHeliumBoilOffRateLitersPerMin > (limits.MaxAllowableBoilOffRateLitersPerMin * 2.0) ||
                             state.ColdHeadTemperatureKelvin > 7.5;

        // Hot spot temperature prediction:
        // Adiabatic heating during quench: T_hot = T_base + Delta T
        double hotSpotTemp;
        if (emergencyDump)
        {
            // Rapid heat deposition, quench heaters active to spread heat uniformly
            hotSpotTemp = Math.Min(limits.MaxAllowableHotSpotTempKelvin,
                state.ColdHeadTemperatureKelvin + (state.CoilResistiveVoltageMilliVolts * 2.8) + (boilOffWarning ? 45.0 : 15.0));
        }
        else if (quenchPrecursor)
        {
            hotSpotTemp = state.ColdHeadTemperatureKelvin + (state.CoilResistiveVoltageMilliVolts * 0.9);
        }
        else
        {
            hotSpotTemp = state.ColdHeadTemperatureKelvin;
        }

        // Energy extraction rate if dump resistor is engaged: P = I^2 * R_dump in MW
        double extractionRateMw = emergencyDump ? (Math.Pow(state.OperatingCurrentAmperes, 2.0) * limits.DumpResistorOhms) / 1e6 : 0.0;

        string safetyStatus;
        string recommendedAction;

        if (emergencyDump)
        {
            safetyStatus = "EMERGENCY_QUENCH_HEATER_ACTIVE";
            recommendedAction = "TRIGGER_DUMP_AND_VENT: Immediate controlled energy dump to external resistor. Energize secondary quench heaters to distribute thermal footprint across full coil bundle and verify cryogenic exhaust burst-disk integrity.";
        }
        else if (quenchPrecursor)
        {
            safetyStatus = "QUENCH_PRECURSOR_DETECTED";
            recommendedAction = "RAMP_DOWN_STANDBY: Resistive voltage or cryogen boil-off exceeded nominal baseline. Throttle power supply current, increase cold-head compressor helium flow, and prepare emergency discharge circuit.";
        }
        else
        {
            safetyStatus = "SUPERCONDUCTING_STEADY_STATE";
            recommendedAction = "NOMINAL_OPERATION: Coils fully superconducting with sub-millivolt resistive potential. Cryogenic insulation vacuum optimal for continuous diagnostic imaging.";
        }

        return new MriMagnetQuenchDecision(
            ScannerId: state.ScannerId,
            QuenchPrecursorDetected: quenchPrecursor,
            EmergencyDumpTriggered: emergencyDump,
            EstimatedHotSpotTemperatureKelvin: Math.Round(hotSpotTemp, 1),
            StoredMagneticEnergyMegaJoules: Math.Round(storedEnergyMj, 2),
            EnergyExtractionRateMegaWatts: Math.Round(extractionRateMw, 2),
            SafetyInterlockStatus: safetyStatus,
            RecommendedAction: recommendedAction
        );
    }
}
