using Microsoft.AspNetCore.Mvc;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.CMS.VideoCreativePipeline;

public class VideoCreativePipelineEndpoints : IEndpointModule
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public VideoCreativePipelineEndpoints()
    {
    }

    private static Guid ResolveTenantId(Guid? bodyTenantId, Guid? queryTenantId, Guid? headerTenantId)
    {
        if (bodyTenantId.HasValue && bodyTenantId.Value != Guid.Empty) return bodyTenantId.Value;
        if (queryTenantId.HasValue && queryTenantId.Value != Guid.Empty) return queryTenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/content/video-pipeline")
            .WithTags("Generative AI Video Ad & Creative Variant Pipeline (PRD Domain C5 & J1/J3)");

        // 1. Create Video Project
        group.MapPost("/projects", (
            [FromBody] CreateVideoProjectRequest request,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IVideoCreativePipelineService service) =>
        {
            var effectiveTenant = ResolveTenantId(request.TenantId, queryTenantId, headerTenantId);
            var effectiveRequest = request.TenantId == effectiveTenant
                ? request
                : request with { TenantId = effectiveTenant };

            var project = service.CreateProject(effectiveRequest);
            return Results.Created($"/api/v1/content/video-pipeline/projects/{project.ProjectId}", project);
        })
        .WithName("CreateVideoProject")
        .WithSummary("Create a new AI video ad campaign project with storyboard scene blueprints");

        // 2. Get Video Projects
        group.MapGet("/projects", (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IVideoCreativePipelineService service) =>
        {
            var effectiveTenant = ResolveTenantId(null, queryTenantId, headerTenantId);
            var projects = service.GetProjects(effectiveTenant);
            return Results.Ok(projects);
        })
        .WithName("GetVideoProjects")
        .WithSummary("Retrieve all video ad storyboard projects for the tenant");

        // 3. Get Video Project by ID
        group.MapGet("/projects/{id:guid}", (
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IVideoCreativePipelineService service) =>
        {
            var effectiveTenant = ResolveTenantId(null, queryTenantId, headerTenantId);
            var project = service.GetProject(effectiveTenant, id);
            return project is not null ? Results.Ok(project) : Results.NotFound();
        })
        .WithName("GetVideoProjectById")
        .WithSummary("Retrieve a specific video ad campaign project by ID");

        // 4. Generate Multi-Armed Bandit Variants
        group.MapPost("/projects/{id:guid}/generate-variants", (
            Guid id,
            [FromBody] GenerateVariantsEndpointBody? body,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? queryTenantId,
            IVideoCreativePipelineService service) =>
        {
            var effectiveTenant = ResolveTenantId(body?.TenantId, queryTenantId, headerTenantId);
            var variantCount = body?.VariantCount ?? 3;

            try
            {
                var variants = service.GenerateVariants(new GenerateVariantsRequest(effectiveTenant, id, variantCount));
                return Results.Ok(variants);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Project {id} not found for tenant {effectiveTenant}." });
            }
        })
        .WithName("GenerateVideoCreativeVariants")
        .WithSummary("Generate multi-armed bandit creative variants with predicted CTR and exploration scores");

        // 5. Synthesize Regional Dialect Script
        group.MapPost("/synthesize-script", (
            [FromBody] SynthesizeDialectScriptRequest request,
            IVideoCreativePipelineService service) =>
        {
            var result = service.SynthesizeDialectScript(request);
            return Results.Ok(result);
        })
        .WithName("SynthesizeDialectScript")
        .WithSummary("Synthesize colloquial regional Arabic dialect script with vernacular idiom injection");
    }
}

public static class VideoCreativePipelineServiceExtensions
{
    public static IServiceCollection AddVideoCreativePipelineServices(this IServiceCollection services)
    {
        services.AddSingleton<IVideoCreativePipelineService, VideoCreativePipelineService>();
        return services;
    }
}
