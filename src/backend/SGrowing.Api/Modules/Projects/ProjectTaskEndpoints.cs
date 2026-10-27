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

        return app;
    }
}
