namespace SGrowing.Api.Modules.Quantum.SiliconSpinQubitExchange;

public interface ISiliconSpinQubitEngine
{
    SiliconSpinQubitEvaluation AnalyzeSpinQubit(SiliconSpinQubitTelemetry telemetry);
}

public class SiliconSpinQubitEngine : ISiliconSpinQubitEngine
{
    public SiliconSpinQubitEvaluation AnalyzeSpinQubit(SiliconSpinQubitTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // J(eps) = (4 * tc^2 / U) * 1 / (1 - (eps / U)^2)
        // Convert units: tc in ueV, U in meV = 1000 ueV.
        double tc = telemetry.InterDotTunnelCouplingMicroElectronVolts;
        double uUev = telemetry.OnSiteChargingEnergyMilliElectronVolts * 1000.0;
        double epsUev = telemetry.DetuningBiasMillivolts * 1000.0;

        double detuningRatio = Math.Clamp(epsUev / Math.Max(100.0, uUev), -0.85, 0.85);
        double jEnergyUev = (4.0 * tc * tc / uUev) / (1.0 - detuningRatio * detuningRatio);
        // Planck constant h = 4.135667e-9 ueV / MHz
        double jMhz = jEnergyUev / 4.135667e-3;

        // Derivative dJ/deps = (8 * tc^2 * eps / U^2) / (1 - (eps/U)^2)^2
        double dJdeps = (8.0 * tc * tc * epsUev / (uUev * uUev)) / Math.Pow(1.0 - detuningRatio * detuningRatio, 2.0);
        // Dephasing rate from charge noise: Gamma_phi = sqrt(2)*pi * |dJ/deps| * sigma_eps (in kHz)
        double chargeNoiseDephasingKhz = Math.Sqrt(2.0) * Math.PI * Math.Abs(dJdeps) * (telemetry.RmsDetuningChargeNoiseMicrovolts / 4.135667e-3) * 1000.0;

        // Two-qubit gate fidelity
        double baseFidelity = 99.85;
        // Incoherence penalty from gate duration vs dephasing rate
        double dephasingInfidelity = (chargeNoiseDephasingKhz * 1e3) * (telemetry.ExchangeGateDurationNanoseconds * 1e-9) * 100.0;
        if (telemetry.ValleySplittingEnergyMicroElectronVolts < 100.0)
        {
            baseFidelity -= (100.0 - telemetry.ValleySplittingEnergyMicroElectronVolts) * 0.08;
        }
        double swapFidelity = Math.Clamp(baseFidelity - dephasingInfidelity, 50.0, 99.95);

        bool isAtSweetSpot = Math.Abs(telemetry.DetuningBiasMillivolts) <= 0.15 && chargeNoiseDephasingKhz <= 1500.0;
        bool isValleyAdequate = telemetry.ValleySplittingEnergyMicroElectronVolts >= 90.0;
        bool isReadoutOptimal = telemetry.PauliSpinBlockadeReadoutFidelityPercent >= 98.0;

        if (!isValleyAdequate)
        {
            regimes.Add("VALLEY_SPLITTING_COLLAPSE_DEFICIT");
            mitigations.Add("Increase vertical electric field confinement gate bias to enhance Si/SiGe interface valley splitting Ev >= 120 ueV.");
        }

        if (!isAtSweetSpot)
        {
            regimes.Add("CHARGE_NOISE_EXCHANGE_DEPHASING");
            mitigations.Add("Tune detuning bias eps strictly to symmetric operating point (dJ/deps -> 0) and filter high-frequency gate noise.");
        }

        if (!isReadoutOptimal)
        {
            regimes.Add("PAULI_SPIN_BLOCKADE_LATCHING_FAILURE");
            mitigations.Add("Optimize inter-dot tunnel rate at the (1,1)-(0,2) charge transition to maximize singlet-triplet Pauli spin blockade latching.");
        }

        bool isLogicReady = isAtSweetSpot && isValleyAdequate && isReadoutOptimal && swapFidelity >= 99.0;

        return new SiliconSpinQubitEvaluation
        {
            DotPairId = telemetry.DotPairId,
            CalculatedExchangeCouplingMhz = Math.Round(jMhz, 2),
            ChargeNoiseDephasingRateKhz = Math.Round(chargeNoiseDephasingKhz, 2),
            TwoQubitSwapGateFidelityPercent = Math.Round(swapFidelity, 2),
            IsAtExchangeSweetSpot = isAtSweetSpot,
            IsValleySplittingAdequate = isValleyAdequate,
            IsPauliBlockadeReadoutOptimal = isReadoutOptimal,
            IsTwoQubitLogicReady = isLogicReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
