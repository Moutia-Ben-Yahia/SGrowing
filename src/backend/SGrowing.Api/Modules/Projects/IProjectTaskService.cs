using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Projects;

public interface IProjectTaskService
{
    Task<ProjectRecord> ConvertFromDealAsync(Guid tenantId, Guid dealId, CancellationToken ct = default);
    Task<ProjectRecord> ConvertFromContractAsync(Guid tenantId, Guid contractId, CancellationToken ct = default);
    Task<IEnumerable<ProjectRecord>> GetProjectsAsync(Guid tenantId, CancellationToken ct = default);
    Task<ProjectRecord?> GetProjectByIdAsync(Guid tenantId, Guid projectId, CancellationToken ct = default);
    Task<IEnumerable<TaskItemRecord>> GetProjectTasksAsync(Guid tenantId, Guid projectId, CancellationToken ct = default);
    Task<TaskItemRecord> CreateTaskAsync(Guid tenantId, Guid projectId, CreateTaskRequest req, CancellationToken ct = default);
    Task<TaskItemRecord?> UpdateTaskAsync(Guid tenantId, Guid taskId, UpdateTaskRequest req, CancellationToken ct = default);
    Task<TaskCommentRecord> AddCommentAsync(Guid tenantId, Guid taskId, AddCommentRequest req, CancellationToken ct = default);
    Task<IEnumerable<TaskCommentRecord>> GetCommentsAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    Task<ClientPortalTokenResult> GenerateClientPortalTokenAsync(Guid tenantId, Guid projectId, GeneratePortalTokenRequest req, string baseUrl, CancellationToken ct = default);
    Task<ClientPortalView?> GetClientPortalViewAsync(string token, CancellationToken ct = default);
    Task<ClientPortalFeedbackRecord> SubmitClientPortalFeedbackAsync(string token, ClientPortalFeedbackRequest req, CancellationToken ct = default);
    Task<bool> RevokeClientPortalTokenAsync(Guid tenantId, string token, CancellationToken ct = default);
    Task<CompleteMilestoneResult?> CompleteMilestoneWithBillingAsync(Guid tenantId, Guid taskId, CompleteMilestoneRequest req, CancellationToken ct = default);
}
