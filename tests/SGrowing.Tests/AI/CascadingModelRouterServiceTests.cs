using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SGrowing.Api.Modules.AI.Firewall;
using Xunit;

namespace SGrowing.Tests.AI;

public class CascadingModelRouterServiceTests
{
    private readonly CascadingModelRouterService _router;

    public CascadingModelRouterServiceTests()
    {
        var mockLogger = new Mock<ILogger<CascadingModelRouterService>>();
        _router = new CascadingModelRouterService(mockLogger.Object);
        _router.ResetAll();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrimarySucceeds_ReturnsPrimaryResultWithoutFallback()
    {
        var request = new CascadingRouteRequest(Prompt: "Explain supply chain resilience.");

        Task<ModelProviderResponse> CustomInvoker(ModelTierConfig tier, CancellationToken ct)
        {
            if (tier.Priority == 0)
            {
                return Task.FromResult(new ModelProviderResponse(
                    Success: true,
                    StatusCode: 200,
                    Content: "Primary GPT-4o output on supply chains.",
                    LatencyMs: 120
                ));
            }

            return Task.FromResult(new ModelProviderResponse(false, 500, "Should not be called"));
        }

        var result = await _router.ExecuteAsync(request, CustomInvoker);

        result.Success.Should().BeTrue();
        result.IsFallback.Should().BeFalse();
        result.ActualProvider.Should().Be("OpenAI");
        result.ActualModel.Should().Be("gpt-4o");
        result.Content.Should().Be("Primary GPT-4o output on supply chains.");
        result.FallbackReason.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPrimaryReturns429_CascadesToFallbackTierAndTripsBreaker()
    {
        var request = new CascadingRouteRequest(Prompt: "Calculate COD net margin.");

        Task<ModelProviderResponse> CustomInvoker(ModelTierConfig tier, CancellationToken ct)
        {
            if (tier.Priority == 0) // OpenAI rate limited
            {
                return Task.FromResult(new ModelProviderResponse(
                    Success: false,
                    StatusCode: 429,
                    Content: string.Empty,
                    ErrorMessage: "Rate limit reached",
                    LatencyMs: 80
                ));
            }

            if (tier.Priority == 1) // Anthropic succeeds
            {
                return Task.FromResult(new ModelProviderResponse(
                    Success: true,
                    StatusCode: 200,
                    Content: "Claude 3.5 Sonnet calculation: Net margin is 22%.",
                    LatencyMs: 250
                ));
            }

            return Task.FromResult(new ModelProviderResponse(false, 500, "Unreachable"));
        }

        var result = await _router.ExecuteAsync(request, CustomInvoker);

        result.Success.Should().BeTrue();
        result.IsFallback.Should().BeTrue();
        result.ActualProvider.Should().Be("Anthropic");
        result.ActualModel.Should().Be("claude-3-5-sonnet");
        result.Content.Should().Contain("Claude 3.5 Sonnet calculation");
        result.FallbackReason.Should().Contain("429");

        // Verify OpenAI circuit breaker was tripped to Open
        var statuses = _router.GetCircuitBreakerStatuses();
        var openAiStatus = statuses.First(s => s.Provider == "OpenAI");
        openAiStatus.State.Should().Be(CircuitState.Open);
        openAiStatus.LastFailureReason.Should().Contain("429");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTierExceedsLatencyTimeout_CascadesToNextTier()
    {
        var request = new CascadingRouteRequest(Prompt: "Heavy reasoning prompt");

        Task<ModelProviderResponse> CustomInvoker(ModelTierConfig tier, CancellationToken ct)
        {
            if (tier.Priority == 0) // OpenAI exceeds 5000ms latency threshold
            {
                return Task.FromResult(new ModelProviderResponse(
                    Success: true,
                    StatusCode: 200,
                    Content: "Late response",
                    LatencyMs: 6500
                ));
            }

            if (tier.Priority == 1) // Anthropic responds quickly
            {
                return Task.FromResult(new ModelProviderResponse(
                    Success: true,
                    StatusCode: 200,
                    Content: "Fast Claude fallback response",
                    LatencyMs: 300
                ));
            }

            return Task.FromResult(new ModelProviderResponse(false, 500, "Unreachable"));
        }

        var result = await _router.ExecuteAsync(request, CustomInvoker);

        result.Success.Should().BeTrue();
        result.IsFallback.Should().BeTrue();
        result.ActualProvider.Should().Be("Anthropic");
        result.FallbackReason.Should().Contain("Latency");
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllTiersFail_ReturnsFailureWithAuditTrail()
    {
        var request = new CascadingRouteRequest(Prompt: "Mission critical evaluation");

        Task<ModelProviderResponse> CustomInvoker(ModelTierConfig tier, CancellationToken ct)
        {
            return Task.FromResult(new ModelProviderResponse(
                Success: false,
                StatusCode: 503,
                Content: string.Empty,
                ErrorMessage: $"{tier.Provider} service unavailable"
            ));
        }

        var result = await _router.ExecuteAsync(request, CustomInvoker);

        result.Success.Should().BeFalse();
        result.Content.Should().BeEmpty();
        result.FallbackAuditTrail.Should().HaveCount(CascadingModelRouterService.DefaultTiers.Count);
    }

    [Fact]
    public void CircuitBreaker_ManualTripAndReset_TogglesStateCorrectly()
    {
        _router.TripCircuitBreaker("Anthropic", "Outage maintenance");

        var statuses = _router.GetCircuitBreakerStatuses();
        var anthropicStatus = statuses.First(s => s.Provider == "Anthropic");
        anthropicStatus.State.Should().Be(CircuitState.Open);
        anthropicStatus.LastFailureReason.Should().Be("Outage maintenance");

        _router.ResetCircuitBreaker("Anthropic");

        var refreshed = _router.GetCircuitBreakerStatuses();
        refreshed.First(s => s.Provider == "Anthropic").State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void GetCircuitBreakerStatuses_IncludesAllDefaultConfiguredProviders()
    {
        var statuses = _router.GetCircuitBreakerStatuses();
        statuses.Should().HaveCount(4);
        statuses.Select(s => s.Provider).Should().BeEquivalentTo(["OpenAI", "Anthropic", "Local", "Google"]);
    }
}
