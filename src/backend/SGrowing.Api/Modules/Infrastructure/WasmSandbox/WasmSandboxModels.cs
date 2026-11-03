namespace SGrowing.Api.Modules.Infrastructure.WasmSandbox;

public record WasmMicroVmWorker(
    string WorkerId,
    string TenantId,
    string ModuleName,
    int MemoryLimitMb,
    double ExecutionDurationLimitMs,
    string IsolationRuntime, // WASMTIME_AOT, WASMER_LLVM, V8_ISOLATE
    double ColdStartLatencyMs,
    string Status, // RUNNING, IDLE, TERMINATED
    DateTime LaunchedAt
);

public record WasmSecurityCapability(
    string CapabilityId,
    string WorkerId,
    string AllowedOutboundDnsRegex,
    bool FileSystemAccessAllowed,
    bool CryptographicSigningAllowed,
    string CapabilityPolicyLevel // STRICT_ZERO_TRUST, RESTRICTED_EGRESS, ADMIN_PROVISIONED
);

public record WasmSandboxOverview(
    int TotalRunningMicroVmsCount,
    double AverageColdStartLatencyMs,
    int ActiveTenantSandboxesCount,
    double TotalMemoryAllocatedMb,
    List<WasmMicroVmWorker> ActiveWorkers,
    List<WasmSecurityCapability> SecurityCapabilities
);

public record DeployWasmWorkerRequest(
    string TenantId,
    string ModuleName,
    int MemoryLimitMb,
    double ExecutionDurationLimitMs,
    string IsolationRuntime
);

public record TerminateWasmWorkerRequest(
    string WorkerId
);
