namespace SGrowing.Core.Entities;

public enum CadenceChannel
{
    Email = 1,
    WhatsApp = 2,
    PhoneCallTask = 3,
    LinkedInTask = 4,
    Sms = 5
}

public enum CadenceEnrollmentState
{
    Active = 1,
    Paused = 2,
    Completed = 3,
    RepliedAndStopped = 4,
    Bounced = 5
}

public class SalesOutreachCadence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int StepsCount { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SalesCadenceStepItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CadenceId { get; set; }
    public int StepNumber { get; set; }
    public int DayOffset { get; set; } // days after enrollment
    public CadenceChannel Channel { get; set; }
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
}

public class SalesCadenceLeadEnrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CadenceId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Company { get; set; }
    public int CurrentStepNumber { get; set; } = 1;
    public CadenceEnrollmentState State { get; set; } = CadenceEnrollmentState.Active;
    public DateTime? NextExecutionAtUtc { get; set; }
    public DateTime? LastExecutedAtUtc { get; set; }
    public DateTime EnrolledAtUtc { get; set; } = DateTime.UtcNow;
}

public class SdrTaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid CadenceId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string TaskType { get; set; } = "PhoneCall"; // PhoneCall, LinkedInAction
    public string Instruction { get; set; } = string.Empty;
    public DateTime DueAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAtUtc { get; set; }
    public string? Outcome { get; set; }
}
