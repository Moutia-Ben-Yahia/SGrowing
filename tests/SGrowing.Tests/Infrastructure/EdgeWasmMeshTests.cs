using System;
using System.Linq;
using SGrowing.Api.Modules.Infrastructure.EdgeWasmMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class EdgeWasmMeshTests
{
    [Fact]
    public void GetOverview_ReturnsInitialModulesAndInvocations()
    {
        var engine = new EdgeWasmMeshEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalDeployedModulesCount >= 2);
        Assert.True(overview.GlobalEdgePoPsCount >= 200);
        Assert.True(overview.P99ExecutionLatencyMs > 0);
        Assert.NotEmpty(overview.Modules);
        Assert.NotEmpty(overview.RecentInvocations);
    }

    [Fact]
    public void DeployModule_DistributesWasmBytecodeAcrossPoPs()
    {
        var engine = new EdgeWasmMeshEngine();
        var request = new DeployWasmModuleRequest(
            "edge-header-rate-limit-check",
            "CLOUDFLARE_WORKERS",
            32400
        );

        var module = engine.DeployModule(request);

        Assert.NotNull(module);
        Assert.StartsWith("WASM-", module.ModuleId);
        Assert.Equal("edge-header-rate-limit-check", module.ModuleName);
        Assert.Equal("ACTIVE_GLOBAL", module.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Modules, m => m.ModuleId == module.ModuleId);
    }

    [Fact]
    public void InvokeEdge_SimulatesSubFiveMillisecondExecution()
    {
        var engine = new EdgeWasmMeshEngine();
        var request = new InvokeEdgeFunctionRequest(
            "WASM-MOD-GEO-01",
            "FRA",
            "{\"ip\": \"194.25.0.1\"}"
        );

        var metric = engine.InvokeEdge(request);

        Assert.NotNull(metric);
        Assert.StartsWith("INV-FRA-", metric.InvocationId);
        Assert.Equal("FRA", metric.EdgeLocationIata);
        Assert.Equal(200, metric.StatusCode);
        Assert.True(metric.ExecutionDurationMs < 5.0);
    }
}
