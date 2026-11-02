using System;

namespace SGrowing.Api.Modules.AI.NeuromorphicStdpSynapticEnergy;

public interface INeuromorphicStdpSynapticEnergyEngine
{
    NeuromorphicCoreAnalysisResult AnalyzeNeuromorphicCore(NeuromorphicCoreTelemetry telemetry);
}

public sealed class NeuromorphicStdpSynapticEnergyEngine : INeuromorphicStdpSynapticEnergyEngine
{
    public NeuromorphicCoreAnalysisResult AnalyzeNeuromorphicCore(NeuromorphicCoreTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.ActiveNeuronCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Neuron count must be strictly positive.");
        }

        double meanFiringRate = Math.Max(0.1, telemetry.ObservedMeanFiringRateHz);
        double totalSpikesPerSec = telemetry.ActiveNeuronCount * meanFiringRate;

        // Dynamic event power: Spikes/sec * Energy/spike
        double energyPerSpikeJoules = Math.Max(1.0, telemetry.EnergyPerSpikeFemtojoules) * 1e-15;
        double dynamicPowerWatts = totalSpikesPerSec * energyPerSpikeJoules;
        double dynamicPowerMilliwatts = dynamicPowerWatts * 1000.0;

        double staticLeakMilliwatts = telemetry.StaticLeakPowerMicrowatts / 1000.0;
        double totalPowerMilliwatts = dynamicPowerMilliwatts + staticLeakMilliwatts;

        // Effective energy per synaptic event
        double totalSynapses = Math.Max(1.0, telemetry.ActiveSynapseCount);
        double synapticFanOut = totalSynapses / telemetry.ActiveNeuronCount;
        double effectiveEnergyPerSynapticEventFj = telemetry.EnergyPerSpikeFemtojoules / synapticFanOut;

        // Potentiation to depression ratio
        double ltd = Math.Max(1e-6, telemetry.LtdDepressionMagnitudeA);
        double stdpRatio = telemetry.LtpPotentiationMagnitudeA / ltd;

        string regime;
        string action;
        bool isEfficient;

        if (telemetry.ObservedMeanFiringRateHz > 80.0 || telemetry.SynapticBurstSynchronyIndex > 0.75)
        {
            regime = "EPILEPTIFORM_BURST_SYNCHRONY";
            action = "INJECT_INHIBITORY_GABA_SYNAPTIC_CLAMP_AND_REDUCE_VTH";
            isEfficient = false;
        }
        else if (stdpRatio > 1.60 || stdpRatio < 0.60)
        {
            regime = "SYNAPTIC_PLASTICITY_DRIFT";
            action = "ADJUST_HOMEOSTATIC_DECAY_TIME_CONSTANT";
            isEfficient = true;
        }
        else
        {
            regime = "NEUROMORPHIC_LOW_POWER_OPTIMAL";
            action = "MAINTAIN_ASYNCHRONOUS_EVENT_DRIVEN_SPARSE_ROUTING";
            isEfficient = true;
        }

        return new NeuromorphicCoreAnalysisResult(
            telemetry.CoreClusterId,
            Math.Round(totalPowerMilliwatts, 4),
            Math.Round(effectiveEnergyPerSynapticEventFj, 4),
            Math.Round(stdpRatio, 3),
            regime,
            action,
            isEfficient,
            DateTimeOffset.UtcNow);
    }
}
