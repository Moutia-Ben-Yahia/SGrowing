using MarketingStudio.Api.Data;
using MarketingStudio.Core.Common;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.Outbound;

public record EnrollProspectsRequest(Guid TenantId, Guid CampaignId, List<Guid> ContactIds);
public record EnrollProspectsResponse(Guid CampaignId, int EnrolledCount);

public class EnrollProspectsHandler
{
    private readonly MarketingStudioDbContext _db;

    public EnrollProspectsHandler(MarketingStudioDbContext db)
    {
        _db = db;
    }

    public async Task<Result<EnrollProspectsResponse>> HandleAsync(EnrollProspectsRequest request, CancellationToken cancellationToken = default)
    {
        var campaign = await _db.OutboundCampaigns.FirstOrDefaultAsync(
            c => c.TenantId == request.TenantId && c.Id == request.CampaignId, cancellationToken);

        if (campaign == null)
            return Result<EnrollProspectsResponse>.Failure("Campaign not found.");

        int count = 0;
        foreach (var contactId in request.ContactIds)
        {
            var exists = await _db.ProspectEnrollments.AnyAsync(
                e => e.TenantId == request.TenantId && e.CampaignId == request.CampaignId && e.ContactId == contactId,
                cancellationToken);

            if (!exists)
            {
                var enrollment = ProspectEnrollment.Create(request.TenantId, request.CampaignId, contactId);
                _db.ProspectEnrollments.Add(enrollment);
                count++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<EnrollProspectsResponse>.Success(new EnrollProspectsResponse(campaign.Id, count));
    }
}
