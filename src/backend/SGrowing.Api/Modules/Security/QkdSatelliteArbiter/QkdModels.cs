namespace SGrowing.Api.Modules.Security.QkdSatelliteArbiter;

public record QkdQubitTransmission(
    int PulseId,
    string AliceBasis, // "Rectilinear" (+) or "Diagonal" (x)
    int AliceBit,      // 0 or 1
    string BobBasis,   // "Rectilinear" (+) or "Diagonal" (x)
    int BobBitMeasured,// 0 or 1
    double DarkCountProbability = 0.001
);

public record QkdSessionRequest(
    string SatelliteId,
    string GroundStationId,
    IReadOnlyList<QkdQubitTransmission> RawQubits,
    double ChannelLossDb = 25.0,
    double AtmosphericTurbulenceScintillation = 0.05
);

public record QkdSessionResult(
    string SessionId,
    string SatelliteId,
    string GroundStationId,
    int RawQubitsCount,
    int SiftedKeyLength,
    double MeasuredQberPercent,
    bool InterceptionDetected,
    bool SessionAborted,
    int FinalDistilledKeyBits,
    string KeyFingerprintSha256,
    string SecurityStatus
);
