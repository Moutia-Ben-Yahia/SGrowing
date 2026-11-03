using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Infrastructure.EdgeWasmMesh;

public sealed class EdgeWasmMeshEngine
{
    private readonly ConcurrentDictionary<string, EdgeWasmModuleRecord> _modules = new();
    private readonly ConcurrentBag<EdgeInvocationMetricRecord> _invocations = new();

    public EdgeWasmMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var m1 = new EdgeWasmModuleRecord(
            "WASM-MOD-GEO-01",
            "edge-geo-personalization-rewrite",
            "CLOUDFLARE_WORKERS",
            48520, // ~48KB compact Wasm
            1.2,   // 1.2ms cold start
            310,   // 310 PoPs globally
            "ACTIVE_GLOBAL"
        );

        var m2 = new EdgeWasmModuleRecord(
            "WASM-MOD-AUTH-02",
            "edge-jwt-uid2-token-validator",
            "FASTLY_COMPUTE",
            64200,
            1.8,
            240,
            "ACTIVE_GLOBAL"
        );

        _modules[m1.ModuleId] = m1;
        _modules[m2.ModuleId] = m2;

        var i1 = new EdgeInvocationMetricRecord(
            "INV-LHR-8812",
            m1.ModuleId,
            "LHR",
            2.4,
            128,
            200,
            DateTime.UtcNow.AddSeconds(-15)
        );

        _invocations.Add(i1);
    }

    public EdgeWasmOverview GetOverview()
    {
        var modList = _modules.Values.ToList();
        var invList = _invocations.ToList();
        var p99 = invList.Count > 0 ? invList.Max(i => i.ExecutionDurationMs) : 3.4;

        return new EdgeWasmOverview(
            modList.Count,
            310,
            p99,
            18500000, // 18.5M edge invocations
            modList,
            invList
        );
    }

    public EdgeWasmModuleRecord DeployModule(DeployWasmModuleRequest request)
    {
        var modId = $"WASM-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var module = new EdgeWasmModuleRecord(
            modId,
            request.ModuleName,
            request.EdgeProvider,
            request.BytecodeSizeBytes,
            1.5,
            310,
            "ACTIVE_GLOBAL"
        );

        _modules[modId] = module;
        return module;
    }

    public EdgeInvocationMetricRecord InvokeEdge(InvokeEdgeFunctionRequest request)
    {
        var invId = $"INV-{request.EdgeLocationIata}-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var metric = new EdgeInvocationMetricRecord(
            invId,
            request.ModuleId,
            request.EdgeLocationIata,
            2.8,
            140,
            200,
            DateTime.UtcNow
        );

        _invocations.Add(metric);
        return metric;
    }
}
