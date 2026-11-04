using FluentAssertions;
using SGrowing.Api.Modules.CustomerData.Firmographics;
using Xunit;

namespace SGrowing.Tests.CustomerData;

public class FirmographicsTests
{
    private readonly FirmographicsEngine _engine = new();

    [Fact]
    public void DeanonymizeIp_WithEnterpriseIpOnPricingPage_TriggersTierASalesAlert()
    {
        // Arrange
        var request = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_01",
            IpAddress: "198.51.100.25", // Acme Corp seeded IP
            AnonymousSessionId: "anon_sess_001",
            VisitedPage: "/pricing"
        );

        // Act
        var result = _engine.DeanonymizeIp(request);

        // Assert
        result.Should().NotBeNull();
        result.MatchFound.Should().BeTrue();
        result.Company.Should().NotBeNull();
        result.Company!.CompanyName.Should().Be("Acme Corp");
        result.Company.Domain.Should().Be("acmecorp.com");
        result.Company.EmployeeCount.Should().Be(2500);

        result.IcpFit.Should().NotBeNull();
        result.IcpFit!.FitTier.Should().Be("Tier_A");
        result.IcpFit.CompositeScore.Should().BeGreaterThanOrEqualTo(80);
        result.IcpFit.IsHighIntentTarget.Should().BeTrue();

        result.TriggeredSalesAlert.Should().BeTrue();
        result.AlertDetails.Should().Contain("HIGH INTENT SALES ALERT");
        result.AlertDetails.Should().Contain("Acme Corp");
        result.AlertDetails.Should().Contain("/pricing");
    }

    [Fact]
    public void DeanonymizeIp_WithMidMarketIp_AssignsTierBWithoutHighIntentAlert()
    {
        // Arrange
        var request = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_02",
            IpAddress: "203.0.113.45", // TechScale seeded IP (180 employees)
            AnonymousSessionId: "anon_sess_002",
            VisitedPage: "/pricing"
        );

        // Act
        var result = _engine.DeanonymizeIp(request);

        // Assert
        result.Should().NotBeNull();
        result.MatchFound.Should().BeTrue();
        result.Company.Should().NotBeNull();
        result.Company!.CompanyName.Should().Be("TechScale");
        result.Company.Domain.Should().Be("techscale.io");
        result.Company.EmployeeCount.Should().Be(180);

        result.IcpFit.Should().NotBeNull();
        result.IcpFit!.FitTier.Should().Be("Tier_B");
        result.IcpFit.CompositeScore.Should().BeInRange(60, 79);
        result.IcpFit.IsHighIntentTarget.Should().BeFalse();

        result.TriggeredSalesAlert.Should().BeFalse();
    }

    [Fact]
    public void DeanonymizeIp_WithConsumerIsp_ReturnsMatchNotFound()
    {
        // Arrange - Comcast / Residential consumer IP not in enterprise database
        var request = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_03",
            IpAddress: "73.189.45.12",
            AnonymousSessionId: "anon_sess_003",
            VisitedPage: "/pricing"
        );

        // Act
        var result = _engine.DeanonymizeIp(request);

        // Assert
        result.Should().NotBeNull();
        result.MatchFound.Should().BeFalse();
        result.Company.Should().BeNull();
        result.IcpFit.Should().BeNull();
        result.TriggeredSalesAlert.Should().BeFalse();
        result.AlertDetails.Should().Contain("Consumer ISP or unmapped");
    }

    [Fact]
    public void DeanonymizeIp_WithCustomIcpRules_ScoresIndustryAndEmployeeFitCorrectly()
    {
        // Arrange - Custom ICP rules prioritizing large scale Logistics
        var tenantId = "tenant_custom_icp_04";
        var customRules = new List<IcpScoringRule>
        {
            new IcpScoringRule(
                TargetIndustry: "Logistics",
                MinEmployees: 5000,
                MaxEmployees: 50000,
                Weight: 90m
            )
        };

        _engine.RegisterTenantIcpRules(tenantId, customRules);

        // Act 1: GlobalLogistics (Logistics, 12000 employees) -> should match both industry and employee count
        var matchingRequest = new DeanonymizeIpRequest(
            TenantId: tenantId,
            IpAddress: "192.0.2.88",
            VisitedPage: "/features"
        );
        var matchingResult = _engine.DeanonymizeIp(matchingRequest);

        // Act 2: TechScale (Technology, 180 employees) -> mismatches both industry and employee range
        var mismatchRequest = new DeanonymizeIpRequest(
            TenantId: tenantId,
            IpAddress: "203.0.113.45",
            VisitedPage: "/features"
        );
        var mismatchResult = _engine.DeanonymizeIp(mismatchRequest);

        // Assert matching
        matchingResult.MatchFound.Should().BeTrue();
        matchingResult.Company!.CompanyName.Should().Be("GlobalLogistics");
        matchingResult.IcpFit.Should().NotBeNull();
        matchingResult.IcpFit!.FitTier.Should().Be("Tier_A");
        matchingResult.IcpFit.CompositeScore.Should().Be(90);

        // Assert mismatch
        mismatchResult.MatchFound.Should().BeTrue();
        mismatchResult.Company!.CompanyName.Should().Be("TechScale");
        mismatchResult.IcpFit.Should().NotBeNull();
        mismatchResult.IcpFit!.FitTier.Should().Be("Unqualified");
        mismatchResult.IcpFit.CompositeScore.Should().Be(0);
    }

    [Fact]
    public void GetCompany_WithKnownDomain_ReturnsCachedProfile()
    {
        // Act
        var profile = _engine.GetCompany("tenant_growth_05", "acmecorp.com");

        // Assert
        profile.Should().NotBeNull();
        profile!.CompanyName.Should().Be("Acme Corp");
        profile.Domain.Should().Be("acmecorp.com");
        profile.Industry.Should().Be("Technology");
        profile.EmployeeCount.Should().Be(2500);
        profile.RevenueRange.Should().Be("$100M - $250M");
        profile.HeadquartersCity.Should().Be("San Francisco");
        profile.HeadquartersCountry.Should().Be("United States");
        profile.TechStack.Should().Contain("Kubernetes");
        profile.TechStack.Should().Contain("AWS");
    }

    [Fact]
    public void DeanonymizeIp_WithUnknownIp_ReturnsNoMatchSafely()
    {
        var request = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_06",
            IpAddress: "8.8.8.8",
            VisitedPage: "/home"
        );

        var result = _engine.DeanonymizeIp(request);

        result.Should().NotBeNull();
        result.MatchFound.Should().BeFalse();
        result.Company.Should().BeNull();
        result.IcpFit.Should().BeNull();
        result.TriggeredSalesAlert.Should().BeFalse();
    }

    [Fact]
    public void GetCompany_NormalizesDomainFormatsAccurately()
    {
        var profile1 = _engine.GetCompany("tenant_growth_07", "https://acmecorp.com/");
        var profile2 = _engine.GetCompany("tenant_growth_07", "www.acmecorp.com");
        var profile3 = _engine.GetCompany("tenant_growth_07", "  ACMECORP.COM  ");

        profile1.Should().NotBeNull();
        profile2.Should().NotBeNull();
        profile3.Should().NotBeNull();
        profile1!.CompanyName.Should().Be("Acme Corp");
        profile2!.CompanyName.Should().Be("Acme Corp");
        profile3!.CompanyName.Should().Be("Acme Corp");
    }

    [Fact]
    public void GetCompany_ReturnsNullForUnknownDomain()
    {
        var profile = _engine.GetCompany("tenant_growth_08", "non-existent-startup-99.org");
        profile.Should().BeNull();
    }

    [Fact]
    public void RegisterCompanyProfile_TenantOverride_PrioritizesTenantProfileOverGlobal()
    {
        var tenantId = "tenant_custom_override_01";
        var customAcme = new FirmographicCompanyProfile(
            Domain: "acmecorp.com",
            CompanyName: "Acme Corp (Custom Enterprise Tier)",
            Industry: "FinTech & Cloud",
            EmployeeCount: 3000,
            RevenueRange: "$300M+",
            HeadquartersCity: "San Francisco",
            HeadquartersCountry: "United States",
            TechStack: new List<string> { "CustomMesh", "Kubernetes" }
        );

        _engine.RegisterCompanyProfile(tenantId, customAcme);

        var retrieved = _engine.GetCompany(tenantId, "acmecorp.com");
        retrieved.Should().NotBeNull();
        retrieved!.CompanyName.Should().Be("Acme Corp (Custom Enterprise Tier)");
        retrieved.EmployeeCount.Should().Be(3000);
    }

    [Fact]
    public void DeanonymizeIp_NullOrEmptyIp_ReturnsInvalidFormatResult()
    {
        var requestEmpty = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_09",
            IpAddress: "",
            VisitedPage: "/pricing"
        );
        var requestInvalid = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_09",
            IpAddress: "999.999.999.999",
            VisitedPage: "/pricing"
        );

        var resultEmpty = _engine.DeanonymizeIp(requestEmpty);
        var resultInvalid = _engine.DeanonymizeIp(requestInvalid);

        resultEmpty.MatchFound.Should().BeFalse();
        resultEmpty.AlertDetails.Should().Contain("Invalid or missing IP address format");
        resultInvalid.MatchFound.Should().BeFalse();
        resultInvalid.AlertDetails.Should().Contain("Invalid or missing IP address format");
    }

    [Fact]
    public void DeanonymizeIp_EnterpriseOnNonHighIntentPage_DoesNotTriggerHighIntentAlert()
    {
        // Acme Corp visiting a documentation/blog page
        var request = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_10",
            IpAddress: "198.51.100.25", // Acme Corp seeded IP
            VisitedPage: "/blog/scale-architecture"
        );

        var result = _engine.DeanonymizeIp(request);

        result.MatchFound.Should().BeTrue();
        result.IcpFit.Should().NotBeNull();
        result.IcpFit!.FitTier.Should().Be("Tier_A");
        result.TriggeredSalesAlert.Should().BeFalse(); // Not high intent page
        result.AlertDetails.Should().Contain("Standard nurturing assigned");
    }

    [Fact]
    public void DeanonymizeIp_MegacorpEnterpriseFinance_AchievesTierAWithComposite100()
    {
        // Megacorp: Finance, 45000 employees, $5B+
        var request = new DeanonymizeIpRequest(
            TenantId: "tenant_growth_11",
            IpAddress: "12.145.20.50",
            VisitedPage: "/checkout"
        );

        var result = _engine.DeanonymizeIp(request);

        result.MatchFound.Should().BeTrue();
        result.Company.Should().NotBeNull();
        result.Company!.CompanyName.Should().Be("Megacorp");
        result.IcpFit.Should().NotBeNull();
        result.IcpFit!.CompositeScore.Should().Be(100);
        result.IcpFit.FitTier.Should().Be("Tier_A");
        result.TriggeredSalesAlert.Should().BeTrue();
        result.AlertDetails.Should().Contain("[HIGH INTENT SALES ALERT]");
    }

    [Fact]
    public void DeanonymizeIp_TenantCustomIpRange_MatchesCustomTenantCompany()
    {
        var tenantId = "tenant_custom_subnet_12";
        var customCompany = new FirmographicCompanyProfile(
            Domain: "biovance.eu",
            CompanyName: "Biovance Pharma",
            Industry: "Healthcare",
            EmployeeCount: 8200,
            RevenueRange: "$500M - $1B",
            HeadquartersCity: "Basel",
            HeadquartersCountry: "Switzerland",
            TechStack: new List<string> { "Kubernetes", "Kafka", "PostgreSQL" }
        );

        _engine.RegisterCompanyProfile(tenantId, customCompany, new List<string> { "185.120.44.0/24" });

        var request = new DeanonymizeIpRequest(
            TenantId: tenantId,
            IpAddress: "185.120.44.15",
            VisitedPage: "/demo"
        );

        var result = _engine.DeanonymizeIp(request);

        result.MatchFound.Should().BeTrue();
        result.Company.Should().NotBeNull();
        result.Company!.CompanyName.Should().Be("Biovance Pharma");
        result.Company.Domain.Should().Be("biovance.eu");
        result.TriggeredSalesAlert.Should().BeTrue();
    }

    [Fact]
    public void RegisterTenantIcpRules_NullRules_ResetsToEmptyListSafely()
    {
        var tenantId = "tenant_growth_13";
        _engine.RegisterTenantIcpRules(tenantId, null!);

        // Should fall back to default ICP rules safely without throwing
        var request = new DeanonymizeIpRequest(
            TenantId: tenantId,
            IpAddress: "198.51.100.25",
            VisitedPage: "/pricing"
        );

        var result = _engine.DeanonymizeIp(request);
        result.MatchFound.Should().BeTrue();
        result.IcpFit.Should().NotBeNull();
        result.IcpFit!.FitTier.Should().Be("Tier_A");
    }
}
