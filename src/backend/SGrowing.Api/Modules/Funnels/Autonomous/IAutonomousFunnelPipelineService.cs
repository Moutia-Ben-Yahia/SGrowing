using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Funnels.Autonomous;

public interface IAutonomousFunnelPipelineService
{
    Task<AutonomousFunnelRunDetailDto> ExecutePipelineAsync(Guid tenantId, ExecuteAutonomousFunnelRequest req, CancellationToken ct = default);
    Task<IEnumerable<AutonomousFunnelRunRecord>> GetRunsAsync(Guid tenantId, CancellationToken ct = default);
    Task<AutonomousFunnelRunDetailDto?> GetRunByIdAsync(Guid tenantId, Guid runId, CancellationToken ct = default);
}
