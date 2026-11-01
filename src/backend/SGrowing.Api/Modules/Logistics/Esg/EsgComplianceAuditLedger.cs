using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// Merkle tree-based immutable ledger for corporate CSRD/GRI compliance and carbon accounting.
/// Features SHA-256 cryptographic block chaining and tamper detection.
/// </summary>
public sealed class EsgComplianceAuditLedger : IEsgComplianceAuditLedger
{
    private const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private readonly Lock _lock = new();

    private readonly Dictionary<string, List<EsgEventRecord>> _tenantChains = [];
    private readonly Dictionary<string, CarbonOffsetCertificate> _certificates = [];

    /// <inheritdoc />
    public EsgEventRecord RecordEsgEvent(string tenantId, string eventType, double co2eDeltaKg, string metadataHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        metadataHash ??= string.Empty;

        lock (_lock)
        {
            if (!_tenantChains.TryGetValue(tenantId, out var chain))
            {
                chain = [];
                _tenantChains[tenantId] = chain;
            }

            var sequenceIndex = chain.Count;
            var previousHash = sequenceIndex == 0 ? GenesisHash : chain[^1].CurrentHash;
            var timestampUtc = DateTime.UtcNow;
            var currentHash = ComputeEventHash(sequenceIndex, previousHash, tenantId, eventType, co2eDeltaKg, metadataHash, timestampUtc);

            var record = new EsgEventRecord(
                SequenceIndex: sequenceIndex,
                EventId: $"EVT-{Guid.NewGuid():N}",
                TenantId: tenantId,
                EventType: eventType,
                Co2eDeltaKg: Math.Round(co2eDeltaKg, 4),
                MetadataHash: metadataHash,
                PreviousHash: previousHash,
                CurrentHash: currentHash,
                TimestampUtc: timestampUtc,
                ReportingYear: timestampUtc.Year);

            chain.Add(record);
            return record;
        }
    }

    /// <inheritdoc />
    public EsgBalanceSheet GenerateAuditBalanceSheet(string tenantId, int year, string authoritySecretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authoritySecretKey);

        lock (_lock)
        {
            var isIntegrityValid = VerifyLedgerIntegrityInternal(tenantId);
            if (!_tenantChains.TryGetValue(tenantId, out var chain))
            {
                var emptyRoot = ComputeEmptyHash();
                return new EsgBalanceSheet(
                    TenantId: tenantId,
                    ReportingYear: year,
                    TotalScope1Kg: 0,
                    TotalScope2Kg: 0,
                    TotalScope3Kg: 0,
                    TotalGrossCo2eKg: 0,
                    TotalOffsetsKg: 0,
                    NetEmissionsCo2eKg: 0,
                    CarbonNeutralityAchieved: true,
                    MerkleAuditRoot: emptyRoot);
            }

            var yearEvents = chain.Where(e => e.TimestampUtc.Year == year || e.ReportingYear == year).ToList();

            double scope1 = 0;
            double scope2 = 0;
            double scope3 = 0;
            double offsets = 0;

            foreach (var evt in yearEvents)
            {
                if (evt.Co2eDeltaKg < 0)
                {
                    offsets += Math.Abs(evt.Co2eDeltaKg);
                }
                else
                {
                    if (evt.EventType.Contains("SCOPE1", StringComparison.OrdinalIgnoreCase))
                    {
                        scope1 += evt.Co2eDeltaKg;
                    }
                    else if (evt.EventType.Contains("SCOPE2", StringComparison.OrdinalIgnoreCase))
                    {
                        scope2 += evt.Co2eDeltaKg;
                    }
                    else if (evt.EventType.Contains("SCOPE3", StringComparison.OrdinalIgnoreCase))
                    {
                        scope3 += evt.Co2eDeltaKg;
                    }
                    else
                    {
                        // Default unclassified positive emissions to Scope 3 freight/packaging
                        scope3 += evt.Co2eDeltaKg;
                    }
                }
            }

            scope1 = Math.Round(scope1, 4);
            scope2 = Math.Round(scope2, 4);
            scope3 = Math.Round(scope3, 4);
            var totalGross = Math.Round(scope1 + scope2 + scope3, 4);
            offsets = Math.Round(offsets, 4);
            var netEmissions = Math.Round(totalGross - offsets, 4);
            var neutralityAchieved = netEmissions <= 0.0001 && isIntegrityValid;

            var leafHashes = yearEvents.Select(e => e.CurrentHash).ToList();
            var merkleRoot = ComputeMerkleTreeRoot(leafHashes);

            return new EsgBalanceSheet(
                TenantId: tenantId,
                ReportingYear: year,
                TotalScope1Kg: scope1,
                TotalScope2Kg: scope2,
                TotalScope3Kg: scope3,
                TotalGrossCo2eKg: totalGross,
                TotalOffsetsKg: offsets,
                NetEmissionsCo2eKg: netEmissions,
                CarbonNeutralityAchieved: neutralityAchieved,
                MerkleAuditRoot: merkleRoot);
        }
    }

    /// <inheritdoc />
    public AuditCertificationStatus VerifyAuditCertificate(string certificateId, string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        lock (_lock)
        {
            if (!_certificates.TryGetValue(certificateId, out var cert))
            {
                return AuditCertificationStatus.TamperDetected;
            }

            // 1. Verify ledger chaining integrity for tenant
            if (!VerifyLedgerIntegrityInternal(cert.TenantId))
            {
                return AuditCertificationStatus.TamperDetected;
            }

            // 2. Verify HMAC cryptographic signature
            var payload = $"{cert.CertificateId}|{cert.TenantId}|{cert.ProjectType}|{cert.TotalKgOffset:F4}|{cert.CostDzd:F2}|{cert.RetirementTransactionId}|{cert.IssuedAtUtc:O}";
            var expectedHmac = ComputeHmac(payload, secretKey);

            if (!string.Equals(expectedHmac, cert.CryptographicProofHash, StringComparison.OrdinalIgnoreCase))
            {
                return AuditCertificationStatus.TamperDetected;
            }

            // 3. Verify inclusion in tenant's audit trail
            if (!_tenantChains.TryGetValue(cert.TenantId, out var chain))
            {
                return AuditCertificationStatus.TamperDetected;
            }

            var recorded = chain.Any(e =>
                e.MetadataHash == cert.CryptographicProofHash ||
                e.MetadataHash.Contains(cert.CertificateId, StringComparison.OrdinalIgnoreCase) ||
                e.MetadataHash.Contains(cert.RetirementTransactionId, StringComparison.OrdinalIgnoreCase));

            if (!recorded)
            {
                return AuditCertificationStatus.TamperDetected;
            }

            return AuditCertificationStatus.VerifiedAudited;
        }
    }

    /// <inheritdoc />
    public void RecordOffsetCertificate(CarbonOffsetCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        lock (_lock)
        {
            _certificates[certificate.CertificateId] = certificate;
        }
    }

    /// <inheritdoc />
    public CarbonOffsetCertificate? GetCertificate(string certificateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateId);

        lock (_lock)
        {
            return _certificates.GetValueOrDefault(certificateId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EsgEventRecord> GetAuditTrail(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        lock (_lock)
        {
            if (_tenantChains.TryGetValue(tenantId, out var chain))
            {
                return [.. chain];
            }

            return [];
        }
    }

    /// <inheritdoc />
    public bool VerifyLedgerIntegrity(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        lock (_lock)
        {
            return VerifyLedgerIntegrityInternal(tenantId);
        }
    }

    /// <inheritdoc />
    public void SimulateTamper(string tenantId, int sequenceIndex, double tamperedDeltaKg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        lock (_lock)
        {
            if (_tenantChains.TryGetValue(tenantId, out var chain) && sequenceIndex >= 0 && sequenceIndex < chain.Count)
            {
                var original = chain[sequenceIndex];
                chain[sequenceIndex] = original with { Co2eDeltaKg = tamperedDeltaKg };
            }
        }
    }

    private bool VerifyLedgerIntegrityInternal(string tenantId)
    {
        if (!_tenantChains.TryGetValue(tenantId, out var chain) || chain.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < chain.Count; i++)
        {
            var current = chain[i];

            if (current.SequenceIndex != i)
            {
                return false;
            }

            if (i == 0)
            {
                if (current.PreviousHash != GenesisHash)
                {
                    return false;
                }
            }
            else
            {
                var previous = chain[i - 1];
                if (current.PreviousHash != previous.CurrentHash)
                {
                    return false;
                }
            }

            var expectedHash = ComputeEventHash(
                current.SequenceIndex,
                current.PreviousHash,
                current.TenantId,
                current.EventType,
                current.Co2eDeltaKg,
                current.MetadataHash,
                current.TimestampUtc);

            if (!string.Equals(expectedHash, current.CurrentHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string ComputeEventHash(
        int sequenceIndex,
        string previousHash,
        string tenantId,
        string eventType,
        double co2eDeltaKg,
        string metadataHash,
        DateTime timestampUtc)
    {
        var raw = $"{sequenceIndex}:{previousHash}:{tenantId}:{eventType}:{co2eDeltaKg:F6}:{metadataHash}:{timestampUtc.Ticks}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ComputeMerkleTreeRoot(IReadOnlyList<string> leafHashes)
    {
        if (leafHashes.Count == 0)
        {
            return ComputeEmptyHash();
        }

        if (leafHashes.Count == 1)
        {
            return leafHashes[0];
        }

        var currentLevel = leafHashes.ToList();

        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<string>((currentLevel.Count + 1) / 2);

            for (var i = 0; i < currentLevel.Count; i += 2)
            {
                var left = currentLevel[i];
                var right = (i + 1 < currentLevel.Count) ? currentLevel[i + 1] : left;
                var combined = left + right;
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combined))).ToLowerInvariant();
                nextLevel.Add(hash);
            }

            currentLevel = nextLevel;
        }

        return currentLevel[0];
    }

    private static string ComputeEmptyHash()
    {
        return Convert.ToHexString(SHA256.HashData(ReadOnlySpan<byte>.Empty)).ToLowerInvariant();
    }

    private static string ComputeHmac(string payload, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }
}
