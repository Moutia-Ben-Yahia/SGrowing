using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.SupplyChain.ZkTraceability;

/// <summary>
/// Privacy-preserving anti-sanction screening service using blinded hashing
/// to verify suppliers against OFAC, EU, UN, and domestic embargo lists
/// without revealing the private supplier graph to external audit parties.
/// </summary>
public sealed class SanctionComplianceScreeningService
{
    private readonly ConcurrentDictionary<string, string> _sanctionedEntities = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _syncLock = new();
    private string _sanctionListVersion = "OFAC-EU-UN-2026.11-V4";

    public SanctionComplianceScreeningService()
    {
        // Seed standard test and international sanction entities (OFAC SDN, UN 1267, EU Financial Sanctions)
        RegisterSanctionEntity("OFAC-SDN-99412", "OFAC-GLOBAL-TERRORISM-SPEC");
        RegisterSanctionEntity("EU-FS-88120", "EU-RESTRICTIVE-MEASURES-ANNEX-I");
        RegisterSanctionEntity("UN-SC-77319", "UN-SECURITY-COUNCIL-RESOLUTION-2231");
        RegisterSanctionEntity("DZ-CT-00441", "ALGERIA-FINANCIAL-INTELLIGENCE-UNIT-BLACK");
        RegisterSanctionEntity("GLOBAL-SHELL-CORP-404", "INTERNATIONAL-EXPORT-CONTROL-RESTRICTION");
    }

    /// <summary>
    /// Registers or updates a known sanctioned entity identifier.
    /// </summary>
    public void RegisterSanctionEntity(string rawIdentifier, string programSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(programSource);

        var normalized = rawIdentifier.Trim().ToUpperInvariant();
        _sanctionedEntities[normalized] = programSource;
    }

    /// <summary>
    /// Computes the blinded audit identifier for a supplier using the shared auditor salt.
    /// </summary>
    public static string ComputeBlindedIdentifier(string rawIdentifier, string auditorSalt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditorSalt);

        var normalized = rawIdentifier.Trim().ToUpperInvariant();
        var payload = $"{normalized}:{auditorSalt}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    /// <summary>
    /// Screens a list of blinded supplier identifiers against the active sanction database.
    /// </summary>
    public SanctionScreeningResult ScreenBlindedSuppliers(SanctionScreeningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AuditorSalt))
        {
            throw new ArgumentException("Auditor salt cannot be empty.", nameof(request));
        }

        var screeningId = $"SCR-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var screenedAtUtc = DateTime.UtcNow;

        // Hash all current sanctioned entities with the provided auditor salt
        var currentSanctionedBlindedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in _sanctionedEntities.Keys)
        {
            var blinded = ComputeBlindedIdentifier(entity, request.AuditorSalt);
            currentSanctionedBlindedSet.Add(blinded);
        }

        var violatedHashes = new List<string>();
        foreach (var supplierBlindedHash in request.BlindedSanctionIdentifiers)
        {
            if (currentSanctionedBlindedSet.Contains(supplierBlindedHash))
            {
                violatedHashes.Add(supplierBlindedHash);
            }
        }

        var matchDetected = violatedHashes.Count > 0;
        var status = matchDetected ? SanctionScreeningStatus.SanctionViolationDetected : SanctionScreeningStatus.Compliant;

        // Generate non-repudiation audit proof token
        var auditProofPayload = $"{screeningId}:{screenedAtUtc:O}:{_sanctionListVersion}:{request.BlindedSanctionIdentifiers.Count}:{violatedHashes.Count}:{request.AuditorSalt}";
        var proofToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(auditProofPayload))).ToLowerInvariant();

        return new SanctionScreeningResult(
            ScreeningId: screeningId,
            ScreenedAtUtc: screenedAtUtc,
            SanctionListVersion: _sanctionListVersion,
            TotalBlindedSuppliersScreened: request.BlindedSanctionIdentifiers.Count,
            MatchDetected: matchDetected,
            ViolatedSupplierHashes: violatedHashes,
            CryptographicAuditProof: proofToken,
            Status: status
        );
    }

    /// <summary>
    /// Gets the current version stamp of the sanction database.
    /// </summary>
    public string GetSanctionListVersion() => _sanctionListVersion;

    /// <summary>
    /// Updates the sanction database version tag.
    /// </summary>
    public void UpdateSanctionListVersion(string newVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newVersion);
        lock (_syncLock)
        {
            _sanctionListVersion = newVersion;
        }
    }

    /// <summary>
    /// Returns total count of active tracked sanction entries.
    /// </summary>
    public int GetActiveSanctionCount() => _sanctionedEntities.Count;
}
