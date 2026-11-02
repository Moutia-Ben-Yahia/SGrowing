using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.CRM.MetadataEngine;

public static class CrmMetadataEndpoints
{
    public static IServiceCollection AddCrmMetadataServices(this IServiceCollection services)
    {
        services.AddSingleton<ICrmMetadataEngine, CrmMetadataEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCrmMetadataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/crm/metadata")
            .WithTags("Domain B1 & B4: Extensible CRM Custom Metadata & Dynamic Objects Engine");

        // 1. Create Schema
        group.MapPost("/schemas", (
            [FromBody] CreateSchemaRequest request,
            ICrmMetadataEngine engine) =>
        {
            var schema = engine.CreateSchema(request);
            return Results.Created($"/api/v1/crm/metadata/schemas/{schema.TenantId}/{schema.SchemaId}", schema);
        })
        .WithName("CreateCrmCustomSchema")
        .WithSummary("Registers an extensible custom entity schema (e.g. Properties, Vehicles, Partners).");

        // 2. List Schemas
        group.MapGet("/schemas/{tenantId}", (
            string tenantId,
            ICrmMetadataEngine engine) =>
        {
            var schemas = engine.ListSchemas(tenantId);
            return Results.Ok(schemas);
        })
        .WithName("ListCrmCustomSchemas")
        .WithSummary("Lists all defined custom object schemas for a tenant.");

        // 3. Create Record
        group.MapPost("/records", (
            [FromBody] CreateRecordRequest request,
            ICrmMetadataEngine engine) =>
        {
            try
            {
                var record = engine.CreateRecord(request);
                return Results.Created($"/api/v1/crm/metadata/records/{record.TenantId}/{record.RecordId}", record);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("CreateCrmCustomRecord")
        .WithSummary("Creates a dynamic record adhering to the custom object schema.");

        // 4. List Records
        group.MapGet("/records/{tenantId}/{schemaId}", (
            string tenantId,
            string schemaId,
            ICrmMetadataEngine engine) =>
        {
            var records = engine.ListRecords(tenantId, schemaId);
            return Results.Ok(records);
        })
        .WithName("ListCrmCustomRecords")
        .WithSummary("Retrieves all records belonging to a custom object schema.");

        // 5. Detect Duplicates
        group.MapGet("/duplicates/{tenantId}/{schemaId}", (
            string tenantId,
            string schemaId,
            [FromQuery] string matchField,
            ICrmMetadataEngine engine) =>
        {
            var duplicates = engine.DetectDuplicates(tenantId, schemaId, matchField);
            return Results.Ok(duplicates);
        })
        .WithName("DetectCrmRecordDuplicates")
        .WithSummary("Detects fuzzy duplicate record candidates using Levenshtein distance.");

        // 6. Merge Records
        group.MapPost("/merge", (
            [FromBody] MergeRecordsRequest request,
            ICrmMetadataEngine engine) =>
        {
            try
            {
                var merged = engine.MergeRecords(request);
                return Results.Ok(merged);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("MergeCrmRecords")
        .WithSummary("Merges duplicate records into master record while applying resolved attribute overrides.");

        return endpoints;
    }
}
