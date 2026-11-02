using System;

namespace SGrowing.Api.Modules.Quantum.NvCenterNuclearSpinRegister;

public sealed class NvCenterNuclearSpinRegisterEngine : INvCenterNuclearSpinRegisterEngine
{
    private const double EslacResonanceGauss = 510.0; // Excited-State Level Anti-Crossing field

    public NvCenterNuclearSpinAnalysisResult AnalyzeNuclearRegister(NvCenterNuclearSpinTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.DiamondRegisterId))
        {
            throw new ArgumentException("Diamond register ID must be provided.", nameof(telemetry));
        }

        if (telemetry.AxialMagneticFieldGauss <= 0 || telemetry.GreenLaserOpticalPumpingPowerMilliwatts < 0 ||
            telemetry.NuclearQubitCoherenceT2Seconds < 0)
        {
            throw new ArgumentException("Field, laser power, and coherence time cannot be negative.", nameof(telemetry));
        }

        var eslacDetuning = Math.Abs(telemetry.AxialMagneticFieldGauss - EslacResonanceGauss);
        var fieldFidelityFactor = Math.Exp(-eslacDetuning / 50.0);

        var measuredPol = Math.Clamp(telemetry.MeasuredNuclearSpinPolarizationPercent, 0.0, 100.0);
        var effectiveFidelity = measuredPol * fieldFidelityFactor;

        var spins = Math.Max(1, telemetry.CoupledNuclearSpinsCount);
        var memoryQFactor = spins * telemetry.NuclearQubitCoherenceT2Seconds * (effectiveFidelity / 100.0);

        string regime;
        string guidance;
        bool isFaultTolerant = measuredPol >= 90.0 &&
                               telemetry.NuclearQubitCoherenceT2Seconds >= 0.8 &&
                               eslacDetuning <= 30.0;

        if (telemetry.AxialMagneticFieldGauss < 450.0 || telemetry.AxialMagneticFieldGauss > 570.0)
        {
            regime = "OFF_ESLAC_MAGNETIC_DETUNING_LOW_POLARIZATION_HAZARD";
            guidance = $"Magnetic field ({telemetry.AxialMagneticFieldGauss:F1} G) is far from the 510 G ESLAC resonance. Hyperfine state mixing is suppressed; retune electromagnet to 510 G.";
            isFaultTolerant = false;
        }
        else if (telemetry.NuclearQubitCoherenceT2Seconds < 0.1)
        {
            regime = "PARAMAGNETIC_BATH_DEPHASING_LIMIT";
            guidance = $"Nuclear spin coherence ({telemetry.NuclearQubitCoherenceT2Seconds:F2} s) is suppressed by proximal 13C or N-V bath dipolar fluctuations. Apply XY8/CPMG dynamical decoupling sequences.";
            isFaultTolerant = false;
        }
        else if (telemetry.GreenLaserOpticalPumpingPowerMilliwatts < 2.0)
        {
            regime = "INSUFFICIENT_OPTICAL_PUMPING_POWER";
            guidance = $"Green laser excitation ({telemetry.GreenLaserOpticalPumpingPowerMilliwatts:F1} mW) insufficient for saturation. Increase 532 nm pump power to achieve rapid electron-nuclear spin flip-flops.";
            isFaultTolerant = false;
        }
        else if (measuredPol >= 95.0 && telemetry.NuclearQubitCoherenceT2Seconds >= 1.0)
        {
            regime = "HIGH_FIDELITY_HYPERPOLARIZED_QUANTUM_MEMORY_REGIME";
            guidance = $"Nuclear spin register hyperpolarized to {measuredPol:F1}% at ESLAC with long coherence ({telemetry.NuclearQubitCoherenceT2Seconds:F2} s). Ideal multi-qubit quantum node memory.";
        }
        else
        {
            regime = "NOMINAL_NUCLEAR_REGISTER_OPERATION_REGIME";
            guidance = $"Nuclear spin polarization operating nominally. Memory quality factor is {memoryQFactor:F2}.";
        }

        return new NvCenterNuclearSpinAnalysisResult(
            telemetry.DiamondRegisterId,
            Math.Round(eslacDetuning, 2),
            Math.Round(effectiveFidelity, 2),
            Math.Round(memoryQFactor, 3),
            regime,
            guidance,
            isFaultTolerant
        );
    }
}
