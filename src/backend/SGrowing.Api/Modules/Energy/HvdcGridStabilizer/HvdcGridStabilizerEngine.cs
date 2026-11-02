namespace SGrowing.Api.Modules.Energy.HvdcGridStabilizer;

public class HvdcGridStabilizerEngine
{
    public GridStabilizationResult StabilizeGrid(GridStabilizationRequest request)
    {
        if (request.PmuReadings.Count == 0)
        {
            return new GridStabilizationResult(
                SubstationsMonitored: 0,
                MeanFrequencyHz: request.NominalFrequencyHz,
                MaxPhaseAngleDeviationDegrees: 0.0,
                Actions: [],
                GridFrequencyStable: true,
                GridResilienceSummary: "Nominal: Zero PMU alerts received; no active control required."
            );
        }

        double meanFreq = Math.Round(request.PmuReadings.Average(r => r.FrequencyHz), 3);
        double minAngle = request.PmuReadings.Min(r => r.PhaseAngleDegrees);
        double maxAngle = request.PmuReadings.Max(r => r.PhaseAngleDegrees);
        double angleDeviation = Math.Round(maxAngle - minAngle, 2);

        double deltaF = meanFreq - request.NominalFrequencyHz;
        double maxRoCoF = request.PmuReadings.Max(r => Math.Abs(r.RateOfChangeOfFrequencyHzPerSec));

        bool isStable = Math.Abs(deltaF) <= 0.10 && angleDeviation <= request.MaxAllowedAngleDriftDegrees;
        var actions = new List<ConverterControlAction>();

        foreach (var station in request.ConverterStations)
        {
            double availableHeadroomMw = station.RatedCapacityMw - station.CurrentPowerFlowMw;

            if (deltaF < -0.15) // Severe under-frequency (generation deficit)
            {
                // Synthetic inertia proportional to RoCoF and inertia constant H
                double syntheticInertiaMw = Math.Round(Math.Min(availableHeadroomMw, 2.0 * station.InertiaConstantH * maxRoCoF * (station.RatedCapacityMw * 0.10)), 2);
                double powerBoostMw = Math.Round(Math.Min(availableHeadroomMw - syntheticInertiaMw, station.MaxRampRateMwPerSec * 2.0), 2);

                actions.Add(new ConverterControlAction(
                    StationId: station.StationId,
                    ActivePowerAdjustmentMw: powerBoostMw,
                    SyntheticInertiaInjectedMw: syntheticInertiaMw,
                    ControlMode: "FastFrequencyResponse",
                    Reason: $"Under-frequency detected ({meanFreq} Hz). Synthetic inertia injected to arrests RoCoF."
                ));
            }
            else if (angleDeviation > request.MaxAllowedAngleDriftDegrees) // Inter-area phase angle divergence
            {
                double dampingAdjustmentMw = Math.Round(Math.Min(availableHeadroomMw, (angleDeviation / 10.0) * 25.0), 2);

                actions.Add(new ConverterControlAction(
                    StationId: station.StationId,
                    ActivePowerAdjustmentMw: dampingAdjustmentMw,
                    SyntheticInertiaInjectedMw: 0.0,
                    ControlMode: "PowerOscillationDamping",
                    Reason: $"Phase angle drift ({angleDeviation}° > {request.MaxAllowedAngleDriftDegrees}°). Inter-area damping active."
                ));
            }
            else
            {
                actions.Add(new ConverterControlAction(
                    StationId: station.StationId,
                    ActivePowerAdjustmentMw: 0.0,
                    SyntheticInertiaInjectedMw: 0.0,
                    ControlMode: "BaseScheduledDispatch",
                    Reason: "Grid operates within statutory ENTSO-E / NERC stability tolerances."
                ));
            }
        }

        string summary;
        if (isStable)
        {
            summary = $"Grid Stable: Mean frequency {meanFreq:F3} Hz within ±0.10 Hz statutory boundary. Angle spread {angleDeviation:F1}°.";
        }
        else if (deltaF < -0.15)
        {
            summary = $"Under-Frequency Transient: Grid frequency dipped to {meanFreq:F3} Hz. Autonomous synthetic inertia active.";
        }
        else
        {
            summary = $"Inter-Area Oscillation: Phase drift {angleDeviation:F1}° requires active damping intervention.";
        }

        return new GridStabilizationResult(
            SubstationsMonitored: request.PmuReadings.Count,
            MeanFrequencyHz: meanFreq,
            MaxPhaseAngleDeviationDegrees: angleDeviation,
            Actions: actions,
            GridFrequencyStable: isStable,
            GridResilienceSummary: summary
        );
    }
}
