namespace SGrowing.Api.Modules.Quantum.DualSpeciesNeutralAtomTweezers;

public interface IDualSpeciesTweezersEngine
{
    DualSpeciesTweezersEvaluation AnalyzeTweezers(DualSpeciesTweezersTelemetry telemetry);
}

public class DualSpeciesTweezersEngine : IDualSpeciesTweezersEngine
{
    public DualSpeciesTweezersEvaluation AnalyzeTweezers(DualSpeciesTweezersTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Blockade margin = R_blockade / R_interatomic
        double rDist = Math.Max(0.1, telemetry.InteratomicDistanceMicrons);
        double blockadeMargin = telemetry.RydbergBlockadeRadiusMicrons / rDist;

        // Shuffling survival over typical 50 hops: (1 - loss/100)^50
        double survival = Math.Pow(1.0 - (telemetry.AtomLossRatePerTweezerHopPercent / 100.0), 50) * 100.0;

        bool isBlockadeEnforced = blockadeMargin >= 1.25 && telemetry.InterSpeciesCnotGateFidelityPercent >= 98.8;
        bool isLossAcceptable = telemetry.AtomLossRatePerTweezerHopPercent <= 0.025;
        bool isCrosstalkSuppressed = telemetry.MidCircuitImagingCrosstalkScatterPercent <= 0.05;

        if (blockadeMargin < 1.15)
        {
            regimes.Add("RYDBERG_BLOCKADE_BREAKDOWN_LEAKAGE");
            mitigations.Add("Increase laser Rabi frequency or excite to higher principal quantum number n=75 to expand inter-species blockade radius.");
        }

        if (!isLossAcceptable)
        {
            regimes.Add("TWEEZER_REARRANGEMENT_ATOMIC_LOSS_SPIKE");
            mitigations.Add("Increase acousto-optic deflector (AOD) tweezer potential depth during transfer and optimize adiabatic ramp trajectories.");
        }

        if (!isCrosstalkSuppressed)
        {
            regimes.Add("INTER_SPECIES_FLUORESCENCE_CROSSTALK");
            mitigations.Add("Engage dual-band narrow notch optical filters and shift readout laser wavelength outside spectator absorption profiles.");
        }

        bool isReady = isBlockadeEnforced && isLossAcceptable && isCrosstalkSuppressed &&
                       telemetry.DefectFreeAssemblySuccessRatePercent >= 95.0;

        return new DualSpeciesTweezersEvaluation
        {
            TweezerArrayUnitId = telemetry.TweezerArrayUnitId,
            BlockadeRadiusMarginRatio = Math.Round(blockadeMargin, 2),
            ShufflingSurvivalProbabilityPercent = Math.Round(survival, 2),
            IsRydbergBlockadeEnforced = isBlockadeEnforced,
            IsShufflingLossAcceptable = isLossAcceptable,
            IsSpectatorCrosstalkSuppressed = isCrosstalkSuppressed,
            IsDualSpeciesQubitArrayReady = isReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
