using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Logistics.Esg;

/// <summary>
/// Converts carbon liabilities into certified, cryptographically signed retirement tokens.
/// Standard: 1,000 kg CO2e = 1 Metric Ton = 3,500.00 DZD.
/// </summary>
public sealed class CarbonOffsetTokenizationService(IEsgComplianceAuditLedger? auditLedger = null) : ICarbonOffsetTokenizationService
{
    private const decimal PricePerMetricTonDzd = 3500.00m;
    private const double KgPerMetricTon = 1000.0;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, CarbonOffsetCertificate> _certificates = [];
    private readonly HashSet<string> _retiredTransactions = [];
    private readonly Dictionary<string, List<string>> _tenantCertificates = [];

    /// <inheritdoc />
    public CarbonOffsetCertificate RetireOffsetCredits(
        string tenantId,
        double kgToOffset,
        OffsetProjectType projectType,
        string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        if (kgToOffset <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kgToOffset), "Offset quantity must be strictly greater than 0 kg CO2e.");
        }

        lock (_lock)
        {
            var issuedAtUtc = DateTime.UtcNow;
            var certId = $"CERT-{tenantId.ToUpperInvariant()}-{issuedAtUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
            var retirementTxId = $"TX-RET-{Guid.NewGuid():N}".ToUpperInvariant();

            // Guard against duplicate retirement transaction collision
            if (!_retiredTransactions.Add(retirementTxId))
            {
                throw new InvalidOperationException($"Retirement transaction ID '{retirementTxId}' has already been retired.");
            }

            // Calculation: 3,500 DZD per 1,000 kg CO2e
            var metricTons = (decimal)(kgToOffset / KgPerMetricTon);
            var costDzd = Math.Round(metricTons * PricePerMetricTonDzd, 2);

            // Compute HMAC-SHA256 signature
            var proofPayload = $"{certId}|{tenantId}|{projectType}|{kgToOffset:F4}|{costDzd:F2}|{retirementTxId}|{issuedAtUtc:O}";
            var proofHash = ComputeHmac(proofPayload, secretKey);

            var certificate = new CarbonOffsetCertificate(
                CertificateId: certId,
                TenantId: tenantId,
                ProjectType: projectType,
                TotalKgOffset: Math.Round(kgToOffset, 4),
                CostDzd: costDzd,
                RetirementTransactionId: retirementTxId,
                CryptographicProofHash: proofHash,
                IssuedAtUtc: issuedAtUtc);

            _certificates[certId] = certificate;

            if (!_tenantCertificates.TryGetValue(tenantId, out var tenantList))
            {
                tenantList = [];
                _tenantCertificates[tenantId] = tenantList;
            }
            tenantList.Add(certId);

            // Register with immutable audit ledger
            if (auditLedger != null)
            {
                auditLedger.RecordOffsetCertificate(certificate);
                auditLedger.RecordEsgEvent(
                    tenantId: tenantId,
                    eventType: $"OFFSET_RETIREMENT_{projectType}",
                    co2eDeltaKg: -Math.Round(kgToOffset, 4),
                    metadataHash: proofHash);
            }

            return certificate;
        }
    }

    /// <inheritdoc />
    public CarbonOffsetCertificate? GetCertificate(string certificateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateId);

        lock (_lock)
        {
            if (_certificates.TryGetValue(certificateId, out var cert))
            {
                return cert;
            }

            return auditLedger?.GetCertificate(certificateId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CarbonOffsetCertificate> GetCertificatesForTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        lock (_lock)
        {
            if (!_tenantCertificates.TryGetValue(tenantId, out var ids))
            {
                return [];
            }

            var list = new List<CarbonOffsetCertificate>(ids.Count);
            foreach (var id in ids)
            {
                if (_certificates.TryGetValue(id, out var cert))
                {
                    list.Add(cert);
                }
            }

            return list;
        }
    }

    /// <inheritdoc />
    public double GetTotalOffsetKg(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        lock (_lock)
        {
            if (!_tenantCertificates.TryGetValue(tenantId, out var ids))
            {
                return 0;
            }

            double sum = 0;
            foreach (var id in ids)
            {
                if (_certificates.TryGetValue(id, out var cert))
                {
                    sum += cert.TotalKgOffset;
                }
            }

            return Math.Round(sum, 4);
        }
    }

    private static string ComputeHmac(string payload, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }
}
