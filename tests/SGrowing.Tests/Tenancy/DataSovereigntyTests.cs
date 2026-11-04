using System;
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

    [Fact]
    public void VerifyResidency_UnrestrictedGlobal_AlwaysReturnsCompliant()
    {
        // Arrange
        var req = new DataResidencyVerificationRequest(
            TenantId: "TENANT-GLOBAL-SAAS",
            Jurisdiction: SovereignJurisdiction.UnrestrictedGlobal,
            Classification: DataClassificationLevel.Public,
            NodeLatitude: -33.8688,
            NodeLongitude: 151.2093,
            StorageRegionId: "ap-southeast-sydney"
        );

        // Act
        var attestation = _enforcer.VerifyResidency(req);

        // Assert
        attestation.IsCompliant.Should().BeTrue();
        attestation.BoundaryPolygonRespected.Should().BeTrue();
        attestation.CryptographicProofOfResidencyHex.Should().Be("GLOBAL_RESIDENCY_NOT_RESTRICTED");
        attestation.ComplianceRationale.Should().Contain("Unrestricted global jurisdiction");
    }

    [Fact]
    public void VerifyResidency_EuGdpr_NodeInsideEurope_ReturnsCompliant()
    {
        // Arrange: Paris coordinates (48.8566, 2.3522) inside EU polygon
        var req = new DataResidencyVerificationRequest(
            TenantId: "TENANT-EU-FINANCE",
            Jurisdiction: SovereignJurisdiction.EuGdpr,
            Classification: DataClassificationLevel.ConfidentialPii,
            NodeLatitude: 48.8566,
            NodeLongitude: 2.3522,
            StorageRegionId: "eu-west-paris"
        );

        // Act
        var attestation = _enforcer.VerifyResidency(req);

        // Assert
        attestation.IsCompliant.Should().BeTrue();
        attestation.BoundaryPolygonRespected.Should().BeTrue();
        attestation.ComplianceRationale.Should().Contain("confirmed inside sovereign");
    }

    [Fact]
    public void VerifyResidency_ThrowsArgumentNullException_WhenRequestIsNull()
    {
        // Act
        var act = () => _enforcer.VerifyResidency(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
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

    [Fact]
    public void AuditCrossBorderTransfer_PublicData_AllowsWithAudit()
    {
        // Arrange & Act
        var result = _enforcer.AuditCrossBorderTransfer(
            tenantId: "TENANT-MEDIA-GLOBAL",
            source: SovereignJurisdiction.AlgeriaLaw1807,
            target: SovereignJurisdiction.EuGdpr,
            classification: DataClassificationLevel.Public
        );

        // Assert
        result.TransferPermitted.Should().BeTrue();
        result.EnforcementAction.Should().StartWith("ALLOW_WITH_AUDIT");
        result.EnforcementAction.Should().Contain("standard contractual clauses");
    }

    [Fact]
    public void AuditCrossBorderTransfer_ThrowsArgumentException_WhenTenantIdIsEmpty()
    {
        // Act
        var act = () => _enforcer.AuditCrossBorderTransfer(
            "",
            SovereignJurisdiction.EuGdpr,
            SovereignJurisdiction.UnrestrictedGlobal,
            DataClassificationLevel.Public
        );

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
