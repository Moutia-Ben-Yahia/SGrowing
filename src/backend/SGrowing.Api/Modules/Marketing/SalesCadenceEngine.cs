using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public interface ISalesCadenceEngine
{
    Task<SalesOutreachCadence> CreateCadenceAsync(Guid tenantId, string name, string description, string targetAudience);
    Task<SalesCadenceStepItem> AddCadenceStepAsync(Guid cadenceId, int stepNumber, int dayOffset, CadenceChannel channel, string? subject, string body);
    Task<SalesCadenceLeadEnrollment> EnrollLeadAsync(Guid tenantId, Guid cadenceId, string leadName, string email, string phone, string? company = null);
    Task<int> ProcessDueCadenceStepsAsync(Guid tenantId);
    Task<bool> HandleProspectReplyAsync(Guid tenantId, string emailOrPhone, string channel, string replyText);
    Task<List<SdrTaskItem>> GetPendingSdrTasksAsync(Guid tenantId);
    Task<SdrTaskItem> CompleteSdrTaskAsync(Guid tenantId, Guid taskId, string outcome);
}

public class SalesCadenceEngine : ISalesCadenceEngine
{
    private readonly SGrowingDbContext _dbContext;

    public SalesCadenceEngine(SGrowingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SalesOutreachCadence> CreateCadenceAsync(Guid tenantId, string name, string description, string targetAudience)
    {
        var cadence = new SalesOutreachCadence
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description.Trim(),
            TargetAudience = targetAudience.Trim(),
            IsActive = true,
            StepsCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.SalesOutreachCadences.Add(cadence);
        await _dbContext.SaveChangesAsync();
        return cadence;
    }

    public async Task<SalesCadenceStepItem> AddCadenceStepAsync(Guid cadenceId, int stepNumber, int dayOffset, CadenceChannel channel, string? subject, string body)
    {
        var cadence = await _dbContext.SalesOutreachCadences.FirstOrDefaultAsync(c => c.Id == cadenceId)
            ?? throw new InvalidOperationException($"Cadence {cadenceId} not found.");

        var step = new SalesCadenceStepItem
        {
            CadenceId = cadenceId,
            StepNumber = stepNumber,
            DayOffset = dayOffset,
            Channel = channel,
            SubjectTemplate = subject,
            BodyTemplate = body
        };

        _dbContext.SalesCadenceStepItems.Add(step);
        cadence.StepsCount++;
        await _dbContext.SaveChangesAsync();
        return step;
    }

    public async Task<SalesCadenceLeadEnrollment> EnrollLeadAsync(Guid tenantId, Guid cadenceId, string leadName, string email, string phone, string? company = null)
    {
        var cadence = await _dbContext.SalesOutreachCadences.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == cadenceId)
            ?? throw new InvalidOperationException($"Cadence {cadenceId} not found.");

        var enrollment = new SalesCadenceLeadEnrollment
        {
            TenantId = tenantId,
            CadenceId = cadenceId,
            LeadName = leadName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phone.Trim(),
            Company = company?.Trim(),
            CurrentStepNumber = 1,
            State = CadenceEnrollmentState.Active,
            NextExecutionAtUtc = DateTime.UtcNow,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _dbContext.SalesCadenceLeadEnrollments.Add(enrollment);
        await _dbContext.SaveChangesAsync();
        return enrollment;
    }

    public async Task<int> ProcessDueCadenceStepsAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var dueEnrollments = await _dbContext.SalesCadenceLeadEnrollments
            .Where(e => e.TenantId == tenantId && e.State == CadenceEnrollmentState.Active && e.NextExecutionAtUtc <= now)
            .ToListAsync();

        int executedCount = 0;

        foreach (var enrollment in dueEnrollments)
        {
            var steps = await _dbContext.SalesCadenceStepItems
                .Where(s => s.CadenceId == enrollment.CadenceId)
                .OrderBy(s => s.StepNumber)
                .ToListAsync();

            var currentStep = steps.FirstOrDefault(s => s.StepNumber == enrollment.CurrentStepNumber);

            if (currentStep == null)
            {
                // Completed all steps
                enrollment.State = CadenceEnrollmentState.Completed;
                enrollment.NextExecutionAtUtc = null;
                continue;
            }

            // Execute based on channel
            if (currentStep.Channel == CadenceChannel.PhoneCallTask || currentStep.Channel == CadenceChannel.LinkedInTask)
            {
                // Create human SDR task in workbench
                _dbContext.SdrTaskItems.Add(new SdrTaskItem
                {
                    TenantId = tenantId,
                    EnrollmentId = enrollment.Id,
                    CadenceId = enrollment.CadenceId,
                    LeadName = enrollment.LeadName,
                    PhoneNumber = enrollment.PhoneNumber,
                    Email = enrollment.Email,
                    Company = enrollment.Company,
                    TaskType = currentStep.Channel.ToString(),
                    Instruction = currentStep.BodyTemplate,
                    DueAtUtc = DateTime.UtcNow,
                    IsCompleted = false
                });
            }

            enrollment.LastExecutedAtUtc = DateTime.UtcNow;

            var nextStep = steps.FirstOrDefault(s => s.StepNumber == enrollment.CurrentStepNumber + 1);
            if (nextStep != null)
            {
                enrollment.CurrentStepNumber = nextStep.StepNumber;
                enrollment.NextExecutionAtUtc = DateTime.UtcNow.AddDays(nextStep.DayOffset);
            }
            else
            {
                enrollment.State = CadenceEnrollmentState.Completed;
                enrollment.NextExecutionAtUtc = null;
            }

            executedCount++;
        }

        await _dbContext.SaveChangesAsync();
        return executedCount;
    }

    public async Task<bool> HandleProspectReplyAsync(Guid tenantId, string emailOrPhone, string channel, string replyText)
    {
        var cleanInput = emailOrPhone.Trim().ToLowerInvariant();

        var enrollmentsToStop = await _dbContext.SalesCadenceLeadEnrollments
            .Where(e => e.TenantId == tenantId && 
                       (e.State == CadenceEnrollmentState.Active || e.State == CadenceEnrollmentState.Completed) && 
                       (e.Email == cleanInput || e.PhoneNumber == cleanInput))
            .ToListAsync();

        if (!enrollmentsToStop.Any())
            return false;

        foreach (var enrollment in enrollmentsToStop)
        {
            enrollment.State = CadenceEnrollmentState.RepliedAndStopped;
            enrollment.NextExecutionAtUtc = null;

            // Auto-complete any open SDR tasks with "Replied by prospect"
            var openTasks = await _dbContext.SdrTaskItems
                .Where(t => t.TenantId == tenantId && t.EnrollmentId == enrollment.Id && !t.IsCompleted)
                .ToListAsync();

            foreach (var task in openTasks)
            {
                task.IsCompleted = true;
                task.CompletedAtUtc = DateTime.UtcNow;
                task.Outcome = $"Auto-closed: Prospect replied via {channel}: \"{replyText}\"";
            }
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<SdrTaskItem>> GetPendingSdrTasksAsync(Guid tenantId)
    {
        return await _dbContext.SdrTaskItems
            .Where(t => t.TenantId == tenantId && !t.IsCompleted)
            .OrderBy(t => t.DueAtUtc)
            .ToListAsync();
    }

    public async Task<SdrTaskItem> CompleteSdrTaskAsync(Guid tenantId, Guid taskId, string outcome)
    {
        var task = await _dbContext.SdrTaskItems.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        task.IsCompleted = true;
        task.CompletedAtUtc = DateTime.UtcNow;
        task.Outcome = outcome;

        await _dbContext.SaveChangesAsync();
        return task;
    }
}
