using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using SGrowing.Api.Data;

namespace SGrowing.Api.Modules.Sales;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PredictiveCadenceChannel
{
    WhatsApp = 1,
    Phone = 2,
    Email = 3,
    Sms = 4,
    LinkedIn = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnrollmentStatus
{
    Active = 1,
    Paused = 2,
    Replied = 3,
    Graduated = 4,
    Bounced = 5,
    Completed = 6,
    DealWon = 7
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepConditionRule
{
    Always = 1,
    IfNoReply = 2,
    IfOpenedNoReply = 3,
    IfClickedNoReply = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CadenceCategory
{
    InboundFastResponse = 1,
    CodConfirmation = 2,
    EnterpriseOutbound = 3,
    Reengagement = 4,
    Nurture = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DialerPriorityTier
{
    PeakPriority = 1,
    StandardPriority = 2,
    CoolingOff = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContactTimeBand
{
    Peak = 1,       // >= 80%
    High = 2,       // 65% - 79%
    Moderate = 3,   // 45% - 64%
    Low = 4,        // 20% - 44%
    OffHours = 5    // < 20%
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TelemetryEventType
{
    EmailOpen = 1,
    EmailClick = 2,
    EmailReply = 3,
    EmailBounce = 4,
    WhatsAppDelivered = 5,
    WhatsAppRead = 6,
    WhatsAppReplied = 7,
    CallAnswered = 8,
    CallMissed = 9,
    CallVoicemail = 10,
    SmsDelivered = 11,
    SmsReplied = 12,
    LinkedInView = 13,
    LinkedInInMailReplied = 14,
    DealWon = 15
}

#endregion

#region Domain Entities & DTOs

public class HeatmapSlotDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public int HourOfDay { get; set; } // 0..23
    public double OverallProbabilityScore { get; set; } // 0..100 %
    public ContactTimeBand Band { get; set; }
    public int HistoricalSamples { get; set; }
    public Dictionary<PredictiveCadenceChannel, double> ChannelScores { get; set; } = new();
    public bool IsQuietHours { get; set; }
}

public class HeatmapMatrixDto
{
    public string TimeZoneId { get; set; } = "UTC";
    public int UtcOffsetMinutes { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public List<HeatmapSlotDto> Slots { get; set; } = new();
    public HeatmapSlotDto? PeakSlot { get; set; }
    public Dictionary<PredictiveCadenceChannel, double> AverageChannelWeights { get; set; } = new();
}

public class OptimalSlotRecommendation
{
    public DateTime UtcSlotTime { get; set; }
    public DateTime LocalSlotTime { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int HourOfDay { get; set; }
    public double ConfidenceScore { get; set; }
    public PredictiveCadenceChannel Channel { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public ContactTimeBand Band { get; set; }
}

public class CadenceStepDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int StepNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public PredictiveCadenceChannel Channel { get; set; }
    public TimeSpan DelayFromPrevious { get; set; } = TimeSpan.Zero;
    public bool UseOptimalTiming { get; set; } = true;
    public StepConditionRule ConditionRule { get; set; } = StepConditionRule.IfNoReply;
    public string Subject { get; set; } = string.Empty;
    public string TemplateBody { get; set; } = string.Empty;
    public Dictionary<string, string> ChannelConfig { get; set; } = new();
}

public class PredictiveCadenceDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CadenceCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CadenceStepDefinition> Steps { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public class CadenceStepExecutionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int StepNumber { get; set; }
    public PredictiveCadenceChannel Channel { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Success"; // Success, Skipped, DeferredOptimalTiming, Failed
    public string? Details { get; set; }
    public double? PredictedScoreAtExecution { get; set; }
}

public class LeadCadenceEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CadenceId { get; set; }
    public string CadenceName { get; set; } = string.Empty;
    public Guid LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string LeadEmail { get; set; } = string.Empty;
    public string LeadPhone { get; set; } = string.Empty;
    public string LeadCompany { get; set; } = string.Empty;
    public string LeadTimeZone { get; set; } = "UTC";
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public int CurrentStepNumber { get; set; } = 1;
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastStepExecutedAtUtc { get; set; }
    public DateTime? NextStepScheduledAtUtc { get; set; }
    public PredictiveCadenceChannel? NextStepChannel { get; set; }
    public string? PauseReason { get; set; }
    public double InstantaneousScore { get; set; }
    public List<CadenceStepExecutionLog> ExecutionLogs { get; set; } = new();
}

public class TelemetryInteractionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public PredictiveCadenceChannel Channel { get; set; }
    public TelemetryEventType EventType { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? MetadataJson { get; set; }
}

public class PredictiveDialerItem
{
    public Guid EnrollmentId { get; set; }
    public Guid LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string LeadPhone { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "UTC";
    public DateTime LeadLocalTime { get; set; }
    public double InstantaneousConfidenceScore { get; set; }
    public DialerPriorityTier PriorityTier { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public int CurrentStepNumber { get; set; }
    public int AttemptsCount { get; set; }
}

public class DialerCallSimulationResult
{
    public Guid LeadId { get; set; }
    public Guid EnrollmentId { get; set; }
    public string Outcome { get; set; } = string.Empty; // Answered, Voicemail, Busy, NoAnswer
    public DateTime CallTimestampUtc { get; set; } = DateTime.UtcNow;
    public bool CadenceAutoPausedOrGraduated { get; set; }
    public string ResultMessage { get; set; } = string.Empty;
    public double UpdatedConfidenceScore { get; set; }
}

public class CadenceOverviewStatsDto
{
    public int TotalCadences { get; set; }
    public int ActiveEnrollments { get; set; }
    public int RepliedOrGraduated { get; set; }
    public int CompletedWithoutReply { get; set; }
    public double OverallReplyRatePercentage { get; set; }
    public int DialerQueueCount { get; set; }
    public string TopChannel { get; set; } = "WhatsApp";
    public string TopContactWindow { get; set; } = "Tuesday 10:00 - 11:30 AM";
    public int TotalTelemetryEventsRecorded { get; set; }
}

#endregion

#region Request Contracts

public record CreateCadenceRequest(
    string Name,
    string Description,
    CadenceCategory Category,
    List<CadenceStepDefinition> Steps);

public record UpdateCadenceRequest(
    string? Name,
    string? Description,
    CadenceCategory? Category,
    bool? IsActive,
    List<CadenceStepDefinition>? Steps);

public record EnrollLeadRequest(
    Guid CadenceId,
    Guid LeadId,
    string LeadName,
    string LeadEmail,
    string LeadPhone,
    string LeadCompany,
    string? LeadTimeZone);

public record RecordTelemetryRequest(
    Guid LeadId,
    PredictiveCadenceChannel Channel,
    TelemetryEventType EventType,
    DateTime? OccurredAtUtc = null,
    string? MetadataJson = null);

public record SimulateCallRequest(
    Guid EnrollmentId,
    string Outcome); // Answered, Voicemail, Busy, NoAnswer

public record UpdateEnrollmentStatusRequest(
    EnrollmentStatus Status,
    string? Reason);

#endregion

#region Service Interface & Implementation

public interface IPredictiveCadenceService
{
    Task<List<PredictiveCadenceDefinition>> GetCadencesAsync(Guid tenantId, CadenceCategory? category = null);
    Task<PredictiveCadenceDefinition?> GetCadenceByIdAsync(Guid tenantId, Guid cadenceId);
    Task<PredictiveCadenceDefinition> CreateCadenceAsync(Guid tenantId, CreateCadenceRequest request);
    Task<PredictiveCadenceDefinition?> UpdateCadenceAsync(Guid tenantId, Guid cadenceId, UpdateCadenceRequest request);
    Task<bool> DeleteCadenceAsync(Guid tenantId, Guid cadenceId);

    Task<List<LeadCadenceEnrollment>> GetEnrollmentsAsync(Guid tenantId, Guid? cadenceId = null, EnrollmentStatus? status = null);
    Task<LeadCadenceEnrollment?> GetEnrollmentByIdAsync(Guid tenantId, Guid enrollmentId);
    Task<LeadCadenceEnrollment> EnrollLeadAsync(Guid tenantId, EnrollLeadRequest request);
    Task<LeadCadenceEnrollment?> PauseEnrollmentAsync(Guid tenantId, Guid enrollmentId, string reason);
    Task<LeadCadenceEnrollment?> ResumeEnrollmentAsync(Guid tenantId, Guid enrollmentId);
    Task<LeadCadenceEnrollment?> AdvanceStepAsync(Guid tenantId, Guid enrollmentId);
    Task<LeadCadenceEnrollment?> UpdateEnrollmentStatusAsync(Guid tenantId, Guid enrollmentId, EnrollmentStatus newStatus, string? reason);

    Task<HeatmapMatrixDto> Get7x24HeatmapAsync(Guid tenantId, string? timeZone = null, PredictiveCadenceChannel? channel = null, Guid? leadId = null);
    Task<List<OptimalSlotRecommendation>> GetOptimalContactSlotsAsync(Guid tenantId, Guid leadId, PredictiveCadenceChannel? channel = null, string? timeZone = null, int limit = 3, DateTime? referenceTimeUtc = null);

    Task<TelemetryInteractionEvent> RecordTelemetryEventAsync(Guid tenantId, RecordTelemetryRequest request);
    Task<List<TelemetryInteractionEvent>> GetTelemetryEventsAsync(Guid tenantId, Guid? leadId = null, int limit = 50);

    Task<List<PredictiveDialerItem>> GetPredictiveDialerQueueAsync(Guid tenantId, int limit = 20, DateTime? referenceTimeUtc = null);
    Task<DialerCallSimulationResult> SimulateDialerCallAsync(Guid tenantId, Guid enrollmentId, string outcome);

    Task<CadenceOverviewStatsDto> GetOverviewStatsAsync(Guid tenantId);
}

public class PredictiveCadenceService : IPredictiveCadenceService
{
    private readonly SGrowingDbContext? _db;

    // Thread-safe in-memory storage partitioned by tenant
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, PredictiveCadenceDefinition>> s_cadences = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, LeadCadenceEnrollment>> s_enrollments = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentBag<TelemetryInteractionEvent>> s_telemetry = new();

    private static readonly object s_initLock = new();
    private static bool s_seeded = false;

    public static readonly Guid DefaultDemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static PredictiveCadenceService Instance { get; } = new(null);

    public PredictiveCadenceService(SGrowingDbContext? db = null)
    {
        _db = db;
        EnsureSeeded();
    }

    #region Cadence Management

    public Task<List<PredictiveCadenceDefinition>> GetCadencesAsync(Guid tenantId, CadenceCategory? category = null)
    {
        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        var query = tenantCadences.Values.AsEnumerable();
        if (category.HasValue)
        {
            query = query.Where(c => c.Category == category.Value);
        }
        return Task.FromResult(query.OrderByDescending(c => c.CreatedAtUtc).ToList());
    }

    public Task<PredictiveCadenceDefinition?> GetCadenceByIdAsync(Guid tenantId, Guid cadenceId)
    {
        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        if (!tenantCadences.TryGetValue(cadenceId, out var cadence))
        {
            var demoCadences = s_cadences.GetOrAdd(DefaultDemoTenantId, _ => new());
            demoCadences.TryGetValue(cadenceId, out cadence);
        }
        return Task.FromResult(cadence);
    }

    public Task<PredictiveCadenceDefinition> CreateCadenceAsync(Guid tenantId, CreateCadenceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Cadence name is required.", nameof(request));

        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        var cadence = new PredictiveCadenceDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Category = request.Category,
            IsActive = true,
            Steps = request.Steps ?? new List<CadenceStepDefinition>(),
            CreatedAtUtc = DateTime.UtcNow
        };

        // Ensure step numbers are 1-based sequential
        for (int i = 0; i < cadence.Steps.Count; i++)
        {
            cadence.Steps[i].StepNumber = i + 1;
            if (cadence.Steps[i].Id == Guid.Empty)
                cadence.Steps[i].Id = Guid.NewGuid();
        }

        tenantCadences[cadence.Id] = cadence;
        return Task.FromResult(cadence);
    }

    public Task<PredictiveCadenceDefinition?> UpdateCadenceAsync(Guid tenantId, Guid cadenceId, UpdateCadenceRequest request)
    {
        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        if (!tenantCadences.TryGetValue(cadenceId, out var existing))
            return Task.FromResult<PredictiveCadenceDefinition?>(null);

        if (!string.IsNullOrWhiteSpace(request.Name))
            existing.Name = request.Name.Trim();

        if (request.Description != null)
            existing.Description = request.Description;

        if (request.Category.HasValue)
            existing.Category = request.Category.Value;

        if (request.IsActive.HasValue)
            existing.IsActive = request.IsActive.Value;

        if (request.Steps != null)
        {
            existing.Steps = request.Steps;
            for (int i = 0; i < existing.Steps.Count; i++)
            {
                existing.Steps[i].StepNumber = i + 1;
                if (existing.Steps[i].Id == Guid.Empty)
                    existing.Steps[i].Id = Guid.NewGuid();
            }
        }

        existing.UpdatedAtUtc = DateTime.UtcNow;
        return Task.FromResult<PredictiveCadenceDefinition?>(existing);
    }

    public Task<bool> DeleteCadenceAsync(Guid tenantId, Guid cadenceId)
    {
        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        var removed = tenantCadences.TryRemove(cadenceId, out _);
        return Task.FromResult(removed);
    }

    #endregion

    #region Lead Enrollment & Lifecycle

    public Task<List<LeadCadenceEnrollment>> GetEnrollmentsAsync(Guid tenantId, Guid? cadenceId = null, EnrollmentStatus? status = null)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        var query = tenantEnrollments.Values.AsEnumerable();

        if (cadenceId.HasValue)
            query = query.Where(e => e.CadenceId == cadenceId.Value);

        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        // Update instantaneous scores dynamically before returning
        var nowUtc = DateTime.UtcNow;
        var list = query.OrderByDescending(e => e.EnrolledAtUtc).ToList();
        foreach (var item in list)
        {
            item.InstantaneousScore = CalculateInstantaneousScore(tenantId, item.LeadId, item.NextStepChannel ?? PredictiveCadenceChannel.Phone, item.LeadTimeZone, nowUtc);
        }

        return Task.FromResult(list);
    }

    public Task<LeadCadenceEnrollment?> GetEnrollmentByIdAsync(Guid tenantId, Guid enrollmentId)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        if (tenantEnrollments.TryGetValue(enrollmentId, out var enrollment))
        {
            enrollment.InstantaneousScore = CalculateInstantaneousScore(tenantId, enrollment.LeadId, enrollment.NextStepChannel ?? PredictiveCadenceChannel.Phone, enrollment.LeadTimeZone, DateTime.UtcNow);
            return Task.FromResult<LeadCadenceEnrollment?>(enrollment);
        }
        return Task.FromResult<LeadCadenceEnrollment?>(null);
    }

    public Task<LeadCadenceEnrollment> EnrollLeadAsync(Guid tenantId, EnrollLeadRequest request)
    {
        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        if (!tenantCadences.TryGetValue(request.CadenceId, out var cadence))
        {
            var demoCadences = s_cadences.GetOrAdd(DefaultDemoTenantId, _ => new());
            if (!demoCadences.TryGetValue(request.CadenceId, out cadence))
                throw new InvalidOperationException($"Cadence {request.CadenceId} not found for tenant {tenantId}.");
        }

        var firstStep = cadence.Steps.OrderBy(s => s.StepNumber).FirstOrDefault();
        var nextChannel = firstStep?.Channel ?? PredictiveCadenceChannel.WhatsApp;
        var timeZone = string.IsNullOrWhiteSpace(request.LeadTimeZone) ? "UTC" : request.LeadTimeZone.Trim();

        var enrollment = new LeadCadenceEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CadenceId = cadence.Id,
            CadenceName = cadence.Name,
            LeadId = request.LeadId != Guid.Empty ? request.LeadId : Guid.NewGuid(),
            LeadName = request.LeadName,
            LeadEmail = request.LeadEmail,
            LeadPhone = request.LeadPhone,
            LeadCompany = request.LeadCompany,
            LeadTimeZone = timeZone,
            Status = EnrollmentStatus.Active,
            CurrentStepNumber = 1,
            EnrolledAtUtc = DateTime.UtcNow,
            NextStepChannel = nextChannel,
            ExecutionLogs = new List<CadenceStepExecutionLog>()
        };

        // Schedule first step based on delay or optimal slot
        var initialDelay = firstStep?.DelayFromPrevious ?? TimeSpan.Zero;
        if (firstStep?.UseOptimalTiming == true)
        {
            var optimal = RecommendBestSingleSlot(tenantId, enrollment.LeadId, nextChannel, timeZone, DateTime.UtcNow.Add(initialDelay));
            enrollment.NextStepScheduledAtUtc = optimal.UtcSlotTime;
        }
        else
        {
            enrollment.NextStepScheduledAtUtc = DateTime.UtcNow.Add(initialDelay);
        }

        enrollment.InstantaneousScore = CalculateInstantaneousScore(tenantId, enrollment.LeadId, nextChannel, timeZone, DateTime.UtcNow);

        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        tenantEnrollments[enrollment.Id] = enrollment;

        return Task.FromResult(enrollment);
    }

    public Task<LeadCadenceEnrollment?> PauseEnrollmentAsync(Guid tenantId, Guid enrollmentId, string reason)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        if (!tenantEnrollments.TryGetValue(enrollmentId, out var enrollment))
            return Task.FromResult<LeadCadenceEnrollment?>(null);

        enrollment.Status = EnrollmentStatus.Paused;
        enrollment.PauseReason = reason ?? "Manually paused by operator.";
        enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
        {
            StepNumber = enrollment.CurrentStepNumber,
            Channel = enrollment.NextStepChannel ?? PredictiveCadenceChannel.Phone,
            ExecutedAtUtc = DateTime.UtcNow,
            Status = "Paused",
            Details = enrollment.PauseReason
        });

        return Task.FromResult<LeadCadenceEnrollment?>(enrollment);
    }

    public Task<LeadCadenceEnrollment?> ResumeEnrollmentAsync(Guid tenantId, Guid enrollmentId)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        if (!tenantEnrollments.TryGetValue(enrollmentId, out var enrollment))
            return Task.FromResult<LeadCadenceEnrollment?>(null);

        enrollment.Status = EnrollmentStatus.Active;
        enrollment.PauseReason = null;
        enrollment.NextStepScheduledAtUtc = DateTime.UtcNow.AddMinutes(5); // Schedule immediate resumption

        enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
        {
            StepNumber = enrollment.CurrentStepNumber,
            Channel = enrollment.NextStepChannel ?? PredictiveCadenceChannel.Phone,
            ExecutedAtUtc = DateTime.UtcNow,
            Status = "Resumed",
            Details = "Resumed cadence execution sequence."
        });

        return Task.FromResult<LeadCadenceEnrollment?>(enrollment);
    }

    public Task<LeadCadenceEnrollment?> AdvanceStepAsync(Guid tenantId, Guid enrollmentId)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        if (!tenantEnrollments.TryGetValue(enrollmentId, out var enrollment))
            return Task.FromResult<LeadCadenceEnrollment?>(null);

        if (enrollment.Status != EnrollmentStatus.Active)
            return Task.FromResult<LeadCadenceEnrollment?>(enrollment);

        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        if (!tenantCadences.TryGetValue(enrollment.CadenceId, out var cadence))
        {
            var demoCadences = s_cadences.GetOrAdd(DefaultDemoTenantId, _ => new());
            if (!demoCadences.TryGetValue(enrollment.CadenceId, out cadence))
                return Task.FromResult<LeadCadenceEnrollment?>(enrollment);
        }

        var currentStepDef = cadence.Steps.FirstOrDefault(s => s.StepNumber == enrollment.CurrentStepNumber);
        var scoreAtExec = CalculateInstantaneousScore(tenantId, enrollment.LeadId, enrollment.NextStepChannel ?? PredictiveCadenceChannel.Phone, enrollment.LeadTimeZone, DateTime.UtcNow);

        // Record execution of the current step
        enrollment.LastStepExecutedAtUtc = DateTime.UtcNow;
        enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
        {
            StepNumber = enrollment.CurrentStepNumber,
            Channel = enrollment.NextStepChannel ?? PredictiveCadenceChannel.WhatsApp,
            ExecutedAtUtc = DateTime.UtcNow,
            Status = "Executed",
            Details = $"Executed step #{enrollment.CurrentStepNumber} ({currentStepDef?.Name ?? "Outreach Step"})",
            PredictedScoreAtExecution = scoreAtExec
        });

        // Determine next step
        var nextStepDef = cadence.Steps.FirstOrDefault(s => s.StepNumber == enrollment.CurrentStepNumber + 1);
        if (nextStepDef != null)
        {
            enrollment.CurrentStepNumber = nextStepDef.StepNumber;
            enrollment.NextStepChannel = nextStepDef.Channel;

            if (nextStepDef.UseOptimalTiming)
            {
                var nextWindow = RecommendBestSingleSlot(
                    tenantId,
                    enrollment.LeadId,
                    nextStepDef.Channel,
                    enrollment.LeadTimeZone,
                    DateTime.UtcNow.Add(nextStepDef.DelayFromPrevious));

                enrollment.NextStepScheduledAtUtc = nextWindow.UtcSlotTime;
            }
            else
            {
                enrollment.NextStepScheduledAtUtc = DateTime.UtcNow.Add(nextStepDef.DelayFromPrevious);
            }
        }
        else
        {
            // All steps completed without reply
            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.NextStepScheduledAtUtc = null;
            enrollment.NextStepChannel = null;
            enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
            {
                StepNumber = enrollment.CurrentStepNumber,
                Channel = currentStepDef?.Channel ?? PredictiveCadenceChannel.Email,
                ExecutedAtUtc = DateTime.UtcNow,
                Status = "Completed",
                Details = "Sequence finished all scheduled cadence touches."
            });
        }

        return Task.FromResult<LeadCadenceEnrollment?>(enrollment);
    }

    public Task<LeadCadenceEnrollment?> UpdateEnrollmentStatusAsync(Guid tenantId, Guid enrollmentId, EnrollmentStatus newStatus, string? reason)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        if (!tenantEnrollments.TryGetValue(enrollmentId, out var enrollment))
            return Task.FromResult<LeadCadenceEnrollment?>(null);

        enrollment.Status = newStatus;
        if (!string.IsNullOrWhiteSpace(reason))
            enrollment.PauseReason = reason;

        if (newStatus == EnrollmentStatus.Replied || newStatus == EnrollmentStatus.Graduated || newStatus == EnrollmentStatus.DealWon || newStatus == EnrollmentStatus.Completed)
        {
            enrollment.NextStepScheduledAtUtc = null;
        }

        enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
        {
            StepNumber = enrollment.CurrentStepNumber,
            Channel = enrollment.NextStepChannel ?? PredictiveCadenceChannel.Phone,
            ExecutedAtUtc = DateTime.UtcNow,
            Status = newStatus.ToString(),
            Details = $"Status updated to {newStatus}. {reason}".Trim()
        });

        return Task.FromResult<LeadCadenceEnrollment?>(enrollment);
    }

    #endregion

    #region 7x24 Contact Time Heatmap & Optimal Slot Recommendation

    public Task<HeatmapMatrixDto> Get7x24HeatmapAsync(Guid tenantId, string? timeZone = null, PredictiveCadenceChannel? channel = null, Guid? leadId = null)
    {
        var tzOffset = ParseTimeZoneOffset(timeZone);
        var slots = new List<HeatmapSlotDto>(168);

        // Fetch telemetry samples for this tenant/lead
        var telemetryEvents = GetTelemetryBag(tenantId);
        if (leadId.HasValue && leadId.Value != Guid.Empty)
        {
            telemetryEvents = telemetryEvents.Where(t => t.LeadId == leadId.Value).ToList();
        }

        var channelAverages = new Dictionary<PredictiveCadenceChannel, double>
        {
            [PredictiveCadenceChannel.WhatsApp] = 0.0,
            [PredictiveCadenceChannel.Phone] = 0.0,
            [PredictiveCadenceChannel.Email] = 0.0,
            [PredictiveCadenceChannel.Sms] = 0.0,
            [PredictiveCadenceChannel.LinkedIn] = 0.0
        };

        HeatmapSlotDto? peakSlot = null;
        double maxProb = -1.0;

        // Iterate 7 days (Sunday=0 to Saturday=6) and 24 hours (0..23)
        for (int d = 0; d < 7; d++)
        {
            var dayOfWeek = (DayOfWeek)d;
            for (int h = 0; h < 24; h++)
            {
                // Translate target local (dayOfWeek, h) back to UTC to match recorded events or calculate prior
                var slotDto = CalculateSlotProbability(dayOfWeek, h, tzOffset, telemetryEvents, channel);
                slots.Add(slotDto);

                if (slotDto.OverallProbabilityScore > maxProb)
                {
                    maxProb = slotDto.OverallProbabilityScore;
                    peakSlot = slotDto;
                }

                foreach (var kvp in slotDto.ChannelScores)
                {
                    channelAverages[kvp.Key] += kvp.Value;
                }
            }
        }

        foreach (var key in channelAverages.Keys.ToList())
        {
            channelAverages[key] = Math.Round(channelAverages[key] / 168.0, 1);
        }

        var result = new HeatmapMatrixDto
        {
            TimeZoneId = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
            UtcOffsetMinutes = (int)tzOffset.TotalMinutes,
            GeneratedAtUtc = DateTime.UtcNow,
            Slots = slots,
            PeakSlot = peakSlot,
            AverageChannelWeights = channelAverages
        };

        return Task.FromResult(result);
    }

    public Task<List<OptimalSlotRecommendation>> GetOptimalContactSlotsAsync(
        Guid tenantId,
        Guid leadId,
        PredictiveCadenceChannel? channel = null,
        string? timeZone = null,
        int limit = 3,
        DateTime? referenceTimeUtc = null)
    {
        if (limit <= 0) limit = 3;
        var startUtc = referenceTimeUtc ?? DateTime.UtcNow;

        // If leadId is known, check if we have their timezone stored in enrollment
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            var enrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
            var enrollee = enrollments.Values.FirstOrDefault(e => e.LeadId == leadId);
            timeZone = enrollee?.LeadTimeZone ?? "UTC";
        }

        var tzOffset = ParseTimeZoneOffset(timeZone);
        var targetChannel = channel ?? PredictiveCadenceChannel.Phone;
        var telemetry = GetTelemetryBag(tenantId);

        var candidateSlots = new List<OptimalSlotRecommendation>();

        // Project next 7 days in 1-hour increments (168 prospective hours)
        for (int hourStep = 1; hourStep <= 168; hourStep++)
        {
            var prospectiveUtc = startUtc.AddHours(hourStep);
            var prospectiveLocal = prospectiveUtc.Add(tzOffset);

            var localHour = prospectiveLocal.Hour;
            var localDay = prospectiveLocal.DayOfWeek;

            // Filter out quiet hours (Lead local time: 20:00 - 07:59, or Sunday for B2B phone calls)
            bool isNight = localHour < 8 || localHour >= 20;
            bool isSundayB2B = (targetChannel == PredictiveCadenceChannel.Phone || targetChannel == PredictiveCadenceChannel.LinkedIn) && localDay == DayOfWeek.Sunday;

            if (isNight || isSundayB2B)
                continue;

            // Compute probability for this exact slot
            var slotProb = CalculateSlotProbability(localDay, localHour, tzOffset, telemetry, targetChannel);
            var score = slotProb.OverallProbabilityScore;

            // Personalization boost if lead previously engaged in this exact local hour window
            var leadActivityMatches = telemetry.Count(t => t.LeadId == leadId && t.OccurredAtUtc.Add(tzOffset).Hour == localHour);
            if (leadActivityMatches > 0)
            {
                score = Math.Min(98.5, score + (leadActivityMatches * 4.5));
            }

            var reasoning = BuildRecommendationReasoning(localDay, localHour, targetChannel, score, leadActivityMatches);

            candidateSlots.Add(new OptimalSlotRecommendation
            {
                UtcSlotTime = prospectiveUtc,
                LocalSlotTime = prospectiveLocal,
                DayOfWeek = localDay,
                HourOfDay = localHour,
                ConfidenceScore = Math.Round(score, 1),
                Channel = targetChannel,
                Reasoning = reasoning,
                Band = GetBand(score)
            });
        }

        // Select top distinct candidate slots (avoid picking identical contiguous hours if possible)
        var ranked = candidateSlots
            .OrderByDescending(s => s.ConfidenceScore)
            .ThenBy(s => s.UtcSlotTime)
            .Take(limit)
            .ToList();

        return Task.FromResult(ranked);
    }

    private OptimalSlotRecommendation RecommendBestSingleSlot(Guid tenantId, Guid leadId, PredictiveCadenceChannel channel, string timeZone, DateTime earliestUtc)
    {
        var slots = GetOptimalContactSlotsAsync(tenantId, leadId, channel, timeZone, 1, earliestUtc).GetAwaiter().GetResult();
        if (slots.Count > 0)
            return slots[0];

        var tzOffset = ParseTimeZoneOffset(timeZone);
        return new OptimalSlotRecommendation
        {
            UtcSlotTime = earliestUtc,
            LocalSlotTime = earliestUtc.Add(tzOffset),
            DayOfWeek = earliestUtc.Add(tzOffset).DayOfWeek,
            HourOfDay = earliestUtc.Add(tzOffset).Hour,
            ConfidenceScore = 75.0,
            Channel = channel,
            Reasoning = "Standard scheduled cadence window.",
            Band = ContactTimeBand.High
        };
    }

    #endregion

    #region Telemetry & Event Recording (Trigger auto-pause on reply)

    public Task<TelemetryInteractionEvent> RecordTelemetryEventAsync(Guid tenantId, RecordTelemetryRequest request)
    {
        var evt = new TelemetryInteractionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LeadId = request.LeadId,
            Channel = request.Channel,
            EventType = request.EventType,
            OccurredAtUtc = request.OccurredAtUtc ?? DateTime.UtcNow,
            MetadataJson = request.MetadataJson
        };

        var bag = s_telemetry.GetOrAdd(tenantId, _ => new());
        bag.Add(evt);

        // Automated Pause-on-Reply / Deal-Won trigger
        TriggerAutomatedCadenceStateTransition(tenantId, evt);

        return Task.FromResult(evt);
    }

    public Task<List<TelemetryInteractionEvent>> GetTelemetryEventsAsync(Guid tenantId, Guid? leadId = null, int limit = 50)
    {
        var bag = GetTelemetryBag(tenantId);
        var query = bag.AsEnumerable();
        if (leadId.HasValue && leadId.Value != Guid.Empty)
        {
            query = query.Where(t => t.LeadId == leadId.Value);
        }

        var result = query.OrderByDescending(t => t.OccurredAtUtc).Take(limit).ToList();
        return Task.FromResult(result);
    }

    private void TriggerAutomatedCadenceStateTransition(Guid tenantId, TelemetryInteractionEvent evt)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        var matching = tenantEnrollments.Values.Where(e => e.LeadId == evt.LeadId && e.Status == EnrollmentStatus.Active).ToList();

        if (matching.Count == 0) return;

        bool isReplyOrAnswer = evt.EventType is
            TelemetryEventType.EmailReply or
            TelemetryEventType.WhatsAppReplied or
            TelemetryEventType.SmsReplied or
            TelemetryEventType.LinkedInInMailReplied or
            TelemetryEventType.CallAnswered;

        bool isDealWon = evt.EventType == TelemetryEventType.DealWon;
        bool isBounce = evt.EventType == TelemetryEventType.EmailBounce;

        foreach (var enrollment in matching)
        {
            if (isReplyOrAnswer)
            {
                enrollment.Status = EnrollmentStatus.Replied;
                enrollment.PauseReason = $"Auto-paused: Prospect replied via {evt.Channel} at {evt.OccurredAtUtc:yyyy-MM-dd HH:mm} UTC.";
                enrollment.NextStepScheduledAtUtc = null;
                enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
                {
                    StepNumber = enrollment.CurrentStepNumber,
                    Channel = evt.Channel,
                    ExecutedAtUtc = evt.OccurredAtUtc,
                    Status = "Replied - Auto-Paused",
                    Details = $"Cadence automatically paused on inbound response ({evt.EventType})."
                });
            }
            else if (isDealWon)
            {
                enrollment.Status = EnrollmentStatus.DealWon;
                enrollment.PauseReason = $"Auto-graduated: Deal marked as Won (Opportunity closed won at {evt.OccurredAtUtc:yyyy-MM-dd HH:mm} UTC).";
                enrollment.NextStepScheduledAtUtc = null;
                enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
                {
                    StepNumber = enrollment.CurrentStepNumber,
                    Channel = evt.Channel,
                    ExecutedAtUtc = evt.OccurredAtUtc,
                    Status = "DealWon - Graduated",
                    Details = "Cadence graduated as deal won."
                });
            }
            else if (isBounce)
            {
                enrollment.Status = EnrollmentStatus.Bounced;
                enrollment.PauseReason = $"Delivery failure: Channel address bounced.";
                enrollment.NextStepScheduledAtUtc = null;
                enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
                {
                    StepNumber = enrollment.CurrentStepNumber,
                    Channel = evt.Channel,
                    ExecutedAtUtc = evt.OccurredAtUtc,
                    Status = "Bounced",
                    Details = "Delivery failed, cadence halted."
                });
            }
        }
    }

    #endregion

    #region Predictive Dialer Queue & Simulation

    public Task<List<PredictiveDialerItem>> GetPredictiveDialerQueueAsync(Guid tenantId, int limit = 20, DateTime? referenceTimeUtc = null)
    {
        var nowUtc = referenceTimeUtc ?? DateTime.UtcNow;
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());

        // Leads currently active at a Phone step
        var activePhoneLeads = tenantEnrollments.Values
            .Where(e => e.Status == EnrollmentStatus.Active && e.NextStepChannel == PredictiveCadenceChannel.Phone)
            .ToList();

        var queue = new List<PredictiveDialerItem>();

        foreach (var lead in activePhoneLeads)
        {
            var tzOffset = ParseTimeZoneOffset(lead.LeadTimeZone);
            var leadLocalTime = nowUtc.Add(tzOffset);
            var score = CalculateInstantaneousScore(tenantId, lead.LeadId, PredictiveCadenceChannel.Phone, lead.LeadTimeZone, nowUtc);

            var priorityTier = score >= 75.0 ? DialerPriorityTier.PeakPriority
                             : score >= 45.0 ? DialerPriorityTier.StandardPriority
                             : DialerPriorityTier.CoolingOff;

            string actionMessage;
            if (leadLocalTime.Hour < 8 || leadLocalTime.Hour >= 20)
            {
                actionMessage = $"Hold: Local time is {leadLocalTime:hh:mm tt} (Quiet hours in client timezone).";
                priorityTier = DialerPriorityTier.CoolingOff;
            }
            else if (score >= 80.0)
            {
                actionMessage = $"Connect Now: Peak window ({score:F1}% connect probability) in {lead.LeadCompany}.";
            }
            else if (score >= 60.0)
            {
                actionMessage = $"Good window ({score:F1}%): High likelihood of pickup.";
            }
            else
            {
                actionMessage = $"Standard cadence queue ({score:F1}%): Awaiting higher confidence slot.";
            }

            var previousCallAttempts = lead.ExecutionLogs.Count(l => l.Channel == PredictiveCadenceChannel.Phone);

            queue.Add(new PredictiveDialerItem
            {
                EnrollmentId = lead.Id,
                LeadId = lead.LeadId,
                LeadName = lead.LeadName,
                LeadPhone = lead.LeadPhone,
                Company = lead.LeadCompany,
                TimeZone = lead.LeadTimeZone,
                LeadLocalTime = leadLocalTime,
                InstantaneousConfidenceScore = Math.Round(score, 1),
                PriorityTier = priorityTier,
                RecommendedAction = actionMessage,
                CurrentStepNumber = lead.CurrentStepNumber,
                AttemptsCount = previousCallAttempts
            });
        }

        var sortedQueue = queue
            .OrderByDescending(q => q.InstantaneousConfidenceScore)
            .ThenBy(q => q.AttemptsCount)
            .Take(limit)
            .ToList();

        return Task.FromResult(sortedQueue);
    }

    public Task<DialerCallSimulationResult> SimulateDialerCallAsync(Guid tenantId, Guid enrollmentId, string outcome)
    {
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        if (!tenantEnrollments.TryGetValue(enrollmentId, out var enrollment))
        {
            var demoEnrollments = s_enrollments.GetOrAdd(DefaultDemoTenantId, _ => new());
            if (!demoEnrollments.TryGetValue(enrollmentId, out enrollment))
                throw new KeyNotFoundException($"Enrollment {enrollmentId} was not found.");
        }

        string normOutcome = outcome?.Trim().ToLowerInvariant() switch
        {
            "answered" => "Answered",
            "busy" => "Busy",
            "voicemail" => "Voicemail",
            _ => "NoAnswer"
        };

        var timestamp = DateTime.UtcNow;
        bool autoPausedOrGraduated = false;
        string resultMsg;

        if (normOutcome == "Answered")
        {
            // Record telemetry event: CallAnswered
            RecordTelemetryEventAsync(tenantId, new RecordTelemetryRequest(
                enrollment.LeadId,
                PredictiveCadenceChannel.Phone,
                TelemetryEventType.CallAnswered,
                timestamp,
                "{\"simulatedDurationSec\": 145, \"disposition\": \"Connected & Pitched\"}")).GetAwaiter().GetResult();

            autoPausedOrGraduated = true;
            resultMsg = $"Call answered by {enrollment.LeadName}! Cadence automatically paused and transitioned to Replied.";
        }
        else
        {
            var eventType = normOutcome == "Voicemail" ? TelemetryEventType.CallVoicemail : TelemetryEventType.CallMissed;
            RecordTelemetryEventAsync(tenantId, new RecordTelemetryRequest(
                enrollment.LeadId,
                PredictiveCadenceChannel.Phone,
                eventType,
                timestamp,
                $"{{\"simulatedOutcome\": \"{normOutcome}\"}}")).GetAwaiter().GetResult();

            // Advance step or reschedule
            enrollment.ExecutionLogs.Add(new CadenceStepExecutionLog
            {
                StepNumber = enrollment.CurrentStepNumber,
                Channel = PredictiveCadenceChannel.Phone,
                ExecutedAtUtc = timestamp,
                Status = normOutcome,
                Details = $"Dialer attempt completed: {normOutcome}. Scheduled next touch."
            });

            AdvanceStepAsync(tenantId, enrollment.Id).GetAwaiter().GetResult();

            resultMsg = $"Call outcome was '{normOutcome}'. Logged attempt and advanced to subsequent cadence step.";
        }

        var updatedScore = CalculateInstantaneousScore(tenantId, enrollment.LeadId, PredictiveCadenceChannel.Phone, enrollment.LeadTimeZone, timestamp);

        return Task.FromResult(new DialerCallSimulationResult
        {
            LeadId = enrollment.LeadId,
            EnrollmentId = enrollment.Id,
            Outcome = normOutcome,
            CallTimestampUtc = timestamp,
            CadenceAutoPausedOrGraduated = autoPausedOrGraduated,
            ResultMessage = resultMsg,
            UpdatedConfidenceScore = Math.Round(updatedScore, 1)
        });
    }

    #endregion

    #region Overview Statistics

    public Task<CadenceOverviewStatsDto> GetOverviewStatsAsync(Guid tenantId)
    {
        var tenantCadences = s_cadences.GetOrAdd(tenantId, _ => new());
        var tenantEnrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        var tenantTelemetry = GetTelemetryBag(tenantId);

        int totalCadences = tenantCadences.Count;
        int active = tenantEnrollments.Values.Count(e => e.Status == EnrollmentStatus.Active);
        int repliedOrGraduated = tenantEnrollments.Values.Count(e => e.Status == EnrollmentStatus.Replied || e.Status == EnrollmentStatus.Graduated || e.Status == EnrollmentStatus.DealWon);
        int completed = tenantEnrollments.Values.Count(e => e.Status == EnrollmentStatus.Completed);
        int totalTerminal = repliedOrGraduated + completed;

        double replyRate = totalTerminal > 0
            ? Math.Round(((double)repliedOrGraduated / totalTerminal) * 100.0, 1)
            : 42.8; // Baseline demo rate

        int dialerQueueCount = tenantEnrollments.Values.Count(e => e.Status == EnrollmentStatus.Active && e.NextStepChannel == PredictiveCadenceChannel.Phone);

        return Task.FromResult(new CadenceOverviewStatsDto
        {
            TotalCadences = totalCadences,
            ActiveEnrollments = active,
            RepliedOrGraduated = repliedOrGraduated,
            CompletedWithoutReply = completed,
            OverallReplyRatePercentage = replyRate,
            DialerQueueCount = dialerQueueCount,
            TopChannel = "WhatsApp (58% Reply Rate)",
            TopContactWindow = "Tuesday & Thursday 10:00 - 11:30 AM",
            TotalTelemetryEventsRecorded = tenantTelemetry.Count
        });
    }

    #endregion

    #region Mathematical & Statistical Calculations

    public double CalculateInstantaneousScore(Guid tenantId, Guid leadId, PredictiveCadenceChannel channel, string timeZone, DateTime referenceUtc)
    {
        var tzOffset = ParseTimeZoneOffset(timeZone);
        var localTime = referenceUtc.Add(tzOffset);
        var localDay = localTime.DayOfWeek;
        var localHour = localTime.Hour;

        var telemetry = GetTelemetryBag(tenantId);
        var slotProb = CalculateSlotProbability(localDay, localHour, tzOffset, telemetry, channel);
        var score = slotProb.OverallProbabilityScore;

        // Apply personal lead telemetry boost
        var leadActivityMatches = telemetry.Count(t => t.LeadId == leadId && t.OccurredAtUtc.Add(tzOffset).Hour == localHour);
        if (leadActivityMatches > 0)
        {
            score = Math.Min(98.0, score + (leadActivityMatches * 3.5));
        }

        // Night off-hours dampener
        if (localHour < 8 || localHour >= 20)
        {
            score = Math.Min(score, 12.0);
        }

        return Math.Round(score, 1);
    }

    private HeatmapSlotDto CalculateSlotProbability(
        DayOfWeek localDay,
        int localHour,
        TimeSpan tzOffset,
        List<TelemetryInteractionEvent> telemetryEvents,
        PredictiveCadenceChannel? filterChannel = null)
    {
        // 1. Baseline prior probability curve based on local hour and business days
        var baselineChannelScores = new Dictionary<PredictiveCadenceChannel, double>();
        foreach (PredictiveCadenceChannel ch in Enum.GetValues<PredictiveCadenceChannel>())
        {
            baselineChannelScores[ch] = CalculateChannelPrior(ch, localDay, localHour);
        }

        // 2. Telemetry evidence modulation
        // Filter events that occurred during this specific day and hour in local time
        int sampleCount = 0;
        double telemetryModifier = 0.0;

        foreach (var evt in telemetryEvents)
        {
            var evtLocal = evt.OccurredAtUtc.Add(tzOffset);
            if (evtLocal.DayOfWeek == localDay && evtLocal.Hour == localHour)
            {
                sampleCount++;
                double eventWeight = evt.EventType switch
                {
                    TelemetryEventType.CallAnswered => 6.0,
                    TelemetryEventType.WhatsAppReplied => 5.5,
                    TelemetryEventType.EmailReply => 5.0,
                    TelemetryEventType.SmsReplied => 4.5,
                    TelemetryEventType.LinkedInInMailReplied => 4.0,
                    TelemetryEventType.EmailClick => 3.0,
                    TelemetryEventType.WhatsAppRead => 2.0,
                    TelemetryEventType.EmailOpen => 1.5,
                    TelemetryEventType.LinkedInView => 1.0,
                    TelemetryEventType.CallVoicemail => -1.0,
                    TelemetryEventType.CallMissed => -1.5,
                    TelemetryEventType.EmailBounce => -4.0,
                    _ => 0.0
                };

                telemetryModifier += eventWeight;
                if (baselineChannelScores.ContainsKey(evt.Channel))
                {
                    baselineChannelScores[evt.Channel] = Math.Clamp(baselineChannelScores[evt.Channel] + eventWeight, 2.0, 99.0);
                }
            }
        }

        // Calculate combined score
        double overallScore;
        if (filterChannel.HasValue && baselineChannelScores.TryGetValue(filterChannel.Value, out var specificScore))
        {
            overallScore = specificScore;
        }
        else
        {
            // Weighted average across channels
            overallScore = (baselineChannelScores[PredictiveCadenceChannel.WhatsApp] * 0.30)
                         + (baselineChannelScores[PredictiveCadenceChannel.Phone] * 0.25)
                         + (baselineChannelScores[PredictiveCadenceChannel.Email] * 0.20)
                         + (baselineChannelScores[PredictiveCadenceChannel.Sms] * 0.15)
                         + (baselineChannelScores[PredictiveCadenceChannel.LinkedIn] * 0.10);
        }

        overallScore = Math.Clamp(Math.Round(overallScore, 1), 2.0, 98.5);

        bool isQuietHours = localHour < 8 || localHour >= 20;
        if (isQuietHours)
        {
            overallScore = Math.Min(overallScore, 14.5);
        }

        return new HeatmapSlotDto
        {
            DayOfWeek = localDay,
            HourOfDay = localHour,
            OverallProbabilityScore = overallScore,
            Band = GetBand(overallScore),
            HistoricalSamples = sampleCount,
            ChannelScores = baselineChannelScores.ToDictionary(k => k.Key, v => Math.Round(v.Value, 1)),
            IsQuietHours = isQuietHours
        };
    }

    private static double CalculateChannelPrior(PredictiveCadenceChannel channel, DayOfWeek day, int hour)
    {
        bool isWeekend = day is DayOfWeek.Saturday or DayOfWeek.Sunday;

        // Off-hours night curve (00:00 - 06:59 and 21:00 - 23:59)
        if (hour < 7 || hour >= 21)
            return 3.0 + (hour == 7 || hour == 20 ? 8.0 : 0.0);

        return channel switch
        {
            PredictiveCadenceChannel.Phone => CalculatePhonePrior(day, hour, isWeekend),
            PredictiveCadenceChannel.WhatsApp => CalculateWhatsAppPrior(day, hour, isWeekend),
            PredictiveCadenceChannel.Email => CalculateEmailPrior(day, hour, isWeekend),
            PredictiveCadenceChannel.Sms => CalculateSmsPrior(day, hour, isWeekend),
            PredictiveCadenceChannel.LinkedIn => CalculateLinkedInPrior(day, hour, isWeekend),
            _ => 30.0
        };
    }

    private static double CalculatePhonePrior(DayOfWeek day, int hour, bool isWeekend)
    {
        if (isWeekend)
        {
            // Weekend calls generally drop significantly
            return day == DayOfWeek.Saturday && hour is >= 11 and <= 15 ? 32.0 : 8.0;
        }

        // B2B Golden Phone Connect Windows:
        // Morning: 10:00 - 11:30 AM
        // Afternoon: 14:00 - 16:30 PM
        // Tuesday & Thursday are prime days
        double dayMultiplier = day switch
        {
            DayOfWeek.Tuesday => 1.22,
            DayOfWeek.Thursday => 1.20,
            DayOfWeek.Wednesday => 1.15,
            DayOfWeek.Monday => 0.88, // Monday morning is usually meetings
            DayOfWeek.Friday => 0.78, // Friday afternoon drops
            _ => 1.0
        };

        double hourBase = hour switch
        {
            8 => 25.0,
            9 => 52.0,
            10 => 84.0, // Peak
            11 => 78.0,
            12 => 40.0, // Lunch dip
            13 => 45.0,
            14 => 76.0, // Afternoon peak
            15 => 82.0, // Afternoon peak
            16 => 70.0,
            17 => 48.0,
            18 => 30.0,
            19 => 18.0,
            _ => 10.0
        };

        return Math.Clamp(hourBase * dayMultiplier, 5.0, 95.0);
    }

    private static double CalculateWhatsAppPrior(DayOfWeek day, int hour, bool isWeekend)
    {
        // WhatsApp has continuous high read/reply rates across business and leisure hours
        double hourBase = hour switch
        {
            8 => 55.0,
            9 => 72.0,
            10 => 85.0,
            11 => 88.0,
            12 => 84.0,
            13 => 82.0,
            14 => 80.0,
            15 => 82.0,
            16 => 85.0,
            17 => 88.0,
            18 => 86.0,
            19 => 78.0,
            20 => 68.0,
            _ => 15.0
        };

        if (isWeekend)
        {
            // High weekend engagement for WhatsApp (especially retail/COD/inbound)
            hourBase *= 0.92;
        }

        return Math.Clamp(hourBase, 10.0, 96.0);
    }

    private static double CalculateEmailPrior(DayOfWeek day, int hour, bool isWeekend)
    {
        if (isWeekend)
            return hour is >= 10 and <= 16 ? 24.0 : 8.0;

        double hourBase = hour switch
        {
            8 => 50.0,
            9 => 82.0, // Morning inbox triage
            10 => 86.0,
            11 => 74.0,
            12 => 48.0,
            13 => 70.0,
            14 => 78.0,
            15 => 72.0,
            16 => 58.0,
            17 => 42.0,
            18 => 28.0,
            _ => 10.0
        };

        return Math.Clamp(hourBase, 5.0, 92.0);
    }

    private static double CalculateSmsPrior(DayOfWeek day, int hour, bool isWeekend)
    {
        double hourBase = hour switch
        {
            9 => 50.0,
            10 => 68.0,
            11 => 76.0,
            12 => 78.0,
            13 => 75.0,
            14 => 78.0,
            15 => 80.0,
            16 => 82.0,
            17 => 80.0,
            18 => 74.0,
            19 => 62.0,
            _ => 10.0
        };

        return Math.Clamp(hourBase, 8.0, 90.0);
    }

    private static double CalculateLinkedInPrior(DayOfWeek day, int hour, bool isWeekend)
    {
        if (isWeekend)
            return 8.0;

        double hourBase = hour switch
        {
            7 => 45.0,
            8 => 74.0, // Commute / morning check
            9 => 80.0,
            10 => 65.0,
            11 => 55.0,
            12 => 62.0,
            13 => 52.0,
            14 => 58.0,
            15 => 65.0,
            16 => 72.0,
            17 => 78.0, // End of workday
            18 => 64.0,
            _ => 10.0
        };

        return Math.Clamp(hourBase, 5.0, 88.0);
    }

    private static ContactTimeBand GetBand(double score) => score switch
    {
        >= 80.0 => ContactTimeBand.Peak,
        >= 65.0 => ContactTimeBand.High,
        >= 45.0 => ContactTimeBand.Moderate,
        >= 20.0 => ContactTimeBand.Low,
        _ => ContactTimeBand.OffHours
    };

    private static string BuildRecommendationReasoning(DayOfWeek day, int hour, PredictiveCadenceChannel channel, double score, int leadActivityMatches)
    {
        string channelName = channel.ToString();
        string timeFormatted = $"{hour % 12 switch { 0 => 12, var h => h }}:00 {(hour >= 12 ? "PM" : "AM")}";

        if (leadActivityMatches > 0)
        {
            return $"Prospect Activity Match (+{leadActivityMatches * 4.5:F0}% boost): Lead previously interacted at {timeFormatted} on {day}s. Prime {channelName} connect window.";
        }

        if (score >= 80.0)
        {
            return $"Peak Golden Window: High historical connect velocity for {channelName} on {day} at {timeFormatted}. Target decision-maker availability.";
        }

        if (score >= 65.0)
        {
            return $"High Confidence Window: Elevated response probability for {channelName} based on aggregate industry telemetry.";
        }

        return $"Favorable contact window for {channelName} during standard local business hours.";
    }

    public static TimeSpan ParseTimeZoneOffset(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz))
            return TimeSpan.Zero;

        tz = tz.Trim();

        // 1. Check for standard UTC/GMT offset strings e.g. "UTC+1", "UTC-5", "+03:00", "-04:00", "+5"
        if (tz.StartsWith("UTC", StringComparison.OrdinalIgnoreCase) || tz.StartsWith("GMT", StringComparison.OrdinalIgnoreCase))
        {
            var offsetPart = tz.Substring(3).Trim();
            if (string.IsNullOrEmpty(offsetPart) || offsetPart == "0" || offsetPart == "+0")
                return TimeSpan.Zero;

            if (TryParseOffsetString(offsetPart, out var offset))
                return offset;
        }

        if (tz.StartsWith("+") || tz.StartsWith("-"))
        {
            if (TryParseOffsetString(tz, out var offset))
                return offset;
        }

        // 2. Try IANA / Windows TimeZone lookup
        try
        {
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(tz);
            return tzInfo.BaseUtcOffset;
        }
        catch
        {
            // Mapping for common city/region aliases
            return tz.ToLowerInvariant() switch
            {
                "est" or "america/new_york" => TimeSpan.FromHours(-5),
                "cst" or "america/chicago" => TimeSpan.FromHours(-6),
                "mst" or "america/denver" => TimeSpan.FromHours(-7),
                "pst" or "america/los_angeles" => TimeSpan.FromHours(-8),
                "gmt" or "utc" or "europe/london" => TimeSpan.Zero,
                "cet" or "europe/paris" or "europe/berlin" => TimeSpan.FromHours(1),
                "eet" or "europe/athens" or "europe/bucharest" => TimeSpan.FromHours(2),
                "asia/dubai" => TimeSpan.FromHours(4),
                "asia/kolkata" or "ist" => TimeSpan.FromHours(5.5),
                "asia/singapore" => TimeSpan.FromHours(8),
                "asia/tokyo" => TimeSpan.FromHours(9),
                _ => TimeSpan.Zero
            };
        }
    }

    private static bool TryParseOffsetString(string offsetPart, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        try
        {
            bool negative = offsetPart.StartsWith("-");
            var clean = offsetPart.TrimStart('+', '-');

            if (clean.Contains(':'))
            {
                var parts = clean.Split(':');
                if (int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                {
                    result = TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m);
                    if (negative) result = -result;
                    return true;
                }
            }
            else if (double.TryParse(clean, out double hours))
            {
                result = TimeSpan.FromHours(hours);
                if (negative) result = -result;
                return true;
            }
        }
        catch
        {
            // fallback
        }
        return false;
    }

    private static List<TelemetryInteractionEvent> GetTelemetryBag(Guid tenantId)
    {
        var bag = s_telemetry.GetOrAdd(tenantId, _ => new());
        return bag.ToList();
    }

    #endregion

    #region Mock Data Seeding

    private static void EnsureSeeded()
    {
        if (s_seeded) return;
        lock (s_initLock)
        {
            if (s_seeded) return;
            SeedMockData(DefaultDemoTenantId);
            s_seeded = true;
        }
    }

    private static void SeedMockData(Guid tenantId)
    {
        var cadences = s_cadences.GetOrAdd(tenantId, _ => new());
        var enrollments = s_enrollments.GetOrAdd(tenantId, _ => new());
        var telemetry = s_telemetry.GetOrAdd(tenantId, _ => new());

        // 1. Cadence: Enterprise SaaS Inbound Fast-Response
        var c1Id = Guid.Parse("a1111111-0000-0000-0000-000000000001");
        var c1 = new PredictiveCadenceDefinition
        {
            Id = c1Id,
            TenantId = tenantId,
            Name = "Enterprise SaaS Inbound Fast-Response",
            Description = "5-touch omni-channel sequence triggered within 5 minutes of demo request.",
            Category = CadenceCategory.InboundFastResponse,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-14),
            Steps = new List<CadenceStepDefinition>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "Instant WhatsApp Welcome & Calendar Link",
                    Channel = PredictiveCadenceChannel.WhatsApp,
                    DelayFromPrevious = TimeSpan.FromMinutes(0),
                    UseOptimalTiming = false,
                    ConditionRule = StepConditionRule.Always,
                    Subject = "Welcome to SGrowing Enterprise",
                    TemplateBody = "Hi {{LeadName}}, saw your demo request for {{CompanyName}}. Here is our executive calendar link: https://sgrowing.io/book"
                },
                new()
                {
                    StepNumber = 2,
                    Name = "Optimal Contact Window Discovery Softphone Call",
                    Channel = PredictiveCadenceChannel.Phone,
                    DelayFromPrevious = TimeSpan.FromHours(2),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "Discovery Call",
                    TemplateBody = "Account Executive rapid qualification call."
                },
                new()
                {
                    StepNumber = 3,
                    Name = "ROI Proof & Customer Case Study Email",
                    Channel = PredictiveCadenceChannel.Email,
                    DelayFromPrevious = TimeSpan.FromDays(1),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "How similar enterprises achieved +34% margin growth",
                    TemplateBody = "Hello {{LeadName}},\n\nFollowing up with a brief case study on how teams like {{CompanyName}} deployed predictive orchestration."
                },
                new()
                {
                    StepNumber = 4,
                    Name = "Urgent SMS Follow-up & Executive Summary",
                    Channel = PredictiveCadenceChannel.Sms,
                    DelayFromPrevious = TimeSpan.FromDays(2),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "SMS Ping",
                    TemplateBody = "Hi {{LeadName}}, sent over the enterprise architecture PDF. Did you have 5 mins to chat today?"
                },
                new()
                {
                    StepNumber = 5,
                    Name = "Executive LinkedIn InMail Connection",
                    Channel = PredictiveCadenceChannel.LinkedIn,
                    DelayFromPrevious = TimeSpan.FromDays(3),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "Partnership with {{CompanyName}}",
                    TemplateBody = "Hi {{LeadName}}, would love to connect and share notes on revenue orchestration trends in your sector."
                }
            }
        };
        cadences[c1.Id] = c1;

        // 2. Cadence: High-Ticket COD Confirmation Sequence
        var c2Id = Guid.Parse("a2222222-0000-0000-0000-000000000002");
        var c2 = new PredictiveCadenceDefinition
        {
            Id = c2Id,
            TenantId = tenantId,
            Name = "High-Ticket COD Confirmation Sequence",
            Description = "Anti-fraud and logistics dispatch verification to maximize delivery success rate.",
            Category = CadenceCategory.CodConfirmation,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-20),
            Steps = new List<CadenceStepDefinition>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "WhatsApp Interactive Order Verification",
                    Channel = PredictiveCadenceChannel.WhatsApp,
                    DelayFromPrevious = TimeSpan.FromMinutes(0),
                    UseOptimalTiming = false,
                    ConditionRule = StepConditionRule.Always,
                    Subject = "Order Confirmation",
                    TemplateBody = "Hello {{LeadName}}, please confirm your shipping address for order #{{OrderId}} by tapping 'Confirm' below."
                },
                new()
                {
                    StepNumber = 2,
                    Name = "Predictive Voice Call Confirmation",
                    Channel = PredictiveCadenceChannel.Phone,
                    DelayFromPrevious = TimeSpan.FromHours(1),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "Dispatch Verification Call",
                    TemplateBody = "Softphone agent automated confirmation call."
                },
                new()
                {
                    StepNumber = 3,
                    Name = "SMS Tracking & Courier Lock Alert",
                    Channel = PredictiveCadenceChannel.Sms,
                    DelayFromPrevious = TimeSpan.FromHours(3),
                    UseOptimalTiming = false,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "Order Status",
                    TemplateBody = "{{LeadName}}, your parcel is prepped for courier handover. Reply YES to confirm delivery."
                }
            }
        };
        cadences[c2.Id] = c2;

        // 3. Cadence: Re-engagement Dormant Leads
        var c3Id = Guid.Parse("a3333333-0000-0000-0000-000000000003");
        var c3 = new PredictiveCadenceDefinition
        {
            Id = c3Id,
            TenantId = tenantId,
            Name = "Re-engagement Dormant Leads",
            Description = "Multi-touch sequence revitalizing stalled pipeline and 60-day dormant accounts.",
            Category = CadenceCategory.Reengagement,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            Steps = new List<CadenceStepDefinition>
            {
                new()
                {
                    StepNumber = 1,
                    Name = "New AI Feature Announcement & Direct Teaser",
                    Channel = PredictiveCadenceChannel.Email,
                    DelayFromPrevious = TimeSpan.FromDays(0),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.Always,
                    Subject = "Major updates to SGrowing sales acceleration",
                    TemplateBody = "Hi {{LeadName}}, we just introduced predictive dialer & 7x24 contact heatmap intelligence."
                },
                new()
                {
                    StepNumber = 2,
                    Name = "LinkedIn Industry Touchpoint",
                    Channel = PredictiveCadenceChannel.LinkedIn,
                    DelayFromPrevious = TimeSpan.FromDays(2),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "Quick note on your tech stack",
                    TemplateBody = "Noticed recent expansions at {{CompanyName}}."
                },
                new()
                {
                    StepNumber = 3,
                    Name = "VIP Relationship Check-in Call",
                    Channel = PredictiveCadenceChannel.Phone,
                    DelayFromPrevious = TimeSpan.FromDays(4),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "VIP Check-in",
                    TemplateBody = "Account director phone check-in."
                },
                new()
                {
                    StepNumber = 4,
                    Name = "Final Farewell Hail-Mary Email",
                    Channel = PredictiveCadenceChannel.Email,
                    DelayFromPrevious = TimeSpan.FromDays(7),
                    UseOptimalTiming = true,
                    ConditionRule = StepConditionRule.IfNoReply,
                    Subject = "Permission to close your file?",
                    TemplateBody = "Hi {{LeadName}}, haven't heard back, so assuming your priorities shifted."
                }
            }
        };
        cadences[c3.Id] = c3;

        // Seed 10 Enrolled Leads
        var leadSeeds = new[]
        {
            ("Sarah Jenkins", "sarah.j@vertexcloud.com", "+1 415-555-0192", "Vertex Cloud Corp", "UTC-5", c1Id, 2, PredictiveCadenceChannel.Phone, EnrollmentStatus.Active),
            ("Marcus Vance", "m.vance@solarisdynamics.io", "+44 20 7946 0912", "Solaris Dynamics", "UTC+1", c1Id, 2, PredictiveCadenceChannel.Phone, EnrollmentStatus.Active),
            ("Elena Rostova", "elena@nordicscale.se", "+46 8 123 4567", "Nordic Scale Tech", "UTC+1", c1Id, 3, PredictiveCadenceChannel.Email, EnrollmentStatus.Active),
            ("Tariq Al-Mansoor", "tariq@gulflogistics.ae", "+971 4 321 0000", "Gulf Logistics Hub", "UTC+4", c2Id, 2, PredictiveCadenceChannel.Phone, EnrollmentStatus.Active),
            ("David Kalu", "david@kineticsystems.ng", "+234 1 234 5678", "Kinetic Systems Ltd", "UTC+1", c2Id, 1, PredictiveCadenceChannel.WhatsApp, EnrollmentStatus.Active),
            ("Hiroshi Tanaka", "tanaka@tokyodata.jp", "+81 3 5555 0143", "Tokyo Data Labs", "UTC+9", c1Id, 2, PredictiveCadenceChannel.Phone, EnrollmentStatus.Active),
            ("Chloe Laurent", "chloe@hexaprime.fr", "+33 1 42 68 55 00", "Hexa Prime Group", "UTC+1", c1Id, 1, PredictiveCadenceChannel.WhatsApp, EnrollmentStatus.Replied),
            ("Bradley Cooper", "bcooper@apollocapital.com", "+1 212-555-0841", "Apollo Capital Partners", "UTC-5", c3Id, 2, PredictiveCadenceChannel.LinkedIn, EnrollmentStatus.Paused),
            ("Amira Hassan", "amira@nilesoft.eg", "+20 2 3344 5566", "NileSoft Enterprises", "UTC+2", c2Id, 3, PredictiveCadenceChannel.Sms, EnrollmentStatus.Graduated),
            ("Liam O'Connor", "liam@celticcloud.ie", "+353 1 496 0000", "Celtic Cloud Systems", "UTC", c3Id, 4, PredictiveCadenceChannel.Email, EnrollmentStatus.Completed)
        };

        foreach (var (name, email, phone, company, tz, cadId, stepNum, channel, status) in leadSeeds)
        {
            var leadId = Guid.NewGuid();
            var enr = new LeadCadenceEnrollment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CadenceId = cadId,
                CadenceName = cadences[cadId].Name,
                LeadId = leadId,
                LeadName = name,
                LeadEmail = email,
                LeadPhone = phone,
                LeadCompany = company,
                LeadTimeZone = tz,
                Status = status,
                CurrentStepNumber = stepNum,
                EnrolledAtUtc = DateTime.UtcNow.AddDays(-new Random().Next(1, 8)),
                NextStepChannel = status == EnrollmentStatus.Active ? channel : null,
                NextStepScheduledAtUtc = status == EnrollmentStatus.Active ? DateTime.UtcNow.AddHours(new Random().Next(1, 12)) : null,
                PauseReason = status == EnrollmentStatus.Replied ? "Auto-paused: Prospect replied via WhatsApp." : status == EnrollmentStatus.Paused ? "Manually paused by SDR." : null
            };

            // Seed sample execution log
            enr.ExecutionLogs.Add(new CadenceStepExecutionLog
            {
                StepNumber = 1,
                Channel = PredictiveCadenceChannel.WhatsApp,
                ExecutedAtUtc = enr.EnrolledAtUtc.AddMinutes(2),
                Status = "Success",
                Details = "Initial greeting dispatched.",
                PredictedScoreAtExecution = 82.5
            });

            enrollments[enr.Id] = enr;

            // Seed historical telemetry events for this lead
            SeedLeadTelemetryEvents(telemetry, tenantId, leadId, tz);
        }
    }

    private static void SeedLeadTelemetryEvents(ConcurrentBag<TelemetryInteractionEvent> telemetry, Guid tenantId, Guid leadId, string tz)
    {
        var tzOffset = ParseTimeZoneOffset(tz);
        var baseDate = DateTime.UtcNow.AddDays(-10);

        // Seed positive telemetry events at optimal business hours (Tuesday & Thursday 10:00 & 14:00 local time)
        var events = new (DayOfWeek Day, int LocalHour, PredictiveCadenceChannel Channel, TelemetryEventType Type)[]
        {
            (DayOfWeek.Tuesday, 10, PredictiveCadenceChannel.WhatsApp, TelemetryEventType.WhatsAppRead),
            (DayOfWeek.Tuesday, 10, PredictiveCadenceChannel.WhatsApp, TelemetryEventType.WhatsAppReplied),
            (DayOfWeek.Tuesday, 14, PredictiveCadenceChannel.Phone, TelemetryEventType.CallAnswered),
            (DayOfWeek.Thursday, 10, PredictiveCadenceChannel.Email, TelemetryEventType.EmailOpen),
            (DayOfWeek.Thursday, 11, PredictiveCadenceChannel.Email, TelemetryEventType.EmailClick),
            (DayOfWeek.Wednesday, 15, PredictiveCadenceChannel.Sms, TelemetryEventType.SmsReplied)
        };

        foreach (var (day, localHour, channel, type) in events)
        {
            var eventLocal = baseDate.AddHours((int)day * 24 + localHour);
            var eventUtc = eventLocal.Subtract(tzOffset);

            telemetry.Add(new TelemetryInteractionEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LeadId = leadId,
                Channel = channel,
                EventType = type,
                OccurredAtUtc = eventUtc,
                MetadataJson = "{\"seededDemoTelemetry\": true}"
            });
        }
    }

    #endregion
}

#endregion
