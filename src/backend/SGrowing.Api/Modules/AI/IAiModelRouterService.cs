using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI;

public interface IAiModelRouterService
{
    ModelRoutingDecision SelectModelForTask(AiTaskComplexity complexity, string? taskHint = null);
    Task<ModelRoutingExecutionResult<T>> ExecuteWithFallbackChainAsync<T>(
        Guid tenantId,
        AiTaskComplexity complexity,
        Func<string, string, Task<T>> promptExecutor,
        string? taskHint = null,
        CancellationToken ct = default);
    Task<IEnumerable<AiModelCatalogEntry>> GetModelCatalogAsync(CancellationToken ct = default);
    Task<TenantTokenQuotaStatus> GetTenantTokenQuotaAsync(Guid tenantId, CancellationToken ct = default);
}
