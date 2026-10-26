using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.AI;

public class AiAgentAndBiTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly AiAgentOrchestratorService _orchestratorService;
    private readonly ConversationalBiService _biService;

    public AiAgentAndBiTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _outboxMock = new Mock<IOutbox>();
        _orchestratorService = new AiAgentOrchestratorService(_db, _outboxMock.Object);
        _biService = new ConversationalBiService(_db);
    }

    [Fact]
    public async Task AllocateBudget_CreatesOrUpdatesTenantBudget()
    {
        var tenantId = Guid.NewGuid();

        var budget = await _orchestratorService.AllocateBudgetAsync(tenantId, tokenLimit: 500_000, maxCostUsd: 25.0m, enforceHardCap: true);

        Assert.NotNull(budget);
        Assert.Equal(500_000, budget.MonthlyTokenLimit);
        Assert.Equal(25.0m, budget.MaxMonthlyCostUsd);
        Assert.True(budget.IsHardCapEnforced);
        Assert.False(budget.IsBudgetExceeded);
    }

    [Fact]
    public async Task ExecuteTool_EnforcesBudgetHardCap()
    {
        var tenantId = Guid.NewGuid();
        var budget = await _orchestratorService.AllocateBudgetAsync(tenantId, tokenLimit: 100, maxCostUsd: 0.0001m);
        budget.TokensConsumed = 200; // Force exceed
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orchestratorService.ExecuteToolAsync(tenantId, "SDR", "CalculatePipelineForecast", "{}"));

        Assert.Contains("AI monthly token budget exceeded", ex.Message);
    }

    [Fact]
    public async Task ExecuteTool_SensitiveAction_RequiresApproval()
    {
        var tenantId = Guid.NewGuid();
        await _orchestratorService.AllocateBudgetAsync(tenantId, tokenLimit: 1_000_000, maxCostUsd: 50.0m);

        // 1. Discount > 20% without approval -> Requires approval
        var unapproved = await _orchestratorService.ExecuteToolAsync(
            tenantId,
            "DiscountAgent",
            "SuggestOrderDiscount",
            "{\"requestedDiscount\": 35}",
            userApproved: false
        );

        Assert.True(unapproved.RequiresApproval);
        Assert.False(unapproved.IsSuccess);
        Assert.Contains("PendingApproval", unapproved.OutputJson);

        // 2. Discount > 20% WITH approval -> Approved
        var approved = await _orchestratorService.ExecuteToolAsync(
            tenantId,
            "DiscountAgent",
            "SuggestOrderDiscount",
            "{\"requestedDiscount\": 35}",
            userApproved: true
        );

        Assert.False(approved.RequiresApproval);
        Assert.True(approved.IsSuccess);
        Assert.Contains("Authorized", approved.OutputJson);
    }

    [Fact]
    public async Task ExecuteTool_DeductsTokensAndLogsToAudit()
    {
        var tenantId = Guid.NewGuid();
        var budget = await _orchestratorService.AllocateBudgetAsync(tenantId, tokenLimit: 100_000, maxCostUsd: 10.0m);

        var result = await _orchestratorService.ExecuteToolAsync(
            tenantId,
            "RevenueAgent",
            "CalculatePipelineForecast",
            "{}"
        );

        Assert.True(result.IsSuccess);
        Assert.True(result.TokensConsumed > 0);

        // Check budget updated
        var refreshedBudget = await _orchestratorService.GetBudgetAsync(tenantId);
        Assert.Equal(result.TokensConsumed, refreshedBudget.TokensConsumed);

        // Check audit log in DB
        var auditLogs = await _orchestratorService.GetAuditLogsAsync(tenantId);
        Assert.Single(auditLogs);
        Assert.Equal("RevenueAgent", auditLogs[0].AgentName);
        Assert.Equal("CalculatePipelineForecast", auditLogs[0].ToolName);

        // Check outbox event dispatched
        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "AI.ToolExecuted",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConversationalBi_AnswersFunnelAndQuotaQueries()
    {
        var tenantId = Guid.NewGuid();

        // 1. Funnel query
        var funnelResponse = await _biService.AskBiQuestionAsync(tenantId, "What is our current conversion funnel drop-off rate?");
        Assert.NotNull(funnelResponse);
        Assert.Contains("funnel conversion", funnelResponse.AnswerSummary.ToLower());
        Assert.True(funnelResponse.KeyMetrics.ContainsKey("OverallConversionPercent"));
        Assert.NotEmpty(funnelResponse.SuggestedFollowupQuestions);

        // 2. Quota query
        var quotaResponse = await _biService.AskBiQuestionAsync(tenantId, "How is our quarterly sales quota attainment tracking?");
        Assert.NotNull(quotaResponse);
        Assert.Contains("quota attainment", quotaResponse.AnswerSummary.ToLower());
        Assert.True(quotaResponse.KeyMetrics.ContainsKey("QuotaAttainmentPercent"));
    }
}
