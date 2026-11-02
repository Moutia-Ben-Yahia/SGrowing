using System;

namespace SGrowing.Api.Modules.AI.NeuromorphicStdpSynapticEnergy;

public sealed record NeuromorphicCoreTelemetry(
    string CoreClusterId,
    int ActiveNeuronCount,
    int ActiveSynapseCount,
    double ObservedMeanFiringRateHz,
    double PeakNeuronFiringRateHz,
    double LtpPotentiationMagnitudeA,
    double LtdDepressionMagnitudeA,
    double EnergyPerSpikeFemtojoules,
    double StaticLeakPowerMicrowatts,
    double SynapticBurstSynchronyIndex);

public sealed record NeuromorphicCoreAnalysisResult(
    string CoreClusterId,
    double TotalPowerDissipationMilliwatts,
    double EffectiveEnergyPerSynapticEventFemtojoules,
    double PotentiationToDepressionRatio,
    string NeuromorphicRegimeStatus,
    string HomeostaticRegulationAction,
    bool IsEnergyEfficient,
    DateTimeOffset Timestamp);
