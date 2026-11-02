namespace SGrowing.Api.Modules.Aerospace.SpacecraftRcsPlumeImpingement;

public interface ISpacecraftRcsEngine
{
    SpacecraftRcsEvaluation AnalyzeRcsFiring(SpacecraftRcsTelemetry telemetry);
}

public class SpacecraftRcsEngine : ISpacecraftRcsEngine
{
    private const double MaxPlumePressureLimit = 12.0; // Pa

    public SpacecraftRcsEvaluation AnalyzeRcsFiring(SpacecraftRcsTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Plume margin % = (Limit - Pressure) / Limit * 100
        double plumeMargin = Math.Max(0.0, (MaxPlumePressureLimit - telemetry.PeakPlumeImpingementPressurePascals) / MaxPlumePressureLimit * 100.0);

        // Valve thermal headroom: 140 C - T_seat
        double thermalHeadroom = Math.Max(0.0, 140.0 - telemetry.RcsValveSeatTemperatureCelsius);

        bool isPlumeTolerated = telemetry.PeakPlumeImpingementPressurePascals <= 12.0 &&
                                telemetry.InducedDisturbanceTorqueNewtonMeters <= 18.0 &&
                                telemetry.PlumeHeatFluxKiloWattsPerMeter2 <= 4.5;
        bool isValveThermalStable = telemetry.RcsValveSeatTemperatureCelsius <= 135.0;
        bool isDutyOptimal = telemetry.StationKeepingPwmDutyCyclePercent <= 14.0 && telemetry.AttitudeDeadbandErrorDegrees <= 0.08;

        if (!isPlumeTolerated)
        {
            regimes.Add("PLUME_IMPINGEMENT_AERO_DISTURBANCE_TORQUE");
            mitigations.Add("Gimbal solar array panels away from RCS thruster exhaust cant angles and re-route reaction control pulses to paired thrusters.");
        }

        if (!isValveThermalStable)
        {
            regimes.Add("RCS_VALVE_THERMAL_SOAKBACK_CHATTER");
            mitigations.Add("Enforce minimum off-time cooling duration between consecutive pulse bursts and activate heat-pipe thermal straps.");
        }

        if (!isDutyOptimal)
        {
            regimes.Add("LIMIT_CYCLE_PROPELLANT_CONSUMPTION_BLOWOUT");
            mitigations.Add("Widen attitude deadband tolerance or engage low-authority magnetic torque rod unloading to conserve hypergolic propellant.");
        }

        bool isApproved = isPlumeTolerated && isValveThermalStable && isDutyOptimal &&
                          telemetry.MinimumImpulseBitDurationMilliseconds >= 10.0;

        return new SpacecraftRcsEvaluation
        {
            SpacecraftVehicleUid = telemetry.SpacecraftVehicleUid,
            PlumeImpingementSafetyMarginPercent = Math.Round(plumeMargin, 1),
            ValveThermalHeadroomCelsius = Math.Round(thermalHeadroom, 1),
            IsPlumeImpingementTolerated = isPlumeTolerated,
            IsRcsValveThermalStateStable = isValveThermalStable,
            IsDutyCycleFuelOptimal = isDutyOptimal,
            IsRcsFiringApprovedForProximityOps = isApproved,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
