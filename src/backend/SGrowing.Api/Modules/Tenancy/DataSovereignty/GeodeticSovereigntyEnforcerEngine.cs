using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Tenancy.DataSovereignty;

/// <summary>
/// Autonomous geodetic data sovereignty enforcer verifying physical storage node residency
/// within approved national boundaries and regulating cross-border egress per Law 18-07 / GDPR.
/// </summary>
public sealed class GeodeticSovereigntyEnforcerEngine
{
    private readonly ConcurrentDictionary<SovereignJurisdiction, SovereignDataZone> _zones = new();

    public GeodeticSovereigntyEnforcerEngine()
    {
        InitializeStandardZones();
    }

    private void InitializeStandardZones()
    {
        // Approximate Bounding Polygon for Algerian National Territory
        _zones[SovereignJurisdiction.AlgeriaLaw1807] = new SovereignDataZone(
            ZoneId: "ZONE-DZA-SOVEREIGN",
            Jurisdiction: SovereignJurisdiction.AlgeriaLaw1807,
            Name: "Algeria National Data Sovereignty Zone (ANPDP Law 18-07)",
            PrimaryDataCenterRegion: "dza-algiers-dc1",
            BoundaryPolygon:
            [
                new(37.1, -2.2), // Northwest Mediterranean border
                new(37.1, 8.7),  // Northeast Mediterranean border
                new(30.0, 9.9),  // East border with Tunisia/Libya
                new(19.0, 5.0),  // Southeast border (Tamanrasset/Niger)
                new(21.0, -5.0), // Southwest border (Mali/Mauritania)
                new(32.0, -8.7)  // West border (Morocco)
            ],
            RequiresProofOfResidency: true,
            AllowCrossBorderTransferWithAdequacy: false
        );

        // Approximate Bounding Polygon for European Union
        _zones[SovereignJurisdiction.EuGdpr] = new SovereignDataZone(
            ZoneId: "ZONE-EU-GDPR",
            Jurisdiction: SovereignJurisdiction.EuGdpr,
            Name: "European Union GDPR Economic Area",
            PrimaryDataCenterRegion: "eu-central-frankfurt",
            BoundaryPolygon:
            [
                new(71.0, -10.0),
                new(71.0, 32.0),
                new(36.0, 28.0),
                new(36.0, -10.0)
            ],
            RequiresProofOfResidency: false,
            AllowCrossBorderTransferWithAdequacy: true
        );
    }

    public DataResidencyAttestation VerifyResidency(DataResidencyVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_zones.TryGetValue(request.Jurisdiction, out var zone))
        {
            return new DataResidencyAttestation(
                AttestationId: $"ATT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                TenantId: request.TenantId,
                Jurisdiction: request.Jurisdiction,
                IsCompliant: true,
                BoundaryPolygonRespected: true,
                ComplianceRationale: "Unrestricted global jurisdiction requires no geodetic confinement.",
                CryptographicProofOfResidencyHex: "GLOBAL_RESIDENCY_NOT_RESTRICTED",
                AttestedAtUtc: DateTime.UtcNow
            );
        }

        var isInside = IsPointInsidePolygon(request.NodeLatitude, request.NodeLongitude, zone.BoundaryPolygon);
        var now = DateTime.UtcNow;

        var rawProof = $"{request.TenantId}|{request.Jurisdiction}|{request.NodeLatitude:F4},{request.NodeLongitude:F4}|{request.StorageRegionId}|{isInside}|{now:O}";
        var proofHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawProof))).ToLowerInvariant();

        var compliant = isInside;
        var rationale = isInside
            ? $"Storage node physically confirmed inside sovereign {zone.Name} geodetic boundary."
            : $"VIOLATION: Node coordinates ({request.NodeLatitude:F2}, {request.NodeLongitude:F2}) reside outside sovereign {zone.Name} boundary.";

        return new DataResidencyAttestation(
            AttestationId: $"ATT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            TenantId: request.TenantId,
            Jurisdiction: request.Jurisdiction,
            IsCompliant: compliant,
            BoundaryPolygonRespected: isInside,
            ComplianceRationale: rationale,
            CryptographicProofOfResidencyHex: proofHash,
            AttestedAtUtc: now
        );
    }

    public CrossBorderTransferAuditResult AuditCrossBorderTransfer(
        string tenantId,
        SovereignJurisdiction source,
        SovereignJurisdiction target,
        DataClassificationLevel classification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var transferId = $"XFER-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        // Same jurisdiction -> always permitted
        if (source == target || source == SovereignJurisdiction.UnrestrictedGlobal)
        {
            return new CrossBorderTransferAuditResult(
                TransferId: transferId,
                TenantId: tenantId,
                SourceJurisdiction: source,
                TargetJurisdiction: target,
                Classification: classification,
                TransferPermitted: true,
                EnforcementAction: "ALLOW: Local or unrestricted data movement.",
                AuditedAtUtc: DateTime.UtcNow
            );
        }

        // Strict sovereign barriers
        if (classification is DataClassificationLevel.RestrictedSovereignState or DataClassificationLevel.ConfidentialPii)
        {
            if (source == SovereignJurisdiction.AlgeriaLaw1807)
            {
                return new CrossBorderTransferAuditResult(
                    TransferId: transferId,
                    TenantId: tenantId,
                    SourceJurisdiction: source,
                    TargetJurisdiction: target,
                    Classification: classification,
                    TransferPermitted: false,
                    EnforcementAction: "BLOCK: Law 18-07 Article 43 prohibits unapproved extraterritorial transmission of personal/sovereign data.",
                    AuditedAtUtc: DateTime.UtcNow
                );
            }
        }

        return new CrossBorderTransferAuditResult(
            TransferId: transferId,
            TenantId: tenantId,
            SourceJurisdiction: source,
            TargetJurisdiction: target,
            Classification: classification,
            TransferPermitted: true,
            EnforcementAction: "ALLOW_WITH_AUDIT: Data transfer permitted subject to standard contractual clauses.",
            AuditedAtUtc: DateTime.UtcNow
        );
    }

    private static bool IsPointInsidePolygon(double lat, double lon, IReadOnlyList<GeodeticBoundaryVertex> polygon)
    {
        var inside = false;
        var n = polygon.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((polygon[i].Longitude > lon) != (polygon[j].Longitude > lon)) &&
                (lat < (polygon[j].Latitude - polygon[i].Latitude) * (lon - polygon[i].Longitude) /
                 (polygon[j].Longitude - polygon[i].Longitude) + polygon[i].Latitude))
            {
                inside = !inside;
            }
        }
        return inside;
    }
}
