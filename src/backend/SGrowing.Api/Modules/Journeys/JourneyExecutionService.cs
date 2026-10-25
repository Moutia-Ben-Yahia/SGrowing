using System.Text.Json;
using SGrowing.Api.Data;
using SGrowing.Api.Services;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Modules.Journeys;

public class JourneyExecutionService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public JourneyExecutionService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<Result<CustomerEvent>> IngestEventAsync(
        Guid tenantId,
        Guid contactId,
        string eventName,
        string payloadJson = "{}",
        CancellationToken cancellationToken = default)
    {
        var evt = CustomerEvent.Create(tenantId, contactId, eventName, payloadJson);
        _db.CustomerEvents.Add(evt);

        // Find active journeys listening to this trigger event
        var journeys = await _db.CustomerJourneys
            .Where(j => j.TenantId == tenantId && j.Status == JourneyStatus.Active && j.TriggerEvent == eventName)
            .ToListAsync(cancellationToken);

        foreach (var journey in journeys)
        {
            // Avoid duplicate active enrollment
            var exists = await _db.JourneyEnrollments.AnyAsync(
                e => e.TenantId == tenantId && e.JourneyId == journey.Id && e.ContactId == contactId && e.Status == JourneyEnrollmentStatus.Active,
                cancellationToken);

            if (!exists)
            {
                var enrollment = JourneyEnrollment.Create(tenantId, journey.Id, contactId);
                _db.JourneyEnrollments.Add(enrollment);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<CustomerEvent>.Success(evt);
    }

    public async Task<int> ProcessDueStepsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueEnrollments = await _db.JourneyEnrollments
            .Where(e => e.TenantId == tenantId && e.Status == JourneyEnrollmentStatus.Active && e.NextExecutionAtUtc <= now)
            .Take(50)
            .ToListAsync(cancellationToken);

        int processed = 0;

        foreach (var enrollment in dueEnrollments)
        {
            var steps = await _db.JourneySteps
                .Where(s => s.TenantId == tenantId && s.JourneyId == enrollment.JourneyId)
                .OrderBy(s => s.StepOrder)
                .ToListAsync(cancellationToken);

            var currentStep = steps.FirstOrDefault(s => s.StepOrder == enrollment.CurrentStepOrder);
            if (currentStep == null)
            {
                enrollment.MarkCompleted();
                processed++;
                continue;
            }

            var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.Id == enrollment.ContactId, cancellationToken);
            if (contact != null)
            {
                var renderedBody = currentStep.BodyTemplate
                    .Replace("{{first_name}}", contact.FirstName)
                    .Replace("{{last_name}}", contact.LastName)
                    .Trim();

                var payloadObj = new
                {
                    contactId = contact.Id,
                    email = contact.Email,
                    phone = contact.Phone,
                    stepType = currentStep.StepType.ToString(),
                    subject = currentStep.SubjectTemplate ?? "Update from SGrowing",
                    body = renderedBody
                };

                var eventType = currentStep.StepType == JourneyStepType.SendWhatsApp ? "Journey.WhatsApp.Send" : "Journey.Email.Send";
                await _outbox.EnqueueAsync(tenantId, eventType, payloadObj, cancellationToken);
            }

            // Find next step
            var nextStep = steps.FirstOrDefault(s => s.StepOrder > currentStep.StepOrder);
            if (nextStep != null)
            {
                enrollment.AdvanceToStep(nextStep.StepOrder, currentStep.DelaySeconds);
            }
            else
            {
                enrollment.MarkCompleted();
            }

            processed++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return processed;
    }
}
