using MarketingStudio.Api.Data;
using MarketingStudio.Core.Entities;
using MarketingStudio.Core.Outbox;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.Outbound;

public class CadenceDispatcherService
{
    private readonly MarketingStudioDbContext _db;
    private readonly MailboxService _mailboxService;
    private readonly IAiSdrService _aiSdrService;
    private readonly IOutbox _outbox;

    public CadenceDispatcherService(
        MarketingStudioDbContext db,
        MailboxService mailboxService,
        IAiSdrService aiSdrService,
        IOutbox outbox)
    {
        _db = db;
        _mailboxService = mailboxService;
        _aiSdrService = aiSdrService;
        _outbox = outbox;
    }

    public async Task<int> ProcessDueCadencesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueEnrollments = await _db.ProspectEnrollments
            .Where(e => e.TenantId == tenantId && e.Status == EnrollmentStatus.Active && e.NextRunAtUtc != null && e.NextRunAtUtc <= now)
            .OrderBy(e => e.NextRunAtUtc)
            .Take(25)
            .ToListAsync(cancellationToken);

        int processed = 0;

        foreach (var enrollment in dueEnrollments)
        {
            var campaign = await _db.OutboundCampaigns.FirstOrDefaultAsync(c => c.Id == enrollment.CampaignId, cancellationToken);
            if (campaign == null || campaign.Status != CampaignStatus.Active) continue;

            var senderPoolIds = campaign.GetSenderPoolIds();
            var sender = await _mailboxService.GetNextAvailableSenderAsync(tenantId, senderPoolIds, cancellationToken);
            if (sender == null)
            {
                // No available sender with remaining daily quota; pause processing for this batch
                break;
            }

            var currentStep = await _db.CadenceSteps.FirstOrDefaultAsync(
                s => s.CampaignId == enrollment.CampaignId && s.StepNumber == enrollment.CurrentStepNumber,
                cancellationToken);

            if (currentStep == null)
            {
                enrollment.Complete();
                continue;
            }

            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == enrollment.ContactId, cancellationToken);
            if (contact == null) continue;

            Company? company = null;
            if (contact.CompanyId.HasValue)
            {
                company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == contact.CompanyId.Value, cancellationToken);
            }

            // 1. Generate 1-to-1 AI Personalization if not already cached
            var sdrInput = new SdrPersonalizationInput(
                FirstName: contact.FirstName,
                LastName: contact.LastName,
                Company: company?.Name ?? "your company",
                Industry: company?.Industry,
                Role: null
            );

            var sdrOutput = await _aiSdrService.GeneratePersonalizationAsync(sdrInput, cancellationToken);
            enrollment.PersonalizedHook = sdrOutput.Icebreaker;

            // 2. Render templates
            var renderedSubject = _aiSdrService.RenderTemplate(currentStep.SubjectTemplate, contact, company, sdrOutput);
            var renderedBody = _aiSdrService.RenderTemplate(currentStep.BodyTemplate, contact, company, sdrOutput);

            // 3. Dispatch through Outbox
            await _outbox.EnqueueAsync(tenantId, "OutboundMessageDispatched", new
            {
                SenderAccountId = sender.Id,
                SenderEmail = sender.Email,
                RecipientEmail = contact.Email,
                RecipientPhone = contact.Phone,
                Channel = currentStep.Channel.ToString(),
                Subject = renderedSubject,
                Body = renderedBody,
                CampaignId = campaign.Id,
                StepNumber = currentStep.StepNumber,
                ContactId = contact.Id
            }, cancellationToken);

            sender.RecordSend();

            // 4. Advance to Next Step
            var nextStep = await _db.CadenceSteps.FirstOrDefaultAsync(
                s => s.CampaignId == enrollment.CampaignId && s.StepNumber == enrollment.CurrentStepNumber + 1,
                cancellationToken);

            if (nextStep != null)
            {
                enrollment.AdvanceStep(nextStep.StepNumber, nextStep.DelayDays);
            }
            else
            {
                enrollment.Complete();
            }

            processed++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return processed;
    }
}
