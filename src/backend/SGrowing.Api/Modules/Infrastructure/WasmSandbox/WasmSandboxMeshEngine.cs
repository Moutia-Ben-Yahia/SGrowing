using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.WasmSandbox;

public class WasmSandboxMeshEngine
{
    private readonly ConcurrentDictionary<string, WasmMicroVmWorker> _workers = new();
    private readonly ConcurrentBag<WasmSecurityCapability> _capabilities = new();

    public WasmSandboxMeshEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var w1 = new WasmMicroVmWorker(
            "WASM-VM-001",
            "tenant-fintech-core",
            "fx-arbitrage-pricing.wasm",
            64,
            50.0,
            "WASMTIME_AOT",
            0.48, // 480 microseconds
            "RUNNING",
            DateTime.UtcNow.AddMinutes(-40)
        );

        var w2 = new WasmMicroVmWorker(
            "WASM-VM-002",
            "tenant-adtech-rtb",
            "contextual-bid-evaluator.wasm",
            32,
            25.0,
            "WASMER_LLVM",
            0.35, // 350 microseconds
            "RUNNING",
            DateTime.UtcNow.AddMinutes(-22)
        );

        _workers[w1.WorkerId] = w1;
        _workers[w2.WorkerId] = w2;

        _capabilities.Add(new WasmSecurityCapability(
            "CAP-001",
            "WASM-VM-001",
            "^api\\.marketdata\\.enterprise\\.corp$",
            false,
            true,
            "STRICT_ZERO_TRUST"
        ));

        _capabilities.Add(new WasmSecurityCapability(
            "CAP-002",
            "WASM-VM-002",
            "^dsp-exchange\\.adtech\\.internal$",
            false,
            false,
            "RESTRICTED_EGRESS"
        ));
    }

    public WasmSandboxOverview GetOverview()
    {
        var workersList = _workers.Values.ToList();
        var capList = _capabilities.ToList();

        var running = workersList.Where(w => w.Status == "RUNNING").ToList();
        double avgColdStart = running.Count > 0 ? running.Average(w => w.ColdStartLatencyMs) : 0.45;
        double totalMem = running.Sum(w => w.MemoryLimitMb);

        return new WasmSandboxOverview(
            running.Count,
            Math.Round(avgColdStart, 2),
            running.Select(w => w.TenantId).Distinct().Count(),
            totalMem,
            workersList,
            capList
        );
    }

    public WasmMicroVmWorker DeployWorker(DeployWasmWorkerRequest request)
    {
        var workerId = $"WASM-VM-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var worker = new WasmMicroVmWorker(
            workerId,
            request.TenantId,
            request.ModuleName,
            request.MemoryLimitMb,
            request.ExecutionDurationLimitMs,
            request.IsolationRuntime,
            0.42,
            "RUNNING",
            DateTime.UtcNow
        );

        _workers[workerId] = worker;

        // Auto grant strict zero trust capability
        _capabilities.Add(new WasmSecurityCapability(
            $"CAP-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            workerId,
            "^api\\.tenant\\.internal$",
            false,
            false,
            "STRICT_ZERO_TRUST"
        ));

        return worker;
    }

    public WasmMicroVmWorker TerminateWorker(TerminateWasmWorkerRequest request)
    {
        if (_workers.TryGetValue(request.WorkerId, out var existing))
        {
            var terminated = existing with { Status = "TERMINATED" };
            _workers[request.WorkerId] = terminated;
            return terminated;
        }

        var fallback = new WasmMicroVmWorker(
            request.WorkerId,
            "unknown-tenant",
            "unknown.wasm",
            32,
            10.0,
            "WASMTIME_AOT",
            0.5,
            "TERMINATED",
            DateTime.UtcNow
        );
        _workers[request.WorkerId] = fallback;
        return fallback;
    }
}
