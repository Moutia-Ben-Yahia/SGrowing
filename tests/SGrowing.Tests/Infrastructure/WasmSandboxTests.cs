using SGrowing.Api.Modules.Infrastructure.WasmSandbox;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class WasmSandboxTests
{
    [Fact]
    public void GetOverview_ReportsWorkersLatencyAndCapabilities()
    {
        var engine = new WasmSandboxMeshEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalRunningMicroVmsCount >= 2);
        Assert.True(overview.AverageColdStartLatencyMs < 1.0); // Sub-millisecond cold start
        Assert.True(overview.ActiveTenantSandboxesCount >= 2);
        Assert.True(overview.TotalMemoryAllocatedMb >= 64);
        Assert.NotEmpty(overview.ActiveWorkers);
        Assert.NotEmpty(overview.SecurityCapabilities);
    }

    [Fact]
    public void DeployWorker_SpawnsMicroVmWithSubMillisecondColdStart()
    {
        var engine = new WasmSandboxMeshEngine();
        var request = new DeployWasmWorkerRequest(
            "tenant-analytics-engine",
            "hyperloglog-cardinality.wasm",
            128,
            10.0,
            "WASMTIME_AOT"
        );

        var worker = engine.DeployWorker(request);

        Assert.NotNull(worker);
        Assert.StartsWith("WASM-VM-", worker.WorkerId);
        Assert.Equal("tenant-analytics-engine", worker.TenantId);
        Assert.Equal("hyperloglog-cardinality.wasm", worker.ModuleName);
        Assert.Equal("RUNNING", worker.Status);
        Assert.True(worker.ColdStartLatencyMs < 1.0);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveWorkers, w => w.WorkerId == worker.WorkerId);
    }

    [Fact]
    public void TerminateWorker_TransitionsStateToTerminated()
    {
        var engine = new WasmSandboxMeshEngine();
        var request = new TerminateWasmWorkerRequest("WASM-VM-001");

        var terminated = engine.TerminateWorker(request);

        Assert.NotNull(terminated);
        Assert.Equal("WASM-VM-001", terminated.WorkerId);
        Assert.Equal("TERMINATED", terminated.Status);
    }
}
