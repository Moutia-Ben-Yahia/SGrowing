using FluentAssertions;
using SGrowing.Api.Modules.Tenancy.DataSovereignty;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public sealed class DataSovereigntyTests
{
    private readonly GeodeticSovereigntyEnforcerEngine _enforcer = new();

    #region Geodetic Residency Attestation Tests

    [Fact]
    public void VerifyResidency_NodeInsideNationalBorder_ReturnsCompliantAttestation()
    {
        // Algiers sovereign data center coordinates
        var req = new DataResidencyVerificationRequest(
            TenantId: "TENANT-ALGERIA-TELECOM",
            Jurisdiction: SovereignJurisdiction.AlgeriaLaw1807,
            Classification: DataClassificationLevel.ConfidentialPii,
            NodeLatitude: 36.7538,
            NodeLongitude: 3.0588,
            StorageRegionId: "dza-algiers-dc1"
        );

        var attestation = _enforcer.VerifyResidency(req);

        attestation.Should().NotBeNull();
        attestation.IsCompliant.Should().BeTrue();
        attestation.BoundaryPolygonRespected.Should().BeTrue();
        attestation.CryptographicProofOfResidencyHex.Should().NotBeNullOrWhiteSpace();
        attestation.ComplianceRationale.Should().Contain("confirmed inside sovereign");
    }

    [Fact]
    public void VerifyResidency_NodeOutsideNationalBorder_FlagsViolation()
    {
        // Frankfurt coordinates (50.11N, 8.68E) outside Algeria
        var req = new DataResidencyVerificationRequest(
            TenantId: "TENANT-ALGERIA-BANK",
            Jurisdiction: SovereignJurisdiction.AlgeriaLaw1807,
            Classification: DataClassificationLevel.RestrictedSovereignState,
            NodeLatitude: 50.1109,
            NodeLongitude: 8.6821,
            StorageRegionId: "eu-central-frankfurt"
        );

        var attestation = _enforcer.VerifyResidency(req);

        attestation.IsCompliant.Should().BeFalse();
        attestation.BoundaryPolygonRespected.Should().BeFalse();
        attestation.ComplianceRationale.Should().StartWith("VIOLATION");
    }

    #endregion

    #region Cross-Border Transfer Gating Tests

    [Fact]
    public void AuditCrossBorderTransfer_ConfidentialPiiFromAlgeriaToExternal_BlocksTransfer()
    {
        var result = _enforcer.AuditCrossBorderTransfer(
            tenantId: "TENANT-HEALTHCARE-DZA",
            source: SovereignJurisdiction.AlgeriaLaw1807,
            target: SovereignJurisdiction.EuGdpr,
            classification: DataClassificationLevel.ConfidentialPii
        );

        result.TransferPermitted.Should().BeFalse();
        result.EnforcementAction.Should().StartWith("BLOCK");
        result.EnforcementAction.Should().Contain("Law 18-07");
    }

    [Fact]
    public void AuditCrossBorderTransfer_InternalSameJurisdiction_AllowsTransfer()
    {
        var result = _enforcer.AuditCrossBorderTransfer(
            tenantId: "TENANT-LOCAL",
            source: SovereignJurisdiction.AlgeriaLaw1807,
            target: SovereignJurisdiction.AlgeriaLaw1807,
            classification: DataClassificationLevel.RestrictedSovereignState
        );

        result.TransferPermitted.Should().BeTrue();
        result.EnforcementAction.Should().StartWith("ALLOW");
    }

    #endregion
}
