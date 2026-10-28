using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Projects;

public class ProjectTaskService : IProjectTaskService
{
    private readonly SGrowingDbContext _db;
    private readonly IInvoiceService _invoiceService;

    public ProjectTaskService(SGrowingDbContext db, IInvoiceService? invoiceService = null)
    {
        _db = db;
        _invoiceService = invoiceService ?? new InvoiceService(db);
    }

    public async Task<ProjectRecord> ConvertFromDealAsync(Guid tenantId, Guid dealId, CancellationToken ct = default)
    {
        var existingProject = await _db.Projects.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.DealId == dealId, ct);
        if (existingProject != null) return existingProject;

        var deal = await _db.Deals.FirstOrDefaultAsync(d => d.Id == dealId && d.TenantId == tenantId, ct);
        var dealTitle = deal?.Title ?? $"Enterprise Deal {dealId.ToString()[..8]}";

        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DealId = dealId,
            Name = $"Client Onboarding: {dealTitle}",
            Description = $"Automated post-close onboarding sprint generated from Won Deal '{dealTitle}'.",
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow,
            TargetLaunchDateUtc = DateTime.UtcNow.AddDays(30)
        };

        _db.Projects.Add(project);

        var templateTasks = new List<TaskItemRecord>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Kickoff & Stakeholder Requirements",
                Description = "Align key stakeholders, define success criteria, and sign off on project timeline.",
                Status = "Todo",
                Priority = "Urgent",
                AssigneeName = "Sarah Jenkins",
                AssigneeAvatar = "SJ",
                Position = 1,
                SubtasksCompleted = 1,
                SubtasksTotal = 3,
                DueDateUtc = DateTime.UtcNow.AddDays(5),
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Technical Infrastructure & API Provisioning",
                Description = "Configure multi-tenant sub-account, API credentials, and webhooks for CRM ingestion.",
                Status = "InProgress",
                Priority = "High",
                AssigneeName = "Alex Rivera",
                AssigneeAvatar = "AR",
                Position = 2,
                SubtasksCompleted = 2,
                SubtasksTotal = 4,
                DueDateUtc = DateTime.UtcNow.AddDays(10),
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Catalog & Customer Data Migration",
                Description = "Ingest historical catalog, warehouse stock balances, and contact records into system.",
                Status = "Backlog",
                Priority = "Medium",
                AssigneeName = "Liam Chen",
                AssigneeAvatar = "LC",
                Position = 3,
                SubtasksCompleted = 0,
                SubtasksTotal = 5,
                DueDateUtc = DateTime.UtcNow.AddDays(18),
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Operator Workflow Training & Testing",
                Description = "Deliver team training for SDRs, warehouse managers, and customer support representatives.",
                Status = "Backlog",
                Priority = "High",
                AssigneeName = "Emma Watson",
                AssigneeAvatar = "EW",
                Position = 4,
                SubtasksCompleted = 0,
                SubtasksTotal = 4,
                DueDateUtc = DateTime.UtcNow.AddDays(25),
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Production Go-Live & Post-Launch Sign-off",
                Description = "Final cutover, live order fulfillment verification, and stakeholder celebration.",
                Status = "Backlog",
                Priority = "Urgent",
                AssigneeName = "Sarah Jenkins",
                AssigneeAvatar = "SJ",
                Position = 5,
                SubtasksCompleted = 0,
                SubtasksTotal = 2,
                DueDateUtc = DateTime.UtcNow.AddDays(30),
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        _db.TaskItems.AddRange(templateTasks);
        await _db.SaveChangesAsync(ct);

        return project;
    }

    public async Task<ProjectRecord> ConvertFromContractAsync(Guid tenantId, Guid contractId, CancellationToken ct = default)
    {
        var existingProject = await _db.Projects.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ContractId == contractId, ct);
        if (existingProject != null) return existingProject;

        var contract = await _db.DigitalContracts.FirstOrDefaultAsync(c => c.Id == contractId && c.TenantId == tenantId, ct);
        var contractTitle = contract?.Title ?? $"Master Services Agreement {contractId.ToString()[..8]}";

        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractId = contractId,
            Name = $"Deployment Sprint: {contractTitle}",
            Description = $"Post-signature implementation sprint generated from Sealed Contract '{contractTitle}'.",
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow,
            TargetLaunchDateUtc = DateTime.UtcNow.AddDays(45)
        };

        _db.Projects.Add(project);

        var templateTasks = new List<TaskItemRecord>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Contract Term Review & Scope Validation",
                Description = "Review contractual SLA, compliance guarantees, and deliverables.",
                Status = "Todo",
                Priority = "Urgent",
                AssigneeName = "Marcus Vance",
                AssigneeAvatar = "MV",
                Position = 1,
                SubtasksCompleted = 2,
                SubtasksTotal = 2,
                DueDateUtc = DateTime.UtcNow.AddDays(4),
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Enterprise Security Audit & Network Whitelisting",
                Description = "Verify TLS ciphers, IP allowlists, and cryptographic signature verification keys.",
                Status = "InProgress",
                Priority = "High",
                AssigneeName = "Elena Rostova",
                AssigneeAvatar = "ER",
                Position = 2,
                SubtasksCompleted = 1,
                SubtasksTotal = 3,
                DueDateUtc = DateTime.UtcNow.AddDays(12),
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                TenantId = tenantId,
                Title = "Warehouse & Logistics Integration Testing",
                Description = "Validate multi-carrier webhook dispatches and barcode fulfillment workflows.",
                Status = "Backlog",
                Priority = "Medium",
                AssigneeName = "Tariq Mansour",
                AssigneeAvatar = "TM",
                Position = 3,
                SubtasksCompleted = 0,
                SubtasksTotal = 4,
                DueDateUtc = DateTime.UtcNow.AddDays(22),
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        _db.TaskItems.AddRange(templateTasks);
        await _db.SaveChangesAsync(ct);

        return project;
    }

    public async Task<IEnumerable<ProjectRecord>> GetProjectsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Projects
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ProjectRecord?> GetProjectByIdAsync(Guid tenantId, Guid projectId, CancellationToken ct = default)
    {
        return await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId, ct);
    }

    public async Task<IEnumerable<TaskItemRecord>> GetProjectTasksAsync(Guid tenantId, Guid projectId, CancellationToken ct = default)
    {
        return await _db.TaskItems
            .Where(t => t.ProjectId == projectId && t.TenantId == tenantId)
            .OrderBy(t => t.Position)
            .ToListAsync(ct);
    }

    public async Task<TaskItemRecord> CreateTaskAsync(Guid tenantId, Guid projectId, CreateTaskRequest req, CancellationToken ct = default)
    {
        var existingCount = await _db.TaskItems.CountAsync(t => t.ProjectId == projectId && t.TenantId == tenantId, ct);

        var task = new TaskItemRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TenantId = tenantId,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim() ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(req.Status) ? "Todo" : req.Status,
            Priority = string.IsNullOrWhiteSpace(req.Priority) ? "Medium" : req.Priority,
            AssigneeName = req.AssigneeName,
            AssigneeAvatar = GenerateAvatar(req.AssigneeName),
            Position = existingCount + 1,
            SubtasksCompleted = Math.Max(0, req.SubtasksCompleted),
            SubtasksTotal = Math.Max(0, req.SubtasksTotal),
            DueDateUtc = req.DueDateUtc ?? DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync(ct);

        return task;
    }

    public async Task<TaskItemRecord?> UpdateTaskAsync(Guid tenantId, Guid taskId, UpdateTaskRequest req, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId && t.TenantId == tenantId, ct);
        if (task == null) return null;

        if (req.Title != null) task.Title = req.Title.Trim();
        if (req.Description != null) task.Description = req.Description.Trim();
        if (req.Status != null) task.Status = req.Status;
        if (req.Priority != null) task.Priority = req.Priority;
        if (req.AssigneeName != null)
        {
            task.AssigneeName = req.AssigneeName;
            task.AssigneeAvatar = GenerateAvatar(req.AssigneeName);
        }
        if (req.Position.HasValue) task.Position = req.Position.Value;
        if (req.SubtasksCompleted.HasValue) task.SubtasksCompleted = Math.Max(0, req.SubtasksCompleted.Value);
        if (req.SubtasksTotal.HasValue) task.SubtasksTotal = Math.Max(0, req.SubtasksTotal.Value);
        if (req.DueDateUtc.HasValue) task.DueDateUtc = req.DueDateUtc.Value;

        await _db.SaveChangesAsync(ct);
        return task;
    }

    public async Task<TaskCommentRecord> AddCommentAsync(Guid tenantId, Guid taskId, AddCommentRequest req, CancellationToken ct = default)
    {
        var comment = new TaskCommentRecord
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            TenantId = tenantId,
            AuthorName = req.AuthorName.Trim(),
            Content = req.Content.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync(ct);

        return comment;
    }

    public async Task<IEnumerable<TaskCommentRecord>> GetCommentsAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        return await _db.TaskComments
            .Where(c => c.TaskId == taskId && c.TenantId == tenantId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ClientPortalTokenResult> GenerateClientPortalTokenAsync(
        Guid tenantId,
        Guid projectId,
        GeneratePortalTokenRequest req,
        string baseUrl,
        CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == tenantId, ct);
        if (project == null)
        {
            throw new KeyNotFoundException($"Project {projectId} not found for tenant {tenantId}");
        }

        var randomBytes = new byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        var token = "spt_" + Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var days = Math.Clamp(req.ValidDays <= 0 ? 30 : req.ValidDays, 1, 365);
        var clientOrg = string.IsNullOrWhiteSpace(req.ClientOrganization)
            ? (!string.IsNullOrWhiteSpace(project.Name) ? $"{project.Name} Stakeholders" : "Client Organization")
            : req.ClientOrganization.Trim();

        var tokenRecord = new ClientPortalTokenRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            Token = token,
            ClientContactEmail = req.ClientEmail?.Trim() ?? string.Empty,
            ClientOrganizationName = clientOrg,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(days),
            CreatedAtUtc = DateTime.UtcNow,
            IsRevoked = false
        };

        _db.ClientPortalTokens.Add(tokenRecord);
        await _db.SaveChangesAsync(ct);

        var cleanBase = string.IsNullOrWhiteSpace(baseUrl) ? "/portal/onboarding" : $"{baseUrl.TrimEnd('/')}/portal/onboarding";
        var shareableUrl = $"{cleanBase}/{token}";

        return new ClientPortalTokenResult(
            Token: token,
            ShareableUrl: shareableUrl,
            ExpiresAtUtc: tokenRecord.ExpiresAtUtc,
            ClientOrganizationName: clientOrg
        );
    }

    public async Task<ClientPortalView?> GetClientPortalViewAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var tokenRecord = await _db.ClientPortalTokens
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsRevoked, ct);

        if (tokenRecord == null || tokenRecord.ExpiresAtUtc < DateTime.UtcNow)
        {
            return null;
        }

        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == tokenRecord.ProjectId && p.TenantId == tokenRecord.TenantId, ct);

        if (project == null) return null;

        var tasks = await _db.TaskItems
            .Where(t => t.ProjectId == project.Id && t.TenantId == project.TenantId)
            .OrderBy(t => t.Position)
            .ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();
        var feedbacks = await _db.ClientPortalFeedbacks
            .Where(f => f.ProjectId == project.Id && f.TenantId == project.TenantId)
            .OrderByDescending(f => f.SubmittedAtUtc)
            .ToListAsync(ct);

        var comments = await _db.TaskComments
            .Where(c => c.TenantId == project.TenantId && taskIds.Contains(c.TaskId))
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(15)
            .ToListAsync(ct);

        int totalTasks = tasks.Count;
        int completedTasks = tasks.Count(t => string.Equals(t.Status, "Done", StringComparison.OrdinalIgnoreCase));
        int overallProgress = totalTasks > 0 ? (int)Math.Round((double)completedTasks / totalTasks * 100.0) : 0;

        var milestoneViews = tasks.Select(t =>
        {
            var latestVote = feedbacks.FirstOrDefault(f => f.TaskId == t.Id)?.StatusVote;
            int progress = t.SubtasksTotal > 0
                ? (int)Math.Round((double)t.SubtasksCompleted / t.SubtasksTotal * 100.0)
                : (string.Equals(t.Status, "Done", StringComparison.OrdinalIgnoreCase) ? 100 : 0);

            return new ClientPortalTaskView(
                Id: t.Id,
                Title: t.Title,
                Description: t.Description,
                Status: t.Status,
                Priority: t.Priority,
                SubtasksCompleted: t.SubtasksCompleted,
                SubtasksTotal: t.SubtasksTotal,
                ProgressPercent: progress,
                DueDateUtc: t.DueDateUtc,
                LatestClientVote: latestVote
            );
        }).ToList();

        return new ClientPortalView(
            ProjectId: project.Id,
            ProjectName: project.Name,
            ProjectDescription: project.Description,
            ClientOrganizationName: tokenRecord.ClientOrganizationName,
            OverallProgressPercent: overallProgress,
            CompletedMilestones: completedTasks,
            TotalMilestones: totalTasks,
            TargetLaunchDateUtc: project.TargetLaunchDateUtc,
            TokenExpiresAtUtc: tokenRecord.ExpiresAtUtc,
            Milestones: milestoneViews,
            RecentUpdates: comments
        );
    }

    public async Task<ClientPortalFeedbackRecord> SubmitClientPortalFeedbackAsync(
        string token,
        ClientPortalFeedbackRequest req,
        CancellationToken ct = default)
    {
        var tokenRecord = await _db.ClientPortalTokens
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsRevoked, ct);

        if (tokenRecord == null || tokenRecord.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new KeyNotFoundException("Client portal token is invalid or has expired.");
        }

        var task = await _db.TaskItems
            .FirstOrDefaultAsync(t => t.Id == req.TaskId && t.ProjectId == tokenRecord.ProjectId && t.TenantId == tokenRecord.TenantId, ct);

        if (task == null)
        {
            throw new KeyNotFoundException($"Milestone task {req.TaskId} does not exist in this project.");
        }

        var feedback = new ClientPortalFeedbackRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tokenRecord.TenantId,
            ProjectId = tokenRecord.ProjectId,
            TaskId = req.TaskId,
            ClientName = req.ClientName.Trim(),
            ClientEmail = req.ClientEmail.Trim(),
            StatusVote = string.IsNullOrWhiteSpace(req.StatusVote) ? "Approved" : req.StatusVote.Trim(),
            FeedbackNotes = req.FeedbackNotes.Trim(),
            SubmittedAtUtc = DateTime.UtcNow
        };

        _db.ClientPortalFeedbacks.Add(feedback);

        // Add corresponding audit comment to task thread
        var auditComment = new TaskCommentRecord
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            TenantId = tokenRecord.TenantId,
            AuthorName = $"[Client Signoff] {feedback.ClientName}",
            Content = $"Client Review: {feedback.StatusVote}. Note: {feedback.FeedbackNotes}",
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.TaskComments.Add(auditComment);

        if (string.Equals(feedback.StatusVote, "Approved", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(task.Status, "InReview", StringComparison.OrdinalIgnoreCase))
        {
            task.Status = "Done";
            task.SubtasksCompleted = task.SubtasksTotal;
        }

        await _db.SaveChangesAsync(ct);
        return feedback;
    }

    public async Task<bool> RevokeClientPortalTokenAsync(Guid tenantId, string token, CancellationToken ct = default)
    {
        var tokenRecord = await _db.ClientPortalTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.TenantId == tenantId, ct);

        if (tokenRecord == null) return false;

        tokenRecord.IsRevoked = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CompleteMilestoneResult?> CompleteMilestoneWithBillingAsync(Guid tenantId, Guid taskId, CompleteMilestoneRequest req, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId, ct);
        if (task == null) return null;

        task.Status = "Done";
        task.SubtasksCompleted = task.SubtasksTotal > 0 ? task.SubtasksTotal : 1;
        if (task.SubtasksTotal == 0) task.SubtasksTotal = 1;
        await _db.SaveChangesAsync(ct);

        InvoiceRecord? invoice = null;
        if (req.TriggerInvoice)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == task.ProjectId, ct);
            var clientName = project?.Name ?? "Client Enterprise Partner";
            var milestoneAmount = req.MilestoneAmount ?? 1500.00m;
            var currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.ToUpperInvariant();

            var createInvoiceReq = new CreateInvoiceRequest(
                ClientName: clientName,
                ClientEmail: "billing@" + (project != null ? project.Name.Replace(" ", "").ToLowerInvariant() : "enterprise") + ".com",
                Currency: currency,
                TaxPercentage: 19.0m,
                DealId: project?.DealId,
                DueDateUtc: DateTime.UtcNow.AddDays(15),
                Notes: $"Milestone sprint sign-off: '{task.Title}' on project '{project?.Name ?? "General"}'. Automated B2B Billing (Ever-Gauzy/Ever-Teams).",
                LineItems: new List<CreateInvoiceLineItemRequest>
                {
                    new(
                        Description: $"Milestone Deliverable Sign-Off: {task.Title}",
                        Quantity: 1,
                        UnitPrice: milestoneAmount
                    )
                }
            );

            invoice = await _invoiceService.CreateInvoiceAsync(tenantId, createInvoiceReq, ct);
        }

        return new CompleteMilestoneResult(
            Task: task,
            Invoice: invoice,
            Message: invoice != null
                ? $"Milestone '{task.Title}' signed off. Triggered Invoice {invoice.InvoiceNumber} for {invoice.TotalAmount} {invoice.Currency} (19% VAT inclusive)."
                : $"Milestone '{task.Title}' signed off without invoicing."
        );
    }

    private static string GenerateAvatar(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "OP";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }
        return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}
