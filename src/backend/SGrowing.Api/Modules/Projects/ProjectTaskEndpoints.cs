using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Projects;

public static class ProjectTaskEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        return tenantId ?? headerTenantId ?? DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapProjectTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects")
            .WithTags("Projects & Sprint Tasks");

        group.MapPost("/convert-deal/{dealId:guid}", async (
            Guid dealId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var project = await service.ConvertFromDealAsync(effectiveTenantId, dealId, ct);
            return Results.Ok(project);
        })
        .WithName("ConvertDealToOnboardingProject")
        .WithSummary("Convert a won deal into an onboarding project with milestone sprint task templates");

        group.MapPost("/convert-contract/{contractId:guid}", async (
            Guid contractId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var project = await service.ConvertFromContractAsync(effectiveTenantId, contractId, ct);
            return Results.Ok(project);
        })
        .WithName("ConvertContractToDeploymentProject")
        .WithSummary("Convert a sealed contract into an enterprise deployment project");

        group.MapGet("/", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var projects = await service.GetProjectsAsync(effectiveTenantId, ct);
            return Results.Ok(projects);
        })
        .WithName("ListProjects")
        .WithSummary("List all projects for the tenant");

        group.MapGet("/{projectId:guid}", async (
            Guid projectId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var project = await service.GetProjectByIdAsync(effectiveTenantId, projectId, ct);
            return project != null ? Results.Ok(project) : Results.NotFound(new { error = "Project not found" });
        })
        .WithName("GetProjectById")
        .WithSummary("Get project details by Id");

        group.MapGet("/{projectId:guid}/tasks", async (
            Guid projectId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var tasks = await service.GetProjectTasksAsync(effectiveTenantId, projectId, ct);
            return Results.Ok(tasks);
        })
        .WithName("GetProjectTasks")
        .WithSummary("Get linear-style sprint tasks for a project");

        group.MapPost("/{projectId:guid}/tasks", async (
            Guid projectId,
            [FromBody] CreateTaskRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var task = await service.CreateTaskAsync(effectiveTenantId, projectId, req, ct);
            return Results.Ok(task);
        })
        .WithName("CreateProjectTask")
        .WithSummary("Create a new task in a project");

        group.MapPatch("/tasks/{taskId:guid}", async (
            Guid taskId,
            [FromBody] UpdateTaskRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var task = await service.UpdateTaskAsync(effectiveTenantId, taskId, req, ct);
            return task != null ? Results.Ok(task) : Results.NotFound(new { error = "Task not found" });
        })
        .WithName("UpdateProjectTask")
        .WithSummary("Update task status, priority, position, or subtask progress");

        group.MapPost("/tasks/{taskId:guid}/comments", async (
            Guid taskId,
            [FromBody] AddCommentRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var comment = await service.AddCommentAsync(effectiveTenantId, taskId, req, ct);
            return Results.Ok(comment);
        })
        .WithName("AddTaskComment")
        .WithSummary("Add a comment to a task");

        group.MapGet("/tasks/{taskId:guid}/comments", async (
            Guid taskId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var comments = await service.GetCommentsAsync(effectiveTenantId, taskId, ct);
            return Results.Ok(comments);
        })
        .WithName("GetTaskComments")
        .WithSummary("Get comments for a task");

        group.MapPost("/{projectId:guid}/portal-token", async (
            Guid projectId,
            [FromBody] GeneratePortalTokenRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await service.GenerateClientPortalTokenAsync(effectiveTenantId, projectId, req, baseUrl, ct);
            return Results.Ok(result);
        })
        .WithName("GenerateClientPortalToken")
        .WithSummary("Generate a secure time-limited token for external client onboarding portal");

        group.MapDelete("/portal-tokens/{token}", async (
            string token,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var revoked = await service.RevokeClientPortalTokenAsync(effectiveTenantId, token, ct);
            return revoked ? Results.Ok(new { message = "Portal token revoked successfully" }) : Results.NotFound(new { error = "Token not found" });
        })
        .WithName("RevokeClientPortalToken")
        .WithSummary("Revoke client access via an onboarding portal token");

        // Public External Portal Group
        var publicPortalGroup = app.MapGroup("/api/v1/portal/onboarding")
            .WithTags("Public Client Onboarding Portal");

        publicPortalGroup.MapGet("/{token}", async (
            string token,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            var view = await service.GetClientPortalViewAsync(token, ct);
            return view != null ? Results.Ok(view) : Results.NotFound(new { error = "Invalid, revoked, or expired client onboarding token." });
        })
        .WithName("GetClientOnboardingPortalView")
        .WithSummary("Public external endpoint to view project onboarding milestones via secure token");

        publicPortalGroup.MapPost("/{token}/feedback", async (
            string token,
            [FromBody] ClientPortalFeedbackRequest req,
            IProjectTaskService service,
            CancellationToken ct) =>
        {
            try
            {
                var feedback = await service.SubmitClientPortalFeedbackAsync(token, req, ct);
                return Results.Ok(feedback);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SubmitClientPortalFeedback")
        .WithSummary("Submit milestone approval or change request feedback from the client portal");

        return app;
    }
}
