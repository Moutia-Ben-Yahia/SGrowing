using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Infrastructure.EdgeMesh;

public interface IEdgeMeshEngine
{
    EdgeMeshOverview GetOverview();
    EdgeDeploymentResult DeployFunction(DeployEdgeFunctionCommand command);
    EdgeDeploymentResult UpdateCanarySplit(UpdateCanarySplitCommand command);
    EdgePoPNode TogglePoPHealth(string popCode);
}

public class EdgeMeshEngine : IEdgeMeshEngine
{
    private readonly ConcurrentDictionary<string, EdgePoPNode> _pops = new();
    private readonly ConcurrentDictionary<string, ServerlessEdgeFunction> _functions = new();

    public EdgeMeshEngine()
    {
        SeedNodesAndFunctions();
    }

    private void SeedNodesAndFunctions()
    {
        var n1 = new EdgePoPNode("IAD-01", "Northern Virginia (US-East)", EdgePoPRegion.NorthAmericaEast, 4.2, 28.5, 342, true);
        var n2 = new EdgePoPNode("SFO-02", "San Francisco (US-West)", EdgePoPRegion.NorthAmericaWest, 5.8, 32.1, 289, true);
        var n3 = new EdgePoPNode("FRA-01", "Frankfurt (Europe-Central)", EdgePoPRegion.EuropeWest, 6.1, 35.4, 310, true);
        var n4 = new EdgePoPNode("NRT-01", "Tokyo Narita (Asia-Northeast)", EdgePoPRegion.AsiaEast, 7.4, 29.8, 240, true);
        var n5 = new EdgePoPNode("SYD-01", "Sydney (Oceania)", EdgePoPRegion.Oceania, 11.2, 22.0, 180, true);
        var n6 = new EdgePoPNode("GRU-01", "São Paulo (South America)", EdgePoPRegion.SouthAmerica, 14.5, 26.4, 155, true);

        _pops[n1.PopCode] = n1;
        _pops[n2.PopCode] = n2;
        _pops[n3.PopCode] = n3;
        _pops[n4.PopCode] = n4;
        _pops[n5.PopCode] = n5;
        _pops[n6.PopCode] = n6;

        var f1 = new ServerlessEdgeFunction(
            FunctionId: "EDGE-FN-01",
            FunctionName: "Dynamic-Geo-Personalization-Gate",
            Runtime: "V8-Isolate-ES2026",
            CanaryTrafficSplitPct: 100,
            DeploymentState: "PromotedGA",
            DeployedAt: DateTime.UtcNow.AddDays(-3),
            Sha256Hash: "SHA256:7B94F3C8A0129"
        );

        var f2 = new ServerlessEdgeFunction(
            FunctionId: "EDGE-FN-02",
            FunctionName: "Zero-Trust-JWT-Token-Introspection",
            Runtime: "Wasm-Rust-v1",
            CanaryTrafficSplitPct: 50,
            DeploymentState: "CanaryRolling",
            DeployedAt: DateTime.UtcNow.AddHours(-4),
            Sha256Hash: "SHA256:4C82E11D9A773"
        );

        var f3 = new ServerlessEdgeFunction(
            FunctionId: "EDGE-FN-03",
            FunctionName: "CAPI-Payload-Transform-Streamer",
            Runtime: "V8-Isolate-ES2026",
            CanaryTrafficSplitPct: 10,
            DeploymentState: "CanaryRolling",
            DeployedAt: DateTime.UtcNow.AddMinutes(-30),
            Sha256Hash: "SHA256:1E99F82A33BC8"
        );

        _functions[f1.FunctionId] = f1;
        _functions[f2.FunctionId] = f2;
        _functions[f3.FunctionId] = f3;
    }

    public EdgeMeshOverview GetOverview()
    {
        var nodes = _pops.Values.OrderBy(n => n.P95LatencyMs).ToList();
        var healthy = nodes.Count(n => n.IsHealthy);
        var fns = _functions.Values.OrderByDescending(f => f.DeployedAt).ToList();
        var avgLatency = nodes.Count > 0 ? nodes.Average(n => n.P95LatencyMs) : 8.0;

        return new EdgeMeshOverview(
            TotalPoPsCount: nodes.Count,
            HealthyPoPsCount: healthy,
            GlobalP95LatencyMs: Math.Round(avgLatency, 1),
            GlobalRequestsPerSecond: 284500, // 284,500 req/s edge mesh volume
            Nodes: nodes,
            DeployedFunctions: fns
        );
    }

    public EdgeDeploymentResult DeployFunction(DeployEdgeFunctionCommand command)
    {
        var fnId = $"EDGE-FN-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(command.SourceCode));
        var hashStr = $"SHA256:{Convert.ToHexString(hashBytes)[..12]}";

        var fn = new ServerlessEdgeFunction(
            FunctionId: fnId,
            FunctionName: command.FunctionName,
            Runtime: command.Runtime,
            CanaryTrafficSplitPct: command.CanarySplitPct,
            DeploymentState: command.CanarySplitPct >= 100 ? "PromotedGA" : "CanaryRolling",
            DeployedAt: DateTime.UtcNow,
            Sha256Hash: hashStr
        );

        _functions[fnId] = fn;

        return new EdgeDeploymentResult(
            FunctionId: fnId,
            FunctionName: command.FunctionName,
            CanaryTrafficSplitPct: command.CanarySplitPct,
            DeploymentStatus: fn.DeploymentState,
            Message: $"Deployed to all 6 global Edge PoP isolates with {command.CanarySplitPct}% canary split."
        );
    }

    public EdgeDeploymentResult UpdateCanarySplit(UpdateCanarySplitCommand command)
    {
        if (!_functions.TryGetValue(command.FunctionId, out var existing))
        {
            throw new KeyNotFoundException($"Edge function {command.FunctionId} not found.");
        }

        var clamped = Math.Clamp(command.NewTrafficSplitPct, 0, 100);
        var state = clamped >= 100 ? "PromotedGA" : (clamped == 0 ? "Disabled" : "CanaryRolling");

        var updated = existing with
        {
            CanaryTrafficSplitPct = clamped,
            DeploymentState = state
        };

        _functions[command.FunctionId] = updated;

        return new EdgeDeploymentResult(
            FunctionId: command.FunctionId,
            FunctionName: existing.FunctionName,
            CanaryTrafficSplitPct: clamped,
            DeploymentStatus: state,
            Message: $"Canary traffic split updated to {clamped}%. Propagation synchronized across all global edge nodes."
        );
    }

    public EdgePoPNode TogglePoPHealth(string popCode)
    {
        if (!_pops.TryGetValue(popCode, out var existing))
        {
            throw new KeyNotFoundException($"Edge PoP {popCode} not found.");
        }

        var updated = existing with
        {
            IsHealthy = !existing.IsHealthy
        };

        _pops[popCode] = updated;
        return updated;
    }
}
