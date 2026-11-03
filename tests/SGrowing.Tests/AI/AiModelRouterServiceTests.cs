using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.AI;

public sealed class AiModelRouterServiceTests
{
    private readonly SGrowingDbContext _db;
    private readonly AiModelRouterService _service;

    public AiModelRouterServiceTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new AiModelRouterService(_db, NullLogger<AiModelRouterService>.Instance);
    }

    [Theory]
    [InlineData(AiTaskComplexity.FastAndCheap, "gemini-2.5-flash", "Google", 0.075, 2048)]
    [InlineData(AiTaskComplexity.Balanced, "gpt-4o-mini", "OpenAI", 0.150, 4096)]
    [InlineData(AiTaskComplexity.DeepReasoning, "claude-3-5-sonnet-20241022", "Anthropic", 3.000, 8192)]
    public void SelectModelForTask_ReturnsExpectedRoutingDecision_ForDefinedComplexities(
        AiTaskComplexity complexity,
        string expectedModel,
        string expectedProvider,
        decimal expectedCost,
        int expectedMaxTokens)
    {
        var decision = _service.SelectModelForTask(complexity, "Unit test execution");

        decision.Should().NotBeNull();
        decision.Complexity.Should().Be(complexity);
        decision.SelectedModel.Should().Be(expectedModel);
        decision.Provider.Should().Be(expectedProvider);
        decision.EstimatedCostPerMillionTokens.Should().Be(expectedCost);
        decision.MaxOutputTokens.Should().Be(expectedMaxTokens);
    }

    [Fact]
    public void SelectModelForTask_DefaultsToBalanced_ForUnknownComplexity()
    {
        var unknown = (AiTaskComplexity)999;
        var decision = _service.SelectModelForTask(unknown, "Unknown complexity test");

        decision.Should().NotBeNull();
        decision.Complexity.Should().Be(AiTaskComplexity.Balanced);
        decision.SelectedModel.Should().Be("gpt-4o-mini");
        decision.Provider.Should().Be("OpenAI");
        decision.EstimatedCostPerMillionTokens.Should().Be(0.150m);
        decision.MaxOutputTokens.Should().Be(4096);
    }

    [Fact]
    public async Task ExecuteWithFallbackChainAsync_PrimarySucceeds_ReturnsResultWithoutFallback()
    {
        var tenantId = Guid.NewGuid();

        var result = await _service.ExecuteWithFallbackChainAsync(
            tenantId,
            AiTaskComplexity.FastAndCheap,
            (model, provider) => Task.FromResult($"Generated output from {model} ({provider})"),
            taskHint: "Fast extraction");

        result.Should().NotBeNull();
        result.Result.Should().Be("Generated output from gemini-2.5-flash (Google)");
        result.PrimaryModel.Should().Be("gemini-2.5-flash");
        result.ActualModelUsed.Should().Be("gemini-2.5-flash");
        result.Provider.Should().Be("Google");
        result.FallbackOccurred.Should().BeFalse();
        result.FallbackReason.Should().BeNull();
        result.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteWithFallbackChainAsync_PrimaryFails_SecondarySucceeds()
    {
        var tenantId = Guid.NewGuid();
        var callCount = 0;

        var result = await _service.ExecuteWithFallbackChainAsync(
            tenantId,
            AiTaskComplexity.FastAndCheap,
            (model, provider) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new HttpRequestException("HTTP 429: Gemini quota exceeded");
                }

                return Task.FromResult($"Success with fallback: {model}");
            },
            taskHint: "Resilience check");

        result.Should().NotBeNull();
        result.Result.Should().Be("Success with fallback: gpt-4o-mini");
        result.PrimaryModel.Should().Be("gemini-2.5-flash");
        result.ActualModelUsed.Should().Be("gpt-4o-mini");
        result.Provider.Should().Be("OpenAI");
        result.FallbackOccurred.Should().BeTrue();
        result.FallbackReason.Should().Contain("HTTP 429: Gemini quota exceeded");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteWithFallbackChainAsync_AllModelsFail_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();

        var act = async () => await _service.ExecuteWithFallbackChainAsync<string>(
            tenantId,
            AiTaskComplexity.DeepReasoning,
            (model, provider) => throw new TimeoutException($"Connection timed out to {provider}"),
            taskHint: "Deep failure test");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*All models in OpenClaude fallback chain failed*");
    }

    [Fact]
    public async Task GetModelCatalogAsync_ReturnsFullCatalogAcrossProviders()
    {
        var catalog = (await _service.GetModelCatalogAsync()).ToList();

        catalog.Should().NotBeEmpty();
        catalog.Should().HaveCount(15);

        catalog.Should().Contain(m => m.Provider == "Google" && m.ModelId == "gemini-2.5-flash");
        catalog.Should().Contain(m => m.Provider == "OpenAI" && m.ModelId == "gpt-4o");
        catalog.Should().Contain(m => m.Provider == "Anthropic" && m.ModelId == "claude-3-5-sonnet-20241022");
        catalog.Should().Contain(m => m.Provider == "OpenRouter" && m.ModelId == "meta-llama/llama-3.3-70b-instruct");
        catalog.Should().Contain(m => m.Provider == "Ollama" && m.ModelId == "ollama/llama3.2:3b");
    }

    [Fact]
    public async Task GetTenantTokenQuotaAsync_WithoutRecords_ReturnsDefaultActiveUsage()
    {
        var tenantId = Guid.NewGuid();
        var serviceWithoutDb = new AiModelRouterService(db: null);

        var quota = await serviceWithoutDb.GetTenantTokenQuotaAsync(tenantId);

        quota.Should().NotBeNull();
        quota.TenantId.Should().Be(tenantId);
        quota.MonthlyBudgetTokens.Should().Be(5000000);
        quota.ConsumedTokens.Should().Be(1423850);
        quota.RemainingTokens.Should().Be(3576150);
        quota.UsagePercentage.Should().Be(28.48m);
        quota.QuotaExceeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetTenantTokenQuotaAsync_WithMeteredRecords_ComputesAggregatedUsageAndFlagsExceeded()
    {
        var tenantId = Guid.NewGuid();

        // Seed usage records totaling 5,250,000 tokens (exceeding 5M cap)
        _db.UsageMeterRecords.AddRange(
            new UsageMeterRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Metric = UsageMetricType.AiTokens,
                Quantity = 3000000,
                RecordedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            new UsageMeterRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Metric = UsageMetricType.AiTokens,
                Quantity = 2250000,
                RecordedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            // Record for another metric (should be ignored)
            new UsageMeterRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Metric = UsageMetricType.MonthlyColdEmails,
                Quantity = 50000,
                RecordedAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        );
        await _db.SaveChangesAsync();

        var quota = await _service.GetTenantTokenQuotaAsync(tenantId);

        quota.Should().NotBeNull();
        quota.TenantId.Should().Be(tenantId);
        quota.MonthlyBudgetTokens.Should().Be(5000000);
        quota.ConsumedTokens.Should().Be(5250000);
        quota.RemainingTokens.Should().Be(0); // Clamped at 0
        quota.UsagePercentage.Should().Be(105.00m);
        quota.QuotaExceeded.Should().BeTrue();
    }
}
