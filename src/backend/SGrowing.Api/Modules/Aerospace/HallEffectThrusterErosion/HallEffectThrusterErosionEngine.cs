using System;

namespace SGrowing.Api.Modules.Aerospace.HallEffectThrusterErosion;

public interface IHallEffectThrusterErosionEngine
{
    HallEffectThrusterAnalysisResult AnalyzeThruster(HallEffectThrusterTelemetry telemetry);
}

public sealed class HallEffectThrusterErosionEngine : IHallEffectThrusterErosionEngine
{
    private const double MaxAllowableErosionRateMicronsPerKh = 12.0; // max 12 um/kh for >= 10,000h life
    private const double MaxBreathingModeOscillationFraction = 0.35; // 35% oscillation amplitude
    private const double MinElectricalEfficiencyPercent = 52.0; // 52% total efficiency

    public HallEffectThrusterAnalysisResult AnalyzeThruster(HallEffectThrusterTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.DischargeVoltageVolts <= 0.0 || telemetry.DischargeCurrentAmps <= 0.0 || telemetry.AnodeMassFlowRateMgPerSec <= 0.0)
        {
            throw new ArgumentException("Discharge voltage, current, and mass flow rate must be positive.", nameof(telemetry));
        }

        // 1. Ion Mass (Xenon: 131.293 amu, Krypton: 83.798 amu)
        double amuKg = 1.66054e-27;
        double ionMassKg = telemetry.PropellantType.Contains("Krypton", StringComparison.OrdinalIgnoreCase)
            ? 83.798 * amuKg
            : 131.293 * amuKg; // default Xenon

        // 2. Beam Velocity and Thrust:
        // v_exhaust = sqrt(2 * e * V_d / M_ion) * cos(theta_div)
        double elemCharge = 1.602176634e-19;
        double g0 = 9.80665;
        double divergenceRad = telemetry.PlumeDivergenceHalfAngleDegrees * (Math.PI / 180.0);
        double beamDivergenceFactor = Math.Cos(divergenceRad);

        double idealExhaustVelocityMPerS = Math.Sqrt((2.0 * elemCharge * telemetry.DischargeVoltageVolts) / ionMassKg);
        double actualExhaustVelocityMPerS = idealExhaustVelocityMPerS * beamDivergenceFactor * 0.94; // acceleration efficiency ~94%

        double mDotKgPerS = telemetry.AnodeMassFlowRateMgPerSec * 1e-6;
        double thrustNewtons = mDotKgPerS * actualExhaustVelocityMPerS;
        double thrustMilliNewtons = thrustNewtons * 1000.0;
        double specificImpulseSec = actualExhaustVelocityMPerS / g0;

        // 3. Electrical Efficiency: eta_T = T^2 / (2 * mDot * P_in)
        double pinWatts = telemetry.DischargeVoltageVolts * telemetry.DischargeCurrentAmps;
        double electricalEfficiency = (Math.Pow(thrustNewtons, 2.0) / (2.0 * mDotKgPerS * pinWatts)) * 100.0;
        electricalEfficiency = Math.Clamp(electricalEfficiency, 20.0, 75.0);

        // 4. Breathing Mode Oscillation Fraction: Delta I_d / I_d
        double breathingModeFraction = telemetry.PeakBreathingModeOscillationAmps / telemetry.DischargeCurrentAmps;

        // 5. Ceramic Wall Sputtering Erosion Rate:
        // Yamamura-type sputtering yield Y(E, theta) ~ Y0 * (E/Eth - 1)^n * exp(f(theta))
        // Typical BN sputtering yield around 85 eV: ~ 0.02 - 0.08 atoms/ion
        double angleRad = telemetry.IonImpactAngleDegrees * (Math.PI / 180.0);
        double angleFactor = Math.Pow(Math.Cos(angleRad), -1.2); // angular enhancement
        angleFactor = Math.Clamp(angleFactor, 0.5, 3.5);

        double energyOverThreshold = Math.Max(0.0, (telemetry.MeanIonImpactEnergyEv - 30.0) / 100.0);
        double sputteringYieldAtomsPerIon = 0.035 * Math.Pow(energyOverThreshold, 1.2) * angleFactor;

        // Erosion rate in um/1000h:
        // j_ion in mA/cm^2 = j_ion * 10 A/m^2
        // Volume rate = (j_ion / e) * Y * (M_ceramic / (N_A * rho))
        // Simplified calibrated scaling:
        double ceramicDensity = Math.Max(1.5, telemetry.CeramicWallDensityGPerCm3);
        double erosionRateUmPerKh = (telemetry.WallIonCurrentDensityMaPerCm2 * 45.0 * sputteringYieldAtomsPerIon) / (ceramicDensity / 2.1);
        erosionRateUmPerKh = Math.Round(Math.Clamp(erosionRateUmPerKh, 0.1, 80.0), 2);

        // 6. Projected Wall Lifetime (Channel wall thickness ~ 120 microns sacrificial layer)
        double sacrificialWallThicknessUm = 120.0;
        double projectedLifeHours = erosionRateUmPerKh > 0.0
            ? (sacrificialWallThicknessUm / erosionRateUmPerKh) * 1000.0
            : 50000.0;
        projectedLifeHours = Math.Round(Math.Clamp(projectedLifeHours, 100.0, 50000.0), 0);

        // 7. Validation Checks
        bool isErosionOk = erosionRateUmPerKh <= MaxAllowableErosionRateMicronsPerKh;
        bool isBreathingStable = breathingModeFraction <= MaxBreathingModeOscillationFraction;
        bool isEfficiencyOk = electricalEfficiency >= MinElectricalEfficiencyPercent;

        // 8. Regime & Recommendations
        string regime;
        string guidance;

        if (!isErosionOk)
        {
            regime = "ANOMALOUS_WALL_SPUTTERING_EROSION_EXCESS";
            guidance = $"Channel wall sputtering erosion ({erosionRateUmPerKh:F2} um/kh) violates max qualification envelope ({MaxAllowableErosionRateMicronsPerKh:F1} um/kh), projecting premature failure in {projectedLifeHours:F0} hours. Adjust magnetic shielding coil currents to align field lines parallel to ceramic boundary.";
        }
        else if (!isBreathingStable)
        {
            regime = "BREATHING_MODE_DISCHARGE_INSTABILITY";
            guidance = $"Breathing mode current oscillation ({breathingModeFraction * 100.0:F1}%) exceeded stable discharge threshold ({MaxBreathingModeOscillationFraction * 100.0:F1}%). Regulate anode neutral xenon flow conductance and adjust cathode neutralizer coupling.";
        }
        else if (!isEfficiencyOk)
        {
            regime = "SUB_OPTIMAL_ELECTRICAL_THRUST_EFFICIENCY";
            guidance = $"Total thruster efficiency ({electricalEfficiency:F1}%) is sub-optimal. Minimize plume divergence angle or increase discharge voltage to boost specific impulse.";
        }
        else
        {
            regime = "NOMINAL_HALL_THRUSTER_LONG_LIFE_CRUISE";
            guidance = "Hall thruster operating at peak qualification: wall erosion within 10,000-hour life margin, breathing mode oscillations damped, and high specific impulse achieved.";
        }

        return new HallEffectThrusterAnalysisResult
        {
            ThrusterUnitId = telemetry.ThrusterUnitId,
            SputteringErosionRateMicronsPerKiloHour = erosionRateUmPerKh,
            ProjectedWallLifeHours = projectedLifeHours,
            BreathingModeOscillationFraction = Math.Round(breathingModeFraction, 3),
            ThrustMilliNewtons = Math.Round(thrustMilliNewtons, 1),
            SpecificImpulseSeconds = Math.Round(specificImpulseSec, 1),
            TotalElectricalEfficiencyPercent = Math.Round(electricalEfficiency, 2),
            IsWallErosionWithinLifeEnvelope = isErosionOk,
            IsBreathingModeStable = isBreathingStable,
            IsEfficiencyTargetMet = isEfficiencyOk,
            ThrusterOperationRegime = regime,
            OperationalGuidance = guidance
        };
    }
}
