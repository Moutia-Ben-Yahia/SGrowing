using FluentAssertions;
using SGrowing.Api.Modules.CustomerData.IdentityResolution;
using Xunit;

namespace SGrowing.Tests.CustomerData;

public class IdentityResolutionTests
{
    private readonly CdpIdentityEngine _engine = new();

    [Fact]
    public void StitchIdentity_WithNewAnonymousAndKnown_CreatesCanonicalProfileAndLinksBoth()
    {
        var request = new StitchIdentityRequest(
            TenantId: "tenant_growth_01",
            AnonymousId: "cookie_anon_abc123",
            KnownIdentifier: new IdentityIdentifier(IdentityType.Email, "alex.morgan@growthops.io"),
            SessionId: "sess_1001",
            AdditionalTraits: new Dictionary<string, string> { { "lead_source", "google_organic" } }
        );

        var result = _engine.StitchIdentity(request);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.WasMerged.Should().BeFalse();
        result.CanonicalProfileId.Should().StartWith("prof_");
        result.LinkedIdentifiersCount.Should().Be(2);
        result.OverallConfidence.Should().Be(1.0);
        result.MergedSessionIds.Should().Contain("sess_1001");

        var profile = _engine.GetProfileGraph("tenant_growth_01", result.CanonicalProfileId);
        profile.Should().NotBeNull();
        profile!.Identifiers.Should().HaveCount(2);
        profile.UnifiedTraits.Should().ContainKey("lead_source").WhoseValue.Should().Be("google_organic");
    }

    [Fact]
    public void StitchIdentity_WithExistingAnonymousSession_AttachesSessionToProfile()
    {
        var tenantId = "tenant_growth_02";
        var anonId = "anon_visitor_99";
        var sessionId = "sess_early_browsing";

        _engine.IngestSession(tenantId, new VisitorSessionRecord(
            SessionId: sessionId,
            AnonymousId: anonId,
            StartedAtUtc: DateTime.UtcNow.AddHours(-1),
            EndedAtUtc: DateTime.UtcNow.AddMinutes(-30),
            IpAddress: "192.168.1.1",
            UserAgent: "Mozilla/5.0",
            PageViews: new List<string> { "/pricing", "/demo", "/features" },
            UtmParameters: new Dictionary<string, string> { { "utm_campaign", "black_friday" } }
        ));

        var stitchRequest = new StitchIdentityRequest(
            TenantId: tenantId,
            AnonymousId: anonId,
            KnownIdentifier: new IdentityIdentifier(IdentityType.CustomerId, "cust_corp_88"),
            SessionId: sessionId
        );

        var result = _engine.StitchIdentity(stitchRequest);
        result.Success.Should().BeTrue();

        var sessions = _engine.GetSessionsForProfile(tenantId, result.CanonicalProfileId);
        sessions.Should().HaveCount(1);
        sessions[0].SessionId.Should().Be(sessionId);
        sessions[0].PageViews.Should().Contain("/pricing");
    }

    [Fact]
    public void StitchIdentity_WhenAnonymousAndKnownBelongToDifferentProfiles_MergesProfilesAndRemapsIdentifiers()
    {
        var tenantId = "tenant_growth_03";

        // Step 1: Stitch anon_1 to device_phone
        var req1 = new StitchIdentityRequest(
            TenantId: tenantId,
            AnonymousId: "anon_laptop_111",
            KnownIdentifier: new IdentityIdentifier(IdentityType.DeviceId, "device_macbook_pro"),
            SessionId: "sess_laptop",
            AdditionalTraits: new Dictionary<string, string> { { "browser", "chrome" } }
        );
        var res1 = _engine.StitchIdentity(req1);

        // Step 2: Stitch anon_2 to verified email
        var req2 = new StitchIdentityRequest(
            TenantId: tenantId,
            AnonymousId: "anon_phone_222",
            KnownIdentifier: new IdentityIdentifier(IdentityType.Email, "ceo@enterprise.com"),
            SessionId: "sess_phone",
            AdditionalTraits: new Dictionary<string, string> { { "tier", "enterprise" } }
        );
        var res2 = _engine.StitchIdentity(req2);

        res1.CanonicalProfileId.Should().NotBe(res2.CanonicalProfileId);

        // Step 3: Now anon_laptop_111 logs in with ceo@enterprise.com
        var mergeReq = new StitchIdentityRequest(
            TenantId: tenantId,
            AnonymousId: "anon_laptop_111",
            KnownIdentifier: new IdentityIdentifier(IdentityType.Email, "ceo@enterprise.com"),
            SessionId: "sess_cross_device"
        );
        var mergeRes = _engine.StitchIdentity(mergeReq);

        mergeRes.Success.Should().BeTrue();
        mergeRes.WasMerged.Should().BeTrue();
        mergeRes.CanonicalProfileId.Should().Be(res2.CanonicalProfileId);

        // Verify resolved profile contains unified traits and all sessions
        var unifiedProfile = _engine.GetProfileGraph(tenantId, mergeRes.CanonicalProfileId);
        unifiedProfile.Should().NotBeNull();
        unifiedProfile!.UnifiedTraits.Should().ContainKey("browser").WhoseValue.Should().Be("chrome");
        unifiedProfile.UnifiedTraits.Should().ContainKey("tier").WhoseValue.Should().Be("enterprise");
        unifiedProfile.AssociatedSessionIds.Should().Contain(new[] { "sess_laptop", "sess_phone", "sess_cross_device" });

        // Querying by old device ID should now resolve to the unified canonical profile
        var resolvedDevice = _engine.ResolveIdentifier(tenantId, new IdentityIdentifier(IdentityType.DeviceId, "device_macbook_pro"));
        resolvedDevice.Should().NotBeNull();
        resolvedDevice!.CanonicalProfileId.Should().Be(mergeRes.CanonicalProfileId);
    }

    [Fact]
    public void ResolveIdentifier_WithNormalizedEmail_FindsSameProfileRegardlessOfCasing()
    {
        var tenantId = "tenant_growth_04";
        var request = new StitchIdentityRequest(
            TenantId: tenantId,
            AnonymousId: "anon_visitor_case_test",
            KnownIdentifier: new IdentityIdentifier(IdentityType.Email, "User.Name@Example.COM  ")
        );

        var stitchResult = _engine.StitchIdentity(request);
        stitchResult.Success.Should().BeTrue();

        var queryLower = new IdentityIdentifier(IdentityType.Email, "user.name@example.com");
        var resolved = _engine.ResolveIdentifier(tenantId, queryLower);

        resolved.Should().NotBeNull();
        resolved!.CanonicalProfileId.Should().Be(stitchResult.CanonicalProfileId);
    }

    [Fact]
    public void StitchIdentity_WithPhoneIdentifier_NormalizesFormattingAndAssignsCorrectConfidence()
    {
        var tenantId = "tenant_growth_05";
        var request = new StitchIdentityRequest(
            TenantId: tenantId,
            AnonymousId: "anon_mobile_sms",
            KnownIdentifier: new IdentityIdentifier(IdentityType.PhoneNumber, "+1 (555) 234-5678")
        );

        var result = _engine.StitchIdentity(request);
        result.Success.Should().BeTrue();
        result.OverallConfidence.Should().Be(0.95);

        var query = new IdentityIdentifier(IdentityType.PhoneNumber, "+15552345678");
        var profile = _engine.ResolveIdentifier(tenantId, query);
        profile.Should().NotBeNull();
        profile!.CanonicalProfileId.Should().Be(result.CanonicalProfileId);
    }

    [Theory]
    [InlineData(null, "anon_1", "email@test.com")]
    [InlineData("", "anon_1", "email@test.com")]
    [InlineData("tenant_1", null, "email@test.com")]
    [InlineData("tenant_1", "", "email@test.com")]
    public void StitchIdentity_ValidatesRequiredFields(string? tenantId, string? anonId, string email)
    {
        var request = new StitchIdentityRequest(
            TenantId: tenantId!,
            AnonymousId: anonId!,
            KnownIdentifier: new IdentityIdentifier(IdentityType.Email, email)
        );

        var action = () => _engine.StitchIdentity(request);
        action.Should().Throw<System.ArgumentException>();
    }

    [Fact]
    public void StitchIdentity_ThrowsWhenKnownIdentifierEmpty()
    {
        var request = new StitchIdentityRequest(
            TenantId: "tenant_val",
            AnonymousId: "anon_val",
            KnownIdentifier: new IdentityIdentifier(IdentityType.Email, "")
        );

        var action = () => _engine.StitchIdentity(request);
        action.Should().Throw<System.ArgumentException>();
    }

    [Fact]
    public void GetProfileGraph_ReturnsNullForNonExistentProfile()
    {
        var profile = _engine.GetProfileGraph("tenant_unknown", "prof_non_existent_99");
        profile.Should().BeNull();
    }

    [Fact]
    public void ResolveIdentifier_ReturnsNullForUnregisteredIdentifier()
    {
        var profile = _engine.ResolveIdentifier("tenant_unknown", new IdentityIdentifier(IdentityType.Email, "never_seen@test.com"));
        profile.Should().BeNull();
    }

    [Fact]
    public void GetSessionsForProfile_ReturnsIngestedSessions()
    {
        var tenantId = "tenant_sessions_test";
        var anonId = "anon_visitor_session_test";
        var sessId = "sess_rec_9988";

        _engine.IngestSession(tenantId, new VisitorSessionRecord(
            SessionId: sessId,
            AnonymousId: anonId,
            StartedAtUtc: System.DateTime.UtcNow.AddMinutes(-20),
            EndedAtUtc: System.DateTime.UtcNow,
            IpAddress: "127.0.0.1",
            UserAgent: "Browser",
            PageViews: new List<string> { "/cart", "/checkout" },
            UtmParameters: new Dictionary<string, string>()
        ));

        var stitch = _engine.StitchIdentity(new StitchIdentityRequest(
            TenantId: tenantId,
            AnonymousId: anonId,
            KnownIdentifier: new IdentityIdentifier(IdentityType.Email, "shopper@test.com"),
            SessionId: sessId
        ));

        var sessions = _engine.GetSessionsForProfile(tenantId, stitch.CanonicalProfileId);
        sessions.Should().NotBeNull();
        sessions.Should().Contain(s => s.SessionId == sessId);
    }
}
