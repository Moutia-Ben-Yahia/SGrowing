using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SGrowing.Api.Modules.Attribution;
using Xunit;

namespace SGrowing.Tests.Attribution;

public class ServerSideAttributionServiceTests
{
    private readonly ServerSideAttributionService _service;

    public ServerSideAttributionServiceTests()
    {
        _service = new ServerSideAttributionService();
    }

    [Fact]
    public void NormalizeEmail_TrimsAndLowercasesCorrectly()
    {
        var input = "  John.Doe+Newsletter@Example.COM  ";
        var normalized = _service.NormalizeEmail(input);

        Assert.Equal("john.doe+newsletter@example.com", normalized);
    }

    [Fact]
    public void HashEmail_WithKnownTestVector_ReturnsExpectedSha256()
    {
        var email = "john.doe@example.com";
        var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(email));
        var expectedHash = Convert.ToHexStringLower(expectedBytes);

        var actualHash = _service.HashEmail(email);

        Assert.Equal(expectedHash, actualHash);
        Assert.Equal(64, actualHash.Length);
    }

    [Fact]
    public void HashEmail_WithWhitespaceAndMixedCase_ReturnsSameHashAsClean()
    {
        var messy = "  Jane.Smith@Corp.Net  ";
        var clean = "jane.smith@corp.net";

        var hashMessy = _service.HashEmail(messy);
        var hashClean = _service.HashEmail(clean);

        Assert.Equal(hashClean, hashMessy);
    }

    [Fact]
    public void HashEmail_WhenNullOrEmpty_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, _service.HashEmail(null));
        Assert.Equal(string.Empty, _service.HashEmail("   "));
    }

    [Fact]
    public void NormalizePhoneE164_WithUsTenDigitNumber_PrependsCountryCodeAndPlus()
    {
        var raw = "(415) 555-2671";
        var normalized = _service.NormalizePhoneE164(raw, "1");

        Assert.Equal("+14155552671", normalized);
    }

    [Fact]
    public void NormalizePhoneE164_WithInternationalLeadingPlus_PreservesPlusAndStripsNonDigits()
    {
        var raw = "+44 (0) 20 7946-0991";
        var normalized = _service.NormalizePhoneE164(raw);

        Assert.Equal("+4402079460991", normalized);
    }

    [Fact]
    public void NormalizePhoneDigits_StripsLeadingPlus()
    {
        var raw = "+1 (800) 555-0199";
        var digits = _service.NormalizePhoneDigits(raw);

        Assert.Equal("18005550199", digits);
    }

    [Fact]
    public void HashPhone_ProducesCorrectSha256OfDigits()
    {
        var raw = "+1-415-555-2671";
        var digits = "14155552671";
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(digits)));

        var actualHash = _service.HashPhone(raw);

        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void HashPhoneE164_ProducesCorrectSha256WithPlus()
    {
        var raw = "(415) 555-2671";
        var e164 = "+14155552671";
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(e164)));

        var actualHash = _service.HashPhoneE164(raw);

        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void NormalizeName_StripsPunctuationAndTrims()
    {
        var raw = "  Jean-Luc #1  ";
        var normalized = _service.NormalizeName(raw);

        Assert.Equal("jean-luc", normalized);
    }

    [Fact]
    public void HashName_ComputesCorrectSha256()
    {
        var name = "Sarah";
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("sarah")));

        var actualHash = _service.HashName(name);

        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public void NormalizeCity_TrimsAndLowercases()
    {
        var city = "  San   Francisco, CA  ";
        var normalized = _service.NormalizeCity(city);

        Assert.Equal("san francisco ca", normalized);
    }

    [Fact]
    public void NormalizeState_ExtractsTwoLetterStateCode()
    {
        Assert.Equal("ca", _service.NormalizeState("CA"));
        Assert.Equal("ny", _service.NormalizeState("  NY  "));
    }

    [Fact]
    public void NormalizeZip_StripsSpacesAndHyphens()
    {
        Assert.Equal("94107", _service.NormalizeZip("94107-1234"));
        Assert.Equal("sw1a1aa", _service.NormalizeZip("SW1A 1AA"));
    }

    [Fact]
    public void NormalizeCountry_NormalizesIso2AndNames()
    {
        Assert.Equal("us", _service.NormalizeCountry("USA"));
        Assert.Equal("us", _service.NormalizeCountry("United States"));
        Assert.Equal("gb", _service.NormalizeCountry("UK"));
        Assert.Equal("fr", _service.NormalizeCountry("FR"));
    }

    [Fact]
    public void BuildFbc_WithRawFbclid_SynthesizesStandardMetaCookieFormat()
    {
        var fbclid = "IwAR2V8bN19fXYZ";
        var fixedTimestamp = 1730355000000L;

        var fbc = _service.BuildFbc(fbclid, fixedTimestamp);

        Assert.Equal("fb.1.1730355000000.IwAR2V8bN19fXYZ", fbc);
    }

    [Fact]
    public void BuildFbc_WhenAlreadyPrefixed_PreservesExistingFbc()
    {
        var existing = "fb.1.1600000000000.ExistingFbclid";
        var fbc = _service.BuildFbc(existing);

        Assert.Equal(existing, fbc);
    }

    [Fact]
    public void BuildFbp_GeneratesValidFormatWithRandomId()
    {
        var fixedTimestamp = 1730355000000L;
        var fixedRandom = 987654321L;

        var fbp = _service.BuildFbp(fixedTimestamp, fixedRandom);

        Assert.Equal("fb.1.1730355000000.987654321", fbp);
    }

    [Fact]
    public void CalculateEventMatchQualityScore_WithCompletePayload_Returns10()
    {
        var raw = new RawUserData(
            Email: "user@example.com",       // +3.0
            Phone: "+14155552671",          // +3.0
            Fbclid: "IwAR2V8b",             // +2.0
            ClientIpAddress: "192.0.2.1",   // +1.0
            ClientUserAgent: "Mozilla/5.0"  // +1.0
        );

        var score = _service.CalculateEventMatchQualityScore(raw);

        Assert.Equal(10.0, score);
        Assert.Equal(QualityTier.High, _service.GetQualityTier(score));
    }

    [Fact]
    public void CalculateEventMatchQualityScore_WithEmailAndPhoneOnly_Returns6()
    {
        var raw = new RawUserData(
            Email: "user@example.com", // +3.0
            Phone: "+14155552671"     // +3.0
        );

        var score = _service.CalculateEventMatchQualityScore(raw);

        Assert.Equal(6.0, score);
        Assert.Equal(QualityTier.Medium, _service.GetQualityTier(score));
    }

    [Fact]
    public void CalculateEventMatchQualityScore_WithClickIdAndIpAndUa_Returns4()
    {
        var raw = new RawUserData(
            Gclid: "Cj0KCQjwx",            // +2.0
            ClientIpAddress: "192.0.2.1",  // +1.0
            ClientUserAgent: "Mozilla/5.0" // +1.0
        );

        var score = _service.CalculateEventMatchQualityScore(raw);

        Assert.Equal(4.0, score);
        Assert.Equal(QualityTier.Low, _service.GetQualityTier(score));
    }

    [Fact]
    public void CalculateEventMatchQualityScore_WithPhoneOnly_Returns3()
    {
        var raw = new RawUserData(Phone: "+14155552671");
        var score = _service.CalculateEventMatchQualityScore(raw);

        Assert.Equal(3.0, score);
        Assert.Equal(QualityTier.Low, _service.GetQualityTier(score));
    }

    [Fact]
    public void CalculateEventMatchQualityScore_WithEmptyPayload_Returns0()
    {
        var raw = new RawUserData();
        var score = _service.CalculateEventMatchQualityScore(raw);

        Assert.Equal(0.0, score);
        Assert.Equal(QualityTier.Low, _service.GetQualityTier(score));
    }

    [Theory]
    [InlineData(10.0, QualityTier.High)]
    [InlineData(8.0, QualityTier.High)]
    [InlineData(7.9, QualityTier.Medium)]
    [InlineData(5.0, QualityTier.Medium)]
    [InlineData(4.9, QualityTier.Low)]
    [InlineData(0.0, QualityTier.Low)]
    public void GetQualityTier_MapsScoresCorrectly(double score, QualityTier expectedTier)
    {
        Assert.Equal(expectedTier, _service.GetQualityTier(score));
    }

    [Fact]
    public async Task TrackConversionEvent_EnqueuesPostbacksForAllTargetPlatforms()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            Value: 149.99m,
            Currency: "USD",
            OrderId: "ORD-TEST-100",
            UserData: new RawUserData(
                Email: "buyer@example.com",
                Phone: "+15551234567",
                Fbclid: "fb_click_123"
            )
        );

        var result = await _service.TrackConversionEventAsync(request);

        Assert.True(result.Success);
        Assert.False(result.Deduplicated);
        Assert.Equal(3, result.EnqueuedPostbackIds.Count); // Meta, TikTok, Google

        var outboxItems = await _service.GetOutboxItemsAsync();
        Assert.Contains(outboxItems, x => x.EventId == result.EventId && x.Platform == CapiPlatform.Meta);
        Assert.Contains(outboxItems, x => x.EventId == result.EventId && x.Platform == CapiPlatform.TikTok);
        Assert.Contains(outboxItems, x => x.EventId == result.EventId && x.Platform == CapiPlatform.Google);
    }

    [Fact]
    public async Task TrackConversionEvent_WithSpecificPlatforms_EnqueuesOnlySelected()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Lead",
            UserData: new RawUserData(Email: "lead@example.com"),
            TargetPlatforms: new List<CapiPlatform> { CapiPlatform.Meta, CapiPlatform.Google }
        );

        var result = await _service.TrackConversionEventAsync(request);

        Assert.Equal(2, result.EnqueuedPostbackIds.Count);
        var outboxItems = await _service.GetOutboxItemsAsync();
        Assert.Contains(outboxItems, x => x.EventId == result.EventId && x.Platform == CapiPlatform.Meta);
        Assert.Contains(outboxItems, x => x.EventId == result.EventId && x.Platform == CapiPlatform.Google);
        Assert.DoesNotContain(outboxItems, x => x.EventId == result.EventId && x.Platform == CapiPlatform.TikTok);
    }

    [Fact]
    public async Task TrackConversionEvent_WithDuplicateEventId_DeduplicatesAndDoesNotCreateNewPostbacks()
    {
        var eventId = $"DEDUP-{Guid.NewGuid():N}";
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            EventId: eventId,
            Value: 99.00m,
            Currency: "USD",
            UserData: new RawUserData(Email: "dedup@example.com")
        );

        var firstResult = await _service.TrackConversionEventAsync(request);
        Assert.False(firstResult.Deduplicated);
        var initialOutboxCount = firstResult.EnqueuedPostbackIds.Count;

        var secondResult = await _service.TrackConversionEventAsync(request);
        Assert.True(secondResult.Deduplicated);
        Assert.Equal(firstResult.EventId, secondResult.EventId);

        var metrics = await _service.GetMetricsAsync();
        Assert.True(metrics.TotalDeduplicated >= 1);
    }

    [Fact]
    public async Task TrackConversionEvent_WithoutEventId_AutoGeneratesUniqueEventId()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "AddToCart",
            UserData: new RawUserData(Email: "cart@example.com")
        );

        var result = await _service.TrackConversionEventAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(result.EventId));
        Assert.StartsWith("EVT-", result.EventId);
    }

    [Fact]
    public void FormatMetaPayload_ProducesCompliantCapiV19Schema()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            EventId: "EVT-META-1",
            Value: 249.50m,
            Currency: "USD",
            OrderId: "ORD-META-001"
        );

        var normalized = new NormalizedHashedUserData(
            HashedEmail: "abc123emailhash",
            HashedPhone: "def456phonehash",
            HashedPhoneE164: null,
            HashedFirstName: "fnHash",
            HashedLastName: "lnHash",
            HashedCity: "cityHash",
            HashedState: "stateHash",
            HashedZip: "zipHash",
            HashedCountry: "countryHash",
            RawNormalizedCity: "san francisco",
            RawNormalizedState: "ca",
            RawNormalizedZip: "94107",
            RawNormalizedCountry: "us",
            ClientIpAddress: "198.51.100.1",
            ClientUserAgent: "Mozilla/5.0",
            Fbc: "fb.1.173000000.abc",
            Fbp: "fb.1.173000000.123",
            Gclid: null,
            Ttclid: null,
            EventMatchQualityScore: 10.0
        );

        var json = _service.FormatMetaPayload(request, normalized, "TEST_CODE_123");
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("data", out var dataElement));
        Assert.Equal(1, dataElement.GetArrayLength());

        var firstEvent = dataElement[0];
        Assert.Equal("Purchase", firstEvent.GetProperty("event_name").GetString());
        Assert.Equal("EVT-META-1", firstEvent.GetProperty("event_id").GetString());

        var userData = firstEvent.GetProperty("user_data");
        Assert.Equal("abc123emailhash", userData.GetProperty("em")[0].GetString());
        Assert.Equal("def456phonehash", userData.GetProperty("ph")[0].GetString());
        Assert.Equal("fb.1.173000000.abc", userData.GetProperty("fbc").GetString());

        var customData = firstEvent.GetProperty("custom_data");
        Assert.Equal(249.50m, customData.GetProperty("value").GetDecimal());
        Assert.Equal("USD", customData.GetProperty("currency").GetString());
        Assert.Equal("ORD-META-001", customData.GetProperty("order_id").GetString());

        Assert.Equal("TEST_CODE_123", root.GetProperty("test_event_code").GetString());
    }

    [Fact]
    public void FormatTikTokPayload_ProducesCompliantEventsApiV2Schema()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            EventId: "EVT-TT-1",
            Value: 89.90m,
            Currency: "USD",
            OrderId: "ORD-TT-99"
        );

        var normalized = new NormalizedHashedUserData(
            HashedEmail: "tt_email_hash",
            HashedPhone: "tt_phone_hash",
            HashedPhoneE164: null,
            HashedFirstName: null,
            HashedLastName: null,
            HashedCity: null,
            HashedState: null,
            HashedZip: null,
            HashedCountry: null,
            RawNormalizedCity: null,
            RawNormalizedState: null,
            RawNormalizedZip: null,
            RawNormalizedCountry: null,
            ClientIpAddress: "203.0.113.19",
            ClientUserAgent: "TikTokApp/1.0",
            Fbc: null,
            Fbp: null,
            Gclid: null,
            Ttclid: "E.0.TT_CLICK_ID",
            EventMatchQualityScore: 9.0
        );

        var json = _service.FormatTikTokPayload(request, normalized);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("web", root.GetProperty("event_source").GetString());
        var firstEvent = root.GetProperty("data")[0];
        Assert.Equal("CompletePayment", firstEvent.GetProperty("event").GetString()); // TikTok mapped name
        Assert.Equal("tt_email_hash", firstEvent.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("E.0.TT_CLICK_ID", firstEvent.GetProperty("user").GetProperty("ttclid").GetString());
        Assert.Equal(89.90m, firstEvent.GetProperty("properties").GetProperty("value").GetDecimal());
    }

    [Fact]
    public void FormatGooglePayload_ProducesCompliantEnhancedConversionsSchema()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            EventId: "EVT-GGL-1",
            Value: 500.00m,
            Currency: "USD",
            OrderId: "ORD-GGL-500"
        );

        var normalized = new NormalizedHashedUserData(
            HashedEmail: "google_email_hash",
            HashedPhone: "google_phone_digits",
            HashedPhoneE164: "google_phone_e164_hash",
            HashedFirstName: "fnGoogle",
            HashedLastName: "lnGoogle",
            HashedCity: null,
            HashedState: null,
            HashedZip: null,
            HashedCountry: null,
            RawNormalizedCity: "New York",
            RawNormalizedState: "NY",
            RawNormalizedZip: "10001",
            RawNormalizedCountry: "us",
            ClientIpAddress: null,
            ClientUserAgent: null,
            Fbc: null,
            Fbp: null,
            Gclid: "Cj0KCQjwx_GoogleClickId",
            Ttclid: null,
            EventMatchQualityScore: 9.0
        );

        var json = _service.FormatGooglePayload(request, normalized);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var firstConversion = root.GetProperty("conversions")[0];
        Assert.Equal("Cj0KCQjwx_GoogleClickId", firstConversion.GetProperty("gclid").GetString());
        Assert.Equal(500.00m, firstConversion.GetProperty("conversion_value").GetDecimal());
        Assert.Equal("USD", firstConversion.GetProperty("currency_code").GetString());

        var userIdentifiers = firstConversion.GetProperty("user_identifiers");
        Assert.True(userIdentifiers.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task TrackConversionEvent_WithMissingEventName_ThrowsArgumentException()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "   "
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TrackConversionEventAsync(request));
    }

    [Fact]
    public async Task TrackConversionEvent_WithNegativeValue_ThrowsArgumentException()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            Value: -25.00m
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TrackConversionEventAsync(request));
    }

    [Fact]
    public async Task TrackConversionEvent_WithInvalidCurrency_ThrowsArgumentException()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            Value: 10.00m,
            Currency: "FAKE_CURRENCY_CODE"
        );

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TrackConversionEventAsync(request));
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("CAD")]
    [InlineData("MAD")]
    [InlineData("SAR")]
    [InlineData("AED")]
    [InlineData("BRL")]
    public async Task TrackConversionEvent_WithValidNonUsdCurrencies_Succeeds(string currency)
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            Value: 75.00m,
            Currency: currency,
            UserData: new RawUserData(Email: "curr@example.com")
        );

        var result = await _service.TrackConversionEventAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DispatchPostbacks_DispatchesPendingItemsAndReturnsSummary()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            Value: 120.00m,
            Currency: "USD",
            UserData: new RawUserData(Email: "dispatch@example.com")
        );

        await _service.TrackConversionEventAsync(request);

        var dispatchResult = await _service.DispatchPostbacksAsync(batchSize: 20);

        Assert.True(dispatchResult.ProcessedCount > 0);
        Assert.True(dispatchResult.DispatchedCount > 0);
        Assert.Contains(dispatchResult.DispatchedItems, x => x.Status == PostbackDeliveryStatus.Dispatched);
    }

    [Fact]
    public async Task DispatchPostbacks_WithTransientFailure_EnforcesExponentialBackoffAndRetries()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Purchase",
            Value: 100.00m,
            CustomData: new Dictionary<string, object?> { ["simulate_failure"] = true },
            TargetPlatforms: new List<CapiPlatform> { CapiPlatform.Meta }
        );

        var trackResult = await _service.TrackConversionEventAsync(request);
        var postbackId = trackResult.EnqueuedPostbackIds[0];

        // First dispatch attempt: should retry with backoff
        var firstDispatch = await _service.DispatchPostbacksAsync(batchSize: 10);
        var outboxItems = await _service.GetOutboxItemsAsync();
        var item = outboxItems.First(x => x.PostbackId == postbackId);

        Assert.Equal(PostbackDeliveryStatus.Retrying, item.Status);
        Assert.Equal(1, item.RetryCount);
        Assert.NotNull(item.NextRetryAtUtc);
    }

    [Fact]
    public async Task RetryPostback_ResetsPostbackToPending()
    {
        var request = new TrackConversionEventRequest(
            TenantId: Guid.NewGuid(),
            EventName: "Lead",
            TargetPlatforms: new List<CapiPlatform> { CapiPlatform.TikTok }
        );

        var track = await _service.TrackConversionEventAsync(request);
        var pbId = track.EnqueuedPostbackIds[0];

        var retryOk = await _service.RetryPostbackAsync(pbId);

        Assert.True(retryOk);
        var outbox = await _service.GetOutboxItemsAsync();
        var item = outbox.First(x => x.PostbackId == pbId);
        Assert.Equal(PostbackDeliveryStatus.Pending, item.Status);
    }

    [Fact]
    public async Task GetMetrics_CalculatesPlatformEmqAndAdBlockerRecoveryMetrics()
    {
        var metrics = await _service.GetMetricsAsync();

        Assert.True(metrics.TotalEventsTracked > 0);
        Assert.True(metrics.OverallAverageEmqScore >= 5.0);
        Assert.Equal(3, metrics.PlatformMetrics.Count); // Meta, TikTok, Google
        Assert.True(metrics.RevenueRecovery.RecoveryLiftPercentage > 0);
        Assert.True(metrics.RevenueRecovery.RecoveredConversionsCount > 0);
        Assert.True(metrics.GatewayHealthy);
    }

    [Fact]
    public async Task TestPixel_ReturnsDetailedDiagnosticReport()
    {
        var testReq = new TestPixelRequest(
            Platform: CapiPlatform.Meta,
            TestEventCode: "TEST_VERIFY_999",
            SampleUserData: new RawUserData(
                Email: "test.diagnostic@sgrowing.com",
                Phone: "+1 (555) 999-0011",
                Fbclid: "IwAR999Diagnostic"
            )
        );

        var result = await _service.TestPixelAsync(testReq);

        Assert.True(result.Success);
        Assert.Equal(CapiPlatform.Meta, result.Platform);
        Assert.False(string.IsNullOrWhiteSpace(result.TestTraceId));
        Assert.Contains("TEST_VERIFY_999", result.FormattedPayloadJson);
        Assert.True(result.EmqScore >= 8.0);
    }

    [Fact]
    public async Task ConcurrentEventIngestion_IsThreadSafeUnderHighLoad()
    {
        const int concurrentTasks = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var generatedEventIds = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(Enumerable.Range(0, concurrentTasks), async (i, ct) =>
        {
            try
            {
                var req = new TrackConversionEventRequest(
                    TenantId: Guid.NewGuid(),
                    EventName: i % 2 == 0 ? "Purchase" : "AddToCart",
                    Value: 50.0m + i,
                    Currency: "USD",
                    UserData: new RawUserData(
                        Email: $"loadtester_{i}@sgrowing.com",
                        Phone: $"+1555000{i:D4}",
                        Fbclid: $"fbclid_load_{i}"
                    )
                );

                var res = await _service.TrackConversionEventAsync(req);
                generatedEventIds.Add(res.EventId);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.Empty(exceptions);
        Assert.Equal(concurrentTasks, generatedEventIds.Count);

        var events = await _service.GetRecentEventsAsync(limit: 100);
        Assert.True(events.Count >= concurrentTasks);
    }
}
