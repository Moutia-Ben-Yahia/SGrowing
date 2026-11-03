using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.ChaosMesh;

public interface IChaosMeshEngine
{
    List<ChaosExperiment> GetExperiments();
    ChaosExperiment InjectFault(InjectFaultRequest request);
    ChaosExperimentResult HaltExperiment(HaltExperimentRequest request);
}

public class ChaosMeshEngine : IChaosMeshEngine
{
    private readonly ConcurrentDictionary<string, ChaosExperiment> _experiments = new(StringComparer.OrdinalIgnoreCase);

    public ChaosMeshEngine()
    {
        // Seed chaos experiments
        var e1 = new ChaosExperiment("exp_chaos_01", "api-gateway", "us-east-1", FaultType.LatencyInjection, 40, ExperimentState.InjectingFault, 185.0, 22.0, DateTime.UtcNow.AddMinutes(-12));
        var e2 = new ChaosExperiment("exp_chaos_02", "shard-broker", "eu-central-1", FaultType.PacketLossSpike, 15, ExperimentState.RecoveringSelfHealing, 34.0, 18.0, DateTime.UtcNow.AddMinutes(-5));
        var e3 = new ChaosExperiment("exp_chaos_03", "auth-vault", "ap-southeast-1", FaultType.RegionFailoverSimulation, 100, ExperimentState.Idle, 14.0, 14.0, DateTime.UtcNow.AddHours(-1));

        _experiments[e1.ExperimentId] = e1;
        _experiments[e2.ExperimentId] = e2;
        _experiments[e3.ExperimentId] = e3;
    }

    public List<ChaosExperiment> GetExperiments() => _experiments.Values.OrderBy(e => e.ExperimentId).ToList();

    public ChaosExperiment InjectFault(InjectFaultRequest request)
    {
        var id = $"exp_chaos_{Guid.NewGuid().ToString("N")[..6]}";
        var exp = new ChaosExperiment(
            ExperimentId: id,
            TargetServiceName: request.TargetServiceName,
            Region: request.Region,
            Fault: request.Fault,
            MagnitudePercentage: request.MagnitudePercentage,
            State: ExperimentState.InjectingFault,
            CurrentLatencyMs: 145.0 + (request.MagnitudePercentage * 1.5),
            SteadyStateLatencyMs: 20.0,
            StartedAtUtc: DateTime.UtcNow
        );

        _experiments[id] = exp;
        return exp;
    }

    public ChaosExperimentResult HaltExperiment(HaltExperimentRequest request)
    {
        if (!_experiments.TryGetValue(request.ExperimentId, out var existing))
        {
            throw new KeyNotFoundException($"Experiment {request.ExperimentId} not found.");
        }

        var updated = existing with
        {
            State = ExperimentState.HaltedEmergencyStop,
            CurrentLatencyMs = existing.SteadyStateLatencyMs
        };

        _experiments[request.ExperimentId] = updated;

        return new ChaosExperimentResult(
            request.ExperimentId,
            ExperimentState.HaltedEmergencyStop,
            $"Fault injection halted on service '{existing.TargetServiceName}' ({existing.Region}). System latency restored to steady state ({existing.SteadyStateLatencyMs:F1}ms)."
        );
    }
}
