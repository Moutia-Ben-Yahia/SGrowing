using FluentAssertions;
using SGrowing.Api.Modules.Tenancy.Licensing;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class EnterpriseLicensingTests
{
    private readonly EnterpriseLicensingEngine _engine = new();

    [Fact]
    public void GetLicenseStatus_ReturnsValidDefaultLicense()
    {
        var result = _engine.GetLicenseStatus();

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.License.Should().NotBeNull();
        result.DaysRemaining.Should().BeGreaterThan(300);
        result.License!.IsAirGappedValid.Should().BeTrue();
        result.License.EntitledModules.Should().Contain("AI.Swarm");
    }

    [Fact]
    public void ActivateLicense_WithValidKey_UpdatesActiveLicense()
    {
        var req = new ActivateLicenseRequest(
            LicenseKey: "SG-ENT-ACME-CORP-2026-X818",
            OrganizationName: "Acme Corporation Global",
            SignedPayload: "{\"org\":\"Acme\",\"tier\":\"Enterprise Dedicated\"}"
        );

        var result = _engine.ActivateLicense(req);

        result.IsValid.Should().BeTrue();
        result.License.Should().NotBeNull();
        result.License!.OrganizationName.Should().Be("Acme Corporation Global");
        result.License.MaxSeats.Should().Be(1000);

        var status = _engine.GetLicenseStatus();
        status.License?.OrganizationName.Should().Be("Acme Corporation Global");
    }

    [Fact]
    public void ActivateLicense_WithInvalidKey_ReturnsError()
    {
        var req = new ActivateLicenseRequest("", "Acme", "payload");
        var result = _engine.ActivateLicense(req);

        result.IsValid.Should().BeFalse();
        result.License.Should().BeNull();
        result.StatusMessage.Should().Contain("Invalid license key format");
    }

    [Fact]
    public void ActivateLicense_ShortLicenseKey_ReturnsError()
    {
        // Arrange: key length < 10 characters
        var req = new ActivateLicenseRequest("SG-SHORT", "Acme", "payload");

        // Act
        var result = _engine.ActivateLicense(req);

        // Assert
        result.IsValid.Should().BeFalse();
        result.StatusMessage.Should().Contain("Invalid license key format");
    }

    [Fact]
    public void VerifyAirGappedPayload_ValidatesHardwareFingerprint()
    {
        var req = new AirGapVerificationRequest("valid_signed_blob", "NODE-01-MAC-48-2C");
        var result = _engine.VerifyAirGappedPayload(req);

        result.IsValid.Should().BeTrue();
        result.StatusMessage.Should().Contain("NODE-01-MAC-48-2C");
    }

    [Fact]
    public void VerifyAirGappedPayload_MissingMachineFingerprint_ReturnsError()
    {
        // Arrange
        var req = new AirGapVerificationRequest("valid_signed_blob", "");

        // Act
        var result = _engine.VerifyAirGappedPayload(req);

        // Assert
        result.IsValid.Should().BeFalse();
        result.StatusMessage.Should().Contain("Missing air-gap payload");
    }

    [Fact]
    public void VerifyAirGappedPayload_MissingSignedPayload_ReturnsError()
    {
        // Arrange
        var req = new AirGapVerificationRequest("", "NODE-02-MAC");

        // Act
        var result = _engine.VerifyAirGappedPayload(req);

        // Assert
        result.IsValid.Should().BeFalse();
        result.StatusMessage.Should().Contain("Missing air-gap payload");
    }

    [Fact]
    public void ActivateLicense_BindsCustomOrganizationAndAirGappedCapability()
    {
        // Arrange
        var req = new ActivateLicenseRequest("SG-AIRGAP-GOV-TUNISIA-2026", "National Digital Agency", "sig_blob");

        // Act
        var result = _engine.ActivateLicense(req);

        // Assert
        result.IsValid.Should().BeTrue();
        result.License!.OrganizationName.Should().Be("National Digital Agency");
        result.License.EntitledModules.Should().Contain("AirGapped.Core");
        result.License.MaxMonthlyEvents.Should().Be(50_000_000);
    }
}
