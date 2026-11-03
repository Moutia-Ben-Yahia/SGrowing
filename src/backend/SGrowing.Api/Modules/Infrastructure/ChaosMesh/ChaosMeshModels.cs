using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.ChaosMesh;

public enum FaultType
{
    LatencyInjection,
    PacketLossSpike,
    RegionFailoverSimulation,
    MemoryPressureSpike
}

public enum ExperimentState
{
    Idle,
    InjectingFault,
    RecoveringSelfHealing,
    HaltedEmergencyStop
}

public record ChaosExperiment(
    string ExperimentId,
    string TargetServiceName,
    string Region,
    FaultType Fault,
    int MagnitudePercentage, // e.g. 20% packet drop or +150ms latency
    ExperimentState State,
    double CurrentLatencyMs,
    double SteadyStateLatencyMs,
    DateTime StartedAtUtc
);

public record InjectFaultRequest(
    string TargetServiceName,
    string Region,
    FaultType Fault,
    int MagnitudePercentage
);

public record HaltExperimentRequest(
    string ExperimentId
);

public record ChaosExperimentResult(
    string ExperimentId,
    ExperimentState State,
    string LogSummary
);
