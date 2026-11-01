namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// Production-grade implementation of CryptoAgilityMigrationGateway.
/// Manages dynamic cipher negotiation and maintains thread-safe quantum readiness telemetry.
/// </summary>
public sealed class CryptoAgilityMigrationGateway : ICryptoAgilityMigrationGateway
{
    private readonly Lock _lock = new();
    private long _totalCommunications;
    private long _quantumSafeCommunications;
    private long _classicalCommunications;

    private static readonly string[] SupportedAlgorithmNames = [
        nameof(PqcAlgorithm.HybridClassicalPqc),
        nameof(PqcAlgorithm.MlKem768_CrystalsKyber),
        nameof(PqcAlgorithm.MlDsa65_CrystalsDilithium),
        nameof(PqcAlgorithm.ClassicalX25519)
    ];

    /// <summary>
    /// Negotiates the highest security cipher suite supported by both client and server,
    /// upgrading capable clients to HybridClassicalPqc while preserving legacy X25519 compatibility.
    /// </summary>
    public NegotiatedCipherSuiteResult NegotiateCipherSuite(string? clientSupportedSuitesHeader, string? secPqcSupportedHeader = null)
    {
        bool secPqcFlag = false;
        if (!string.IsNullOrWhiteSpace(secPqcSupportedHeader))
        {
            string trimmed = secPqcSupportedHeader.Trim();
            secPqcFlag = string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(trimmed, "1", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(trimmed, "enabled", StringComparison.OrdinalIgnoreCase);
        }

        string suites = clientSupportedSuitesHeader?.ToUpperInvariant() ?? string.Empty;

        bool supportsPqc = secPqcFlag ||
                           suites.Contains("HYBRID") ||
                           suites.Contains("PQC") ||
                           suites.Contains("KYBER") ||
                           suites.Contains("ML-KEM") ||
                           suites.Contains("DILITHIUM") ||
                           suites.Contains("ML-DSA");

        PqcAlgorithm selectedAlgorithm;
        string suiteName;
        string description;

        if (supportsPqc)
        {
            if (suites.Contains("HYBRID") || secPqcFlag)
            {
                selectedAlgorithm = PqcAlgorithm.HybridClassicalPqc;
                suiteName = "Hybrid-X25519-ML-KEM-768";
                description = "Dual-defense hybrid key exchange combining X25519 classical ECDH with ML-KEM-768.";
            }
            else if (suites.Contains("ML-DSA") || suites.Contains("DILITHIUM"))
            {
                selectedAlgorithm = PqcAlgorithm.MlDsa65_CrystalsDilithium;
                suiteName = "ML-DSA-65-Crystals-Dilithium";
                description = "Pure Post-Quantum Digital Signature lattice suite under NIST FIPS 204.";
            }
            else if (suites.Contains("ML-KEM") || suites.Contains("KYBER"))
            {
                selectedAlgorithm = PqcAlgorithm.MlKem768_CrystalsKyber;
                suiteName = "ML-KEM-768-Crystals-Kyber";
                description = "Pure Post-Quantum Key Encapsulation lattice suite under NIST FIPS 203.";
            }
            else
            {
                selectedAlgorithm = PqcAlgorithm.HybridClassicalPqc;
                suiteName = "Hybrid-X25519-ML-KEM-768";
                description = "Dual-defense hybrid key exchange combining X25519 classical ECDH with ML-KEM-768.";
            }
        }
        else
        {
            selectedAlgorithm = PqcAlgorithm.ClassicalX25519;
            suiteName = "Classical-X25519";
            description = "Legacy classical elliptic curve key exchange (non quantum-safe).";
        }

        bool isQuantumResistant = selectedAlgorithm != PqcAlgorithm.ClassicalX25519;
        RecordCommunication(selectedAlgorithm);

        return new NegotiatedCipherSuiteResult(selectedAlgorithm, isQuantumResistant, suiteName, description);
    }

    /// <summary>
    /// Records a completed communication using the specified cryptographic algorithm.
    /// </summary>
    public void RecordCommunication(PqcAlgorithm algorithm)
    {
        lock (_lock)
        {
            _totalCommunications++;
            if (algorithm == PqcAlgorithm.ClassicalX25519)
            {
                _classicalCommunications++;
            }
            else
            {
                _quantumSafeCommunications++;
            }
        }
    }

    /// <summary>
    /// Returns real-time quantum readiness metrics and migration posture for enterprise communications.
    /// </summary>
    public QuantumReadinessResponse GetQuantumReadinessStatus()
    {
        long total;
        long quantumSafe;
        long classical;

        lock (_lock)
        {
            total = _totalCommunications;
            quantumSafe = _quantumSafeCommunications;
            classical = _classicalCommunications;
        }

        double percentage = total > 0
            ? Math.Round((double)quantumSafe / total * 100.0, 2)
            : 100.0;

        string strategy = percentage switch
        {
            < 25.0 => "Critical: Legacy classical crypto dominates. Mandate 'Sec-PQC-Supported: true' and migrate key agreements to HybridClassicalPqc.",
            < 75.0 => "Progressing: Multi-party hybrid migration underway. Initiate deprecation schedule for legacy X25519-only clients.",
            _ => "Quantum-Resilient: Enterprise posture meets NIST FIPS 203/204 guidelines. Ready for permanent post-quantum cutover."
        };

        return new QuantumReadinessResponse
        {
            QuantumSafePercentage = percentage,
            TotalCommunications = total,
            QuantumSafeCommunications = quantumSafe,
            ClassicalCommunications = classical,
            RecommendedMigrationStrategy = strategy,
            SupportedAlgorithms = SupportedAlgorithmNames
        };
    }

    /// <summary>
    /// Resets communication metrics counters (for diagnostics or testing).
    /// </summary>
    public void ResetMetrics()
    {
        lock (_lock)
        {
            _totalCommunications = 0;
            _quantumSafeCommunications = 0;
            _classicalCommunications = 0;
        }
    }
}
