using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.EdgeWasmMesh;

public sealed record EdgeWasmModuleRecord(
    string ModuleId,
    string ModuleName,
    string EdgeProvider, // CLOUDFLARE_WORKERS, FASTLY_COMPUTE, VERCEL_EDGE
    long BytecodeSizeBytes,
    double ColdStartLatencyMs,
    int ActiveEdgePoPsDeployedCount,
    string Status // ACTIVE_GLOBAL, CANARY_TEST, DEPRECATED
);

public sealed record EdgeInvocationMetricRecord(
    string InvocationId,
    string ModuleId,
    string EdgeLocationIata, // LHR, FRA, JFK, NRT, SIN
    double ExecutionDurationMs,
    int MemoryConsumedKb,
    int StatusCode,
    DateTime Timestamp
);

public sealed record EdgeWasmOverview(
    int TotalDeployedModulesCount,
    int GlobalEdgePoPsCount,
    double P99ExecutionLatencyMs,
    long TotalEdgeInvocations24h,
    IReadOnlyList<EdgeWasmModuleRecord> Modules,
    IReadOnlyList<EdgeInvocationMetricRecord> RecentInvocations
);

public sealed record DeployWasmModuleRequest(
    string ModuleName,
    string EdgeProvider,
    long BytecodeSizeBytes
);

public sealed record InvokeEdgeFunctionRequest(
    string ModuleId,
    string EdgeLocationIata,
    string RequestPayload
);
