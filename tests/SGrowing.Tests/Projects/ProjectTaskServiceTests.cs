using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Api.Modules.Projects;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Projects;

public sealed class ProjectTaskServiceTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IInvoiceService> _invoiceMock;
    private readonly ProjectTaskService _service;

    public ProjectTaskServiceTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _invoiceMock = new Mock<IInvoiceService>();
        _service = new ProjectTaskService(_db, _invoiceMock.Object);
    }

    [Fact]
    public async Task ConvertFromDealAsync_CreatesProjectWithFiveTemplateTasks()
    {
        var tenantId = Guid.NewGuid();
        var deal = Deal.Create(
            tenantId,
            Guid.NewGuid(),
            "Acme Global Enterprise",
            150000m,
            DealStage.Won);

        _db.Deals.Add(deal);
        await _db.SaveChangesAsync();

        var project = await _service.ConvertFromDealAsync(tenantId, deal.Id);

        project.Should().NotBeNull();
        project.TenantId.Should().Be(tenantId);
        project.DealId.Should().Be(deal.Id);
        project.Name.Should().Contain("Acme Global Enterprise");
        project.Status.Should().Be("Active");

        var tasks = (await _service.GetProjectTasksAsync(tenantId, project.Id)).ToList();
        tasks.Should().HaveCount(5);
        tasks[0].Title.Should().Be("Kickoff & Stakeholder Requirements");
        tasks[1].Title.Should().Be("Technical Infrastructure & API Provisioning");
        tasks[2].Title.Should().Be("Catalog & Customer Data Migration");
        tasks[3].Title.Should().Be("Operator Workflow Training & Testing");
        tasks[4].Title.Should().Be("Production Go-Live & Post-Launch Sign-off");
    }

    [Fact]
    public async Task ConvertFromDealAsync_ExistingProject_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var dealId = Guid.NewGuid();

        var project1 = await _service.ConvertFromDealAsync(tenantId, dealId);
        var project2 = await _service.ConvertFromDealAsync(tenantId, dealId);

        project2.Id.Should().Be(project1.Id);
        var totalProjects = await _db.Projects.CountAsync(p => p.TenantId == tenantId && p.DealId == dealId);
        totalProjects.Should().Be(1);
    }

    [Fact]
    public async Task ConvertFromContractAsync_CreatesProjectWithThreeTemplateTasks()
    {
        var tenantId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        _db.DigitalContracts.Add(new DigitalContract
        {
            Id = contractId,
            TenantId = tenantId,
            Title = "Enterprise Master SLA v4",
            ContractValue = 250000m,
            Status = ContractStatus.Signed
        });
        await _db.SaveChangesAsync();

        var project = await _service.ConvertFromContractAsync(tenantId, contractId);

        project.Should().NotBeNull();
        project.ContractId.Should().Be(contractId);
        project.Name.Should().Contain("Enterprise Master SLA v4");

        var tasks = (await _service.GetProjectTasksAsync(tenantId, project.Id)).ToList();
        tasks.Should().HaveCount(3);
        tasks[0].Title.Should().Be("Contract Term Review & Scope Validation");
        tasks[1].Title.Should().Be("Enterprise Security Audit & Network Whitelisting");
        tasks[2].Title.Should().Be("Warehouse & Logistics Integration Testing");
    }

    [Fact]
    public async Task CreateTaskAsync_CalculatesPositionAndAssigneeAvatar()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var taskReq = new CreateTaskRequest(
            Title: "Configure Cloudflare CDN WAF",
            Description: "Set up rate limiting and bot management rules",
            Status: "Todo",
            Priority: "High",
            AssigneeName: "Elena Rostova",
            DueDateUtc: DateTime.UtcNow.AddDays(7),
            SubtasksTotal: 3,
            SubtasksCompleted: 0
        );

        var task = await _service.CreateTaskAsync(tenantId, projectId, taskReq);

        task.Should().NotBeNull();
        task.Title.Should().Be("Configure Cloudflare CDN WAF");
        task.AssigneeAvatar.Should().Be("ER");
        task.Position.Should().Be(1);

        // Create second task -> Position should be 2
        var task2 = await _service.CreateTaskAsync(tenantId, projectId, taskReq with { Title = "Configure DNSSEC", AssigneeName = "Solo" });
        task2.Position.Should().Be(2);
        task2.AssigneeAvatar.Should().Be("SO");
    }

    [Fact]
    public async Task UpdateTaskAsync_ExistingAndMissing_UpdatesOrReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var created = await _service.CreateTaskAsync(tenantId, projectId, new CreateTaskRequest("Initial Task", "Initial Desc", "Todo", "Medium", "Init User"));

        var updated = await _service.UpdateTaskAsync(tenantId, created.Id, new UpdateTaskRequest(
            Title: "Updated Task Title",
            Status: "InProgress",
            Priority: "Urgent",
            AssigneeName: "Marcus Vance",
            SubtasksCompleted: 2,
            SubtasksTotal: 4
        ));

        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Updated Task Title");
        updated.Status.Should().Be("InProgress");
        updated.Priority.Should().Be("Urgent");
        updated.AssigneeAvatar.Should().Be("MV");
        updated.SubtasksCompleted.Should().Be(2);
        updated.SubtasksTotal.Should().Be(4);

        // Non-existent task returns null
        var nonExistent = await _service.UpdateTaskAsync(tenantId, Guid.NewGuid(), new UpdateTaskRequest("Ghost"));
        nonExistent.Should().BeNull();
    }

    [Fact]
    public async Task AddCommentAndGetCommentsAsync_PersistsAndOrdersByDate()
    {
        var tenantId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var c1 = await _service.AddCommentAsync(tenantId, taskId, new AddCommentRequest("Alice", "First comment"));
        var c2 = await _service.AddCommentAsync(tenantId, taskId, new AddCommentRequest("Bob", "Second comment"));

        var comments = (await _service.GetCommentsAsync(tenantId, taskId)).ToList();

        comments.Should().HaveCount(2);
        comments[0].Content.Should().Be("First comment");
        comments[1].Content.Should().Be("Second comment");
    }

    [Fact]
    public async Task ClientPortalLifecycle_TokenCreation_ViewQuery_AndFeedbackSignoff()
    {
        var tenantId = Guid.NewGuid();
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Globex B2B Omnichannel Onboarding",
            Description = "Custom wholesale deployment",
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow,
            TargetLaunchDateUtc = DateTime.UtcNow.AddDays(30)
        };
        _db.Projects.Add(project);

        var task = new TaskItemRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TenantId = tenantId,
            Title = "API Webhook Ingestion Verification",
            Description = "Verify real-time carrier status webhooks",
            Status = "InReview",
            Priority = "High",
            Position = 1,
            SubtasksTotal = 3,
            SubtasksCompleted = 2,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();

        // 1. Generate client portal token
        var tokenResult = await _service.GenerateClientPortalTokenAsync(
            tenantId,
            project.Id,
            new GeneratePortalTokenRequest("exec@globex.corp", "Globex Corporation", ValidDays: 14),
            baseUrl: "https://portal.sgrowing.io");

        tokenResult.Should().NotBeNull();
        tokenResult.Token.Should().StartWith("spt_");
        tokenResult.ShareableUrl.Should().StartWith("https://portal.sgrowing.io/portal/onboarding/spt_");
        tokenResult.ClientOrganizationName.Should().Be("Globex Corporation");

        // 2. Query portal view with valid token
        var portalView = await _service.GetClientPortalViewAsync(tokenResult.Token);

        portalView.Should().NotBeNull();
        portalView!.ProjectName.Should().Be("Globex B2B Omnichannel Onboarding");
        portalView.ClientOrganizationName.Should().Be("Globex Corporation");
        portalView.Milestones.Should().HaveCount(1);
        portalView.Milestones[0].Title.Should().Be("API Webhook Ingestion Verification");
        portalView.Milestones[0].ProgressPercent.Should().Be(67); // 2/3 = 67%

        // 3. Client submits feedback with "Approved" status
        var feedback = await _service.SubmitClientPortalFeedbackAsync(
            tokenResult.Token,
            new ClientPortalFeedbackRequest(
                TaskId: task.Id,
                ClientName: "Karim Trabelsi (VP Engineering)",
                ClientEmail: "karim@globex.corp",
                StatusVote: "Approved",
                FeedbackNotes: "Tested end-to-end webhook delivery in staging. Verified & signed off."
            ));

        feedback.Should().NotBeNull();
        feedback.StatusVote.Should().Be("Approved");

        // 4. Verify task transitioned to "Done" automatically because of Approved status on InReview task
        var updatedTask = await _db.TaskItems.FirstAsync(t => t.Id == task.Id);
        updatedTask.Status.Should().Be("Done");
        updatedTask.SubtasksCompleted.Should().Be(updatedTask.SubtasksTotal);

        // Verify audit comment was recorded
        var taskComments = await _service.GetCommentsAsync(tenantId, task.Id);
        taskComments.Should().Contain(c => c.AuthorName.Contains("[Client Signoff]") && c.Content.Contains("Tested end-to-end"));

        // 5. Revoke token
        var revoked = await _service.RevokeClientPortalTokenAsync(tenantId, tokenResult.Token);
        revoked.Should().BeTrue();

        // Querying view with revoked token returns null
        var revokedView = await _service.GetClientPortalViewAsync(tokenResult.Token);
        revokedView.Should().BeNull();
    }

    [Fact]
    public async Task CompleteMilestoneWithBilling_TriggersInvoiceWhenRequested()
    {
        var tenantId = Guid.NewGuid();
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Zenith ERP Integration",
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Projects.Add(project);

        var task = new TaskItemRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TenantId = tenantId,
            Title = "Phase 1: Catalog Sync Go-Live",
            Status = "InProgress",
            SubtasksTotal = 5,
            SubtasksCompleted = 3,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();

        var mockInvoice = new InvoiceRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "INV-2026-9041",
            TotalAmount = 2975.00m,
            Currency = "EUR",
            Status = InvoiceStatus.Draft
        };

        _invoiceMock.Setup(i => i.CreateInvoiceAsync(
                tenantId,
                It.Is<CreateInvoiceRequest>(r => r.Currency == "EUR" && r.LineItems != null && r.LineItems.Count > 0 && r.LineItems[0].UnitPrice == 2500.00m),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockInvoice);

        var result = await _service.CompleteMilestoneWithBillingAsync(
            tenantId,
            task.Id,
            new CompleteMilestoneRequest(
                TriggerInvoice: true,
                MilestoneAmount: 2500.00m,
                Currency: "EUR"
            ));

        result.Should().NotBeNull();
        result!.Task.Status.Should().Be("Done");
        result.Task.SubtasksCompleted.Should().Be(5);
        result.Invoice.Should().NotBeNull();
        result.Invoice!.InvoiceNumber.Should().Be("INV-2026-9041");
        result.Message.Should().Contain("Triggered Invoice INV-2026-9041");
    }

    [Fact]
    public async Task CompleteMilestoneWithBilling_WithoutInvoice_MarksTaskDoneWithoutInvoiceCall()
    {
        var tenantId = Guid.NewGuid();
        var task = new TaskItemRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = "Internal Documentation",
            Status = "Todo",
            SubtasksTotal = 2,
            SubtasksCompleted = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();

        var result = await _service.CompleteMilestoneWithBillingAsync(
            tenantId,
            task.Id,
            new CompleteMilestoneRequest(TriggerInvoice: false));

        result.Should().NotBeNull();
        result!.Task.Status.Should().Be("Done");
        result.Invoice.Should().BeNull();
        result.Message.Should().Contain("signed off without invoicing");
        _invoiceMock.Verify(i => i.CreateInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CreateInvoiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
