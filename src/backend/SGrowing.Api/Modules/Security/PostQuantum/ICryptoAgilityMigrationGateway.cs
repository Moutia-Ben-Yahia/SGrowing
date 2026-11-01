namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// Gateway for dynamic cipher suite negotiation and quantum readiness audit reporting.
/// Enforces crypto-agility across legacy and post-quantum capable clients.
/// </summary>
public interface ICryptoAgilityMigrationGateway
{
    /// <summary>
    /// Negotiates the highest security cipher suite supported by both client and server,
    /// upgrading capable clients to HybridClassicalPqc while preserving legacy X25519 compatibility.
    /// </summary>
    NegotiatedCipherSuiteResult NegotiateCipherSuite(string? clientSupportedSuitesHeader, string? secPqcSupportedHeader = null);

    /// <summary>
    /// Returns real-time quantum readiness metrics and migration posture for enterprise communications.
    /// </summary>
    QuantumReadinessResponse GetQuantumReadinessStatus();

    /// <summary>
    /// Records a completed communication using the specified cryptographic algorithm.
    /// </summary>
    void RecordCommunication(PqcAlgorithm algorithm);

    /// <summary>
    /// Resets communication metrics counters (for diagnostics or testing).
    /// </summary>
    void ResetMetrics();
}
