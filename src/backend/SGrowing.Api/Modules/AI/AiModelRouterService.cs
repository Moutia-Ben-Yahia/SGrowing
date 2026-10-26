using SGrowing.Core.Entities;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Modules.AI;

public class AiModelRouterService
{
    private readonly ILogger<AiModelRouterService>? _logger;

    public AiModelRouterService(ILogger<AiModelRouterService>? logger = null)
    {
        _logger = logger;
    }

    public ModelRoutingDecision SelectModelForTask(AiTaskComplexity complexity, string? taskHint = null)
    {
        var decision = complexity switch
        {
            AiTaskComplexity.FastAndCheap => new ModelRoutingDecision
            {
                Complexity = complexity,
                SelectedModel = "gemini-2.5-flash",
                Provider = "Google",
                EstimatedCostPerMillionTokens = 0.075m,
                MaxOutputTokens = 2048
            },
            AiTaskComplexity.Balanced => new ModelRoutingDecision
            {
                Complexity = complexity,
                SelectedModel = "gpt-4o-mini",
                Provider = "OpenAI",
                EstimatedCostPerMillionTokens = 0.150m,
                MaxOutputTokens = 4096
            },
            AiTaskComplexity.DeepReasoning => new ModelRoutingDecision
            {
                Complexity = complexity,
                SelectedModel = "claude-3-5-sonnet-20241022",
                Provider = "Anthropic",
                EstimatedCostPerMillionTokens = 3.000m,
                MaxOutputTokens = 8192
            },
            _ => new ModelRoutingDecision
            {
                Complexity = AiTaskComplexity.Balanced,
                SelectedModel = "gpt-4o-mini",
                Provider = "OpenAI",
                EstimatedCostPerMillionTokens = 0.150m,
                MaxOutputTokens = 4096
            }
        };

        _logger?.LogDebug("Routed AI task '{Hint}' (Complexity: {Complexity}) to model {Model} ({Provider})",
            taskHint ?? "Standard", complexity, decision.SelectedModel, decision.Provider);

        return decision;
    }
}
