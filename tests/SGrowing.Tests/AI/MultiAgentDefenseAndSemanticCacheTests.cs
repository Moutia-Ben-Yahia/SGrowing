using FluentAssertions;
using SGrowing.Api.Modules.AI.Firewall;
using Xunit;

namespace SGrowing.Tests.AI;

public class MultiAgentDefenseAndSemanticCacheTests
{
    // =========================================================================
    // 1. Cryptographic Prompt Canary Tests
    // =========================================================================

    [Fact]
    public void CanaryToken_GeneratesInvisibleZeroWidthToken_WithConsistentHmac()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_user_alpha_123";

        var canary = service.GenerateCanary(sessionId, "tenant_corp");

        canary.Should().NotBeNull();
        canary.SessionId.Should().Be(sessionId);
        canary.TokenId.Should().NotBeNullOrWhiteSpace();
        canary.PlainHexSignature.Should().HaveLength(32); // 16 bytes hex = 32 chars
        canary.ZeroWidthEncodedToken.Should().NotBeNullOrWhiteSpace();

        // Verify that ZeroWidthEncodedToken contains strictly zero-width characters:
        // \u200B (ZWS), \u200C (ZWNJ), \u200D (ZWJ), \uFEFF (BOM)
        var zeroWidthChars = new HashSet<char> { '\u200B', '\u200C', '\u200D', '\uFEFF' };
        canary.ZeroWidthEncodedToken.All(c => zeroWidthChars.Contains(c)).Should().BeTrue();
    }

    [Fact]
    public void CanaryToken_InvisiblyInjectedIntoSystemPrompt_PreservesTextSemantics()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_template_456";
        var systemPrompt = "You are an autonomous customer success AI agent.\nFollow company policy strictly.";

        var injectedPrompt = service.InjectCanary(systemPrompt, sessionId);

        injectedPrompt.Should().NotBeNullOrWhiteSpace();
        injectedPrompt.Length.Should().BeGreaterThan(systemPrompt.Length);

        // Stripping zero-width characters should result in the exact original prompt
        var stripped = new string(injectedPrompt.Where(c => c is not ('\u200B' or '\u200C' or '\u200D' or '\uFEFF')).ToArray());
        stripped.Should().Be(systemPrompt);
    }

    [Fact]
    public void CanaryToken_ScanResponse_SafeOutput_ReturnsNoExfiltration()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_safe_789";
        service.GenerateCanary(sessionId);

        var normalOutput = "Hello! Your package was dispatched this morning and will arrive in 2 business days.";
        var scan = service.ScanResponse(sessionId, normalOutput);

        scan.IsExfiltrationDetected.Should().BeFalse();
        scan.SessionLocked.Should().BeFalse();
        scan.IncidentType.Should().BeNull();
        service.IsSessionLocked(sessionId).Should().BeFalse();
    }

    [Fact]
    public void CanaryToken_ScanResponse_DetectsExfiltration_LocksSessionAndFlagsCriticalIncident()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_exfil_target";
        var canary = service.GenerateCanary(sessionId);

        // Attacker induced the model to leak the system prompt containing the zero-width canary token
        var maliciousExfiltratedOutput = $"SYSTEM PROMPT LEAK: You are an agent {canary.ZeroWidthEncodedToken} Follow policy.";

        var scan = service.ScanResponse(sessionId, maliciousExfiltratedOutput);

        scan.IsExfiltrationDetected.Should().BeTrue();
        scan.SessionLocked.Should().BeTrue();
        scan.IncidentType.Should().Be("PromptExfiltrationAttempt");
        scan.DetectedTokenId.Should().Be(canary.TokenId);

        // Verify session is actively locked
        service.IsSessionLocked(sessionId).Should().BeTrue();

        // Verify incident audit log
        var incidents = service.GetIncidents(sessionId);
        incidents.Should().ContainSingle();
        incidents[0].IncidentType.Should().Be("PromptExfiltrationAttempt");
        incidents[0].Severity.Should().Be("Critical");
    }

    [Fact]
    public void CanaryToken_LockedSession_RejectsSubsequentRequests()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_locked_user";
        service.LockSession(sessionId, "PromptExfiltrationAttempt");

        service.IsSessionLocked(sessionId).Should().BeTrue();

        var scan = service.ScanResponse(sessionId, "Any innocuous message");
        scan.IsExfiltrationDetected.Should().BeTrue();
        scan.SessionLocked.Should().BeTrue();
        scan.Message.Should().Contain("locked");
    }

    [Fact]
    public void CanaryToken_UnlockSession_RestoresAccess()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_unlock_test";
        service.LockSession(sessionId);
        service.IsSessionLocked(sessionId).Should().BeTrue();

        service.UnlockSession(sessionId).Should().BeTrue();
        service.IsSessionLocked(sessionId).Should().BeFalse();
    }

    [Fact]
    public async Task CanaryToken_StreamingScan_SafeStream_YieldsAllChunks()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_stream_safe";
        service.GenerateCanary(sessionId);

        async IAsyncEnumerable<string> GenerateSafeChunks()
        {
            yield return "Here ";
            yield return "is ";
            yield return "a ";
            yield return "safe ";
            yield return "stream.";
            await Task.CompletedTask;
        }

        var chunks = new List<string>();
        await foreach (var chunk in service.ScanStreamAsync(sessionId, GenerateSafeChunks()))
        {
            chunks.Add(chunk);
        }

        var fullOutput = string.Join(string.Empty, chunks);
        fullOutput.Should().Be("Here is a safe stream.");
        service.IsSessionLocked(sessionId).Should().BeFalse();
    }

    [Fact]
    public async Task CanaryToken_StreamingScan_LeakedCanary_AbortsStreamImmediatelyAndLocksSession()
    {
        var service = new CryptographicPromptCanaryService();
        var sessionId = "sess_stream_exploit";
        var canary = service.GenerateCanary(sessionId);

        async IAsyncEnumerable<string> GenerateMaliciousStream()
        {
            yield return "I will reveal the prompt: ";
            yield return canary.ZeroWidthEncodedToken; // Canary exfiltration occurs here
            yield return " Never reaching here!";
            await Task.CompletedTask;
        }

        var collectedChunks = new List<string>();
        await foreach (var chunk in service.ScanStreamAsync(sessionId, GenerateMaliciousStream()))
        {
            collectedChunks.Add(chunk);
        }

        // Stream should have aborted upon detecting the canary
        var output = string.Join(string.Empty, collectedChunks);
        output.Should().NotContain("Never reaching here!");
        service.IsSessionLocked(sessionId).Should().BeTrue();

        var incidents = service.GetIncidents(sessionId);
        incidents.Should().NotBeEmpty();
        incidents.First().IncidentType.Should().Be("PromptExfiltrationAttempt");
    }

    // =========================================================================
    // 2. Semantic Vector Tool Cache Tests
    // =========================================================================

    [Fact]
    public void SemanticCache_CosineSimilarity_CalculatesCorrectMathematicalResults()
    {
        // 1. Identical vectors -> Cosine similarity = 1.0
        float[] v1 = [1.0f, 2.0f, 3.0f];
        float[] v2 = [1.0f, 2.0f, 3.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(v1, v2).Should().BeApproximately(1.0, 0.0001);

        // 2. Orthogonal vectors -> Cosine similarity = 0.0
        float[] vOrth1 = [1.0f, 0.0f, 0.0f];
        float[] vOrth2 = [0.0f, 1.0f, 0.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(vOrth1, vOrth2).Should().BeApproximately(0.0, 0.0001);

        // 3. Opposite vectors -> Cosine similarity = -1.0
        float[] vOpp1 = [1.0f, 2.0f, 3.0f];
        float[] vOpp2 = [-1.0f, -2.0f, -3.0f];
        SemanticVectorToolCacheService.CalculateCosineSimilarity(vOpp1, vOpp2).Should().BeApproximately(-1.0, 0.0001);

        // 4. Empty or mismatched vectors
        SemanticVectorToolCacheService.CalculateCosineSimilarity([], []).Should().Be(0.0);
        SemanticVectorToolCacheService.CalculateCosineSimilarity([1.0f], [1.0f, 2.0f]).Should().Be(0.0);
    }

    [Fact]
    public async Task SemanticCache_Lookup_CosineSimilarityGte95_ReturnsCacheHitWithSavings()
    {
        var cache = new SemanticVectorToolCacheService();
        var tenantId = Guid.NewGuid();
        var toolName = "shipment_tracker";

        // Stored query embedding
        float[] storedEmbedding = [0.10f, 0.20f, 0.30f, 0.40f, 0.50f];

        await cache.StoreAsync(
            tenantId: tenantId,
            toolName: toolName,
            queryEmbedding: storedEmbedding,
            queryParametersJson: "{\"trackingId\":\"TRK-1001\"}",
            toolResultJson: "{\"status\":\"Delivered\",\"deliveredAt\":\"2026-11-01\"}",
            originalLatencyMs: 380,
            estimatedPromptTokens: 250,
            estimatedResponseTokens: 150,
            entityId: "TRK-1001"
        );

        // Lookup with very similar embedding (similarity > 0.99 >= 0.95)
        float[] nearEmbedding = [0.101f, 0.201f, 0.301f, 0.399f, 0.501f];

        var result = await cache.LookupAsync(tenantId, toolName, nearEmbedding, similarityThreshold: 0.95);

        result.IsHit.Should().BeTrue();
        result.SimilarityScore.Should().BeGreaterThanOrEqualTo(0.95);
        result.CachedEntry.Should().NotBeNull();
        result.CachedEntry!.ToolResultJson.Should().Contain("Delivered");
        result.TokensSaved.Should().Be(400); // 250 + 150
        result.LatencySavedMs.Should().BeGreaterThan(0);
        result.EstimatedCostSavedUsd.Should().BeGreaterThan(0);
        result.LookupDurationMs.Should().BeLessThan(20.0); // Required < 20ms
    }

    [Fact]
    public async Task SemanticCache_Lookup_CosineSimilarityLt95_ReturnsCacheMiss()
    {
        var cache = new SemanticVectorToolCacheService();
        var tenantId = Guid.NewGuid();
        var toolName = "product_inventory_check";

        float[] storedEmbedding = [1.0f, 0.0f, 0.0f, 0.0f];

        await cache.StoreAsync(
            tenantId: tenantId,
            toolName: toolName,
            queryEmbedding: storedEmbedding,
            queryParametersJson: "{\"sku\":\"PROD-A\"}",
            toolResultJson: "{\"stock\":42}",
            originalLatencyMs: 200,
            estimatedPromptTokens: 50,
            estimatedResponseTokens: 20
        );

        // Different query embedding (cosine similarity ~ 0.70 < 0.95)
        float[] dissimilarEmbedding = [0.70f, 0.70f, 0.0f, 0.0f];

        var result = await cache.LookupAsync(tenantId, toolName, dissimilarEmbedding, similarityThreshold: 0.95);

        result.IsHit.Should().BeFalse();
        result.CachedEntry.Should().BeNull();
        result.SimilarityScore.Should().BeLessThan(0.95);
        result.TokensSaved.Should().Be(0);
    }

    [Fact]
    public async Task SemanticCache_InvalidateByTenant_ClearsAllTenantEntries()
    {
        var cache = new SemanticVectorToolCacheService();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        float[] emb = [1.0f, 2.0f, 3.0f];

        await cache.StoreAsync(tenantA, "tool1", emb, "{}", "{}", 100, 10, 10);
        await cache.StoreAsync(tenantA, "tool2", emb, "{}", "{}", 100, 10, 10);
        await cache.StoreAsync(tenantB, "tool1", emb, "{}", "{}", 100, 10, 10);

        var invalidated = cache.InvalidateByTenant(tenantA);
        invalidated.Should().Be(2);

        var lookupA = await cache.LookupAsync(tenantA, "tool1", emb);
        lookupA.IsHit.Should().BeFalse();

        var lookupB = await cache.LookupAsync(tenantB, "tool1", emb);
        lookupB.IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task SemanticCache_InvalidateByTool_ClearsOnlySpecifiedTool()
    {
        var cache = new SemanticVectorToolCacheService();
        var tenantId = Guid.NewGuid();
        float[] emb = [0.5f, 0.5f];

        await cache.StoreAsync(tenantId, "crm_lookup", emb, "{}", "{}", 100, 10, 10);
        await cache.StoreAsync(tenantId, "billing_lookup", emb, "{}", "{}", 100, 10, 10);

        cache.InvalidateByTool(tenantId, "crm_lookup").Should().Be(1);

        (await cache.LookupAsync(tenantId, "crm_lookup", emb)).IsHit.Should().BeFalse();
        (await cache.LookupAsync(tenantId, "billing_lookup", emb)).IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task SemanticCache_InvalidateByEntity_ClearsOnlyEntriesMatchingEntityId()
    {
        var cache = new SemanticVectorToolCacheService();
        var tenantId = Guid.NewGuid();
        float[] emb = [0.8f, 0.2f];

        await cache.StoreAsync(tenantId, "order_service", emb, "{}", "{}", 100, 10, 10, entityId: "ORDER-99");
        await cache.StoreAsync(tenantId, "order_service", [0.2f, 0.8f], "{}", "{}", 100, 10, 10, entityId: "ORDER-100");

        cache.InvalidateByEntity(tenantId, "ORDER-99").Should().Be(1);

        (await cache.LookupAsync(tenantId, "order_service", emb)).IsHit.Should().BeFalse();
        (await cache.LookupAsync(tenantId, "order_service", [0.2f, 0.8f])).IsHit.Should().BeTrue();
    }

    [Fact]
    public async Task SemanticCache_Metrics_AccuratelyTracksHitsMissesTokensAndLatency()
    {
        var cache = new SemanticVectorToolCacheService();
        var tenantId = Guid.NewGuid();
        float[] emb = [1.0f, 1.0f];

        await cache.StoreAsync(tenantId, "toolX", emb, "{}", "{}", 500, 100, 100);

        // 1 Hit
        await cache.LookupAsync(tenantId, "toolX", emb);
        // 1 Miss
        await cache.LookupAsync(tenantId, "toolX", [-1.0f, -1.0f]);

        var metrics = cache.GetMetrics(tenantId);
        metrics.TotalLookups.Should().Be(2);
        metrics.TotalHits.Should().Be(1);
        metrics.TotalMisses.Should().Be(1);
        metrics.HitRatio.Should().Be(0.5);
        metrics.TotalTokensSaved.Should().Be(200);
        metrics.TotalLatencySavedMs.Should().BeGreaterThan(0);
        metrics.TotalCachedEntries.Should().Be(1);
    }

    // =========================================================================
    // 3. Cascading Model Router & Circuit Breaker Tests
    // =========================================================================

    [Fact]
    public async Task CascadingRouter_PrimarySuccess_RoutesDirectlyToGpt4o()
    {
        var router = new CascadingModelRouterService();
        var request = new CascadingRouteRequest(Prompt: "Summarize this deal");

        var result = await router.ExecuteAsync(request, (tier, ct) =>
        {
            return Task.FromResult(new ModelProviderResponse(
                Success: true,
                StatusCode: 200,
                Content: "Deal summarized successfully.",
                LatencyMs: 120
            ));
        });

        result.Success.Should().BeTrue();
        result.ActualProvider.Should().Be("OpenAI");
        result.ActualModel.Should().Be("gpt-4o");
        result.ActualTierPriority.Should().Be(0);
        result.IsFallback.Should().BeFalse();
        result.Content.Should().Be("Deal summarized successfully.");
    }

    [Fact]
    public async Task CascadingRouter_PrimaryReturns429_FailsOverToClaudeSonnet()
    {
        var router = new CascadingModelRouterService();
        var request = new CascadingRouteRequest(Prompt: "Write an email");

        var result = await router.ExecuteAsync(request, (tier, ct) =>
        {
            if (tier.Provider == "OpenAI")
            {
                return Task.FromResult(new ModelProviderResponse(
                    Success: false,
                    StatusCode: 429, // Rate limit
                    Content: string.Empty,
                    ErrorMessage: "Rate limit exceeded. Try again in 20s."
                ));
            }

            return Task.FromResult(new ModelProviderResponse(
                Success: true,
                StatusCode: 200,
                Content: "Email drafted by Claude 3.5 Sonnet.",
                LatencyMs: 250
            ));
        });

        result.Success.Should().BeTrue();
        result.IsFallback.Should().BeTrue();
        result.ActualProvider.Should().Be("Anthropic");
        result.ActualModel.Should().Be("claude-3-5-sonnet");
        result.ActualTierPriority.Should().Be(1);
        result.FallbackReason.Should().Contain("429");

        // Verify OpenAI circuit breaker is now OPEN
        var statuses = router.GetCircuitBreakerStatuses();
        var openAiStatus = statuses.First(s => s.Provider == "OpenAI");
        openAiStatus.State.Should().Be(CircuitState.Open);
        openAiStatus.LastFailureReason.Should().Contain("429");
    }

    [Fact]
    public async Task CascadingRouter_PrimaryLatencyExceeds5Seconds_TripsCircuitBreakerAndFallsOver()
    {
        var router = new CascadingModelRouterService();
        router.SetLatencyTimeout(TimeSpan.FromMilliseconds(100)); // Fast timeout for unit test
        var request = new CascadingRouteRequest(Prompt: "Perform high-depth reasoning");

        var result = await router.ExecuteAsync(request, async (tier, ct) =>
        {
            if (tier.Provider == "OpenAI")
            {
                // Simulate latency higher than threshold
                await Task.Delay(150, ct);
                return new ModelProviderResponse(Success: true, StatusCode: 200, Content: "Too slow!", LatencyMs: 150);
            }

            return new ModelProviderResponse(Success: true, StatusCode: 200, Content: "Fast fallback response.", LatencyMs: 20);
        });

        result.Success.Should().BeTrue();
        result.IsFallback.Should().BeTrue();
        result.ActualProvider.Should().Be("Anthropic");
        result.ActualModel.Should().Be("claude-3-5-sonnet");

        // Verify OpenAI tripped due to latency
        var statuses = router.GetCircuitBreakerStatuses();
        var openAiStatus = statuses.First(s => s.Provider == "OpenAI");
        openAiStatus.State.Should().Be(CircuitState.Open);
        openAiStatus.LastFailureReason.Should().Contain("Latency");
    }

    [Fact]
    public async Task CascadingRouter_PrimaryAndFallback1Broken_FallsOverToLocalLlama()
    {
        var router = new CascadingModelRouterService();
        var request = new CascadingRouteRequest(Prompt: "Translate contract");

        var result = await router.ExecuteAsync(request, (tier, ct) =>
        {
            if (tier.Provider == "OpenAI")
            {
                return Task.FromResult(new ModelProviderResponse(Success: false, StatusCode: 429, Content: string.Empty));
            }
            if (tier.Provider == "Anthropic")
            {
                return Task.FromResult(new ModelProviderResponse(Success: false, StatusCode: 503, Content: string.Empty, ErrorMessage: "Service Unavailable"));
            }

            return Task.FromResult(new ModelProviderResponse(
                Success: true,
                StatusCode: 200,
                Content: "Translated locally via Llama 3.3 70B.",
                LatencyMs: 80
            ));
        });

        result.Success.Should().BeTrue();
        result.IsFallback.Should().BeTrue();
        result.ActualProvider.Should().Be("Local");
        result.ActualModel.Should().Be("llama-3.3-70b");
        result.ActualTierPriority.Should().Be(2);
        result.Content.Should().Contain("Llama 3.3");
    }

    [Fact]
    public async Task CascadingRouter_CooldownRecovery_HalfOpenRecoversToClosedOnSuccess()
    {
        var router = new CascadingModelRouterService();
        // Set very short cooldown for testing
        router.SetCooldownPeriod(TimeSpan.FromMilliseconds(50));

        // Trip OpenAI circuit breaker
        router.TripCircuitBreaker("OpenAI", "Manual test trip");
        router.GetCircuitBreakerStatuses().First(s => s.Provider == "OpenAI").State.Should().Be(CircuitState.Open);

        // Wait for cooldown to expire
        await Task.Delay(80);

        var request = new CascadingRouteRequest(Prompt: "Probe request");
        var result = await router.ExecuteAsync(request, (tier, ct) =>
        {
            return Task.FromResult(new ModelProviderResponse(
                Success: true,
                StatusCode: 200,
                Content: "Recovered successfully on OpenAI",
                LatencyMs: 50
            ));
        });

        result.Success.Should().BeTrue();
        result.ActualProvider.Should().Be("OpenAI");
        result.IsFallback.Should().BeFalse();

        // State should now be CLOSED again
        router.GetCircuitBreakerStatuses().First(s => s.Provider == "OpenAI").State.Should().Be(CircuitState.Closed);
    }

    // =========================================================================
    // 4. PII Sanitization Gateway Tests
    // =========================================================================

    [Fact]
    public void PiiScrubber_CreditCard_ValidLuhn_MasksCorrectly()
    {
        var scrubber = new PiiSanitizationGatewayService();
        // 4242-4242-4242-4242 is a valid Luhn test card number
        var input = "Please charge my credit card 4242-4242-4242-4242 for the renewal invoice.";

        var result = scrubber.Sanitize(input);

        result.ContainsPii.Should().BeTrue();
        result.SanitizedText.Should().Contain("[REDACTED_CREDIT_CARD]");
        result.SanitizedText.Should().NotContain("4242-4242-4242-4242");
        result.RedactedTypes.Should().Contain("CreditCard");
    }

    [Fact]
    public void PiiScrubber_CreditCard_InvalidLuhn_PreservesOriginalNumbers()
    {
        var scrubber = new PiiSanitizationGatewayService();
        // 1234-5678-9012-3456 fails Luhn check
        var input = "Tracking reference number is 1234-5678-9012-3456 for shipment.";

        var result = scrubber.Sanitize(input);

        result.ContainsPii.Should().BeFalse();
        result.SanitizedText.Should().Be(input);
        result.SanitizedText.Should().NotContain("[REDACTED_CREDIT_CARD]");
    }

    [Fact]
    public void PiiScrubber_NationalIds_MasksUsSsn_AlgerianNin_FrenchNir()
    {
        var scrubber = new PiiSanitizationGatewayService();

        // 1. US SSN
        var ssnInput = "Applicant SSN is 123-45-6789 submitted today.";
        var ssnResult = scrubber.Sanitize(ssnInput);
        ssnResult.ContainsPii.Should().BeTrue();
        ssnResult.SanitizedText.Should().Contain("[REDACTED_SSN]");
        ssnResult.RedactedTypes.Should().Contain("SSN");

        // 2. Algerian NIN (18 digits)
        var algerianInput = "National ID number is 198516010023456789 for tax declaration.";
        var algerianResult = scrubber.Sanitize(algerianInput);
        algerianResult.ContainsPii.Should().BeTrue();
        algerianResult.SanitizedText.Should().Contain("[REDACTED_ALGERIAN_NIN]");
        algerianResult.RedactedTypes.Should().Contain("AlgerianNIN");

        // 3. French NIR (Securite Sociale)
        var frenchInput = "Numéro de sécurité sociale: 1 85 12 75 108 042 12 vérifié.";
        var frenchResult = scrubber.Sanitize(frenchInput);
        frenchResult.ContainsPii.Should().BeTrue();
        frenchResult.SanitizedText.Should().Contain("[REDACTED_FRENCH_NIR]");
        frenchResult.RedactedTypes.Should().Contain("FrenchNIR");
    }

    [Fact]
    public void PiiScrubber_Secrets_MasksApiKeysAndPlaintextPasswords()
    {
        var scrubber = new PiiSanitizationGatewayService();

        // OpenAI API Key
        var apiKeyInput = "Export OPENAI_API_KEY=sk-proj-abc1234567890def1234567890 in env.";
        var apiKeyResult = scrubber.Sanitize(apiKeyInput);
        apiKeyResult.ContainsPii.Should().BeTrue();
        apiKeyResult.SanitizedText.Should().Contain("[REDACTED_API_KEY]");
        apiKeyResult.SanitizedText.Should().NotContain("sk-proj-abc1234567890def1234567890");

        // Plaintext Password in key-value context
        var pwdInput = "Database connection: password: \"SuperSecretPwd2026!\" host: localhost";
        var pwdResult = scrubber.Sanitize(pwdInput);
        pwdResult.ContainsPii.Should().BeTrue();
        pwdResult.SanitizedText.Should().Contain("[REDACTED_PASSWORD]");
        pwdResult.SanitizedText.Should().NotContain("SuperSecretPwd2026!");
    }

    [Fact]
    public void PiiScrubber_ComplexMixedText_SanitizesAllEntitiesDeterministically()
    {
        var scrubber = new PiiSanitizationGatewayService();
        var mixed = "Customer SSN 987-65-4321 paid with card 4242424242424242 using api_key: \"sk-12345678901234567890123456\".";

        var result = scrubber.Sanitize(mixed);

        result.ContainsPii.Should().BeTrue();
        result.RedactedEntities.Should().HaveCount(3);
        result.RedactedTypes.Should().Contain(["SSN", "CreditCard", "ApiKey"]);
        result.SanitizedText.Should().Contain("[REDACTED_SSN]");
        result.SanitizedText.Should().Contain("[REDACTED_CREDIT_CARD]");
        result.SanitizedText.Should().Contain("[REDACTED_API_KEY]");
    }
}
