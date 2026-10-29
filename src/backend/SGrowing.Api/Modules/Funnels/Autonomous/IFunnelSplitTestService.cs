using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Funnels.Autonomous;

public interface IFunnelSplitTestService
{
    Task<FunnelSplitTestSummaryDto> CreateOrGetSplitTestAsync(Guid tenantId, Guid funnelRunId, CreateFunnelSplitTestRequest req, CancellationToken ct = default);
    Task<FunnelSplitTestSummaryDto?> GetSplitTestAsync(Guid tenantId, Guid funnelRunId, CancellationToken ct = default);
    Task<FunnelSplitTestSummaryDto?> RecordEventAsync(Guid tenantId, Guid funnelRunId, RecordSplitTestEventRequest req, CancellationToken ct = default);
    Task<bool> ConcludeTestAsync(Guid tenantId, Guid funnelRunId, string winningVariant, CancellationToken ct = default);
}
