using MarketingStudio.Api.Data;
using MarketingStudio.Core.Common;
using MarketingStudio.Core.Entities;

namespace MarketingStudio.Api.Modules.Outbound;

public record CreateCadenceStepDto(
    int StepNumber,
    int DelayDays,
    OutboundChannel Channel,
    string SubjectTemplate,
    string BodyTemplate
);

public record CreateCampaignRequest(
    Guid TenantId,
    string Name,
    List<Guid> SenderPoolIds,
    List<CreateCadenceStepDto> Steps
);

public record CampaignDto(
    Guid Id,
    Guid TenantId,
    string Name,
    CampaignStatus Status,
    List<CreateCadenceStepDto> Steps,
    DateTime CreatedAtUtc
);

public class CreateCampaignHandler
{
    private readonly MarketingStudioDbContext _db;

    public CreateCampaignHandler(MarketingStudioDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CampaignDto>> HandleAsync(CreateCampaignRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
            return Result<CampaignDto>.Failure("TenantId is required.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<CampaignDto>.Failure("Campaign name is required.");

        if (request.Steps == null || request.Steps.Count == 0)
            return Result<CampaignDto>.Failure("Campaign must contain at least one step.");

        var campaign = OutboundCampaign.Create(request.TenantId, request.Name, request.SenderPoolIds ?? new List<Guid>());
        _db.OutboundCampaigns.Add(campaign);

        foreach (var stepDto in request.Steps)
        {
            var step = CadenceStep.Create(
                tenantId: request.TenantId,
                campaignId: campaign.Id,
                stepNumber: stepDto.StepNumber,
                delayDays: stepDto.DelayDays,
                channel: stepDto.Channel,
                subjectTemplate: stepDto.SubjectTemplate,
                bodyTemplate: stepDto.BodyTemplate
            );
            _db.CadenceSteps.Add(step);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<CampaignDto>.Success(new CampaignDto(
            campaign.Id,
            campaign.TenantId,
            campaign.Name,
            campaign.Status,
            request.Steps,
            campaign.CreatedAtUtc
        ));
    }
}
