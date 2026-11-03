using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.ChaosMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class ChaosMeshTests
{
    private readonly ChaosMeshEngine _engine = new();

    [Fact]
    public void GetExperiments_ReturnsSeededExperiments()
    {
        var experiments = _engine.GetExperiments();

        experiments.Should().NotBeNull();
        experiments.Should().HaveCount(3);
        experiments.Should().Contain(e => e.ExperimentId == "exp_chaos_01" && e.State == ExperimentState.InjectingFault);
        experiments.Should().Contain(e => e.ExperimentId == "exp_chaos_02" && e.State == ExperimentState.RecoveringSelfHealing);
    }

    [Fact]
    public void InjectFault_CreatesNewActiveExperiment()
    {
        var request = new InjectFaultRequest("billing-service", "eu-west-1", FaultType.LatencyInjection, 50);

        var exp = _engine.InjectFault(request);

        exp.Should().NotBeNull();
        exp.TargetServiceName.Should().Be("billing-service");
        exp.Region.Should().Be("eu-west-1");
        exp.Fault.Should().Be(FaultType.LatencyInjection);
        exp.State.Should().Be(ExperimentState.InjectingFault);
        exp.CurrentLatencyMs.Should().BeGreaterThan(exp.SteadyStateLatencyMs);

        _engine.GetExperiments().Should().Contain(e => e.ExperimentId == exp.ExperimentId);
    }

    [Fact]
    public void HaltExperiment_RestoresSteadyStateLatencyAndHalts()
    {
        var expId = "exp_chaos_01";
        var result = _engine.HaltExperiment(new HaltExperimentRequest(expId));

        result.Should().NotBeNull();
        result.State.Should().Be(ExperimentState.HaltedEmergencyStop);
        result.LogSummary.Should().Contain("restored to steady state");

        var exp = _engine.GetExperiments().Find(e => e.ExperimentId == expId)!;
        exp.State.Should().Be(ExperimentState.HaltedEmergencyStop);
        exp.CurrentLatencyMs.Should().Be(exp.SteadyStateLatencyMs);
    }

    [Fact]
    public void HaltExperiment_ThrowsWhenExperimentNotFound()
    {
        var action = () => _engine.HaltExperiment(new HaltExperimentRequest("invalid_exp_id"));

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }
}
