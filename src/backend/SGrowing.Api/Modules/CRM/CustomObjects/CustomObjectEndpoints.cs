using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.CRM.CustomObjects;

public static class CustomObjectEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapCustomObjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/custom-objects")
            .WithTags("Polymorphic EAV & Dynamic Custom Objects (Salesforce/Twenty Engine)");

        // 1. POST /api/v1/custom-objects/schemas
        group.MapPost("/schemas", async (
            [FromBody] CreateCustomObjectSchemaRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var schema = await service.CreateSchemaAsync(effectiveTenantId, req, ct);
            return Results.Created($"/api/v1/custom-objects/schemas/{schema.ObjectSlug}", schema);
        })
        .WithName("CreateCustomObjectSchema")
        .WithSummary("Define a dynamic polymorphic custom object schema (e.g. vehicles, listings, claims)");

        // 2. GET /api/v1/custom-objects/schemas
        group.MapGet("/schemas", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var schemas = await service.GetSchemasAsync(effectiveTenantId, ct);
            return Results.Ok(schemas);
        })
        .WithName("GetCustomObjectSchemas")
        .WithSummary("List all dynamic custom object schemas defined for the tenant");

        // 3. GET /api/v1/custom-objects/schemas/{slug}
        group.MapGet("/schemas/{slug}", async (
            string slug,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var schema = await service.GetSchemaBySlugAsync(effectiveTenantId, slug, ct);
            return schema != null
                ? Results.Ok(schema)
                : Results.NotFound(new { error = "NotFound", message = $"Custom object schema '{slug}' not found." });
        })
        .WithName("GetCustomObjectSchemaBySlug")
        .WithSummary("Get a specific custom object schema by its unique object slug");

        // 4. POST /api/v1/custom-objects/{slug}/records
        group.MapPost("/{slug}/records", async (
            string slug,
            [FromBody] Dictionary<string, object?> data,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var record = await service.CreateRecordAsync(effectiveTenantId, slug, data, ct);
                return Results.Created($"/api/v1/custom-objects/{slug}/records/{record.Id}", record);
            }
            catch (CustomObjectValidationException ex)
            {
                return Results.UnprocessableEntity(new
                {
                    error = "ValidationFailed",
                    message = ex.Message,
                    validationErrors = ex.Errors
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "NotFound", message = ex.Message });
            }
        })
        .WithName("CreateCustomObjectRecord")
        .WithSummary("Create and persist a validated dynamic record with JSONB data for a custom object");

        // 5. GET /api/v1/custom-objects/{slug}/records
        group.MapGet("/{slug}/records", async (
            string slug,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? sortBy,
            [FromQuery] bool? sortDescending,
            [FromQuery] string? filterField,
            [FromQuery] string? filterOp,
            [FromQuery] string? filterVal,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);

            List<CustomObjectFilter>? filters = null;
            if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterVal))
            {
                var op = CustomObjectFilterOperator.Equals;
                if (!string.IsNullOrWhiteSpace(filterOp) && Enum.TryParse<CustomObjectFilterOperator>(filterOp, true, out var parsedOp))
                {
                    op = parsedOp;
                }
                filters = [new CustomObjectFilter(filterField, op, filterVal)];
            }

            var queryOptions = new CustomObjectQueryOptions(
                Filters: filters,
                SortBy: sortBy,
                SortDescending: sortDescending ?? false,
                Page: page ?? 1,
                PageSize: pageSize ?? 20
            );

            try
            {
                var result = await service.GetRecordsAsync(effectiveTenantId, slug, queryOptions, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "NotFound", message = ex.Message });
            }
        })
        .WithName("GetCustomObjectRecords")
        .WithSummary("Query and filter dynamic records with pagination, multi-operator filtering, and property sorting");

        // 6. GET /api/v1/custom-objects/{slug}/records/{id:guid}
        group.MapGet("/{slug}/records/{id:guid}", async (
            string slug,
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var record = await service.GetRecordByIdAsync(effectiveTenantId, slug, id, ct);
            return record != null
                ? Results.Ok(record)
                : Results.NotFound(new { error = "NotFound", message = $"Record '{id}' not found for custom object '{slug}'." });
        })
        .WithName("GetCustomObjectRecordById")
        .WithSummary("Retrieve a single dynamic custom object record by its unique ID");

        // 7. PUT /api/v1/custom-objects/{slug}/records/{id:guid}
        group.MapPut("/{slug}/records/{id:guid}", async (
            string slug,
            Guid id,
            [FromBody] Dictionary<string, object?> data,
            [FromQuery] bool? partial,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var record = await service.UpdateRecordAsync(effectiveTenantId, slug, id, data, partial ?? false, ct);
                return Results.Ok(record);
            }
            catch (CustomObjectValidationException ex)
            {
                return Results.UnprocessableEntity(new
                {
                    error = "ValidationFailed",
                    message = ex.Message,
                    validationErrors = ex.Errors
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "NotFound", message = ex.Message });
            }
        })
        .WithName("UpdateCustomObjectRecord")
        .WithSummary("Update or replace dynamic custom object record data with schema validation");

        // 7b. PATCH /api/v1/custom-objects/{slug}/records/{id:guid}
        group.MapPatch("/{slug}/records/{id:guid}", async (
            string slug,
            Guid id,
            [FromBody] Dictionary<string, object?> data,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            try
            {
                var record = await service.UpdateRecordAsync(effectiveTenantId, slug, id, data, partialUpdate: true, ct);
                return Results.Ok(record);
            }
            catch (CustomObjectValidationException ex)
            {
                return Results.UnprocessableEntity(new
                {
                    error = "ValidationFailed",
                    message = ex.Message,
                    validationErrors = ex.Errors
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = "NotFound", message = ex.Message });
            }
        })
        .WithName("PatchCustomObjectRecord")
        .WithSummary("Partially patch dynamic custom object record fields with schema validation");

        // 8. DELETE /api/v1/custom-objects/{slug}/records/{id:guid}
        group.MapDelete("/{slug}/records/{id:guid}", async (
            string slug,
            Guid id,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IDynamicCustomObjectService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var deleted = await service.DeleteRecordAsync(effectiveTenantId, slug, id, ct);
            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { error = "NotFound", message = $"Record '{id}' not found for custom object '{slug}'." });
        })
        .WithName("DeleteCustomObjectRecord")
        .WithSummary("Delete a dynamic custom object record");

        return app;
    }
}
