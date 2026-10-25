using SGrowing.Api.Data;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Channels;

public record BroadcastResult(int RecipientsMatched, int SentCount);

public class BroadcastDispatcherService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public BroadcastDispatcherService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<Result<BroadcastResult>> DispatchBroadcastAsync(
        Guid tenantId,
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await _db.BroadcastCampaigns
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == campaignId, cancellationToken);

        if (campaign == null)
            return Result<BroadcastResult>.Failure("Broadcast campaign not found.");

        var template = await _db.WhatsAppTemplates
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == campaign.TemplateId, cancellationToken);

        if (template == null)
            return Result<BroadcastResult>.Failure("Template not found.");

        if (template.Status != WhatsAppTemplateStatus.Approved)
            return Result<BroadcastResult>.Failure($"Template is not approved by Meta (Status: {template.Status}).");

        // Query audience matching target tag with a valid phone number
        var audience = await _db.Contacts
            .Where(c => c.TenantId == tenantId && !string.IsNullOrEmpty(c.Phone) && c.Tags.Contains(campaign.TargetTag))
            .ToListAsync(cancellationToken);

        int sent = 0;
        foreach (var recipient in audience)
        {
            var renderedText = template.BodyText
                .Replace("{{1}}", recipient.FirstName)
                .Replace("{{2}}", recipient.LastName)
                .Trim();

            var payload = new
            {
                contactId = recipient.Id,
                recipientPhone = recipient.Phone,
                templateName = template.Name,
                renderedBody = renderedText
            };

            await _outbox.EnqueueAsync(tenantId, "WhatsApp.Template.Send", payload, cancellationToken);
            sent++;
        }

        campaign.TotalRecipients = audience.Count;
        campaign.SentCount = sent;
        campaign.Status = BroadcastStatus.Completed;

        await _db.SaveChangesAsync(cancellationToken);

        return Result<BroadcastResult>.Success(new BroadcastResult(audience.Count, sent));
    }
}
