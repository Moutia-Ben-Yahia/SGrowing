using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.AI.Personas;

public interface IAgentPersonaService
{
    Task<IEnumerable<AgentPersonaRecord>> GetPersonasAsync(string? layer = null, CancellationToken ct = default);
    Task<AgentPersonaExecutionResult?> ExecutePersonaTaskAsync(Guid tenantId, string roleKey, RunPersonaTaskRequest req, CancellationToken ct = default);
    Task<AgentHeartbeatCycleResult> RunHeartbeatCycleAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<AgentHeartbeatRecord>> GetHeartbeatHistoryAsync(Guid tenantId, CancellationToken ct = default);
}
