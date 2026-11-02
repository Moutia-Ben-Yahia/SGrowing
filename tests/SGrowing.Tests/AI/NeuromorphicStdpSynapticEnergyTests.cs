using System;
using SGrowing.Api.Modules.AI.NeuromorphicStdpSynapticEnergy;
using Xunit;

namespace SGrowing.Tests.AI;

public sealed class NeuromorphicStdpSynapticEnergyTests
{
    private readonly NeuromorphicStdpSynapticEnergyEngine _engine = new();

    [Fact]
    public void AnalyzeNeuromorphicCore_NominalSparseFiring_MaintainsLowPowerOptimal()
    {
        var telemetry = new NeuromorphicCoreTelemetry(
            CoreClusterId: "LOIHI-2-CORE-CLUSTER-01",
            ActiveNeuronCount: 1024,
            ActiveSynapseCount: 1_048_576,
            ObservedMeanFiringRateHz: 16.5,
            PeakNeuronFiringRateHz: 35.0,
            LtpPotentiationMagnitudeA: 0.010,
            LtdDepressionMagnitudeA: 0.009,
            EnergyPerSpikeFemtojoules: 32.0,
            StaticLeakPowerMicrowatts: 85.0,
            SynapticBurstSynchronyIndex: 0.18);

        var result = _engine.AnalyzeNeuromorphicCore(telemetry);

        Assert.Equal("NEUROMORPHIC_LOW_POWER_OPTIMAL", result.NeuromorphicRegimeStatus);
        Assert.Equal("MAINTAIN_ASYNCHRONOUS_EVENT_DRIVEN_SPARSE_ROUTING", result.HomeostaticRegulationAction);
        Assert.True(result.IsEnergyEfficient);
        Assert.True(result.TotalPowerDissipationMilliwatts < 1.0);
    }

    [Fact]
    public void AnalyzeNeuromorphicCore_EpilepticBurstSynchrony_TriggersGabaInhibitoryClamp()
    {
        var telemetry = new NeuromorphicCoreTelemetry(
            CoreClusterId: "LOIHI-2-CORE-CLUSTER-02",
            ActiveNeuronCount: 1024,
            ActiveSynapseCount: 1_048_576,
            ObservedMeanFiringRateHz: 92.0, // > 80.0 Hz
            PeakNeuronFiringRateHz: 150.0,
            LtpPotentiationMagnitudeA: 0.012,
            LtdDepressionMagnitudeA: 0.010,
            EnergyPerSpikeFemtojoules: 35.0,
            StaticLeakPowerMicrowatts: 120.0,
            SynapticBurstSynchronyIndex: 0.40);

        var result = _engine.AnalyzeNeuromorphicCore(telemetry);

        Assert.Equal("EPILEPTIFORM_BURST_SYNCHRONY", result.NeuromorphicRegimeStatus);
        Assert.Equal("INJECT_INHIBITORY_GABA_SYNAPTIC_CLAMP_AND_REDUCE_VTH", result.HomeostaticRegulationAction);
        Assert.False(result.IsEnergyEfficient);
    }

    [Fact]
    public void AnalyzeNeuromorphicCore_HighSynchronyIndex_TriggersClamp()
    {
        var telemetry = new NeuromorphicCoreTelemetry(
            CoreClusterId: "LOIHI-2-CORE-CLUSTER-03",
            ActiveNeuronCount: 1024,
            ActiveSynapseCount: 1_048_576,
            ObservedMeanFiringRateHz: 45.0,
            PeakNeuronFiringRateHz: 60.0,
            LtpPotentiationMagnitudeA: 0.011,
            LtdDepressionMagnitudeA: 0.010,
            EnergyPerSpikeFemtojoules: 30.0,
            StaticLeakPowerMicrowatts: 95.0,
            SynapticBurstSynchronyIndex: 0.82); // > 0.75

        var result = _engine.AnalyzeNeuromorphicCore(telemetry);

        Assert.Equal("EPILEPTIFORM_BURST_SYNCHRONY", result.NeuromorphicRegimeStatus);
        Assert.False(result.IsEnergyEfficient);
    }

    [Fact]
    public void AnalyzeNeuromorphicCore_AsymmetricLtpDrift_AdjustsDecayTimeConstant()
    {
        // A+ = 0.019 vs A- = 0.010 -> ratio = 1.90 > 1.60
        var telemetry = new NeuromorphicCoreTelemetry(
            CoreClusterId: "LOIHI-2-CORE-CLUSTER-04",
            ActiveNeuronCount: 1024,
            ActiveSynapseCount: 1_048_576,
            ObservedMeanFiringRateHz: 20.0,
            PeakNeuronFiringRateHz: 40.0,
            LtpPotentiationMagnitudeA: 0.019,
            LtdDepressionMagnitudeA: 0.010,
            EnergyPerSpikeFemtojoules: 30.0,
            StaticLeakPowerMicrowatts: 90.0,
            SynapticBurstSynchronyIndex: 0.25);

        var result = _engine.AnalyzeNeuromorphicCore(telemetry);

        Assert.Equal("SYNAPTIC_PLASTICITY_DRIFT", result.NeuromorphicRegimeStatus);
        Assert.Equal("ADJUST_HOMEOSTATIC_DECAY_TIME_CONSTANT", result.HomeostaticRegulationAction);
        Assert.True(result.IsEnergyEfficient);
        Assert.True(result.PotentiationToDepressionRatio > 1.60);
    }
}
