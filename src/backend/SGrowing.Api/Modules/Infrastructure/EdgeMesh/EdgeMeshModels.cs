using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.EdgeMesh;

public enum EdgePoPRegion
{
    NorthAmericaEast = 0,
    NorthAmericaWest = 1,
    EuropeWest = 2,
    AsiaEast = 3,
    Oceania = 4,
    SouthAmerica = 5
}

public record EdgePoPNode(
    string PopCode,
    string LocationName,
    EdgePoPRegion Region,
    double P95LatencyMs,
    double CpuLoadPct,
    int ActiveIsolatesCount,
    bool IsHealthy
);

public record ServerlessEdgeFunction(
    string FunctionId,
    string FunctionName,
    string Runtime, // e.g. "V8-Isolate-ES2026", "Wasm-Rust-v1"
    int CanaryTrafficSplitPct, // 0 - 100%
    string DeploymentState,     // "Synchronized", "CanaryRolling", "PromotedGA"
    DateTime DeployedAt,
    string Sha256Hash
);

public record EdgeMeshOverview(
    int TotalPoPsCount,
    int HealthyPoPsCount,
    double GlobalP95LatencyMs,
    long GlobalRequestsPerSecond,
    List<EdgePoPNode> Nodes,
    List<ServerlessEdgeFunction> DeployedFunctions
);

public record DeployEdgeFunctionCommand(
    string FunctionName,
    string Runtime,
    string SourceCode,
    int CanarySplitPct
);

public record UpdateCanarySplitCommand(
    string FunctionId,
    int NewTrafficSplitPct
);

public record EdgeDeploymentResult(
    string FunctionId,
    string FunctionName,
    int CanaryTrafficSplitPct,
    string DeploymentStatus,
    string Message
);
