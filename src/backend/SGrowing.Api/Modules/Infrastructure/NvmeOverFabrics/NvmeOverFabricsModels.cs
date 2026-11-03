namespace SGrowing.Api.Modules.Infrastructure.NvmeOverFabrics;

public enum NvmeTransport
{
    RdmaRocev2,
    Tcp,
    FibreChannel
}

public record NvmeNamespace(
    int Nsid,
    string BlockDeviceId,
    long CapacityBytes,
    string ProvisioningStatus
);

public record NvmeSubsystem(
    string Nqn,
    string DisplayName,
    NvmeTransport TransportProtocol,
    string PrimaryControllerAddress,
    string StandbyControllerAddress,
    string AnaMultipathState, // Optimized, NonOptimized, Inaccessible, ChangeState
    List<NvmeNamespace> Namespaces,
    DateTime CreatedAtUtc
);

public record NvmeTelemetrySample(
    string SampleId,
    string Nqn,
    long ReadIops,
    long WriteIops,
    double ReadBandwidthGbps,
    double WriteBandwidthGbps,
    double AverageLatencyMicros,
    DateTime RecordedAtUtc
);

public record AnaFailoverEvent(
    string EventId,
    string Nqn,
    string Cause,
    string PreviousState,
    string NewState,
    int FailoverDurationMs,
    bool ZeroDataLossVerified,
    DateTime CompletedAtUtc
);
