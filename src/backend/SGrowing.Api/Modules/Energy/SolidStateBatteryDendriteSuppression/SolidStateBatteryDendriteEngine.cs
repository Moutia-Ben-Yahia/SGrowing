using System;

namespace SGrowing.Api.Modules.Energy.SolidStateBatteryDendriteSuppression;

public interface ISolidStateBatteryDendriteEngine
{
    SolidStateBatteryDendriteAnalysisResult AnalyzeDendriteSuppression(SolidStateBatteryDendriteTelemetry telemetry);
}

public sealed class SolidStateBatteryDendriteEngine : ISolidStateBatteryDendriteEngine
{
    private const double MinOptimalStackPressureMpa = 1.5; // below 1.5 MPa -> void formation at Li interface
    private const double MaxOptimalStackPressureMpa = 5.0; // above 5.0 MPa -> excessive Li anode plastic creep
    private const double MaxSafeCurrentDensityRatio = 0.80; // j_applied / j_CCD <= 80%

    public SolidStateBatteryDendriteAnalysisResult AnalyzeDendriteSuppression(SolidStateBatteryDendriteTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.AppliedCurrentDensityMaPerCm2 <= 0.0 || telemetry.UniaxialStackPressureMpa <= 0.0)
        {
            throw new ArgumentException("Applied current density and stack pressure must be positive.", nameof(telemetry));
        }

        // 1. Effective Critical Current Density (j_CCD) with Stack Pressure Enhancement:
        // j_CCD = j_CCD0 * (1 + 0.35 * (P_stack - 1.0)) * temp_factor
        double tempKelvin = telemetry.CellTemperatureCelsius + 273.15;
        double tempFactor = Math.Exp((tempKelvin - 298.15) / 45.0); // Arrhenius thermal enhancement
        tempFactor = Math.Clamp(tempFactor, 0.6, 2.5);

        double pressureGain = 1.0 + 0.32 * (telemetry.UniaxialStackPressureMpa - 1.0);
        double effectiveJccd = telemetry.BaselineCriticalCurrentDensityMaPerCm2 * pressureGain * tempFactor;
        effectiveJccd = Math.Round(Math.Clamp(effectiveJccd, 0.2, 25.0), 2);

        // 2. Current Density Safety Ratio: j_applied / j_CCD
        double currentSafetyRatio = telemetry.AppliedCurrentDensityMaPerCm2 / effectiveJccd;

        // 3. Crack-Tip Stress Intensity Factor K_I:
        // Flaw size in meters: a = flaw_um * 1e-6
        // Stress from overpotential: sigma_overpotential ~ E_SE * (j_applied / j_CCD)
        // External compressive stack pressure acts to close cracks: sigma_net = max(0.1, sigma_tensile - P_stack)
        double flawRadiusM = telemetry.GrainBoundaryFlawRadiusMicrons * 1e-6;
        double tensileStressMpa = (telemetry.SolidElectrolyteShearModulusGpa * 0.05) * Math.Pow(currentSafetyRatio, 1.8);
        double netCrackStressMpa = Math.Max(0.05, tensileStressMpa - 0.75 * telemetry.UniaxialStackPressureMpa);
        double stressIntensityKi = netCrackStressMpa * Math.Sqrt(Math.PI * flawRadiusM * 1e6); // scaled MPa*m^0.5
        stressIntensityKi = Math.Round(Math.Clamp(stressIntensityKi, 0.05, 10.0), 3);

        // 4. Lithium Creep Yield Ratio: P_stack / sigma_yield_Li (Li yield ~ 1.2 MPa at RT)
        double liYieldMpa = 1.2;
        double pressureToYieldRatio = telemetry.UniaxialStackPressureMpa / liYieldMpa;

        // 5. Validation Checks
        bool isPressureOk = telemetry.UniaxialStackPressureMpa >= MinOptimalStackPressureMpa &&
                            telemetry.UniaxialStackPressureMpa <= MaxOptimalStackPressureMpa;
        bool isDendriteSuppressed = stressIntensityKi < telemetry.ElectrolyteFractureToughnessMpaM12;
        bool isFastChargeSafe = currentSafetyRatio <= MaxSafeCurrentDensityRatio && isDendriteSuppressed && isPressureOk;

        // 6. Regime & Protocol
        string regime;
        string protocol;

        if (telemetry.UniaxialStackPressureMpa < MinOptimalStackPressureMpa)
        {
            regime = "INTERFACIAL_VOID_FORMATION_DELAMINATION";
            protocol = $"Stack pressure ({telemetry.UniaxialStackPressureMpa:F1} MPa) is below minimum threshold ({MinOptimalStackPressureMpa:F1} MPa). Increase fixture spring compression to maintain intimate contact during lithium stripping.";
        }
        else if (telemetry.UniaxialStackPressureMpa > MaxOptimalStackPressureMpa)
        {
            regime = "EXCESSIVE_STACK_PRESSURE_LI_PLASTIC_CREEP";
            protocol = $"Stack pressure ({telemetry.UniaxialStackPressureMpa:F1} MPa) exceeds upper limit ({MaxOptimalStackPressureMpa:F1} MPa), risking lithium anode lateral extrusion and shorting. Ease mechanical clamp tension.";
        }
        else if (!isDendriteSuppressed)
        {
            regime = "INTERGRANULAR_DENDRITE_PROPAGATION_HAZARD";
            protocol = $"Crack-tip stress intensity ({stressIntensityKi:F3} MPa*m^0.5) exceeded fracture toughness ({telemetry.ElectrolyteFractureToughnessMpaM12:F2} MPa*m^0.5). Reduce charging current density or refine ceramic sintering to eliminate grain boundary flaws.";
        }
        else if (currentSafetyRatio > MaxSafeCurrentDensityRatio)
        {
            regime = "NEAR_CRITICAL_CURRENT_DENSITY_SURCHARGE";
            protocol = $"Current density ratio ({currentSafetyRatio * 100.0:F1}%) exceeded safe operating limit ({MaxSafeCurrentDensityRatio * 100.0:F1}% of j_CCD). Throttle fast-charge profile to prevent filament nucleation.";
        }
        else
        {
            regime = "OPTIMAL_DENDRITE_SUPPRESSED_FAST_CHARGE";
            protocol = "Solid-state cell operating at peak durability: uniform stack pressure envelope maintained, stress intensity below fracture toughness, and zero dendrite propagation risk.";
        }

        return new SolidStateBatteryDendriteAnalysisResult
        {
            CellPackId = telemetry.CellPackId,
            EffectiveCriticalCurrentDensityMaPerCm2 = effectiveJccd,
            CurrentDensitySafetyRatio = Math.Round(currentSafetyRatio, 3),
            CrackTipStressIntensityFactorMpaM12 = stressIntensityKi,
            StackPressureToYieldRatio = Math.Round(pressureToYieldRatio, 2),
            IsStackPressureWithinOptimalEnvelope = isPressureOk,
            IsDendritePropagationSuppressed = isDendriteSuppressed,
            IsFastChargeSafe = isFastChargeSafe,
            BatteryIntegrityRegime = regime,
            PressureManagementProtocol = protocol
        };
    }
}
