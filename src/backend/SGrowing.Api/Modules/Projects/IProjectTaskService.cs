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
}
