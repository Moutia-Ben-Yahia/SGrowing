using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Feedback.Community;

public record CommunityRoadmapDto(
    Guid TenantId,
    List<CommunityFeedbackPost> Planned,
    List<CommunityFeedbackPost> InProgress,
    List<CommunityFeedbackPost> Completed
);

public interface ICommunityFeedbackService
{
    Task<CommunityFeedbackPost> SubmitPostAsync(
        Guid tenantId,
        string title,
        string description,
        string authorEmail);

    Task<bool> VotePostAsync(
        Guid tenantId,
        Guid postId,
        string voterEmail);

    Task<CommunityFeedbackPost> UpdateRoadmapStatusAsync(
        Guid tenantId,
        Guid postId,
        string newStatus,
        string officialResponse = "");

    Task<CommunityRoadmapDto> GetPublicRoadmapAsync(
        Guid tenantId);

    Task<List<CommunityFeedbackPost>> SearchPostsAsync(
        Guid tenantId,
        string query);
}
