using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.AI;

public record AiToolExecutionResultDto(
    string ToolName,
    bool IsSuccess,
    string OutputJson,
    int TokensConsumed,
    decimal CostUsd,
    bool RequiresApproval,
    string? Message = null
);

public interface IAiAgentOrchestratorService
{
    Task<AiAgentBudget> AllocateBudgetAsync(Guid tenantId, long tokenLimit = 1_000_000, decimal maxCostUsd = 50.0m, bool enforceHardCap = true);
    Task<AiAgentBudget> GetBudgetAsync(Guid tenantId);
    Task<AiToolExecutionResultDto> ExecuteToolAsync(Guid tenantId, string agentName, string toolName, string argumentsJson, bool userApproved = false);
    Task<List<AiToolExecutionLog>> GetAuditLogsAsync(Guid tenantId, int limit = 50);
}

public class AiAgentOrchestratorService : IAiAgentOrchestratorService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public AiAgentOrchestratorService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<AiAgentBudget> AllocateBudgetAsync(Guid tenantId, long tokenLimit = 1_000_000, decimal maxCostUsd = 50.0m, bool enforceHardCap = true)
    {
        var budget = await _db.AiAgentBudgets.FirstOrDefaultAsync(b => b.TenantId == tenantId);
        if (budget == null)
        {
            budget = AiAgentBudget.Create(tenantId, tokenLimit, maxCostUsd, enforceHardCap);
            _db.AiAgentBudgets.Add(budget);
        }
        else
        {
            budget.MonthlyTokenLimit = tokenLimit;
            budget.MaxMonthlyCostUsd = maxCostUsd;
            budget.IsHardCapEnforced = enforceHardCap;
            budget.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return budget;
    }

    public async Task<AiAgentBudget> GetBudgetAsync(Guid tenantId)
    {
        var budget = await _db.AiAgentBudgets.FirstOrDefaultAsync(b => b.TenantId == tenantId);
        if (budget == null)
        {
            budget = AiAgentBudget.Create(tenantId);
            _db.AiAgentBudgets.Add(budget);
            await _db.SaveChangesAsync();
        }
        return budget;
    }

    public async Task<AiToolExecutionResultDto> ExecuteToolAsync(
        Guid tenantId,
        string agentName,
        string toolName,
        string argumentsJson,
        bool userApproved = false)
    {
        var budget = await GetBudgetAsync(tenantId);

        if (budget.IsHardCapEnforced && budget.IsBudgetExceeded)
        {
            throw new InvalidOperationException("AI monthly token budget exceeded. Refill budget or adjust hard cap.");
        }

        var normalizedTool = toolName.Trim().ToLowerInvariant();
        int tokens = 500;
        decimal cost = 0.001m;
        string outputJson;
        bool requiresApproval = false;
        ToolApprovalStatus approvalStatus = ToolApprovalStatus.AutoApproved;

        switch (normalizedTool)
        {
            case "calculatepipelineforecast":
                var periods = await _db.SalesForecastPeriods.Where(p => p.TenantId == tenantId).ToListAsync();
                decimal totalWeighted = periods.Sum(p => p.WeightedForecastRevenue);
                outputJson = JsonSerializer.Serialize(new
                {
                    ActivePeriodsCount = periods.Count,
                    TotalWeightedForecast = totalWeighted,
                    Status = "Calculated"
                });
                tokens = 450;
                cost = 0.0009m;
                break;

            case "suggestorderdiscount":
                // If requested discount > 20%, require human approval
                int discountPercent = 15;
                if (!string.IsNullOrWhiteSpace(argumentsJson) && argumentsJson.Contains("requestedDiscount"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(argumentsJson);
                        if (doc.RootElement.TryGetProperty("requestedDiscount", out var prop))
                            discountPercent = prop.GetInt32();
                    }
                    catch { }
                }

                if (discountPercent > 20 && !userApproved)
                {
                    requiresApproval = true;
                    approvalStatus = ToolApprovalStatus.Pending;
                    outputJson = JsonSerializer.Serialize(new
                    {
                        RequestedDiscount = discountPercent,
                        Status = "PendingApproval",
                        Message = "Discounts above 20% require sales manager authorization."
                    });
                }
                else
                {
                    outputJson = JsonSerializer.Serialize(new
                    {
                        ApprovedDiscount = discountPercent,
                        Status = "Authorized",
                        DiscountCode = $"AI-SAVE-{discountPercent}"
                    });
                }
                tokens = 300;
                cost = 0.0006m;
                break;

            case "analyzefunneldropoff":
                var telemetryEvents = await _db.TelemetryEvents.Where(e => e.TenantId == tenantId).ToListAsync();
                int pageviews = telemetryEvents.Count(e => e.EventName == "pageview");
                int purchases = telemetryEvents.Count(e => e.EventName == "purchase");
                double convRate = pageviews > 0 ? Math.Round(((double)purchases / pageviews) * 100.0, 1) : 0.0;
                outputJson = JsonSerializer.Serialize(new
                {
                    TotalPageviews = pageviews,
                    TotalPurchases = purchases,
                    ConversionRate = convRate,
                    Observation = convRate < 3.0 ? "High drop-off between cart and checkout" : "Funnel health optimal"
                });
                tokens = 600;
                cost = 0.0012m;
                break;

            default:
                outputJson = JsonSerializer.Serialize(new
                {
                    Message = $"Executed default tool action for '{toolName}'",
                    Timestamp = DateTime.UtcNow
                });
                tokens = 250;
                cost = 0.0005m;
                break;
        }

        // Deduct from budget
        budget.TokensConsumed += tokens;
        budget.CostSpentUsd += cost;
        budget.UpdatedAtUtc = DateTime.UtcNow;

        var log = AiToolExecutionLog.Create(
            tenantId,
            agentName,
            toolName,
            argumentsJson,
            outputJson,
            durationMs: 42,
            tokens: tokens,
            costUsd: cost,
            approvalStatus: approvalStatus
        );

        _db.AiToolExecutionLogs.Add(log);
        await _db.SaveChangesAsync();

        await _outbox.EnqueueAsync(tenantId, "AI.ToolExecuted", new
        {
            LogId = log.Id,
            log.AgentName,
            log.ToolName,
            log.TokensConsumed,
            log.CostUsd,
            log.ApprovalStatus
        });

        return new AiToolExecutionResultDto(
            ToolName: toolName,
            IsSuccess: !requiresApproval,
            OutputJson: outputJson,
            TokensConsumed: tokens,
            CostUsd: cost,
            RequiresApproval: requiresApproval,
            Message: requiresApproval ? "Action holds sensitive threshold; approval required." : "Executed successfully."
        );
    }

    public async Task<List<AiToolExecutionLog>> GetAuditLogsAsync(Guid tenantId, int limit = 50)
    {
        return await _db.AiToolExecutionLogs
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.ExecutedAtUtc)
            .Take(limit)
            .ToListAsync();
    }
}
