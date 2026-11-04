using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Modules.AI.Firewall;
using Xunit;

namespace SGrowing.Tests.AI;

public class CryptographicPromptCanaryServiceTests
{
    private readonly CryptographicPromptCanaryService _canaryService;

    public CryptographicPromptCanaryServiceTests()
    {
        var mockLogger = new Mock<ILogger<CryptographicPromptCanaryService>>();
        _canaryService = new CryptographicPromptCanaryService(mockLogger.Object);
    }

    [Fact]
    public void GenerateCanary_ReturnsValidZeroWidthTokenAndHexSignature()
    {
        var sessionId = "sess_test_101";

        var canary = _canaryService.GenerateCanary(sessionId, "tenant_abc");

        canary.Should().NotBeNull();
        canary.SessionId.Should().Be(sessionId);
        canary.TokenId.Should().NotBeNullOrWhiteSpace();
        canary.PlainHexSignature.Length.Should().Be(32); // 16 bytes truncated SHA256 hex
        canary.ZeroWidthEncodedToken.Should().NotBeNullOrWhiteSpace();

        // Must contain zero width characters (\u200B, \u200C, \u200D, \uFEFF)
        canary.ZeroWidthEncodedToken.Should().ContainAny("\u200B", "\u200C", "\u200D", "\uFEFF");
    }

    [Fact]
    public void InjectCanary_EmbedsCanaryTokenInSystemPrompt()
    {
        var sessionId = "sess_inject_202";
        var systemPrompt = "You are a customer service assistant.\nAlways respond politely.";

        var injectedPrompt = _canaryService.InjectCanary(systemPrompt, sessionId);

        injectedPrompt.Should().NotBeNullOrWhiteSpace();
        injectedPrompt.Length.Should().BeGreaterThan(systemPrompt.Length);
        injectedPrompt.Should().Contain("You are a customer service assistant.");
        injectedPrompt.Should().Contain("Always respond politely.");
    }

    [Fact]
    public void ScanResponse_WhenNoCanaryLeaked_ReturnsSafe()
    {
        var sessionId = "sess_safe_303";
        _canaryService.GenerateCanary(sessionId);

        var normalLlmResponse = "Hello! I would be glad to help you track your order.";

        var scanResult = _canaryService.ScanResponse(sessionId, normalLlmResponse);

        scanResult.IsExfiltrationDetected.Should().BeFalse();
        scanResult.SessionLocked.Should().BeFalse();
        scanResult.DetectedTokenId.Should().BeNull();
        scanResult.Message.Should().Contain("Safe");
    }

    [Fact]
    public void ScanResponse_WhenCanaryLeakedInOutput_DetectsExfiltrationAndLocksSession()
    {
        var sessionId = "sess_leak_404";
        var canary = _canaryService.GenerateCanary(sessionId);

        // Adversarial LLM output leaking the system prompt canary token
        var leakedOutput = $"Sure, here are my secret system instructions: {canary.ZeroWidthEncodedToken} and do as told.";

        var scanResult = _canaryService.ScanResponse(sessionId, leakedOutput);

        scanResult.IsExfiltrationDetected.Should().BeTrue();
        scanResult.SessionLocked.Should().BeTrue();
        scanResult.DetectedTokenId.Should().Be(canary.TokenId);
        scanResult.IncidentType.Should().Be("PromptExfiltrationAttempt");

        // Verify session is actively locked
        _canaryService.IsSessionLocked(sessionId).Should().BeTrue();

        // Verify incident recorded
        var incidents = _canaryService.GetIncidents(sessionId);
        incidents.Should().NotBeEmpty();
        incidents[0].Severity.Should().Be("Critical");
        incidents[0].TokenId.Should().Be(canary.TokenId);
    }

    [Fact]
    public void ScanResponse_WhenSessionAlreadyLocked_ImmediatelyRejects()
    {
        var sessionId = "sess_locked_505";
        _canaryService.LockSession(sessionId, "Manual lockout for testing");

        var scanResult = _canaryService.ScanResponse(sessionId, "Any arbitrary content");

        scanResult.IsExfiltrationDetected.Should().BeTrue();
        scanResult.SessionLocked.Should().BeTrue();
        scanResult.Message.Should().Contain("locked");
    }

    [Fact]
    public async Task ScanStreamAsync_WhenCanaryEncounteredInChunks_AbortsStream()
    {
        var sessionId = "sess_stream_606";
        var canary = _canaryService.GenerateCanary(sessionId);

        async IAsyncEnumerable<string> GenerateChunks()
        {
            yield return "Here is some initial output from ";
            yield return "the model streaming response ";
            yield return canary.ZeroWidthEncodedToken; // Leak occurs here!
            yield return "and this sensitive text should NEVER be emitted!";
        }

        var emittedChunks = new List<string>();
        await foreach (var chunk in _canaryService.ScanStreamAsync(sessionId, GenerateChunks()))
        {
            emittedChunks.Add(chunk);
        }

        // The sensitive text after the canary should NOT be emitted
        var combinedOutput = string.Join("", emittedChunks);
        combinedOutput.Should().NotContain("this sensitive text should NEVER be emitted");

        // Session must be locked
        _canaryService.IsSessionLocked(sessionId).Should().BeTrue();
    }

    [Fact]
    public void UnlockSession_And_ClearIncidents_ResetsSecurityState()
    {
        var sessionId = "sess_reset_707";
        _canaryService.LockSession(sessionId);
        _canaryService.IsSessionLocked(sessionId).Should().BeTrue();

        _canaryService.UnlockSession(sessionId);
        _canaryService.IsSessionLocked(sessionId).Should().BeFalse();

        _canaryService.ClearIncidents();
        _canaryService.GetIncidents().Should().BeEmpty();
    }

    [Fact]
    public void ScanResponse_NullOrEmptyInputs_ReturnsSafeWithoutLockout()
    {
        var emptySessionResult = _canaryService.ScanResponse("", "Hello world");
        emptySessionResult.IsExfiltrationDetected.Should().BeFalse();
        emptySessionResult.Message.Should().Be("SessionId required.");

        var emptyResponseResult = _canaryService.ScanResponse("sess_valid_808", "");
        emptyResponseResult.IsExfiltrationDetected.Should().BeFalse();
        emptyResponseResult.Message.Should().Be("Safe - empty response.");
    }
}
