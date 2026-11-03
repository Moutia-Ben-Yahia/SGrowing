namespace SGrowing.Api.Modules.AI.Gateway;

public record LlmCompletionRequest(
    string Prompt,
    string? SystemPrompt = null,
    string Model = "default",
    double Temperature = 0.7,
    int MaxTokens = 2048,
    IReadOnlyList<string>? StopSequences = null,
    string? ResponseFormat = null
);

public record LlmCompletionResult(
    bool Success,
    string Content,
    string Provider,
    string Model,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    int TotalTokens = 0,
    int LatencyMs = 0,
    int StatusCode = 200,
    string? ErrorMessage = null
);

public interface ILlmProviderClient
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct = default);
}
