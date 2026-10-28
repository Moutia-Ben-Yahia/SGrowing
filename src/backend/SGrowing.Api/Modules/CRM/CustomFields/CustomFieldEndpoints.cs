using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.CRM.CustomFields;

public record SetEntityCustomFieldsApiRequest(List<SetFieldValueDto> Fields);

public static class CustomFieldEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapCustomFieldEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm/custom-fields")
            .WithTags("Dynamic Schema & Typed Custom Fields (Twenty CRM)");

        group.MapPost("/", async (
            [FromBody] CreateCustomFieldDefinitionRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICustomFieldService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.CreateDefinitionAsync(effectiveTenantId, req, ct);
            return Results.Created($"/api/v1/crm/custom-fields/{result.EntityType}/{result.Id}", result);
        })
        .WithName("CreateCustomFieldDefinition")
        .WithSummary("Define a dynamic typed custom field attribute for Contacts, Companies, or Deals");

        group.MapGet("/{entityType}", async (
            string entityType,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICustomFieldService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var results = await service.GetDefinitionsAsync(effectiveTenantId, entityType, ct);
            return Results.Ok(results);
        })
        .WithName("GetCustomFieldDefinitions")
        .WithSummary("List all custom field definitions configured for an entity type");

        group.MapPost("/{entityType}/{entityId:guid}/values", async (
            string entityType,
            Guid entityId,
            [FromBody] SetEntityCustomFieldsApiRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICustomFieldService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.SetValuesAsync(effectiveTenantId, entityType, entityId, req.Fields, ct);
            return Results.Ok(result);
        })
        .WithName("SetEntityCustomFieldValues")
        .WithSummary("Set dynamic typed custom attribute values on an entity with validation checks");

        group.MapGet("/{entityType}/{entityId:guid}/values", async (
            string entityType,
            Guid entityId,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            ICustomFieldService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var result = await service.GetValuesAsync(effectiveTenantId, entityType, entityId, ct);
            return Results.Ok(result);
        })
        .WithName("GetEntityCustomFieldValues")
        .WithSummary("Retrieve resolved custom attribute values with field definitions for an entity");

        return app;
    }
}
