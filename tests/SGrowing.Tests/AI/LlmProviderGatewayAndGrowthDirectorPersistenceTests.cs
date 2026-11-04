using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Firewall;
using SGrowing.Api.Modules.AI.Gateway;
using SGrowing.Api.Modules.Agents.GrowthDirector;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.AI;

public class LlmProviderGatewayAndGrowthDirectorPersistenceTests
{
    [Fact]
    public async Task GoogleGeminiLlmClient_Unconfigured_SynthesizesHeuristicCompletion()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new GoogleGeminiLlmClient(new HttpClient(), config, NullLogger<GoogleGeminiLlmClient>.Instance);

        client.IsConfigured.Should().BeFalse();
        client.ProviderName.Should().Be("Google");

        var request = new LlmCompletionRequest(
            Prompt: "Decompose growth objective for Q4 Lead Surge",
            Model: "gemini-2.5-flash"
        );

        var result = await client.CompleteAsync(request);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Google");
        result.Model.Should().Be("gemini-2.5-flash");
        result.Content.Should().Contain("SEO_Specialist");
        result.Content.Should().Contain("Content_Copywriter");
        result.TotalTokens.Should().BeGreaterThan(0);
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task OpenAiLlmClient_Unconfigured_SynthesizesHeuristicCompletion()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new OpenAiLlmClient(new HttpClient(), config, NullLogger<OpenAiLlmClient>.Instance);

        client.IsConfigured.Should().BeFalse();
        client.ProviderName.Should().Be("OpenAI");

        var request = new LlmCompletionRequest(
            Prompt: "Summarize enterprise deal",
            Model: "gpt-4o"
        );

        var result = await client.CompleteAsync(request);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("OpenAI");
        result.Content.Should().Contain("Deal summarized successfully");
    }

    [Fact]
    public async Task AnthropicLlmClient_Unconfigured_SynthesizesHeuristicCompletion()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new AnthropicLlmClient(new HttpClient(), config, NullLogger<AnthropicLlmClient>.Instance);

        client.IsConfigured.Should().BeFalse();
        client.ProviderName.Should().Be("Anthropic");

        var request = new LlmCompletionRequest(
            Prompt: "Write an outreach email for SaaS product",
            Model: "claude-3-5-sonnet"
        );

        var result = await client.CompleteAsync(request);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Anthropic");
        result.Content.Should().Contain("Claude 3.5 Sonnet");
    }

    [Fact]
    public async Task CascadingModelRouterService_WithInjectedClients_ExecutesPrimaryAndFallsBack()
    {
        var config = new ConfigurationBuilder().Build();
        var gemini = new GoogleGeminiLlmClient(new HttpClient(), config);
        var openai = new OpenAiLlmClient(new HttpClient(), config);
        var anthropic = new AnthropicLlmClient(new HttpClient(), config);
        var local = new LocalLlmClient(new HttpClient(), config);

        var router = new CascadingModelRouterService(
            new ILlmProviderClient[] { openai, anthropic, local, gemini },
            NullLogger<CascadingModelRouterService>.Instance
        );

        var req = new CascadingRouteRequest(Prompt: "Summarize enterprise deal");
        var res = await router.ExecuteAsync(req);

        res.Success.Should().BeTrue();
        res.ActualProvider.Should().Be("OpenAI");
        res.ActualModel.Should().Be("gpt-4o");
        res.IsFallback.Should().BeFalse();
        res.Content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GrowthDirectorEngine_DecomposeObjective_PersistsToSGrowingDbContext()
    {
        // Setup in-memory DbContext and ServiceProvider
        var dbName = $"GrowthDirectorDb_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<SGrowingDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var config = new ConfigurationBuilder().Build();
        var gemini = new GoogleGeminiLlmClient(new HttpClient(), config);
        var openai = new OpenAiLlmClient(new HttpClient(), config);
        var anthropic = new AnthropicLlmClient(new HttpClient(), config);
        var local = new LocalLlmClient(new HttpClient(), config);

        var router = new CascadingModelRouterService(
            new ILlmProviderClient[] { openai, anthropic, local, gemini },
            NullLogger<CascadingModelRouterService>.Instance
        );

        var engine = new GrowthDirectorEngine(router, scopeFactory, NullLogger<GrowthDirectorEngine>.Instance);

        var objective = new GrowthMissionObjective(
            ObjectiveId: "obj-persist-test",
            TenantId: "tenant-persistence-1",
            Title: "In-Memory DB Mission Test",
            TargetMetric: "DealsWon",
            TargetValue: 50m,
            MaxBudgetUsd: 1200m,
            DeadlineUtc: DateTime.UtcNow.AddDays(30)
        );

        var plan = engine.DecomposeObjective(objective);

        plan.Should().NotBeNull();
        plan.SubTasks.Should().HaveCount(4);

        // Verify entities were persisted in SGrowingDbContext
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
            var mission = db.AgentMissions.Include(m => m.Steps).FirstOrDefault();
            mission.Should().NotBeNull();
            mission!.Title.Should().Be("In-Memory DB Mission Test");
            mission.TotalBudgetUsd.Should().Be(1200m);
            mission.Steps.Should().HaveCount(4);
        }

        // Test ApproveAndExecuteMission updates DB state
        var taskToApprove = plan.SubTasks[0];
        var execResult = engine.ApproveAndExecuteMission("tenant-persistence-1", plan.MissionId, new List<string> { taskToApprove.TaskId });
        execResult.Success.Should().BeTrue();
        execResult.TasksExecuted.Should().Be(1);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
            var mission = db.AgentMissions.Include(m => m.Steps).First();
            mission.SpentBudgetUsd.Should().Be(taskToApprove.AllocatedBudget);
            var completedStep = mission.Steps.FirstOrDefault(s => s.ToolName == taskToApprove.AgentType);
            completedStep.Should().NotBeNull();
            completedStep!.Status.Should().Be(AgentMissionStepStatus.Completed);
        }
    }

    [Fact]
    public async Task LocalLlmClient_WhenUnreachable_SynthesizesHeuristicCompletion()
    {
        var config = new ConfigurationBuilder().Build();
        var client = new LocalLlmClient(new HttpClient(), config, NullLogger<LocalLlmClient>.Instance);

        client.ProviderName.Should().Be("Local");
        client.IsConfigured.Should().BeTrue();

        var request = new LlmCompletionRequest(
            Prompt: "Translate English sales pitch to French",
            Model: "llama-3.3-70b"
        );

        var result = await client.CompleteAsync(request);

        result.Success.Should().BeTrue();
        result.Provider.Should().Be("Local");
        result.Content.Should().Contain("Translated locally via Llama 3.3");
        result.TotalTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CascadingModelRouterService_WhenPrimaryFailsWith429_CascadesToFallbackTier()
    {
        var router = new CascadingModelRouterService(NullLogger<CascadingModelRouterService>.Instance);

        var req = new CascadingRouteRequest(Prompt: "Urgent growth audit query");

        var res = await router.ExecuteAsync(req, (tier, ct) =>
        {
            if (tier.Provider == "OpenAI")
            {
                return Task.FromResult(new ModelProviderResponse(
                    Success: false,
                    Content: string.Empty,
                    StatusCode: 429,
                    ErrorMessage: "Rate limit exceeded"
                ));
            }

            return Task.FromResult(new ModelProviderResponse(
                Success: true,
                Content: "Anthropic Claude fallback response",
                StatusCode: 200
            ));
        });

        res.Success.Should().BeTrue();
        res.IsFallback.Should().BeTrue();
        res.ActualProvider.Should().Be("Anthropic");
        res.ActualModel.Should().Be("claude-3-5-sonnet");
        res.Content.Should().Be("Anthropic Claude fallback response");
    }

    [Fact]
    public void GrowthDirectorEngine_PersistMissionPlanToDb_StoresApprovalRequirementAndSteps()
    {
        var dbName = $"GrowthDirectorApprovalDb_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<SGrowingDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var engine = new GrowthDirectorEngine(null, scopeFactory, NullLogger<GrowthDirectorEngine>.Instance);

        var objective = new GrowthMissionObjective(
            ObjectiveId: "obj-approval-persist",
            TenantId: "tenant-approval-1",
            Title: "High Spend Growth Blitz",
            TargetMetric: "MQLs",
            TargetValue: 2000m,
            MaxBudgetUsd: 2500m, // High budget exceeds $250 default
            DeadlineUtc: DateTime.UtcNow.AddDays(45)
        );

        var plan = engine.DecomposeObjective(objective);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
            var mission = db.AgentMissions.Include(m => m.Steps).FirstOrDefault();
            mission.Should().NotBeNull();
            mission!.TotalBudgetUsd.Should().Be(2500m);

            var adStep = mission.Steps.FirstOrDefault(s => s.ToolName == "Ad_Optimizer");
            adStep.Should().NotBeNull();
            adStep!.ApprovalRequired.Should().BeTrue();
            adStep.Status.Should().Be(AgentMissionStepStatus.AwaitingApproval);
        }
    }
}
