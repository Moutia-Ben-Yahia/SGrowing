namespace SGrowing.Api.Modules.Aerospace.SpaceElevatorTether;

public interface ISpaceElevatorTetherEngine
{
    TetherDynamicStabilizationPlan ComputeStabilization(SpaceElevatorTetherRequest request);
}

public class SpaceElevatorTetherEngine : ISpaceElevatorTetherEngine
{
    private const double EarthRotationOmegaRadPerSec = 7.2921159e-5;
    private const double CntTetherDensityKgPerM3 = 1300.0; // Carbon nanotube material density

    public TetherDynamicStabilizationPlan ComputeStabilization(SpaceElevatorTetherRequest request)
    {
        var state = request.State;
        var thrusters = request.ThrusterConfig;
        var climbers = state.ActiveClimbers ?? [];

        // 1. Calculate total Coriolis force from all ascending/descending climbers:
        // F_cor = 2 * m * Omega * v_z
        double totalCoriolisForceNewtons = 0.0;
        foreach (var c in climbers)
        {
            double massKg = c.MassMetricTons * 1000.0;
            double velocityMPerSec = c.AscentVelocityKmPerSec * 1000.0;
            double fCor = 2.0 * massKg * EarthRotationOmegaRadPerSec * velocityMPerSec;
            totalCoriolisForceNewtons += Math.Abs(fCor);
        }

        double totalCoriolisKn = totalCoriolisForceNewtons / 1000.0;

        // 2. Transverse wave speed: c = sqrt(T / rho)
        // Tension in GPa: 1 GPa = 1e9 N/m^2
        double nominalTensionPa = state.GeoTensionGigaPascals * 1e9;
        double waveSpeedMPerSec = Math.Sqrt(Math.Max(1e9, nominalTensionPa) / CntTetherDensityKgPerM3);
        double waveSpeedKmPerSec = waveSpeedMPerSec / 1000.0;

        // Dynamic peak tension combining base GEO centrifugal tension and climber weight + Coriolis loading
        double climberLoadGpa = climbers.Count * 1.5;
        double dynamicPeakTensionGpa = state.GeoTensionGigaPascals + climberLoadGpa + (totalCoriolisKn * 0.012);

        // Resonant instability criteria:
        // High transverse wave displacement (> 500m) or tension exceeding 90% of CNT ultimate tensile strength (100 GPa)
        bool oscillationDanger = state.TransverseOscillationAmplitudeMeters > 500.0;
        bool tensionDanger = dynamicPeakTensionGpa > (state.MaxAllowableTensionGigaPascals * 0.90);
        bool resonantDanger = oscillationDanger || tensionDanger || (totalCoriolisKn > 200.0);

        string safetyStatus;
        if (dynamicPeakTensionGpa > (state.MaxAllowableTensionGigaPascals * 0.92))
        {
            safetyStatus = "CRITICAL_TENSION_LIMIT";
        }
        else if (resonantDanger)
        {
            safetyStatus = "RESONANT_WHIP_WARNING";
        }
        else
        {
            safetyStatus = "NOMINAL_DYNAMIC_STABILITY";
        }

        // Active counterweight thruster damping command generation
        var thrusterCommands = new List<CounterweightThrusterCommand>();
        string cadenceDirective;

        if (resonantDanger)
        {
            double dampingThrustKn = Math.Min(thrusters.MaxThrustKiloNewtons, Math.Max(10.0, totalCoriolisKn * 0.25));
            thrusterCommands.Add(new CounterweightThrusterCommand(
                Axis: "TRANSVERSE_EAST_WEST",
                ThrustKiloNewtons: Math.Round(dampingThrustKn, 1),
                BurnDurationSeconds: 180.0,
                Objective: "CORIOLIS_COUNTER_TORQUE: Apply counter-rotation burn at 100,000 km counterweight to null transverse string whip."
            ));
            thrusterCommands.Add(new CounterweightThrusterCommand(
                Axis: "LONGITUDINAL_RADIAL",
                ThrustKiloNewtons: Math.Round(dampingThrustKn * 0.6, 1),
                BurnDurationSeconds: 120.0,
                Objective: "TIDAL_TENSION_FLATTENING: Relieve lunar-solar tidal harmonic resonance."
            ));

            cadenceDirective = "THROTTLE_CLIMBER_SPEED: Reduce climber ascent speed by 40% and enforce 600km inter-vehicle spacing to decouple harmonic reinforcement.";
        }
        else
        {
            thrusterCommands.Add(new CounterweightThrusterCommand(
                Axis: "GEO_STATION_KEEPING",
                ThrustKiloNewtons: 5.0,
                BurnDurationSeconds: 60.0,
                Objective: "TRIM_ORBITAL_PLANE: Maintain nominal equatorial counterweight orbit."
            ));

            cadenceDirective = "NOMINAL_CADENCE: Maintain standard 200 km/h climber transit velocity. Tether strain and transverse oscillation within design envelopes.";
        }

        return new TetherDynamicStabilizationPlan(
            TetherId: state.TetherId,
            PeakTensionGigaPascals: Math.Round(dynamicPeakTensionGpa, 2),
            TotalCoriolisDeflectionForceKiloNewtons: Math.Round(totalCoriolisKn, 2),
            TransverseWaveVelocityKmPerSec: Math.Round(waveSpeedKmPerSec, 2),
            ResonantInstabilityDetected: resonantDanger,
            TensionSafetyMarginStatus: safetyStatus,
            ThrusterCommands: thrusterCommands,
            RecommendedClimberCadenceDirective: cadenceDirective
        );
    }
}
