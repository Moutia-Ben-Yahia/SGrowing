using SGrowing.Api.Modules.Tenancy.Licensing;

namespace SGrowing.Tests.Tenancy;

public class EnterpriseLicensingTests
{
    private readonly EnterpriseLicensingEngine _engine = new();

    [Fact]
    public void GetLicenseStatus_ReturnsValidDefaultLicense()
    {
        var result = _engine.GetLicenseStatus();

        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.True(result.DaysRemaining > 300);
        Assert.True(result.License.IsAirGappedValid);
        Assert.Contains("AI.Swarm", result.License.EntitledModules);
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

        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.Equal("Acme Corporation Global", result.License.OrganizationName);
        Assert.Equal(1000, result.License.MaxSeats);

        var status = _engine.GetLicenseStatus();
        Assert.Equal("Acme Corporation Global", status.License?.OrganizationName);
    }

    [Fact]
    public void ActivateLicense_WithInvalidKey_ReturnsError()
    {
        var req = new ActivateLicenseRequest("", "Acme", "payload");
        var result = _engine.ActivateLicense(req);

        Assert.False(result.IsValid);
        Assert.Null(result.License);
    }

    [Fact]
    public void VerifyAirGappedPayload_ValidatesHardwareFingerprint()
    {
        var req = new AirGapVerificationRequest("valid_signed_blob", "NODE-01-MAC-48-2C");
        var result = _engine.VerifyAirGappedPayload(req);

        Assert.True(result.IsValid);
        Assert.Contains("NODE-01-MAC-48-2C", result.StatusMessage);
    }
}
